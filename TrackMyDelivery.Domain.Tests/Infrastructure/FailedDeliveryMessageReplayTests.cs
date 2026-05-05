using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrackMydelivery.Application.Interfaces;
using TrackMyDelivery.Infrastructure.Configuration;
using TrackMyDelivery.Infrastructure.Constants;
using TrackMyDelivery.Infrastructure.Data;
using TrackMyDelivery.Infrastructure.Messaging;
using Xunit;

namespace TrackMyDelivery.Domain.Tests.Infrastructure;

public sealed class FailedDeliveryMessageReplayTests : IDisposable
{
    private readonly string _databasePath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ManualDateTimeProvider _dateTimeProvider;

    public FailedDeliveryMessageReplayTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), "track-my-delivery-failed-replay-tests", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:DatabasePath"] = _databasePath
            })
            .Build();

        _connectionFactory = new SqliteConnectionFactory(configuration);
        _dateTimeProvider = new ManualDateTimeProvider(new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc));
        new SqliteDatabaseInitializer(_connectionFactory).Initialize();
    }

    [Fact]
    public async Task FailedDeliveryMessageReplay_ShouldRecordAndReplayParkedDeliveryMessage()
    {
        var deliveryEventPublisher = new FakeDeliveryEventPublisher();
        var failedDeliveryMessageReplay = CreateFailedDeliveryMessageReplay(
            deliveryEventPublisher,
            enabled: true);
        var deliveryMessage = new DeliveryMessage
        {
            EventId = Guid.NewGuid(),
            DeliveryId = Guid.NewGuid(),
            CorrelationId = "corr-replay-test-1001",
            EventType = "TrackMyDelivery.Domain.Deliveries.Events.DeliveryCreatedDomainEvent",
            RoutingKey = "delivery.created",
            Payload = "{\"deliveryId\":\"abc\"}",
            OccurredOnUtc = _dateTimeProvider.UtcNow.AddMinutes(-3)
        };

        await failedDeliveryMessageReplay.RecordParkedMessageAsync(
            deliveryMessage,
            attemptCount: 3,
            failureReason: "Tracking projection exploded.");

        var replayedCount = await failedDeliveryMessageReplay.ReplayAsync(10);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        var replayedOnUtc = await ExecuteScalarAsync<string?>(
            connection,
            $"SELECT {StorageNames.ReplayedOnUtc} FROM {StorageNames.FailedDeliveryTable} WHERE {StorageNames.EventId} = '{deliveryMessage.EventId}';");

        Assert.Equal(1, replayedCount);
        Assert.Single(deliveryEventPublisher.PublishedEvents);
        Assert.Equal(deliveryMessage.EventId, deliveryEventPublisher.PublishedEvents[0].EventId);
        Assert.Equal(deliveryMessage.CorrelationId, deliveryEventPublisher.PublishedEvents[0].CorrelationId);
        Assert.NotNull(replayedOnUtc);
    }

    [Fact]
    public async Task FailedDeliveryMessageReplay_ShouldNotReplayWhenMessagingIsDisabled()
    {
        var deliveryEventPublisher = new FakeDeliveryEventPublisher();
        var failedDeliveryMessageReplay = CreateFailedDeliveryMessageReplay(
            deliveryEventPublisher,
            enabled: false);
        var deliveryMessage = new DeliveryMessage
        {
            EventId = Guid.NewGuid(),
            DeliveryId = Guid.NewGuid(),
            EventType = "TrackMyDelivery.Domain.Deliveries.Events.DeliveryCreatedDomainEvent",
            RoutingKey = "delivery.created",
            Payload = "{\"deliveryId\":\"abc\"}",
            OccurredOnUtc = _dateTimeProvider.UtcNow
        };

        await failedDeliveryMessageReplay.RecordParkedMessageAsync(
            deliveryMessage,
            attemptCount: 3,
            failureReason: "Broker was unavailable.");

        var replayedCount = await failedDeliveryMessageReplay.ReplayAsync(5);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        var replayedOnUtc = await ExecuteScalarAsync<string?>(
            connection,
            $"SELECT {StorageNames.ReplayedOnUtc} FROM {StorageNames.FailedDeliveryTable} WHERE {StorageNames.EventId} = '{deliveryMessage.EventId}';");

        Assert.Equal(0, replayedCount);
        Assert.Empty(deliveryEventPublisher.PublishedEvents);
        Assert.Null(replayedOnUtc);
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
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private FailedDeliveryMessageReplay CreateFailedDeliveryMessageReplay(
        IDeliveryEventPublisher deliveryEventPublisher,
        bool enabled)
    {
        return new FailedDeliveryMessageReplay(
            _connectionFactory,
            _dateTimeProvider,
            deliveryEventPublisher,
            Options.Create(new MessagingOptions
            {
                Enabled = enabled,
                DeliveryEventRoutePrefix = "delivery"
            }),
            NullLogger<FailedDeliveryMessageReplay>.Instance);
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

    private sealed class FakeDeliveryEventPublisher : IDeliveryEventPublisher
    {
        public List<DeliveryMessage> PublishedEvents { get; } = [];

        public Task PublishAsync(DeliveryMessage deliveryEvent, CancellationToken cancellationToken = default)
        {
            PublishedEvents.Add(deliveryEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class ManualDateTimeProvider : IDateTimeProvider
    {
        public ManualDateTimeProvider(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; set; }
    }
}
