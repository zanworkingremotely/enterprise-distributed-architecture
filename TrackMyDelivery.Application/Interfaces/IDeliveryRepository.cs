using TrackMyDelivery.Domain.Deliveries.Entities;

namespace TrackMydelivery.Application.Interfaces;

public interface IDeliveryRepository
{
    Task<IReadOnlyList<Delivery>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Delivery?> GetByIdAsync(Guid deliveryId, CancellationToken cancellationToken = default);
    Task AddAsync(Delivery delivery, CancellationToken cancellationToken = default);
    Task UpdateAsync(Delivery delivery, CancellationToken cancellationToken = default);
}
