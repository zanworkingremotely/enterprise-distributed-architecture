using Microsoft.Extensions.Logging;
using TrackMydelivery.Application.Interfaces;
using TrackMyDelivery.Application.Deliveries.Mappers;
using TrackMyDelivery.Application.Deliveries.Models;

namespace TrackMyDelivery.Application.Deliveries.Queries.GetAllDeliveries;

public sealed class GetAllDeliveriesQueryHandler
{
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly ILogger<GetAllDeliveriesQueryHandler> _logger;

    public GetAllDeliveriesQueryHandler(
        IDeliveryRepository deliveryRepository,
        ILogger<GetAllDeliveriesQueryHandler> logger)
    {
        _deliveryRepository = deliveryRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DeliveryDto>> HandleAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching all deliveries");
        var deliveries = await _deliveryRepository.GetAllAsync(cancellationToken);
        var items = new List<DeliveryDto>(deliveries.Count);

        foreach (var delivery in deliveries)
        {
            items.Add(DeliveryMappings.MapToDeliveryDto(delivery));
        }

        _logger.LogInformation("Fetched {DeliveryCount} deliveries", items.Count);
        return items;
    }
}
