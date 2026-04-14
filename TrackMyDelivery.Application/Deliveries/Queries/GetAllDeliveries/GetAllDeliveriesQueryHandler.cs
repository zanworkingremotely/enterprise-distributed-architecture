using TrackMydelivery.Application.Interfaces;
using TrackMyDelivery.Application.Deliveries.Mappers;
using TrackMyDelivery.Application.Deliveries.Models;

namespace TrackMyDelivery.Application.Deliveries.Queries.GetAllDeliveries;

public sealed class GetAllDeliveriesQueryHandler
{
    private readonly IDeliveryRepository _deliveryRepository;

    public GetAllDeliveriesQueryHandler(IDeliveryRepository deliveryRepository)
    {
        _deliveryRepository = deliveryRepository;
    }

    public async Task<IReadOnlyList<DeliveryDto>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var deliveries = await _deliveryRepository.GetAllAsync(cancellationToken);
        var items = new List<DeliveryDto>(deliveries.Count);

        foreach (var delivery in deliveries)
        {
            items.Add(DeliveryMappings.MapToDeliveryDto(delivery));
        }

        return items;
    }
}
