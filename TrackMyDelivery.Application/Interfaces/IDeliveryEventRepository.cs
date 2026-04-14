using TrackMyDelivery.Domain.Common;

namespace TrackMydelivery.Application.Interfaces;

public interface IDeliveryEventRepository
{
    Task AddAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
