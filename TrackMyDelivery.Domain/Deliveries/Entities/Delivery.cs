using TrackMyDelivery.Domain.Common;
using TrackMyDelivery.Domain.Deliveries.Events;

namespace TrackMyDelivery.Domain.Deliveries.Entities;

public sealed class Delivery : AggregateRoot
{
    private Delivery()
    {
    }

    public Guid Id { get; private set; }
    public string TrackingNumber { get; private set; } = string.Empty;
    public string RecipientName { get; private set; } = string.Empty;
    public string DeliveryAddress { get; private set; } = string.Empty;
    public string? AssignedCourier { get; private set; }
    public DeliveryStatus CurrentStatus { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static Delivery Create(
        string trackingNumber,
        string recipientName,
        string deliveryAddress,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber))
        {
            throw new ArgumentException("Tracking number is required.", nameof(trackingNumber));
        }

        if (string.IsNullOrWhiteSpace(recipientName))
        {
            throw new ArgumentException("Recipient name is required.", nameof(recipientName));
        }

        if (string.IsNullOrWhiteSpace(deliveryAddress))
        {
            throw new ArgumentException("Delivery address is required.", nameof(deliveryAddress));
        }

        var delivery = new Delivery
        {
            Id = Guid.NewGuid(),
            TrackingNumber = trackingNumber.Trim(),
            RecipientName = recipientName.Trim(),
            DeliveryAddress = deliveryAddress.Trim(),
            CurrentStatus = DeliveryStatus.Created,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };

        delivery.RaiseDomainEvent(new DeliveryCreatedDomainEvent(
            Guid.NewGuid(),
            delivery.Id,
            delivery.TrackingNumber,
            delivery.RecipientName,
            delivery.DeliveryAddress,
            nowUtc));

        return delivery;
    }

    public void AssignCourier(string courierName, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(courierName))
        {
            throw new ArgumentException("Courier name is required.", nameof(courierName));
        }

        AssignedCourier = courierName.Trim();
        CurrentStatus = DeliveryStatus.Assigned;
        UpdatedAtUtc = nowUtc;

        RaiseDomainEvent(new CourierAssignedDomainEvent(
            Guid.NewGuid(),
            Id,
            AssignedCourier,
            nowUtc));
    }

    public void UpdateStatus(DeliveryStatus status, string? reason, DateTime nowUtc)
    {
        if (status == DeliveryStatus.Assigned && string.IsNullOrWhiteSpace(AssignedCourier))
        {
            throw new InvalidOperationException("A courier must be assigned before setting the delivery to assigned.");
        }

        CurrentStatus = status;
        UpdatedAtUtc = nowUtc;

        RaiseDomainEvent(new DeliveryStatusUpdatedDomainEvent(
            Guid.NewGuid(),
            Id,
            status.ToString(),
            string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            nowUtc));
    }

    public static Delivery Restore(DeliveryState state)
    {
        return new Delivery
        {
            Id = state.Id,
            TrackingNumber = state.TrackingNumber,
            RecipientName = state.RecipientName,
            DeliveryAddress = state.DeliveryAddress,
            AssignedCourier = state.AssignedCourier,
            CurrentStatus = state.CurrentStatus,
            CreatedAtUtc = state.CreatedAtUtc,
            UpdatedAtUtc = state.UpdatedAtUtc
        };
    }
}
