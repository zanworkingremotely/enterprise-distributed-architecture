namespace TrackMyDelivery.Application.Deliveries.Commands.CreateDelivery;

public sealed record CreateDeliveryCommand(
    string TrackingNumber,
    string RecipientName,
    string DeliveryAddress);
