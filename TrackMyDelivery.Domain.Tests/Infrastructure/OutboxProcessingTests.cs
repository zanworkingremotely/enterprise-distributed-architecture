using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TrackMydelivery.Application.Interfaces;
using TrackMyDelivery.Domain.Deliveries.Entities;
using TrackMyDelivery.Infrastructure.Data;
using Xunit;

namespace TrackMyDelivery.Domain.Tests.Infrastructure;

public sealed class OutboxProcessingTests : IDisposable
{
    private readonly string _databasePath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ManualDateTimeProvider _dateTimeProvider;

    public OutboxProcessingTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), "track-my-delivery-tests", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:DatabasePath"] = _databasePath
            })
            .Build();

        _connectionFactory = new SqliteConnectionFactory(configuration);
        _dateTimeProvider = new ManualDateTimeProvider(new DateTime(2026, 4, 21, 10, 0, 0, DateTimeKind.Utc));
        new SqliteDatabaseInitializer(_connectionFactory).Initialize();
    }

    [Fact]
    public async Task DeliveryRepository_ShouldPersistDeliveryAndOutboxMessage()
    {
        var deliveryRepository = new SqliteDeliveryRepository(
            _connectionFactory,
            NullLogger<SqliteDeliveryRepository>.Instance);
        var delivery = Delivery.Create("TRK-2001", "Jane Doe", "123 Main Road", _dateTimeProvider.UtcNow);

        await deliveryRepository.AddAsync(delivery);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        var deliveryCount = await ExecuteScalarAsync<long>(connection, "SELECT COUNT(*) FROM deliveries;");
        var outboxCount = await ExecuteScalarAsync<long>(connection, "SELECT COUNT(*) FROM outbox_messages;");

        Assert.Equal(1, deliveryCount);
        Assert.Equal(1, outboxCount);
    }

    [Fact]
    public async Task DeliveryTrackingUpdater_ShouldProjectProcessedMessages()
    {
        var deliveryRepository = new SqliteDeliveryRepository(
            _connectionFactory,
            NullLogger<SqliteDeliveryRepository>.Instance);
        var updater = CreateUpdater();
        var delivery = Delivery.Create("TRK-2002", "Jane Doe", "123 Main Road", _dateTimeProvider.UtcNow);

        await deliveryRepository.AddAsync(delivery);

        var processedCount = await updater.UpdateTrackingTimelineAsync();

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        var processedOnUtc = await ExecuteScalarAsync<string?>(
            connection,
            "SELECT processed_on_utc FROM outbox_messages LIMIT 1;");
        var trackingCount = await ExecuteScalarAsync<long>(connection, "SELECT COUNT(*) FROM tracking_events;");

        Assert.Equal(1, processedCount);
        Assert.NotNull(processedOnUtc);
        Assert.Equal(1, trackingCount);
    }

    [Fact]
    public async Task DeliveryTrackingUpdater_ShouldSkipAlreadyProjectedDeliveryEvents()
    {
        var deliveryRepository = new SqliteDeliveryRepository(
            _connectionFactory,
            NullLogger<SqliteDeliveryRepository>.Instance);
        var updater = CreateUpdater();
        var delivery = Delivery.Create("TRK-2003", "Jane Doe", "123 Main Road", _dateTimeProvider.UtcNow);

        await deliveryRepository.AddAsync(delivery);

        var firstProjectedCount = await updater.UpdateTrackingTimelineAsync();
        Assert.Equal(1, firstProjectedCount);

        await using (var connection = _connectionFactory.CreateConnection())
        {
            await connection.OpenAsync();
            var originalMessage = await ReadOutboxMessageAsync(connection);
            await InsertDuplicateOutboxMessageAsync(connection, originalMessage);
        }

        var duplicateProjectedCount = await updater.UpdateTrackingTimelineAsync();

        await using var verificationConnection = _connectionFactory.CreateConnection();
        await verificationConnection.OpenAsync();

        var trackingCount = await ExecuteScalarAsync<long>(verificationConnection, "SELECT COUNT(*) FROM tracking_events;");
        var pendingOutboxCount = await ExecuteScalarAsync<long>(
            verificationConnection,
            "SELECT COUNT(*) FROM outbox_messages WHERE processed_on_utc IS NULL;");

        Assert.Equal(0, duplicateProjectedCount);
        Assert.Equal(1, trackingCount);
        Assert.Equal(0, pendingOutboxCount);
    }

    [Fact]
    public async Task DeliveryTrackingUpdater_ShouldRetryFailuresAndDeadLetterAfterMaxAttempts()
    {
        var updater = CreateUpdater();
        var messageId = Guid.NewGuid();

        await using (var connection = _connectionFactory.CreateConnection())
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO outbox_messages (
                    id,
                    type,
                    payload,
                    occurred_on_utc,
                    processed_on_utc,
                    error,
                    retry_count,
                    last_attempt_utc,
                    next_attempt_utc,
                    dead_lettered_on_utc
                ) VALUES (
                    $id,
                    $type,
                    $payload,
                    $occurredOnUtc,
                    NULL,
                    NULL,
                    0,
                    NULL,
                    NULL,
                    NULL
                );
                """;
            command.Parameters.AddWithValue("$id", messageId.ToString());
            command.Parameters.AddWithValue("$type", "TrackMyDelivery.Domain.Deliveries.Events.DeliveryCreatedDomainEvent");
            command.Parameters.AddWithValue("$payload", "{ invalid json }");
            command.Parameters.AddWithValue("$occurredOnUtc", _dateTimeProvider.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync();
        }

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var processedCount = await updater.UpdateTrackingTimelineAsync();
            Assert.Equal(0, processedCount);

            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync();

            var state = await ReadOutboxStateAsync(connection, messageId);

            Assert.Equal(attempt, state.RetryCount);
            Assert.NotNull(state.Error);
            Assert.NotNull(state.LastAttemptUtc);

            if (attempt < 3)
            {
                Assert.Null(state.DeadLetteredOnUtc);
                Assert.NotNull(state.NextAttemptUtc);
                _dateTimeProvider.UtcNow = _dateTimeProvider.UtcNow.AddMinutes(1);
            }
            else
            {
                Assert.Null(state.NextAttemptUtc);
                Assert.NotNull(state.DeadLetteredOnUtc);
            }
        }
    }

    public void Dispose()
    {
        if (!File.Exists(_databasePath))
        {
            return;
        }

        try
        {
            File.Delete(_databasePath);
        }
        catch (IOException)
        {
            //to log
        }
        catch (UnauthorizedAccessException)
        {
            // to log
        }
    }

    private DeliveryTrackingUpdater CreateUpdater()
    {
        return new DeliveryTrackingUpdater(
            _connectionFactory,
            _dateTimeProvider,
            NullLogger<DeliveryTrackingUpdater>.Instance);
    }

    private static async Task<T?> ExecuteScalarAsync<T>(SqliteConnection connection, string commandText)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        var result = await command.ExecuteScalarAsync();

        if (result is null or DBNull)
        {
            return default;
        }

        return (T)result;
    }

    private static async Task<OutboxState> ReadOutboxStateAsync(SqliteConnection connection, Guid messageId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT retry_count, error, last_attempt_utc, next_attempt_utc, dead_lettered_on_utc
            FROM outbox_messages
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", messageId.ToString());

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        return new OutboxState(
            reader.GetInt32(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4));
    }

    private static async Task<OutboxMessage> ReadOutboxMessageAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT type, payload, occurred_on_utc
            FROM outbox_messages
            LIMIT 1;
            """;

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        return new OutboxMessage(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2));
    }

    private static async Task InsertDuplicateOutboxMessageAsync(SqliteConnection connection, OutboxMessage originalMessage)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO outbox_messages (
                id,
                type,
                payload,
                occurred_on_utc,
                processed_on_utc,
                error,
                retry_count,
                last_attempt_utc,
                next_attempt_utc,
                dead_lettered_on_utc
            ) VALUES (
                $id,
                $type,
                $payload,
                $occurredOnUtc,
                NULL,
                NULL,
                0,
                NULL,
                NULL,
                NULL
            );
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("$type", originalMessage.Type);
        command.Parameters.AddWithValue("$payload", originalMessage.Payload);
        command.Parameters.AddWithValue("$occurredOnUtc", originalMessage.OccurredOnUtc);

        await command.ExecuteNonQueryAsync();
    }

    private sealed record OutboxState(
        int RetryCount,
        string? Error,
        string? LastAttemptUtc,
        string? NextAttemptUtc,
        string? DeadLetteredOnUtc);

    private sealed record OutboxMessage(string Type, string Payload, string OccurredOnUtc);

    private sealed class ManualDateTimeProvider : IDateTimeProvider
    {
        public ManualDateTimeProvider(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; set; }
    }
}
