using Microsoft.AspNetCore.Mvc;
using TrackMyDelivery.Application.Operations.Commands.ReplayFailedDeliveryMessages;
using TrackMyDelivery.Application.Operations.Models;

namespace TrackMyDelivery.Api.Controllers;

[Route("api/operations")]
[ApiController]
public sealed class OperationsController : ControllerBase
{
    private readonly ReplayFailedDeliveryMessagesCommandHandler _replayFailedDeliveryMessagesCommandHandler;

    public OperationsController(ReplayFailedDeliveryMessagesCommandHandler replayFailedDeliveryMessagesCommandHandler)
    {
        _replayFailedDeliveryMessagesCommandHandler = replayFailedDeliveryMessagesCommandHandler;
    }

    [HttpPost("replay-failed-delivery-messages")]
    public async Task<ActionResult<ReplayFailedDeliveryMessagesResult>> ReplayFailedDeliveryMessages(
        [FromQuery] int maxCount = 10,
        CancellationToken cancellationToken = default)
    {
        var replayedCount = await _replayFailedDeliveryMessagesCommandHandler.HandleAsync(
            new ReplayFailedDeliveryMessagesCommand(maxCount),
            cancellationToken);

        return Ok(new ReplayFailedDeliveryMessagesResult
        {
            RequestedCount = Math.Clamp(maxCount, 1, 100),
            ReplayedCount = replayedCount
        });
    }
}
