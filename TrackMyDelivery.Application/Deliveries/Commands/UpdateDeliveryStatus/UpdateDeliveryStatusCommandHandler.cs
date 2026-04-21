using Microsoft.Extensions.Logging;
using TrackMydelivery.Application.Interfaces;
using TrackMyDelivery.Application.Deliveries.Mappers;
using TrackMyDelivery.Application.Deliveries.Models;
using TrackMyDelivery.Domain.Deliveries;

namespace TrackMyDelivery.Application.Deliveries.Commands.UpdateDeliveryStatus;

public sealed class UpdateDeliveryStatusCommandHandler
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly ILogger<UpdateDeliveryStatusCommandHandler> _logger;

    public UpdateDeliveryStatusCommandHandler(
        IDateTimeProvider dateTimeProvider,
        IDeliveryRepository deliveryRepository,
        ILogger<UpdateDeliveryStatusCommandHandler> logger)
    {
        _dateTimeProvider = dateTimeProvider;
        _deliveryRepository = deliveryRepository;
        _logger = logger;
    }

    public async Task<DeliveryDto> HandleAsync(
        UpdateDeliveryStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Updating delivery {DeliveryId} to status {RequestedStatus}",
            command.DeliveryId,
            command.Status);

        var delivery = await _deliveryRepository.GetByIdAsync(command.DeliveryId, cancellationToken)
            ?? throw LogDeliveryNotFound(command.DeliveryId);

        if (!Enum.TryParse<DeliveryStatus>(command.Status, true, out var status))
        {
            throw LogInvalidStatus(command.DeliveryId, command.Status);
        }

        delivery.UpdateStatus(status, command.Reason, _dateTimeProvider.UtcNow);

        await _deliveryRepository.UpdateAsync(delivery, cancellationToken);

        _logger.LogInformation(
            "Updated delivery {DeliveryId} to status {CurrentStatus}",
            delivery.Id,
            delivery.CurrentStatus);

        return DeliveryMappings.MapToDeliveryDto(delivery);
    }

    private InvalidOperationException LogDeliveryNotFound(Guid deliveryId)
    {
        _logger.LogWarning("Cannot update status because delivery {DeliveryId} was not found", deliveryId);
        return new InvalidOperationException($"Delivery '{deliveryId}' was not found.");
    }

    private InvalidOperationException LogInvalidStatus(Guid deliveryId, string requestedStatus)
    {
        _logger.LogWarning(
            "Cannot update delivery {DeliveryId} because status {RequestedStatus} is invalid",
            deliveryId,
            requestedStatus);
        return new InvalidOperationException($"'{requestedStatus}' is not a valid delivery status.");
    }
}
