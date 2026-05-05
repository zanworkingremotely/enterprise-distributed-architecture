using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrackMydelivery.Application.Interfaces;
using TrackMyDelivery.Infrastructure.Configuration;
using TrackMyDelivery.Infrastructure.Constants;
using TrackMyDelivery.Infrastructure.Data;

namespace TrackMyDelivery.Infrastructure.Messaging;

public sealed class FailedDeliveryMessageReplay : IFailedDeliveryMessageReplay
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IDeliveryEventPublisher _deliveryEventPublisher;
    private readonly ILogger<FailedDeliveryMessageReplay> _logger;
    private readonly MessagingOptions _messagingOptions;

    public FailedDeliveryMessageReplay(
        SqliteConnectionFactory connectionFactory,
        IDateTimeProvider dateTimeProvider,
        IDeliveryEventPublisher deliveryEventPublisher,
        IOptions<MessagingOptions> messagingOptions,
        ILogger<FailedDeliveryMessageReplay> logger)
    {
        _connectionFactory = connectionFactory;
        _dateTimeProvider = dateTimeProvider;
        _deliveryEventPublisher = deliveryEventPublisher;
        _messagingOptions = messagingOptions.Value;
        _logger = logger;
    }

    public async Task RecordParkedMessageAsync(
        DeliveryMessage deliveryMessage,
        int attemptCount,
        string failureReason,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            INSERT INTO {StorageNames.FailedDeliveryTable} (
                {StorageNames.EventId},
                {StorageNames.DeliveryId},
                {StorageNames.EventType},
                {StorageNames.RoutingKey},
                {StorageNames.Payload},
                {StorageNames.CorrelationId},
                {StorageNames.OccurredOnUtc},
                {StorageNames.ParkedOnUtc},
                {StorageNames.FailureReason},
                {StorageNames.AttemptCount},
                {StorageNames.ReplayedOnUtc}
            ) VALUES (
                $eventId,
                $deliveryId,
                $eventType,
                $routingKey,
                $payload,
                $correlationId,
                $occurredOnUtc,
                $parkedOnUtc,
                $failureReason,
                $attemptCount,
                NULL
            )
            ON CONFLICT({StorageNames.EventId}) DO UPDATE SET
                {StorageNames.DeliveryId} = excluded.{StorageNames.DeliveryId},
                {StorageNames.EventType} = excluded.{StorageNames.EventType},
                {StorageNames.RoutingKey} = excluded.{StorageNames.RoutingKey},
                {StorageNames.Payload} = excluded.{StorageNames.Payload},
                {StorageNames.CorrelationId} = excluded.{StorageNames.CorrelationId},
                {StorageNames.OccurredOnUtc} = excluded.{StorageNames.OccurredOnUtc},
                {StorageNames.ParkedOnUtc} = excluded.{StorageNames.ParkedOnUtc},
                {StorageNames.FailureReason} = excluded.{StorageNames.FailureReason},
                {StorageNames.AttemptCount} = excluded.{StorageNames.AttemptCount},
                {StorageNames.ReplayedOnUtc} = NULL;
            """;
        command.Parameters.AddWithValue("$eventId", deliveryMessage.EventId.ToString());
        command.Parameters.AddWithValue("$deliveryId", deliveryMessage.DeliveryId.ToString());
        command.Parameters.AddWithValue("$eventType", deliveryMessage.EventType);
        command.Parameters.AddWithValue("$routingKey", deliveryMessage.RoutingKey);
        command.Parameters.AddWithValue("$payload", deliveryMessage.Payload);
        command.Parameters.AddWithValue("$correlationId", (object?)deliveryMessage.CorrelationId ?? DBNull.Value);
        command.Parameters.AddWithValue("$occurredOnUtc", deliveryMessage.OccurredOnUtc.ToString("O"));
        command.Parameters.AddWithValue("$parkedOnUtc", _dateTimeProvider.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$failureReason", failureReason);
        command.Parameters.AddWithValue("$attemptCount", attemptCount);
        await command.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogWarning(
            InfrastructureLogMessages.FailedDeliveryMessageRecorded,
            deliveryMessage.EventId,
            attemptCount);
    }

    public async Task<int> ReplayAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        if (!_messagingOptions.Enabled)
        {
            _logger.LogInformation(InfrastructureLogMessages.FailedDeliveryReplayDisabled);
            return 0;
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var parkedMessages = new List<ParkedDeliveryMessageRecord>();
        await using (var selectCommand = connection.CreateCommand())
        {
            selectCommand.CommandText =
                $"""
                SELECT
                    {StorageNames.EventId},
                    {StorageNames.DeliveryId},
                    {StorageNames.EventType},
                    {StorageNames.RoutingKey},
                    {StorageNames.Payload},
                    {StorageNames.CorrelationId},
                    {StorageNames.OccurredOnUtc}
                FROM {StorageNames.FailedDeliveryTable}
                WHERE {StorageNames.ReplayedOnUtc} IS NULL
                ORDER BY {StorageNames.ParkedOnUtc}
                LIMIT $maxCount;
                """;
            selectCommand.Parameters.AddWithValue("$maxCount", maxCount);

            await using var parkedMessagesReader = await selectCommand.ExecuteReaderAsync(cancellationToken);
            while (await parkedMessagesReader.ReadAsync(cancellationToken))
            {
                parkedMessages.Add(new ParkedDeliveryMessageRecord(
                    Guid.Parse(parkedMessagesReader.GetString(0)),
                    Guid.Parse(parkedMessagesReader.GetString(1)),
                    parkedMessagesReader.GetString(2),
                    parkedMessagesReader.GetString(3),
                    parkedMessagesReader.GetString(4),
                    parkedMessagesReader.IsDBNull(5) ? null : parkedMessagesReader.GetString(5),
                    DateTime.Parse(
                        parkedMessagesReader.GetString(6),
                        null,
                        System.Globalization.DateTimeStyles.RoundtripKind)));
            }
        }

        var replayedCount = 0;

        foreach (var parkedMessage in parkedMessages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var deliveryMessage = parkedMessage.ToDeliveryMessage();
            using var correlationScope = _logger.BeginScope(new Dictionary<string, object?>
            {
                [CorrelationNames.LogPropertyName] = deliveryMessage.CorrelationId ?? string.Empty
            });

            try
            {
                await _deliveryEventPublisher.PublishAsync(deliveryMessage, cancellationToken);

                await using var markReplayedCommand = connection.CreateCommand();
                markReplayedCommand.CommandText =
                    $"""
                    UPDATE {StorageNames.FailedDeliveryTable}
                    SET {StorageNames.ReplayedOnUtc} = $replayedOnUtc
                    WHERE {StorageNames.EventId} = $eventId;
                    """;
                markReplayedCommand.Parameters.AddWithValue("$replayedOnUtc", _dateTimeProvider.UtcNow.ToString("O"));
                markReplayedCommand.Parameters.AddWithValue("$eventId", parkedMessage.EventId.ToString());
                await markReplayedCommand.ExecuteNonQueryAsync(cancellationToken);

                replayedCount++;

                _logger.LogInformation(
                    InfrastructureLogMessages.FailedDeliveryMessageReplayed,
                    deliveryMessage.EventId,
                    deliveryMessage.DeliveryId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    InfrastructureLogMessages.FailedDeliveryMessageReplayFailed,
                    deliveryMessage.EventId);
            }
        }

        return replayedCount;
    }

    private sealed record ParkedDeliveryMessageRecord(
        Guid EventId,
        Guid DeliveryId,
        string EventType,
        string RoutingKey,
        string Payload,
        string? CorrelationId,
        DateTime OccurredOnUtc)
    {
        public DeliveryMessage ToDeliveryMessage()
        {
            return new DeliveryMessage
            {
                EventId = EventId,
                DeliveryId = DeliveryId,
                CorrelationId = CorrelationId,
                EventType = EventType,
                RoutingKey = RoutingKey,
                Payload = Payload,
                OccurredOnUtc = OccurredOnUtc
            };
        }
    }
}
