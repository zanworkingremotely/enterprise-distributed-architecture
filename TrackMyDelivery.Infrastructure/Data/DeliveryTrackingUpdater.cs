using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using TrackMydelivery.Application.Interfaces;
using TrackMyDelivery.Domain.Deliveries.Events;

namespace TrackMyDelivery.Infrastructure.Data;

public sealed class DeliveryTrackingUpdater : IDeliveryTrackingUpdater
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ILogger<DeliveryTrackingUpdater> _logger;

    public DeliveryTrackingUpdater(SqliteConnectionFactory connectionFactory, ILogger<DeliveryTrackingUpdater> logger)
    {
        _connectionFactory = connectionFactory;
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
                SELECT id, type, payload
                FROM outbox_messages
                WHERE processed_on_utc IS NULL
                ORDER BY occurred_on_utc;
                """;

            await using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                pendingMessages.Add(new OutboxMessageRecord(
                    Guid.Parse(reader.GetString(0)),
                    reader.GetString(1),
                    reader.GetString(2)));
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
                        error = NULL
                    WHERE id = $id;
                    """;
                updateOutboxCommand.Parameters.AddWithValue("$processedOnUtc", DateTime.UtcNow.ToString("O"));
                updateOutboxCommand.Parameters.AddWithValue("$id", message.Id.ToString());
                await updateOutboxCommand.ExecuteNonQueryAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                processedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process outbox message {OutboxMessageId}", message.Id);

                await using var errorCommand = connection.CreateCommand();
                errorCommand.CommandText =
                    """
                    UPDATE outbox_messages
                    SET error = $error
                    WHERE id = $id;
                    """;
                errorCommand.Parameters.AddWithValue("$error", ex.Message);
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

    private sealed record OutboxMessageRecord(Guid Id, string Type, string Payload);

    private sealed record TrackingEventRecord(
        Guid EventId,
        Guid DeliveryId,
        string EventType,
        string Description,
        DateTime OccurredOnUtc);
}
