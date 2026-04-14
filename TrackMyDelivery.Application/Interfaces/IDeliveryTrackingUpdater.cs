namespace TrackMydelivery.Application.Interfaces;

public interface IDeliveryTrackingUpdater
{
    Task<int> UpdateTrackingTimelineAsync(CancellationToken cancellationToken = default);
}
