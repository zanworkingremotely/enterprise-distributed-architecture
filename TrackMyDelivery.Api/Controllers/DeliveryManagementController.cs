using Microsoft.AspNetCore.Mvc;
using TrackMyDelivery.Application.Deliveries.Commands.AssignCourier;
using TrackMyDelivery.Application.Deliveries.Commands.CreateDelivery;
using TrackMyDelivery.Application.Deliveries.Commands.UpdateDeliveryStatus;
using TrackMyDelivery.Application.Deliveries.Requests;
using TrackMyDelivery.Application.Deliveries.Models;
using TrackMyDelivery.Application.Deliveries.Queries.GetAllDeliveries;
using TrackMyDelivery.Application.Deliveries.Queries.GetDeliveryById;
using TrackMyDelivery.Application.Tracking.Models;
using TrackMyDelivery.Application.Tracking.Queries.GetTrackingTimeline;

namespace TrackMyDelivery.Api.Controllers;

[Route("api/deliveries")]
[ApiController]
public class DeliveryManagementController : ControllerBase
{
    private readonly AssignCourierCommandHandler _assignCourierCommandHandler;
    private readonly CreateDeliveryCommandHandler _createDeliveryCommandHandler;
    private readonly GetAllDeliveriesQueryHandler _getAllDeliveriesQueryHandler;
    private readonly GetDeliveryByIdQueryHandler _getDeliveryByIdQueryHandler;
    private readonly GetTrackingTimelineQueryHandler _getTrackingTimelineQueryHandler;
    private readonly UpdateDeliveryStatusCommandHandler _updateDeliveryStatusCommandHandler;

    public DeliveryManagementController(
        CreateDeliveryCommandHandler createDeliveryCommandHandler,
        AssignCourierCommandHandler assignCourierCommandHandler,
        UpdateDeliveryStatusCommandHandler updateDeliveryStatusCommandHandler,
        GetAllDeliveriesQueryHandler getAllDeliveriesQueryHandler,
        GetDeliveryByIdQueryHandler getDeliveryByIdQueryHandler,
        GetTrackingTimelineQueryHandler getTrackingTimelineQueryHandler)
    {
        _createDeliveryCommandHandler = createDeliveryCommandHandler;
        _assignCourierCommandHandler = assignCourierCommandHandler;
        _updateDeliveryStatusCommandHandler = updateDeliveryStatusCommandHandler;
        _getAllDeliveriesQueryHandler = getAllDeliveriesQueryHandler;
        _getDeliveryByIdQueryHandler = getDeliveryByIdQueryHandler;
        _getTrackingTimelineQueryHandler = getTrackingTimelineQueryHandler;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DeliveryDto>>> GetAll(CancellationToken cancellationToken)
    {
        var deliveries = await _getAllDeliveriesQueryHandler.HandleAsync(cancellationToken);
        return Ok(deliveries);
    }

    [HttpGet("{deliveryId:guid}")]
    public async Task<ActionResult<DeliveryDto>> GetById(Guid deliveryId, CancellationToken cancellationToken)
    {
        var delivery = await _getDeliveryByIdQueryHandler.HandleAsync(deliveryId, cancellationToken);
        return delivery is null ? NotFound() : Ok(delivery);
    }

    [HttpGet("{deliveryId:guid}/tracking")]
    public async Task<ActionResult<IReadOnlyList<TrackingTimelineItemDto>>> GetTrackingTimeline(
        Guid deliveryId,
        CancellationToken cancellationToken)
    {
        var timeline = await _getTrackingTimelineQueryHandler.HandleAsync(deliveryId, cancellationToken);
        return Ok(timeline);
    }

    [HttpPost]
    public async Task<ActionResult<DeliveryDto>> Create(
        [FromBody] CreateDeliveryRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _createDeliveryCommandHandler.HandleAsync(
            new CreateDeliveryCommand(request.TrackingNumber, request.RecipientName, request.DeliveryAddress),
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { deliveryId = created.Id }, created);
    }

    [HttpPost("{deliveryId:guid}/assign-courier")]
    public async Task<ActionResult<DeliveryDto>> AssignCourier(
        Guid deliveryId,
        [FromBody] AssignCourierRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _assignCourierCommandHandler.HandleAsync(
            new AssignCourierCommand(deliveryId, request.CourierName),
            cancellationToken);

        return Ok(updated);
    }

    [HttpPost("{deliveryId:guid}/status")]
    public async Task<ActionResult<DeliveryDto>> UpdateStatus(
        Guid deliveryId,
        [FromBody] UpdateDeliveryStatusRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _updateDeliveryStatusCommandHandler.HandleAsync(
            new UpdateDeliveryStatusCommand(deliveryId, request.Status, request.Reason),
            cancellationToken);

        return Ok(updated);
    }
}
