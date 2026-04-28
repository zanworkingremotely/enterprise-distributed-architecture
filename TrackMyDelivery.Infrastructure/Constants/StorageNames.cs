namespace TrackMyDelivery.Infrastructure.Constants;

public static class StorageNames
{
    public const string DeliveriesTable = "deliveries";
    public const string OutboxTable = "outbox_messages";
    public const string TrackingTimelineTable = "tracking_events";

    public const string PublishedOnUtc = "published_on_utc";
    public const string ProcessedOnUtc = "processed_on_utc";
    public const string RetryCount = "retry_count";
    public const string LastAttemptUtc = "last_attempt_utc";
    public const string NextAttemptUtc = "next_attempt_utc";
    public const string DeadLetteredOnUtc = "dead_lettered_on_utc";
}
