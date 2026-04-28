using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using TrackMyDelivery.Domain.Deliveries.Events;
using TrackMyDelivery.Infrastructure.Constants;
using TrackMyDelivery.Infrastructure.Data;

namespace TrackMyDelivery.Infrastructure.Messaging;

public sealed class TrackingTimelineUpdater : ITrackingTimelineUpdater
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ILogger<TrackingTimelineUpdater> _logger;

    public TrackingTimelineUpdater(
        SqliteConnectionFactory connectionFactory,
        ILogger<TrackingTimelineUpdater> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<bool> ApplyDeliveryEventAsync(DeliveryMessage deliveryEvent, CancellationToken cancellationToken = default)
    {
        var trackingEvent = MapTrackingEntry(deliveryEvent);
        if (trackingEvent is null)
        {
            _logger.LogWarning(
                InfrastructureLogMessages.UnsupportedDeliveryEvent,
                deliveryEvent.EventId,
                deliveryEvent.EventType);
            return false;
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var insertTrackingCommand = connection.CreateCommand();
        insertTrackingCommand.CommandText =
            $"""
            INSERT OR IGNORE INTO {StorageNames.TrackingTimelineTable} (
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

        var insertedCount = await insertTrackingCommand.ExecuteNonQueryAsync(cancellationToken);
        if (insertedCount == 0)
        {
            _logger.LogInformation(
                InfrastructureLogMessages.DeliveryEventSkipped,
                trackingEvent.EventId);
            return false;
        }

        _logger.LogInformation(
            InfrastructureLogMessages.DeliveryEventProjected,
            trackingEvent.EventId,
            trackingEvent.DeliveryId);

        return true;
    }

    private static TrackingEventRecord? MapTrackingEntry(DeliveryMessage deliveryEvent)
    {
        return deliveryEvent.EventType switch
        {
            DeliveryEventNames.DeliveryCreated => CreateTrackingEvent(
                Deserialize<DeliveryCreatedDomainEvent>(deliveryEvent),
                DeliveryEventNames.DeliveryCreatedTimelineEntry,
                domainEvent => $"Delivery created for {domainEvent.RecipientName}."),
            DeliveryEventNames.CourierAssigned => CreateTrackingEvent(
                Deserialize<CourierAssignedDomainEvent>(deliveryEvent),
                DeliveryEventNames.CourierAssignedTimelineEntry,
                domainEvent => $"Courier assigned: {domainEvent.CourierName}."),
            DeliveryEventNames.DeliveryStatusUpdated => CreateTrackingEvent(
                Deserialize<DeliveryStatusUpdatedDomainEvent>(deliveryEvent),
                DeliveryEventNames.DeliveryStatusUpdatedTimelineEntry,
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

    private static TEvent Deserialize<TEvent>(DeliveryMessage deliveryEvent)
        where TEvent : class
    {
        return JsonSerializer.Deserialize<TEvent>(deliveryEvent.Payload, JsonOptions)
            ?? throw new InvalidOperationException($"Delivery event '{deliveryEvent.EventId}' could not be deserialized.");
    }

    private sealed record TrackingEventRecord(
        Guid EventId,
        Guid DeliveryId,
        string EventType,
        string Description,
        DateTime OccurredOnUtc);
}
