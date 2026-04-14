using TrackMyDelivery.Domain.Deliveries;
using TrackMyDelivery.Domain.Deliveries.Entities;
using TrackMyDelivery.Domain.Deliveries.Events;
using Xunit;

namespace TrackMyDelivery.Domain.Tests.Deliveries;

public class DeliveryTests
{
    [Fact]
    public void Create_Should_SetInitialState_AndRaiseDeliveryCreatedEvent()
    {
        var nowUtc = new DateTime(2026, 4, 14, 8, 30, 0, DateTimeKind.Utc);

        var delivery = Delivery.Create(
            "TRK-1001",
            "Jane Doe",
            "123 Main Road",
            nowUtc);

        Assert.Equal("TRK-1001", delivery.TrackingNumber);
        Assert.Equal("Jane Doe", delivery.RecipientName);
        Assert.Equal("123 Main Road", delivery.DeliveryAddress);
        Assert.Equal(DeliveryStatus.Created, delivery.CurrentStatus);
        Assert.Equal(nowUtc, delivery.CreatedAtUtc);
        Assert.Equal(nowUtc, delivery.UpdatedAtUtc);

        var domainEvents = delivery.DequeueDomainEvents();
        var createdEvent = Assert.Single(domainEvents);

        Assert.IsType<DeliveryCreatedDomainEvent>(createdEvent);
    }

    [Fact]
    public void AssignCourier_Should_SetCourier_AndRaiseCourierAssignedEvent()
    {
        var delivery = Delivery.Create(
            "TRK-1002",
            "Jane Doe",
            "123 Main Road",
            new DateTime(2026, 4, 14, 8, 30, 0, DateTimeKind.Utc));

        delivery.DequeueDomainEvents();

        var assignedAtUtc = new DateTime(2026, 4, 14, 9, 00, 0, DateTimeKind.Utc);
        delivery.AssignCourier("Sipho Mokoena", assignedAtUtc);

        Assert.Equal("Sipho Mokoena", delivery.AssignedCourier);
        Assert.Equal(DeliveryStatus.Assigned, delivery.CurrentStatus);
        Assert.Equal(assignedAtUtc, delivery.UpdatedAtUtc);

        var domainEvents = delivery.DequeueDomainEvents();
        var assignedEvent = Assert.Single(domainEvents);

        Assert.IsType<CourierAssignedDomainEvent>(assignedEvent);
    }

    [Fact]
    public void UpdateStatus_Should_Throw_WhenAssigningStatusWithoutCourier()
    {
        var delivery = Delivery.Create(
            "TRK-1003",
            "Jane Doe",
            "123 Main Road",
            new DateTime(2026, 4, 14, 8, 30, 0, DateTimeKind.Utc));

        delivery.DequeueDomainEvents();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            delivery.UpdateStatus(
                DeliveryStatus.Assigned,
                null,
                new DateTime(2026, 4, 14, 9, 15, 0, DateTimeKind.Utc)));

        Assert.Equal("A courier must be assigned before setting the delivery to assigned.", exception.Message);
    }

    [Fact]
    public void UpdateStatus_Should_ChangeStatus_AndRaiseStatusUpdatedEvent()
    {
        var delivery = Delivery.Create(
            "TRK-1004",
            "Jane Doe",
            "123 Main Road",
            new DateTime(2026, 4, 14, 8, 30, 0, DateTimeKind.Utc));

        delivery.DequeueDomainEvents();
        delivery.AssignCourier("Sipho Mokoena", new DateTime(2026, 4, 14, 9, 00, 0, DateTimeKind.Utc));
        delivery.DequeueDomainEvents();

        var updatedAtUtc = new DateTime(2026, 4, 14, 10, 00, 0, DateTimeKind.Utc);
        delivery.UpdateStatus(DeliveryStatus.OutForDelivery, null, updatedAtUtc);

        Assert.Equal(DeliveryStatus.OutForDelivery, delivery.CurrentStatus);
        Assert.Equal(updatedAtUtc, delivery.UpdatedAtUtc);

        var domainEvents = delivery.DequeueDomainEvents();
        var updatedEvent = Assert.Single(domainEvents);

        Assert.IsType<DeliveryStatusUpdatedDomainEvent>(updatedEvent);
    }
}
