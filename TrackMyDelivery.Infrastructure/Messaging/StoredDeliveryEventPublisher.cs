using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrackMydelivery.Application.Interfaces;
using TrackMyDelivery.Infrastructure.Configuration;
using TrackMyDelivery.Infrastructure.Constants;
using TrackMyDelivery.Infrastructure.Data;

namespace TrackMyDelivery.Infrastructure.Messaging;

public sealed class StoredDeliveryEventPublisher
{
    private const int MaxPublishRetryCount = 5;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IDeliveryEventPublisher _deliveryEventPublisher;
    private readonly MessagingOptions _messagingOptions;
    private readonly ILogger<StoredDeliveryEventPublisher> _logger;

    public StoredDeliveryEventPublisher(
        SqliteConnectionFactory connectionFactory,
        IDateTimeProvider dateTimeProvider,
        IDeliveryEventPublisher deliveryEventPublisher,
        IOptions<MessagingOptions> messagingOptions,
        ILogger<StoredDeliveryEventPublisher> logger)
    {
        _connectionFactory = connectionFactory;
        _dateTimeProvider = dateTimeProvider;
        _deliveryEventPublisher = deliveryEventPublisher;
        _messagingOptions = messagingOptions.Value;
        _logger = logger;
    }

    public async Task<int> PublishPendingEventsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var pendingMessages = new List<OutboxMessageRecord>();
        await using (var selectCommand = connection.CreateCommand())
        {
            selectCommand.CommandText =
                $"""
                SELECT id, type, payload, occurred_on_utc, retry_count
                FROM {StorageNames.OutboxTable}
                WHERE {StorageNames.PublishedOnUtc} IS NULL
                  AND {StorageNames.DeadLetteredOnUtc} IS NULL
                  AND ({StorageNames.NextAttemptUtc} IS NULL OR {StorageNames.NextAttemptUtc} <= $nowUtc)
                ORDER BY occurred_on_utc;
                """;
            selectCommand.Parameters.AddWithValue("$nowUtc", _dateTimeProvider.UtcNow.ToString("O"));

            await using var pendingMessagesReader = await selectCommand.ExecuteReaderAsync(cancellationToken);
            while (await pendingMessagesReader.ReadAsync(cancellationToken))
            {
                pendingMessages.Add(new OutboxMessageRecord(
                    Guid.Parse(pendingMessagesReader.GetString(0)),
                    pendingMessagesReader.GetString(1),
                    pendingMessagesReader.GetString(2),
                    DateTime.Parse(
                        pendingMessagesReader.GetString(3),
                        null,
                        System.Globalization.DateTimeStyles.RoundtripKind),
                    pendingMessagesReader.GetInt32(4)));
            }
        }

        var publishedEventCount = 0;

        foreach (var message in pendingMessages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var deliveryEvent = DeliveryMessageFactory.Create(
                    message.Id,
                    message.EventType,
                    message.Payload,
                    message.OccurredOnUtc,
                    _messagingOptions.DeliveryEventRoutePrefix);

                await _deliveryEventPublisher.PublishAsync(deliveryEvent, cancellationToken);

                await using var markPublishedCommand = connection.CreateCommand();
                markPublishedCommand.CommandText =
                    $"""
                    UPDATE {StorageNames.OutboxTable}
                    SET {StorageNames.PublishedOnUtc} = $publishedOnUtc,
                        error = NULL,
                        {StorageNames.LastAttemptUtc} = $lastAttemptUtc,
                        {StorageNames.NextAttemptUtc} = NULL
                    WHERE id = $id;
                    """;
                markPublishedCommand.Parameters.AddWithValue("$publishedOnUtc", _dateTimeProvider.UtcNow.ToString("O"));
                markPublishedCommand.Parameters.AddWithValue("$lastAttemptUtc", _dateTimeProvider.UtcNow.ToString("O"));
                markPublishedCommand.Parameters.AddWithValue("$id", message.Id.ToString());
                await markPublishedCommand.ExecuteNonQueryAsync(cancellationToken);

                publishedEventCount++;
            }
            catch (Exception ex)
            {
                var failedAttemptCount = message.RetryCount + 1;
                var failedAtUtc = _dateTimeProvider.UtcNow;
                var nextAttemptUtc = failedAttemptCount >= MaxPublishRetryCount
                    ? (DateTime?)null
                    : failedAtUtc.AddSeconds(15 * failedAttemptCount);

                _logger.LogError(
                    ex,
                    InfrastructureLogMessages.StoredEventPublishFailed,
                    message.Id,
                    failedAttemptCount);

                await using var publishFailureCommand = connection.CreateCommand();
                publishFailureCommand.CommandText =
                    $"""
                    UPDATE {StorageNames.OutboxTable}
                    SET error = $error,
                        {StorageNames.RetryCount} = $retryCount,
                        {StorageNames.LastAttemptUtc} = $lastAttemptUtc,
                        {StorageNames.NextAttemptUtc} = $nextAttemptUtc,
                        {StorageNames.DeadLetteredOnUtc} = $deadLetteredOnUtc
                    WHERE id = $id;
                    """;
                publishFailureCommand.Parameters.AddWithValue("$error", ex.Message);
                publishFailureCommand.Parameters.AddWithValue("$retryCount", failedAttemptCount);
                publishFailureCommand.Parameters.AddWithValue("$lastAttemptUtc", failedAtUtc.ToString("O"));
                publishFailureCommand.Parameters.AddWithValue("$nextAttemptUtc", (object?)nextAttemptUtc?.ToString("O") ?? DBNull.Value);
                publishFailureCommand.Parameters.AddWithValue(
                    "$deadLetteredOnUtc",
                    failedAttemptCount >= MaxPublishRetryCount ? failedAtUtc.ToString("O") : DBNull.Value);
                publishFailureCommand.Parameters.AddWithValue("$id", message.Id.ToString());
                await publishFailureCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        return publishedEventCount;
    }

    private sealed record OutboxMessageRecord(
        Guid Id,
        string EventType,
        string Payload,
        DateTime OccurredOnUtc,
        int RetryCount);
}
