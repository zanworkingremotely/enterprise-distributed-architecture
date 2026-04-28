using Microsoft.Data.Sqlite;
using TrackMyDelivery.Infrastructure.Constants;

namespace TrackMyDelivery.Infrastructure.Data;

public sealed class SqliteDatabaseInitializer
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteDatabaseInitializer(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public void Initialize()
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS deliveries (
                id TEXT PRIMARY KEY,
                tracking_number TEXT NOT NULL,
                recipient_name TEXT NOT NULL,
                delivery_address TEXT NOT NULL,
                assigned_courier TEXT NULL,
                current_status TEXT NOT NULL,
                created_at_utc TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS outbox_messages (
                id TEXT PRIMARY KEY,
                type TEXT NOT NULL,
                payload TEXT NOT NULL,
                occurred_on_utc TEXT NOT NULL,
                published_on_utc TEXT NULL,
                processed_on_utc TEXT NULL,
                error TEXT NULL,
                retry_count INTEGER NOT NULL DEFAULT 0,
                last_attempt_utc TEXT NULL,
                next_attempt_utc TEXT NULL,
                dead_lettered_on_utc TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS tracking_events (
                event_id TEXT PRIMARY KEY,
                delivery_id TEXT NOT NULL,
                event_type TEXT NOT NULL,
                description TEXT NOT NULL,
                occurred_on_utc TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_deliveries_tracking_number
                ON deliveries (tracking_number);

            CREATE INDEX IF NOT EXISTS idx_outbox_messages_processed
                ON outbox_messages (processed_on_utc);

            CREATE INDEX IF NOT EXISTS idx_outbox_messages_published
                ON outbox_messages (published_on_utc, dead_lettered_on_utc, next_attempt_utc);

            CREATE INDEX IF NOT EXISTS idx_outbox_messages_next_attempt
                ON outbox_messages (processed_on_utc, dead_lettered_on_utc, next_attempt_utc);

            CREATE INDEX IF NOT EXISTS idx_tracking_events_delivery_occurred
                ON tracking_events (delivery_id, occurred_on_utc);
            """;

        command.ExecuteNonQuery();

        EnsureOutboxFieldExists(connection, StorageNames.PublishedOnUtc, $"ALTER TABLE {StorageNames.OutboxTable} ADD COLUMN {StorageNames.PublishedOnUtc} TEXT NULL;");
        EnsureOutboxFieldExists(connection, StorageNames.RetryCount, $"ALTER TABLE {StorageNames.OutboxTable} ADD COLUMN {StorageNames.RetryCount} INTEGER NOT NULL DEFAULT 0;");
        EnsureOutboxFieldExists(connection, StorageNames.LastAttemptUtc, $"ALTER TABLE {StorageNames.OutboxTable} ADD COLUMN {StorageNames.LastAttemptUtc} TEXT NULL;");
        EnsureOutboxFieldExists(connection, StorageNames.NextAttemptUtc, $"ALTER TABLE {StorageNames.OutboxTable} ADD COLUMN {StorageNames.NextAttemptUtc} TEXT NULL;");
        EnsureOutboxFieldExists(connection, StorageNames.DeadLetteredOnUtc, $"ALTER TABLE {StorageNames.OutboxTable} ADD COLUMN {StorageNames.DeadLetteredOnUtc} TEXT NULL;");
    }

    private static void EnsureOutboxFieldExists(
        SqliteConnection connection,
        string fieldName,
        string addFieldStatement)
    {
        using var outboxSchemaLookup = connection.CreateCommand();
        outboxSchemaLookup.CommandText =
            """
            SELECT sql
            FROM sqlite_master
            WHERE type = 'table'
              AND name = '{{OutboxTable}}'
            LIMIT 1;
            """;
        outboxSchemaLookup.CommandText = outboxSchemaLookup.CommandText.Replace("{{OutboxTable}}", StorageNames.OutboxTable);

        var outboxDefinition = outboxSchemaLookup.ExecuteScalar() as string;
        if (outboxDefinition is not null &&
            outboxDefinition.Contains(fieldName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        using var addOutboxField = connection.CreateCommand();
        addOutboxField.CommandText = addFieldStatement;
        addOutboxField.ExecuteNonQuery();
    }
}
