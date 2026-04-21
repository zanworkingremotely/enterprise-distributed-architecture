using Microsoft.Extensions.Logging;
using TrackMydelivery.Application.Interfaces;
using TrackMyDelivery.Application.Tracking.Models;

namespace TrackMyDelivery.Application.Tracking.Queries.GetTrackingTimeline;

public sealed class GetTrackingTimelineQueryHandler
{
    private readonly ITrackingEventRepository _trackingEventRepository;
    private readonly ILogger<GetTrackingTimelineQueryHandler> _logger;

    public GetTrackingTimelineQueryHandler(
        ITrackingEventRepository trackingEventRepository,
        ILogger<GetTrackingTimelineQueryHandler> logger)
    {
        _trackingEventRepository = trackingEventRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TrackingTimelineItemDto>> HandleAsync(
        Guid deliveryId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching tracking timeline for delivery {DeliveryId}", deliveryId);
        var timeline = await _trackingEventRepository.GetTimelineAsync(deliveryId, cancellationToken);
        _logger.LogInformation(
            "Fetched {TimelineEventCount} tracking events for delivery {DeliveryId}",
            timeline.Count,
            deliveryId);
        return timeline;
    }
}
