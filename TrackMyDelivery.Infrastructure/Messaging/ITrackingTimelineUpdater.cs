namespace TrackMyDelivery.Infrastructure.Messaging;

public interface ITrackingTimelineUpdater
{
    Task<bool> ApplyDeliveryEventAsync(DeliveryMessage deliveryEvent, CancellationToken cancellationToken = default);
}
