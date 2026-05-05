namespace TrackMyDelivery.Infrastructure.Constants;

public static class StorageNames
{
    public const string DeliveriesTable = "deliveries";
    public const string OutboxTable = "outbox_messages";
    public const string TrackingTimelineTable = "tracking_events";
    public const string FailedDeliveryTable = "failed_delivery_messages";

    public const string EventId = "event_id";
    public const string DeliveryId = "delivery_id";
    public const string EventType = "event_type";
    public const string Payload = "payload";
    public const string RoutingKey = "routing_key";
    public const string OccurredOnUtc = "occurred_on_utc";

    public const string PublishedOnUtc = "published_on_utc";
    public const string ProcessedOnUtc = "processed_on_utc";
    public const string CorrelationId = "correlation_id";
    public const string RetryCount = "retry_count";
    public const string LastAttemptUtc = "last_attempt_utc";
    public const string NextAttemptUtc = "next_attempt_utc";
    public const string DeadLetteredOnUtc = "dead_lettered_on_utc";
    public const string ParkedOnUtc = "parked_on_utc";
    public const string ReplayedOnUtc = "replayed_on_utc";
    public const string FailureReason = "failure_reason";
    public const string AttemptCount = "attempt_count";
}
