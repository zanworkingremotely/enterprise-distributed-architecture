namespace TrackMyDelivery.Application.Deliveries.Commands.UpdateDeliveryStatus;

public sealed record UpdateDeliveryStatusCommand(Guid DeliveryId, string Status, string? Reason);
