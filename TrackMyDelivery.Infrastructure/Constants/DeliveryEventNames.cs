namespace TrackMyDelivery.Infrastructure.Constants;

public static class DeliveryEventNames
{
    public const string DeliveryCreated = "TrackMyDelivery.Domain.Deliveries.Events.DeliveryCreatedDomainEvent";
    public const string CourierAssigned = "TrackMyDelivery.Domain.Deliveries.Events.CourierAssignedDomainEvent";
    public const string DeliveryStatusUpdated = "TrackMyDelivery.Domain.Deliveries.Events.DeliveryStatusUpdatedDomainEvent";

    public const string DeliveryCreatedRoute = "created";
    public const string CourierAssignedRoute = "assigned";
    public const string DeliveryStatusUpdatedRoute = "status-updated";

    public const string DeliveryCreatedTimelineEntry = "DeliveryCreated";
    public const string CourierAssignedTimelineEntry = "CourierAssigned";
    public const string DeliveryStatusUpdatedTimelineEntry = "DeliveryStatusUpdated";
}
