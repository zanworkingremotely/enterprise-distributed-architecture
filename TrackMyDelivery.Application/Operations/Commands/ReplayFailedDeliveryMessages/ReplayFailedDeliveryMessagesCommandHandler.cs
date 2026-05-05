using Microsoft.Extensions.Logging;
using TrackMydelivery.Application.Interfaces;

namespace TrackMyDelivery.Application.Operations.Commands.ReplayFailedDeliveryMessages;

public sealed class ReplayFailedDeliveryMessagesCommandHandler
{
    private const int MaxReplayBatchSize = 100;
    private readonly IFailedDeliveryMessageReplay _failedDeliveryMessageReplay;
    private readonly ILogger<ReplayFailedDeliveryMessagesCommandHandler> _logger;

    public ReplayFailedDeliveryMessagesCommandHandler(
        IFailedDeliveryMessageReplay failedDeliveryMessageReplay,
        ILogger<ReplayFailedDeliveryMessagesCommandHandler> logger)
    {
        _failedDeliveryMessageReplay = failedDeliveryMessageReplay;
        _logger = logger;
    }

    public async Task<int> HandleAsync(
        ReplayFailedDeliveryMessagesCommand command,
        CancellationToken cancellationToken = default)
    {
        var replayCount = Math.Clamp(command.MaxCount, 1, MaxReplayBatchSize);

        _logger.LogInformation(
            "Replaying up to {ReplayCount} parked delivery message(s)",
            replayCount);

        var replayedCount = await _failedDeliveryMessageReplay.ReplayAsync(replayCount, cancellationToken);

        _logger.LogInformation(
            "Replayed {ReplayedCount} parked delivery message(s)",
            replayedCount);

        return replayedCount;
    }
}
