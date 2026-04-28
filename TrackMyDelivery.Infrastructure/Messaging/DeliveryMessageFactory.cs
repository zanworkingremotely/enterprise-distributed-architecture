using System.Text.Json;
using TrackMyDelivery.Infrastructure.Constants;
using TrackMyDelivery.Domain.Deliveries.Events;

namespace TrackMyDelivery.Infrastructure.Messaging;

internal static class DeliveryMessageFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static DeliveryMessage Create(
        Guid outboxMessageId,
        string eventType,
        string payload,
        DateTime occurredOnUtc,
        string deliveryEventRoutePrefix)
    {
        return eventType switch
        {
            "TrackMyDelivery.Domain.Deliveries.Events.DeliveryCreatedDomainEvent" => CreateMessage(
                Deserialize<DeliveryCreatedDomainEvent>(outboxMessageId, payload),
                eventType,
                payload,
                $"{deliveryEventRoutePrefix}.created"),
            "TrackMyDelivery.Domain.Deliveries.Events.CourierAssignedDomainEvent" => CreateMessage(
                Deserialize<CourierAssignedDomainEvent>(outboxMessageId, payload),
                eventType,
                payload,
                $"{deliveryEventRoutePrefix}.assigned"),
            "TrackMyDelivery.Domain.Deliveries.Events.DeliveryStatusUpdatedDomainEvent" => CreateMessage(
                Deserialize<DeliveryStatusUpdatedDomainEvent>(outboxMessageId, payload),
                eventType,
                payload,
                $"{deliveryEventRoutePrefix}.status-updated"),
            _ => throw new InvalidOperationException($"Unsupported delivery event type '{eventType}'.")
        };
    }

    private static DeliveryMessage CreateMessage<TEvent>(
        TEvent deliveryEvent,
        string eventType,
        string payload,
        string routingKey)
        where TEvent : class
    {
        return deliveryEvent switch
        {
            DeliveryCreatedDomainEvent created => new DeliveryMessage
            {
                EventId = created.EventId,
                DeliveryId = created.DeliveryId,
                EventType = eventType,
                RoutingKey = routingKey,
                Payload = payload,
                OccurredOnUtc = created.OccurredOnUtc
            },
            CourierAssignedDomainEvent assigned => new DeliveryMessage
            {
                EventId = assigned.EventId,
                DeliveryId = assigned.DeliveryId,
                EventType = eventType,
                RoutingKey = routingKey,
                Payload = payload,
                OccurredOnUtc = assigned.OccurredOnUtc
            },
            DeliveryStatusUpdatedDomainEvent updated => new DeliveryMessage
            {
                EventId = updated.EventId,
                DeliveryId = updated.DeliveryId,
                EventType = eventType,
                RoutingKey = routingKey,
                Payload = payload,
                OccurredOnUtc = updated.OccurredOnUtc
            },
            _ => throw new InvalidOperationException($"Unsupported delivery event '{typeof(TEvent).Name}'.")
        };
    }

    private static TEvent Deserialize<TEvent>(Guid outboxMessageId, string payload)
        where TEvent : class
    {
        return JsonSerializer.Deserialize<TEvent>(payload, JsonOptions)
            ?? throw new InvalidOperationException($"Outbox message '{outboxMessageId}' could not be deserialized.");
    }
}
