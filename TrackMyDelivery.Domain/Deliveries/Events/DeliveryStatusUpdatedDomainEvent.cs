using TrackMyDelivery.Domain.Common;

namespace TrackMyDelivery.Domain.Deliveries.Events;

public sealed record DeliveryStatusUpdatedDomainEvent(
    Guid EventId,
    Guid DeliveryId,
    string Status,
    string? Reason,
    DateTime OccurredOnUtc) : IDomainEvent;
