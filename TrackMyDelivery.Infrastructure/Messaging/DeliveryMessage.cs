namespace TrackMyDelivery.Infrastructure.Messaging;

public sealed class DeliveryMessage
{
    public Guid EventId { get; init; }
    public Guid DeliveryId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string RoutingKey { get; init; } = string.Empty;
    public string Payload { get; init; } = string.Empty;
    public DateTime OccurredOnUtc { get; init; }
}
