using TrackMydelivery.Application.Interfaces;
using TrackMyDelivery.Application.Deliveries.Mappers;
using TrackMyDelivery.Application.Deliveries.Models;

namespace TrackMyDelivery.Application.Deliveries.Commands.AssignCourier;

public sealed class AssignCourierCommandHandler
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly IDeliveryEventRepository _deliveryEventRepository;

    public AssignCourierCommandHandler(
        IDateTimeProvider dateTimeProvider,
        IDeliveryRepository deliveryRepository,
        IDeliveryEventRepository deliveryEventRepository)
    {
        _dateTimeProvider = dateTimeProvider;
        _deliveryRepository = deliveryRepository;
        _deliveryEventRepository = deliveryEventRepository;
    }

    public async Task<DeliveryDto> HandleAsync(
        AssignCourierCommand command,
        CancellationToken cancellationToken = default)
    {
        var delivery = await _deliveryRepository.GetByIdAsync(command.DeliveryId, cancellationToken)
            ?? throw new InvalidOperationException($"Delivery '{command.DeliveryId}' was not found.");

        delivery.AssignCourier(command.CourierName, _dateTimeProvider.UtcNow);

        await _deliveryRepository.UpdateAsync(delivery, cancellationToken);
        await _deliveryEventRepository.AddAsync(delivery.DequeueDomainEvents(), cancellationToken);

        return DeliveryMappings.MapToDeliveryDto(delivery);
    }
}
