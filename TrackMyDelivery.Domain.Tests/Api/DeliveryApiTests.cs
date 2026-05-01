using System.Net;
using System.Net.Http.Json;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrackMydelivery.Application.Interfaces;
using TrackMyDelivery.Application.Deliveries.Models;
using TrackMyDelivery.Application.Deliveries.Requests;
using TrackMyDelivery.Application.Tracking.Models;
using TrackMyDelivery.Infrastructure.Constants;
using TrackMyDelivery.Infrastructure.Data;
using Xunit;

namespace TrackMyDelivery.Domain.Tests.Api;

public sealed class DeliveryApiTests : IDisposable
{
    private readonly string _databasePath;
    private readonly WebApplicationFactory<Program> _apiFactory;

    public DeliveryApiTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), "track-my-delivery-api-tests", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);

        _apiFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Storage:DatabasePath"] = _databasePath
                    });
                });
            });
    }

    [Fact]
    public async Task HealthCheck_ShouldReturnOk()
    {
        using var client = _apiFactory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeliveryFlow_ShouldCreateUpdateAndReturnTrackingTimeline()
    {
        using var client = _apiFactory.CreateClient();

        var created = await CreateDeliveryAsync(client);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("TRK-API-1001", created.TrackingNumber);
        Assert.Equal("Created", created.CurrentStatus);

        var fetched = await client.GetFromJsonAsync<DeliveryDto>($"/api/deliveries/{created.Id}");

        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);

        var assigned = await PostJsonAsync<AssignCourierRequest, DeliveryDto>(
            client,
            $"/api/deliveries/{created.Id}/assign-courier",
            new AssignCourierRequest { CourierName = "Nomsa Express" });

        Assert.Equal("Nomsa Express", assigned.AssignedCourier);

        var inTransit = await PostJsonAsync<UpdateDeliveryStatusRequest, DeliveryDto>(
            client,
            $"/api/deliveries/{created.Id}/status",
            new UpdateDeliveryStatusRequest { Status = "OutForDelivery", Reason = "Collected from warehouse" });

        Assert.Equal("OutForDelivery", inTransit.CurrentStatus);

        await ProcessPendingTrackingEventsAsync();

        var timeline = await client.GetFromJsonAsync<IReadOnlyList<TrackingTimelineItemDto>>(
            $"/api/deliveries/{created.Id}/tracking");

        Assert.NotNull(timeline);
        Assert.Equal(3, timeline.Count);
        Assert.All(timeline, item => Assert.Equal(created.Id, item.DeliveryId));
    }

    [Fact]
    public async Task CreateDelivery_ShouldReturnAndStoreCorrelationId()
    {
        using var client = _apiFactory.CreateClient();
        const string correlationId = "corr-api-test-1001";

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/deliveries")
        {
            Content = JsonContent.Create(new CreateDeliveryRequest
            {
                TrackingNumber = "TRK-API-2001",
                RecipientName = "Lerato Moyo",
                DeliveryAddress = "15 Wale Street, Cape Town"
            })
        };
        request.Headers.Add(CorrelationNames.HeaderName, correlationId);

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        Assert.True(response.Headers.TryGetValues(CorrelationNames.HeaderName, out var responseCorrelationIds));
        Assert.Equal(correlationId, Assert.Single(responseCorrelationIds));

        await using var connection = CreateOpenConnection();
        var storedCorrelationId = await ExecuteScalarAsync<string?>(
            connection,
            $"SELECT {StorageNames.CorrelationId} FROM {StorageNames.OutboxTable} LIMIT 1;");

        Assert.Equal(correlationId, storedCorrelationId);
    }

    [Fact]
    public async Task GetMissingDelivery_ShouldReturnNotFound()
    {
        using var client = _apiFactory.CreateClient();

        var response = await client.GetAsync($"/api/deliveries/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public void Dispose()
    {
        _apiFactory.Dispose();

        if (!File.Exists(_databasePath))
        {
            return;
        }

        try
        {
            File.Delete(_databasePath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static async Task<DeliveryDto> CreateDeliveryAsync(HttpClient client)
    {
        return await PostJsonAsync<CreateDeliveryRequest, DeliveryDto>(
            client,
            "/api/deliveries",
            new CreateDeliveryRequest
            {
                TrackingNumber = "TRK-API-1001",
                RecipientName = "Amina Jacobs",
                DeliveryAddress = "42 Loop Street, Cape Town"
            });
    }

    private async Task ProcessPendingTrackingEventsAsync()
    {
        using var scope = _apiFactory.Services.CreateScope();
        var trackingUpdater = scope.ServiceProvider.GetRequiredService<IDeliveryTrackingUpdater>();
        var processedCount = await trackingUpdater.UpdateTrackingTimelineAsync();

        Assert.Equal(3, processedCount);
    }

    private SqliteConnection CreateOpenConnection()
    {
        var connectionFactory = _apiFactory.Services.GetRequiredService<SqliteConnectionFactory>();
        var connection = connectionFactory.CreateConnection();
        connection.Open();
        return connection;
    }

    private static async Task<T?> ExecuteScalarAsync<T>(SqliteConnection connection, string commandText)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        var result = await command.ExecuteScalarAsync();

        if (result is null or DBNull)
        {
            return default;
        }

        return (T)result;
    }

    private static async Task<TResponse> PostJsonAsync<TRequest, TResponse>(
        HttpClient client,
        string requestUri,
        TRequest request)
    {
        var response = await client.PostAsJsonAsync(requestUri, request);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadFromJsonAsync<TResponse>();
        return responseBody ?? throw new InvalidOperationException($"Response from '{requestUri}' was empty.");
    }
}
