namespace TrackMyDelivery.Infrastructure.Constants;

public static class InfrastructureLogMessages
{
    public const string RabbitConsumerDisabled = "RabbitMQ tracking consumer is disabled";
    public const string RabbitConsumerLoopFailed = "RabbitMQ tracking consumer loop failed";
    public const string RabbitConsumerListening = "RabbitMQ tracking consumer is listening on queue {QueueName} bound to exchange {ExchangeName}";
    public const string DeliveryEventConsumed = "Consumed delivery event {DeliveryEventId} for delivery {DeliveryId}";
    public const string DeliveryEventConsumeFailed = "Failed to consume RabbitMQ delivery event with routing key {RoutingKey}";
    public const string DeliveryEventScope = "Delivery event correlation scope {CorrelationId}";
    public const string DeliveryEventRetryScheduled = "Scheduled retry {AttemptNumber} for delivery event {DeliveryEventId} after processing failed";
    public const string DeliveryEventParked = "Moved delivery event {DeliveryEventId} to the failed-delivery queue after {AttemptNumber} failed attempt(s)";
    public const string DeliveryEventFailureHandlingFailed = "Failed to schedule retry or park delivery event {DeliveryEventId}";

    public const string RabbitPublishingDisabled = "RabbitMQ publishing is disabled";
    public const string RabbitPublishingLoopFailed = "RabbitMQ delivery event dispatch loop failed";
    public const string DeliveryEventsDispatched = "Published {PublishedCount} delivery event(s) from storage";
    public const string StoredEventPublishFailed = "Failed to publish stored delivery event {OutboxMessageId} on attempt {AttemptNumber}";
    public const string DeliveryEventPublished = "Published delivery event {DeliveryEventId} for delivery {DeliveryId} with routing key {RoutingKey}";
    public const string StoredEventDiscovered = "Preparing stored delivery event {OutboxMessageId} for delivery {DeliveryId}";

    public const string DeliveryEventSkipped = "Skipped already applied delivery event {DeliveryEventId}";
    public const string DeliveryEventProjected = "Applied delivery event {DeliveryEventId} to the tracking timeline for delivery {DeliveryId}";
    public const string UnsupportedDeliveryEvent = "Skipping delivery event {DeliveryEventId} because event type {EventType} is not supported";
}
