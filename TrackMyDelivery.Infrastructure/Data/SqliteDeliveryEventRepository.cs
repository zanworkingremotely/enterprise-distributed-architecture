using System.Text.Json;
using TrackMydelivery.Application.Interfaces;
using TrackMyDelivery.Domain.Common;

namespace TrackMyDelivery.Infrastructure.Data;

public sealed class SqliteDeliveryEventRepository : IDeliveryEventRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteDeliveryEventRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
        new SqliteDatabaseInitializer(connectionFactory).Initialize();
    }

    public async Task AddAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        foreach (var domainEvent in domainEvents)
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO outbox_messages (
                    id,
                    type,
                    payload,
                    occurred_on_utc,
                    processed_on_utc,
                    error
                ) VALUES (
                    $id,
                    $type,
                    $payload,
                    $occurredOnUtc,
                    NULL,
                    NULL
                );
                """;

            command.Parameters.AddWithValue("$id", domainEvent.EventId.ToString());
            command.Parameters.AddWithValue("$type", domainEvent.GetType().FullName ?? domainEvent.GetType().Name);
            command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), JsonOptions));
            command.Parameters.AddWithValue("$occurredOnUtc", domainEvent.OccurredOnUtc.ToString("O"));

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
