using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrackMyDelivery.Infrastructure.Configuration;
using TrackMyDelivery.Infrastructure.Constants;

namespace TrackMyDelivery.Infrastructure.Messaging;

public sealed class DeliveryEventDispatchWorker : BackgroundService
{
    private readonly MessagingOptions _messagingOptions;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DeliveryEventDispatchWorker> _logger;

    public DeliveryEventDispatchWorker(
        IServiceProvider serviceProvider,
        IOptions<MessagingOptions> messagingOptions,
        ILogger<DeliveryEventDispatchWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _messagingOptions = messagingOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_messagingOptions.Enabled)
        {
            _logger.LogInformation(InfrastructureLogMessages.RabbitPublishingDisabled);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var storedDeliveryEventPublisher = scope.ServiceProvider.GetRequiredService<StoredDeliveryEventPublisher>();
                var publishedCount = await storedDeliveryEventPublisher.PublishPendingEventsAsync(stoppingToken);
                if (publishedCount > 0)
                {
                    _logger.LogInformation(
                        InfrastructureLogMessages.DeliveryEventsDispatched,
                        publishedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, InfrastructureLogMessages.RabbitPublishingLoopFailed);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
