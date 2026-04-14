namespace TrackMyDelivery.Domain.Deliveries;

public sealed record DeliveryState(
    Guid Id,
    string TrackingNumber,
    string RecipientName,
    string DeliveryAddress,
    string? AssignedCourier,
    DeliveryStatus CurrentStatus,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
