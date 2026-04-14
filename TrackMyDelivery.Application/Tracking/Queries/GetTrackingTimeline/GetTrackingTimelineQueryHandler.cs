using TrackMydelivery.Application.Interfaces;
using TrackMyDelivery.Application.Tracking.Models;

namespace TrackMyDelivery.Application.Tracking.Queries.GetTrackingTimeline;

public sealed class GetTrackingTimelineQueryHandler
{
    private readonly ITrackingEventRepository _trackingEventRepository;

    public GetTrackingTimelineQueryHandler(ITrackingEventRepository trackingEventRepository)
    {
        _trackingEventRepository = trackingEventRepository;
    }

    public Task<IReadOnlyList<TrackingTimelineItemDto>> HandleAsync(
        Guid deliveryId,
        CancellationToken cancellationToken = default)
    {
        return _trackingEventRepository.GetTimelineAsync(deliveryId, cancellationToken);
    }
}
