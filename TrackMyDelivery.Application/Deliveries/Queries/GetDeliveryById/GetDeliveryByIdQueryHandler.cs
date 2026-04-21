using Microsoft.Extensions.Logging;
using TrackMydelivery.Application.Interfaces;
using TrackMyDelivery.Application.Deliveries.Mappers;
using TrackMyDelivery.Application.Deliveries.Models;

namespace TrackMyDelivery.Application.Deliveries.Queries.GetDeliveryById;

public sealed class GetDeliveryByIdQueryHandler
{
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly ILogger<GetDeliveryByIdQueryHandler> _logger;

    public GetDeliveryByIdQueryHandler(
        IDeliveryRepository deliveryRepository,
        ILogger<GetDeliveryByIdQueryHandler> logger)
    {
        _deliveryRepository = deliveryRepository;
        _logger = logger;
    }

    public async Task<DeliveryDto?> HandleAsync(Guid deliveryId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching delivery {DeliveryId}", deliveryId);
        var delivery = await _deliveryRepository.GetByIdAsync(deliveryId, cancellationToken);
        if (delivery is null)
        {
            _logger.LogWarning("Delivery {DeliveryId} was not found", deliveryId);
            return null;
        }

        _logger.LogInformation("Fetched delivery {DeliveryId}", deliveryId);
        return DeliveryMappings.MapToDeliveryDto(delivery);
    }
}
