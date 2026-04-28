using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrackMydelivery.Application.Interfaces;
using TrackMyDelivery.Infrastructure.Configuration;
using TrackMyDelivery.Infrastructure.Data;
using TrackMyDelivery.Infrastructure.Messaging;

namespace TrackMyDelivery.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(configuration);
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<MessagingOptions>(configuration.GetSection(MessagingOptions.SectionName));
        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<IDeliveryRepository, SqliteDeliveryRepository>();
        services.AddScoped<ITrackingEventRepository, SqliteTrackingEventRepository>();
        services.AddScoped<IDeliveryTrackingUpdater, DeliveryTrackingUpdater>();
        services.AddScoped<ITrackingTimelineUpdater, TrackingTimelineUpdater>();
        services.AddScoped<StoredDeliveryEventPublisher>();
        services.AddSingleton<IDeliveryEventPublisher, RabbitMqDeliveryEventPublisher>();

        return services;
    }

    public static IServiceCollection AddDeliveryEventPublishing(this IServiceCollection services)
    {
        services.AddHostedService<DeliveryEventDispatchWorker>();
        return services;
    }
}
