using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using TrackMyDelivery.Infrastructure.Configuration;
using TrackMyDelivery.Infrastructure.Constants;

namespace TrackMyDelivery.Infrastructure.Messaging;

public sealed class RabbitMqDeliveryEventPublisher : IDeliveryEventPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ILogger<RabbitMqDeliveryEventPublisher> _logger;
    private readonly MessagingOptions _messagingOptions;

    public RabbitMqDeliveryEventPublisher(
        IOptions<MessagingOptions> messagingOptions,
        ILogger<RabbitMqDeliveryEventPublisher> logger)
    {
        _logger = logger;
        _messagingOptions = messagingOptions.Value;
    }

    public async Task PublishAsync(DeliveryMessage deliveryEvent, CancellationToken cancellationToken = default)
    {
        var factory = new ConnectionFactory
        {
            HostName = _messagingOptions.HostName,
            Port = _messagingOptions.Port,
            UserName = _messagingOptions.UserName,
            Password = _messagingOptions.Password
        };

        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: _messagingOptions.DeliveryEventsExchange,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = deliveryEvent.EventId.ToString(),
            Type = deliveryEvent.EventType,
            Timestamp = new AmqpTimestamp(new DateTimeOffset(deliveryEvent.OccurredOnUtc).ToUnixTimeSeconds()),
            Headers = DeliveryMessageAttemptTracker.CreateHeaders(0)
        };
        properties.Headers[CorrelationNames.HeaderName] = deliveryEvent.CorrelationId ?? string.Empty;

        using var correlationScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            [CorrelationNames.LogPropertyName] = deliveryEvent.CorrelationId ?? string.Empty
        });
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(deliveryEvent, JsonOptions));

        await channel.BasicPublishAsync(
            exchange: _messagingOptions.DeliveryEventsExchange,
            routingKey: deliveryEvent.RoutingKey,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            InfrastructureLogMessages.DeliveryEventPublished,
            deliveryEvent.EventId,
            deliveryEvent.DeliveryId,
            deliveryEvent.RoutingKey);
    }
}
