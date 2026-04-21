using Microsoft.Extensions.Logging;
using TrackMydelivery.Application.Interfaces;
using TrackMyDelivery.Application.Deliveries.Mappers;
using TrackMyDelivery.Application.Deliveries.Models;

namespace TrackMyDelivery.Application.Deliveries.Commands.AssignCourier;

public sealed class AssignCourierCommandHandler
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly ILogger<AssignCourierCommandHandler> _logger;

    public AssignCourierCommandHandler(
        IDateTimeProvider dateTimeProvider,
        IDeliveryRepository deliveryRepository,
        ILogger<AssignCourierCommandHandler> logger)
    {
        _dateTimeProvider = dateTimeProvider;
        _deliveryRepository = deliveryRepository;
        _logger = logger;
    }

    public async Task<DeliveryDto> HandleAsync(
        AssignCourierCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Assigning courier {CourierName} to delivery {DeliveryId}",
            command.CourierName,
            command.DeliveryId);

        var delivery = await _deliveryRepository.GetByIdAsync(command.DeliveryId, cancellationToken)
            ?? throw LogDeliveryNotFound(command.DeliveryId);

        delivery.AssignCourier(command.CourierName, _dateTimeProvider.UtcNow);

        await _deliveryRepository.UpdateAsync(delivery, cancellationToken);

        _logger.LogInformation(
            "Assigned courier {CourierName} to delivery {DeliveryId}",
            delivery.AssignedCourier,
            delivery.Id);

        return DeliveryMappings.MapToDeliveryDto(delivery);
    }

    private InvalidOperationException LogDeliveryNotFound(Guid deliveryId)
    {
        _logger.LogWarning("Cannot assign courier because delivery {DeliveryId} was not found", deliveryId);
        return new InvalidOperationException($"Delivery '{deliveryId}' was not found.");
    }
}
