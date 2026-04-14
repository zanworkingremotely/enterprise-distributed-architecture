using Microsoft.Data.Sqlite;

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
                processed_on_utc TEXT NULL,
                error TEXT NULL
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

            CREATE INDEX IF NOT EXISTS idx_tracking_events_delivery_occurred
                ON tracking_events (delivery_id, occurred_on_utc);
            """;

        command.ExecuteNonQuery();
    }
}
