using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using TrackMyDelivery.Infrastructure.Configuration;
using TrackMyDelivery.Infrastructure.Constants;
using TrackMyDelivery.Infrastructure.Messaging;

namespace TrackMyDelivery.Worker;

public sealed class TrackingTimelineWorker : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ILogger<TrackingTimelineWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly MessagingOptions _deliveryMessagingOptions;

    public TrackingTimelineWorker(
        IServiceProvider serviceProvider,
        IOptions<MessagingOptions> messagingOptions,
        ILogger<TrackingTimelineWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _deliveryMessagingOptions = messagingOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_deliveryMessagingOptions.Enabled)
        {
            _logger.LogInformation(InfrastructureLogMessages.RabbitConsumerDisabled);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ListenForDeliveryEventsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, InfrastructureLogMessages.RabbitConsumerLoopFailed);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ListenForDeliveryEventsAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _deliveryMessagingOptions.HostName,
            Port = _deliveryMessagingOptions.Port,
            UserName = _deliveryMessagingOptions.UserName,
            Password = _deliveryMessagingOptions.Password
        };

        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.ExchangeDeclareAsync(
            exchange: _deliveryMessagingOptions.DeliveryEventsExchange,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(
            queue: _deliveryMessagingOptions.TrackingUpdatesQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await channel.QueueBindAsync(
            queue: _deliveryMessagingOptions.TrackingUpdatesQueue,
            exchange: _deliveryMessagingOptions.DeliveryEventsExchange,
            routingKey: $"{_deliveryMessagingOptions.DeliveryEventRoutePrefix}.#",
            cancellationToken: stoppingToken);

        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: _deliveryMessagingOptions.MaxInFlightDeliveryEvents,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, args) =>
        {
            try
            {
                var deliveryMessage = JsonSerializer.Deserialize<DeliveryMessage>(
                    Encoding.UTF8.GetString(args.Body.ToArray()),
                    JsonOptions)
                    ?? throw new InvalidOperationException("Delivery message was empty.");

                using var scope = _serviceProvider.CreateScope();
                var trackingTimelineUpdater = scope.ServiceProvider.GetRequiredService<ITrackingTimelineUpdater>();
                var wasProjected = await trackingTimelineUpdater.ApplyDeliveryEventAsync(deliveryMessage, stoppingToken);

                if (wasProjected)
                {
                    _logger.LogInformation(
                        InfrastructureLogMessages.DeliveryEventConsumed,
                        deliveryMessage.EventId,
                        deliveryMessage.DeliveryId);
                }

                await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    InfrastructureLogMessages.DeliveryEventConsumeFailed,
                    args.RoutingKey);

                await channel.BasicNackAsync(
                    deliveryTag: args.DeliveryTag,
                    multiple: false,
                    requeue: true,
                    cancellationToken: stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(
            queue: _deliveryMessagingOptions.TrackingUpdatesQueue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            InfrastructureLogMessages.RabbitConsumerListening,
            _deliveryMessagingOptions.TrackingUpdatesQueue,
            _deliveryMessagingOptions.DeliveryEventsExchange);

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}
