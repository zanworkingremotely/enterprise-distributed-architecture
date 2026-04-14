namespace TrackMyDelivery.Application.Deliveries.Models;

public sealed class DeliveryDto
{
    public Guid Id { get; init; }
    public string TrackingNumber { get; init; } = string.Empty;
    public string RecipientName { get; init; } = string.Empty;
    public string DeliveryAddress { get; init; } = string.Empty;
    public string? AssignedCourier { get; init; }
    public string CurrentStatus { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}
