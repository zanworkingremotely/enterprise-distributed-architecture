namespace TrackMyDelivery.Application.Deliveries.Commands.AssignCourier;

public sealed record AssignCourierCommand(Guid DeliveryId, string CourierName);
