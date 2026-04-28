namespace TrackMyDelivery.Infrastructure.Messaging;

public interface IDeliveryEventPublisher
{
    Task PublishAsync(DeliveryMessage deliveryEvent, CancellationToken cancellationToken = default);
}
