using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrackMydelivery.Application.Interfaces;
using TrackMyDelivery.Infrastructure.Data;

namespace TrackMyDelivery.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(configuration);
        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<IDeliveryRepository, SqliteDeliveryRepository>();
        services.AddScoped<IDeliveryEventRepository, SqliteDeliveryEventRepository>();
        services.AddScoped<ITrackingEventRepository, SqliteTrackingEventRepository>();
        services.AddScoped<IDeliveryTrackingUpdater, DeliveryTrackingUpdater>();

        return services;
    }
}
