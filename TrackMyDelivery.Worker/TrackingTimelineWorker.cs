using TrackMydelivery.Application.Interfaces;

namespace TrackMyDelivery.Worker;

public sealed class TrackingTimelineWorker : BackgroundService
{
    private readonly ILogger<TrackingTimelineWorker> _logger;
    private readonly IServiceProvider _serviceProvider;

    public TrackingTimelineWorker(
        IServiceProvider serviceProvider,
        ILogger<TrackingTimelineWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();
            var deliveryTrackingUpdater = scope.ServiceProvider.GetRequiredService<IDeliveryTrackingUpdater>();
            var processedCount = await deliveryTrackingUpdater.UpdateTrackingTimelineAsync(stoppingToken);

            if (processedCount > 0)
            {
                _logger.LogInformation("Updated tracking timeline from {ProcessedCount} delivery event(s)", processedCount);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
