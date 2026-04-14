using TrackMydelivery.Application.Interfaces;
using TrackMyDelivery.Application.Deliveries.Mappers;
using TrackMyDelivery.Application.Deliveries.Models;
using TrackMyDelivery.Domain.Deliveries;

namespace TrackMyDelivery.Application.Deliveries.Commands.UpdateDeliveryStatus;

public sealed class UpdateDeliveryStatusCommandHandler
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly IDeliveryEventRepository _deliveryEventRepository;

    public UpdateDeliveryStatusCommandHandler(
        IDateTimeProvider dateTimeProvider,
        IDeliveryRepository deliveryRepository,
        IDeliveryEventRepository deliveryEventRepository)
    {
        _dateTimeProvider = dateTimeProvider;
        _deliveryRepository = deliveryRepository;
        _deliveryEventRepository = deliveryEventRepository;
    }

    public async Task<DeliveryDto> HandleAsync(
        UpdateDeliveryStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        var delivery = await _deliveryRepository.GetByIdAsync(command.DeliveryId, cancellationToken)
            ?? throw new InvalidOperationException($"Delivery '{command.DeliveryId}' was not found.");

        if (!Enum.TryParse<DeliveryStatus>(command.Status, true, out var status))
        {
            throw new InvalidOperationException($"'{command.Status}' is not a valid delivery status.");
        }

        delivery.UpdateStatus(status, command.Reason, _dateTimeProvider.UtcNow);

        await _deliveryRepository.UpdateAsync(delivery, cancellationToken);
        await _deliveryEventRepository.AddAsync(delivery.DequeueDomainEvents(), cancellationToken);

        return DeliveryMappings.MapToDeliveryDto(delivery);
    }
}
