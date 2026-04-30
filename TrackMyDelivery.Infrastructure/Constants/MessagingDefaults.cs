namespace TrackMyDelivery.Infrastructure.Constants;

public static class MessagingDefaults
{
    public const string DeliveryEventsExchange = "delivery-events";
    public const string TrackingUpdatesQueue = "tracking-timeline";
    public const string FailedDeliveryEventsExchange = "failed-delivery-events";
    public const string FailedTrackingUpdatesQueue = "failed-tracking-updates";
    public const string DeliveryEventRoutePrefix = "delivery";
    public const string FailedDeliveryEventRoute = "delivery.failed";
}
