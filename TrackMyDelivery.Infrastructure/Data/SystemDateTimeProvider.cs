using TrackMydelivery.Application.Interfaces;

namespace TrackMyDelivery.Infrastructure.Data;

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
