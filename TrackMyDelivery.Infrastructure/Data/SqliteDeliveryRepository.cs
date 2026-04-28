using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using TrackMydelivery.Application.Interfaces;
using TrackMyDelivery.Domain.Common;
using TrackMyDelivery.Domain.Deliveries;
using TrackMyDelivery.Domain.Deliveries.Entities;

namespace TrackMyDelivery.Infrastructure.Data;

public sealed class SqliteDeliveryRepository : IDeliveryRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ILogger<SqliteDeliveryRepository> _logger;

    public SqliteDeliveryRepository(
        SqliteConnectionFactory connectionFactory,
        ILogger<SqliteDeliveryRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
        new SqliteDatabaseInitializer(connectionFactory).Initialize();
    }

    public async Task<IReadOnlyList<Delivery>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Reading all deliveries from SQLite");
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

        _logger.LogDebug("Read {DeliveryCount} deliveries from SQLite", deliveries.Count);
        return deliveries;
    }

    public async Task<Delivery?> GetByIdAsync(Guid deliveryId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Reading delivery {DeliveryId} from SQLite", deliveryId);
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
        var delivery = await reader.ReadAsync(cancellationToken) ? MapDelivery(reader) : null;

        if (delivery is null)
        {
            _logger.LogDebug("Delivery {DeliveryId} was not found in SQLite", deliveryId);
        }
        else
        {
            _logger.LogDebug("Read delivery {DeliveryId} from SQLite", deliveryId);
        }

        return delivery;
    }

    public async Task AddAsync(Delivery delivery, CancellationToken cancellationToken = default)
    {
        var pendingEvents = delivery.DequeueDomainEvents();
        _logger.LogInformation(
            "Persisting new delivery {DeliveryId} with {OutboxMessageCount} outbox messages",
            delivery.Id,
            pendingEvents.Count);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await InsertDeliveryAsync(connection, transaction, delivery, cancellationToken);
        await InsertOutboxMessagesAsync(connection, transaction, pendingEvents, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Persisted new delivery {DeliveryId} and {OutboxMessageCount} outbox messages",
            delivery.Id,
            pendingEvents.Count);
    }

    public async Task UpdateAsync(Delivery delivery, CancellationToken cancellationToken = default)
    {
        var pendingEvents = delivery.DequeueDomainEvents();
        _logger.LogInformation(
            "Persisting delivery {DeliveryId} update with {OutboxMessageCount} outbox messages",
            delivery.Id,
            pendingEvents.Count);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await UpdateDeliveryAsync(connection, transaction, delivery, cancellationToken);
        await InsertOutboxMessagesAsync(connection, transaction, pendingEvents, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Persisted delivery {DeliveryId} update and {OutboxMessageCount} outbox messages",
            delivery.Id,
            pendingEvents.Count);
    }

    private static async Task InsertDeliveryAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Delivery delivery,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
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

    private static async Task UpdateDeliveryAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Delivery delivery,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
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

    private static async Task InsertOutboxMessagesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyCollection<IDomainEvent> domainEvents,
        CancellationToken cancellationToken)
    {
        foreach (var domainEvent in domainEvents)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO outbox_messages (
                    id,
                    type,
                    payload,
                    occurred_on_utc,
                    published_on_utc,
                    processed_on_utc,
                    error,
                    retry_count,
                    last_attempt_utc,
                    next_attempt_utc,
                    dead_lettered_on_utc
                ) VALUES (
                    $id,
                    $type,
                    $payload,
                    $occurredOnUtc,
                    NULL,
                    NULL,
                    NULL,
                    0,
                    NULL,
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
