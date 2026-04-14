namespace TrackMyDelivery.Domain.Deliveries;

public enum DeliveryStatus
{
    Created = 1,
    Assigned = 2,
    OutForDelivery = 3,
    Delayed = 4,
    Delivered = 5,
    Failed = 6
}
