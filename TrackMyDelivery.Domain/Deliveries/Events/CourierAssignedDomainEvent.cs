using TrackMyDelivery.Domain.Common;

namespace TrackMyDelivery.Domain.Deliveries.Events;

public sealed record CourierAssignedDomainEvent(
    Guid EventId,
    Guid DeliveryId,
    string CourierName,
    DateTime OccurredOnUtc) : IDomainEvent;
