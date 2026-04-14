using Microsoft.Data.Sqlite;
using TrackMydelivery.Application.Interfaces;
using TrackMyDelivery.Domain.Deliveries;
using TrackMyDelivery.Domain.Deliveries.Entities;

namespace TrackMyDelivery.Infrastructure.Data;

public sealed class SqliteDeliveryRepository : IDeliveryRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteDeliveryRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
        new SqliteDatabaseInitializer(connectionFactory).Initialize();
    }

    public async Task<IReadOnlyList<Delivery>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id,
                tracking_number,
                recipient_name,
                delivery_address,
                assigned_courier,
                current_status,
                created_at_utc,
                updated_at_utc
            FROM deliveries
            ORDER BY created_at_utc;
            """;

        var deliveries = new List<Delivery>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            deliveries.Add(MapDelivery(reader));
        }

        return deliveries;
    }

    public async Task<Delivery?> GetByIdAsync(Guid deliveryId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id,
                tracking_number,
                recipient_name,
                delivery_address,
                assigned_courier,
                current_status,
                created_at_utc,
                updated_at_utc
            FROM deliveries
            WHERE id = $id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", deliveryId.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapDelivery(reader) : null;
    }

    public async Task AddAsync(Delivery delivery, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO deliveries (
                id,
                tracking_number,
                recipient_name,
                delivery_address,
                assigned_courier,
                current_status,
                created_at_utc,
                updated_at_utc
            ) VALUES (
                $id,
                $trackingNumber,
                $recipientName,
                $deliveryAddress,
                $assignedCourier,
                $currentStatus,
                $createdAtUtc,
                $updatedAtUtc
            );
            """;

        BindDelivery(command, delivery);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(Delivery delivery, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE deliveries
            SET
                tracking_number = $trackingNumber,
                recipient_name = $recipientName,
                delivery_address = $deliveryAddress,
                assigned_courier = $assignedCourier,
                current_status = $currentStatus,
                created_at_utc = $createdAtUtc,
                updated_at_utc = $updatedAtUtc
            WHERE id = $id;
            """;

        BindDelivery(command, delivery);
        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);

        if (rowsAffected == 0)
        {
            throw new InvalidOperationException($"Delivery '{delivery.Id}' was not found in storage.");
        }
    }

    private static void BindDelivery(SqliteCommand command, Delivery delivery)
    {
        command.Parameters.AddWithValue("$id", delivery.Id.ToString());
        command.Parameters.AddWithValue("$trackingNumber", delivery.TrackingNumber);
        command.Parameters.AddWithValue("$recipientName", delivery.RecipientName);
        command.Parameters.AddWithValue("$deliveryAddress", delivery.DeliveryAddress);
        command.Parameters.AddWithValue("$assignedCourier", (object?)delivery.AssignedCourier ?? DBNull.Value);
        command.Parameters.AddWithValue("$currentStatus", delivery.CurrentStatus.ToString());
        command.Parameters.AddWithValue("$createdAtUtc", delivery.CreatedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$updatedAtUtc", delivery.UpdatedAtUtc.ToString("O"));
    }

    private static Delivery MapDelivery(SqliteDataReader reader)
    {
        var state = new DeliveryState(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            Enum.Parse<DeliveryStatus>(reader.GetString(5), true),
            DateTime.Parse(reader.GetString(6), null, System.Globalization.DateTimeStyles.RoundtripKind),
            DateTime.Parse(reader.GetString(7), null, System.Globalization.DateTimeStyles.RoundtripKind));

        return Delivery.Restore(state);
    }
}
