using TrackMydelivery.Application.Interfaces;
using TrackMyDelivery.Application.Tracking.Models;

namespace TrackMyDelivery.Infrastructure.Data;

public sealed class SqliteTrackingEventRepository : ITrackingEventRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteTrackingEventRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
        new SqliteDatabaseInitializer(connectionFactory).Initialize();
    }

    public async Task<IReadOnlyList<TrackingTimelineItemDto>> GetTimelineAsync(Guid deliveryId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                event_id,
                delivery_id,
                event_type,
                description,
                occurred_on_utc
            FROM tracking_events
            WHERE delivery_id = $deliveryId
            ORDER BY occurred_on_utc;
            """;
        command.Parameters.AddWithValue("$deliveryId", deliveryId.ToString());

        var timeline = new List<TrackingTimelineItemDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            timeline.Add(new TrackingTimelineItemDto
            {
                EventId = Guid.Parse(reader.GetString(0)),
                DeliveryId = Guid.Parse(reader.GetString(1)),
                EventType = reader.GetString(2),
                Description = reader.GetString(3),
                OccurredOnUtc = DateTime.Parse(reader.GetString(4), null, System.Globalization.DateTimeStyles.RoundtripKind)
            });
        }

        return timeline;
    }
}
