using Microsoft.Extensions.DependencyInjection;
using TrackMyDelivery.Application.Deliveries.Commands.AssignCourier;
using TrackMyDelivery.Application.Deliveries.Commands.CreateDelivery;
using TrackMyDelivery.Application.Deliveries.Commands.UpdateDeliveryStatus;
using TrackMyDelivery.Application.Deliveries.Queries.GetAllDeliveries;
using TrackMyDelivery.Application.Deliveries.Queries.GetDeliveryById;
using TrackMyDelivery.Application.Tracking.Queries.GetTrackingTimeline;

namespace TrackMyDelivery.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateDeliveryCommandHandler>();
        services.AddScoped<AssignCourierCommandHandler>();
        services.AddScoped<UpdateDeliveryStatusCommandHandler>();
        services.AddScoped<GetAllDeliveriesQueryHandler>();
        services.AddScoped<GetDeliveryByIdQueryHandler>();
        services.AddScoped<GetTrackingTimelineQueryHandler>();

        return services;
    }
}
