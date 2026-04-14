using TrackMydelivery.Application.Interfaces;
using TrackMyDelivery.Application.Deliveries.Mappers;
using TrackMyDelivery.Application.Deliveries.Models;

namespace TrackMyDelivery.Application.Deliveries.Queries.GetDeliveryById;

public sealed class GetDeliveryByIdQueryHandler
{
    private readonly IDeliveryRepository _deliveryRepository;

    public GetDeliveryByIdQueryHandler(IDeliveryRepository deliveryRepository)
    {
        _deliveryRepository = deliveryRepository;
    }

    public async Task<DeliveryDto?> HandleAsync(Guid deliveryId, CancellationToken cancellationToken = default)
    {
        var delivery = await _deliveryRepository.GetByIdAsync(deliveryId, cancellationToken);
        return delivery is null
            ? null
            : DeliveryMappings.MapToDeliveryDto(delivery);
    }
}
