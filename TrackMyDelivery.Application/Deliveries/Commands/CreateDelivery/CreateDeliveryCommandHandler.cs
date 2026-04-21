using Microsoft.Extensions.Logging;
using TrackMydelivery.Application.Interfaces;
using TrackMyDelivery.Application.Deliveries.Mappers;
using TrackMyDelivery.Application.Deliveries.Models;
using TrackMyDelivery.Domain.Deliveries.Entities;

namespace TrackMyDelivery.Application.Deliveries.Commands.CreateDelivery;

public sealed class CreateDeliveryCommandHandler
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly ILogger<CreateDeliveryCommandHandler> _logger;

    public CreateDeliveryCommandHandler(
        IDateTimeProvider dateTimeProvider,
        IDeliveryRepository deliveryRepository,
        ILogger<CreateDeliveryCommandHandler> logger)
    {
        _dateTimeProvider = dateTimeProvider;
        _deliveryRepository = deliveryRepository;
        _logger = logger;
    }

    public async Task<DeliveryDto> HandleAsync(
        CreateDeliveryCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating delivery for tracking number {TrackingNumber} and recipient {RecipientName}",
            command.TrackingNumber,
            command.RecipientName);

        var delivery = Delivery.Create(
            command.TrackingNumber,
            command.RecipientName,
            command.DeliveryAddress,
            _dateTimeProvider.UtcNow);

        await _deliveryRepository.AddAsync(delivery, cancellationToken);

        _logger.LogInformation(
            "Created delivery {DeliveryId} for tracking number {TrackingNumber}",
            delivery.Id,
            delivery.TrackingNumber);

        return DeliveryMappings.MapToDeliveryDto(delivery);
    }
}
