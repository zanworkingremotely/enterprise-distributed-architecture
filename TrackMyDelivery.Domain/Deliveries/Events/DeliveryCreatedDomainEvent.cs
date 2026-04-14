using TrackMyDelivery.Domain.Common;

namespace TrackMyDelivery.Domain.Deliveries.Events;

public sealed record DeliveryCreatedDomainEvent(
    Guid EventId,
    Guid DeliveryId,
    string TrackingNumber,
    string RecipientName,
    string DeliveryAddress,
    DateTime OccurredOnUtc) : IDomainEvent;
