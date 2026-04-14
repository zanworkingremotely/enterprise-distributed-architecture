namespace TrackMyDelivery.Application.Deliveries.Requests;

public sealed class CreateDeliveryRequest
{
    public string TrackingNumber { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
}
