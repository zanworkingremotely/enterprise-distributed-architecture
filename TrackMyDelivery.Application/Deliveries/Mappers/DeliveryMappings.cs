using TrackMyDelivery.Application.Deliveries.Models;
using TrackMyDelivery.Domain.Deliveries.Entities;

namespace TrackMyDelivery.Application.Deliveries.Mappers;

internal static class DeliveryMappings
{
    public static DeliveryDto MapToDeliveryDto(Delivery delivery)
    {
        return new DeliveryDto
        {
            Id = delivery.Id,
            TrackingNumber = delivery.TrackingNumber,
            RecipientName = delivery.RecipientName,
            DeliveryAddress = delivery.DeliveryAddress,
            AssignedCourier = delivery.AssignedCourier,
            CurrentStatus = delivery.CurrentStatus.ToString(),
            CreatedAtUtc = delivery.CreatedAtUtc,
            UpdatedAtUtc = delivery.UpdatedAtUtc
        };
    }
}
