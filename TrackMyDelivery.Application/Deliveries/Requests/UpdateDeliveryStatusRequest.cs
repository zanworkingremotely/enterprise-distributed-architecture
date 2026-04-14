namespace TrackMyDelivery.Application.Deliveries.Requests;

public sealed class UpdateDeliveryStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
