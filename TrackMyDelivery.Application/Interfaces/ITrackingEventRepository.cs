using TrackMyDelivery.Application.Tracking.Models;

namespace TrackMydelivery.Application.Interfaces;

public interface ITrackingEventRepository
{
    Task<IReadOnlyList<TrackingTimelineItemDto>> GetTimelineAsync(Guid deliveryId, CancellationToken cancellationToken = default);
}
