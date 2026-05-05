namespace TrackMyDelivery.Application.Operations.Models;

public sealed class ReplayFailedDeliveryMessagesResult
{
    public int RequestedCount { get; init; }
    public int ReplayedCount { get; init; }
}
