namespace TrackMydelivery.Application.Interfaces;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
