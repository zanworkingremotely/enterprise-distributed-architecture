namespace TrackMydelivery.Application.Interfaces;

public interface IFailedDeliveryMessageReplay
{
    Task<int> ReplayAsync(int maxCount, CancellationToken cancellationToken = default);
}
