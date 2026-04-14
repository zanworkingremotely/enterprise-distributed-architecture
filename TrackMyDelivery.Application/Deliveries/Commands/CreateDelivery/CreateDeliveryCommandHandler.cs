using TrackMydelivery.Application.Interfaces;
using TrackMyDelivery.Application.Deliveries.Mappers;
using TrackMyDelivery.Application.Deliveries.Models;
using TrackMyDelivery.Domain.Deliveries.Entities;

namespace TrackMyDelivery.Application.Deliveries.Commands.CreateDelivery;

public sealed class CreateDeliveryCommandHandler
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly IDeliveryEventRepository _deliveryEventRepository;

    public CreateDeliveryCommandHandler(
        IDateTimeProvider dateTimeProvider,
        IDeliveryRepository deliveryRepository,
        IDeliveryEventRepository deliveryEventRepository)
    {
        _dateTimeProvider = dateTimeProvider;
        _deliveryRepository = deliveryRepository;
        _deliveryEventRepository = deliveryEventRepository;
    }

    public async Task<DeliveryDto> HandleAsync(
        CreateDeliveryCommand command,
        CancellationToken cancellationToken = default)
    {
        var delivery = Delivery.Create(
            command.TrackingNumber,
            command.RecipientName,
            command.DeliveryAddress,
            _dateTimeProvider.UtcNow);

        await _deliveryRepository.AddAsync(delivery, cancellationToken);
        await _deliveryEventRepository.AddAsync(delivery.DequeueDomainEvents(), cancellationToken);

        return DeliveryMappings.MapToDeliveryDto(delivery);
    }
}
