using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using TrackMydelivery.Application.Interfaces;
using TrackMyDelivery.Domain.Deliveries.Events;

namespace TrackMyDelivery.Infrastructure.Data;

public sealed class DeliveryTrackingUpdater : IDeliveryTrackingUpdater
{
    private const int MaxRetryCount = 3;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<DeliveryTrackingUpdater> _logger;

    public DeliveryTrackingUpdater(
        SqliteConnectionFactory connectionFactory,
        IDateTimeProvider dateTimeProvider,
        ILogger<DeliveryTrackingUpdater> logger)
    {
        _connectionFactory = connectionFactory;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
        new SqliteDatabaseInitializer(connectionFactory).Initialize();
    }

    public async Task<int> UpdateTrackingTimelineAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var pendingMessages = new List<OutboxMessageRecord>();
        await using (var selectCommand = connection.CreateCommand())
        {
            selectCommand.CommandText =
                """
                SELECT id, type, payload, retry_count
                FROM outbox_messages
                WHERE processed_on_utc IS NULL
                  AND dead_lettered_on_utc IS NULL
                  AND (next_attempt_utc IS NULL OR next_attempt_utc <= $nowUtc)
                ORDER BY occurred_on_utc;
                """;
            selectCommand.Parameters.AddWithValue("$nowUtc", _dateTimeProvider.UtcNow.ToString("O"));

            await using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                pendingMessages.Add(new OutboxMessageRecord(
                    Guid.Parse(reader.GetString(0)),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt32(3)));
            }
        }

        var processedCount = 0;

        foreach (var message in pendingMessages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var trackingEvent = MapTrackingEvent(message);

                await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

                if (trackingEvent is not null)
                {
                    await using var insertTrackingCommand = connection.CreateCommand();
                    insertTrackingCommand.Transaction = transaction;
                    insertTrackingCommand.CommandText =
                        """
                        INSERT OR IGNORE INTO tracking_events (
                            event_id,
                            delivery_id,
                            event_type,
                            description,
                            occurred_on_utc
                        ) VALUES (
                            $eventId,
                            $deliveryId,
                            $eventType,
                            $description,
                            $occurredOnUtc
                        );
                        """;
                    insertTrackingCommand.Parameters.AddWithValue("$eventId", trackingEvent.EventId.ToString());
                    insertTrackingCommand.Parameters.AddWithValue("$deliveryId", trackingEvent.DeliveryId.ToString());
                    insertTrackingCommand.Parameters.AddWithValue("$eventType", trackingEvent.EventType);
                    insertTrackingCommand.Parameters.AddWithValue("$description", trackingEvent.Description);
                    insertTrackingCommand.Parameters.AddWithValue("$occurredOnUtc", trackingEvent.OccurredOnUtc.ToString("O"));
                    await insertTrackingCommand.ExecuteNonQueryAsync(cancellationToken);
                }

                await using var updateOutboxCommand = connection.CreateCommand();
                updateOutboxCommand.Transaction = transaction;
                updateOutboxCommand.CommandText =
                    """
                    UPDATE outbox_messages
                    SET processed_on_utc = $processedOnUtc,
                        error = NULL,
                        last_attempt_utc = $processedOnUtc,
                        next_attempt_utc = NULL
                    WHERE id = $id;
                    """;
                updateOutboxCommand.Parameters.AddWithValue("$processedOnUtc", _dateTimeProvider.UtcNow.ToString("O"));
                updateOutboxCommand.Parameters.AddWithValue("$id", message.Id.ToString());
                await updateOutboxCommand.ExecuteNonQueryAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                processedCount++;
            }
            catch (Exception ex)
            {
                var failedAttemptCount = message.RetryCount + 1;
                var failedAtUtc = _dateTimeProvider.UtcNow;
                var nextAttemptUtc = failedAttemptCount >= MaxRetryCount
                    ? (DateTime?)null
                    : failedAtUtc.AddSeconds(15 * failedAttemptCount);

                _logger.LogError(
                    ex,
                    "Failed to process outbox message {OutboxMessageId} on attempt {AttemptNumber}",
                    message.Id,
                    failedAttemptCount);

                await using var errorCommand = connection.CreateCommand();
                errorCommand.CommandText =
                    """
                    UPDATE outbox_messages
                    SET error = $error,
                        retry_count = $retryCount,
                        last_attempt_utc = $lastAttemptUtc,
                        next_attempt_utc = $nextAttemptUtc,
                        dead_lettered_on_utc = $deadLetteredOnUtc
                    WHERE id = $id;
                    """;
                errorCommand.Parameters.AddWithValue("$error", ex.Message);
                errorCommand.Parameters.AddWithValue("$retryCount", failedAttemptCount);
                errorCommand.Parameters.AddWithValue("$lastAttemptUtc", failedAtUtc.ToString("O"));
                errorCommand.Parameters.AddWithValue("$nextAttemptUtc", (object?)nextAttemptUtc?.ToString("O") ?? DBNull.Value);
                errorCommand.Parameters.AddWithValue(
                    "$deadLetteredOnUtc",
                    failedAttemptCount >= MaxRetryCount ? failedAtUtc.ToString("O") : DBNull.Value);
                errorCommand.Parameters.AddWithValue("$id", message.Id.ToString());
                await errorCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        return processedCount;
    }

    private static TrackingEventRecord? MapTrackingEvent(OutboxMessageRecord message)
    {
        return message.Type switch
        {
            "TrackMyDelivery.Domain.Deliveries.Events.DeliveryCreatedDomainEvent" => CreateTrackingEvent(
                Deserialize<DeliveryCreatedDomainEvent>(message),
                "DeliveryCreated",
                domainEvent => $"Delivery created for {domainEvent.RecipientName}."),
            "TrackMyDelivery.Domain.Deliveries.Events.CourierAssignedDomainEvent" => CreateTrackingEvent(
                Deserialize<CourierAssignedDomainEvent>(message),
                "CourierAssigned",
                domainEvent => $"Courier assigned: {domainEvent.CourierName}."),
            "TrackMyDelivery.Domain.Deliveries.Events.DeliveryStatusUpdatedDomainEvent" => CreateTrackingEvent(
                Deserialize<DeliveryStatusUpdatedDomainEvent>(message),
                "DeliveryStatusUpdated",
                domainEvent => string.IsNullOrWhiteSpace(domainEvent.Reason)
                    ? $"Delivery status updated to {domainEvent.Status}."
                    : $"Delivery status updated to {domainEvent.Status}. Reason: {domainEvent.Reason}."),
            _ => null
        };
    }

    private static TrackingEventRecord CreateTrackingEvent<TEvent>(
        TEvent domainEvent,
        string eventType,
        Func<TEvent, string> descriptionFactory)
        where TEvent : class
    {
        return domainEvent switch
        {
            DeliveryCreatedDomainEvent created => new TrackingEventRecord(
                created.EventId,
                created.DeliveryId,
                eventType,
                descriptionFactory(domainEvent),
                created.OccurredOnUtc),
            CourierAssignedDomainEvent assigned => new TrackingEventRecord(
                assigned.EventId,
                assigned.DeliveryId,
                eventType,
                descriptionFactory(domainEvent),
                assigned.OccurredOnUtc),
            DeliveryStatusUpdatedDomainEvent updated => new TrackingEventRecord(
                updated.EventId,
                updated.DeliveryId,
                eventType,
                descriptionFactory(domainEvent),
                updated.OccurredOnUtc),
            _ => throw new InvalidOperationException($"Unsupported event type '{typeof(TEvent).Name}'.")
        };
    }

    private static TEvent Deserialize<TEvent>(OutboxMessageRecord message)
        where TEvent : class
    {
        return JsonSerializer.Deserialize<TEvent>(message.Payload, JsonOptions)
            ?? throw new InvalidOperationException($"Outbox message '{message.Id}' could not be deserialized.");
    }

    private sealed record OutboxMessageRecord(Guid Id, string Type, string Payload, int RetryCount);

    private sealed record TrackingEventRecord(
        Guid EventId,
        Guid DeliveryId,
        string EventType,
        string Description,
        DateTime OccurredOnUtc);
}
