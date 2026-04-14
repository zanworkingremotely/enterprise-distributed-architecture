namespace TrackMyDelivery.Application.Tracking.Models;

public sealed class TrackingTimelineItemDto
{
    public Guid EventId { get; init; }
    public Guid DeliveryId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime OccurredOnUtc { get; init; }
}
