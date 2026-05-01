using Serilog;
using TrackMyDelivery.Application.DependencyInjection;
using TrackMyDelivery.Infrastructure.Correlation;
using TrackMyDelivery.Infrastructure.Constants;
using TrackMyDelivery.Infrastructure.DependencyInjection;

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true, reloadOnChange: true)
        .Build())
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddDeliveryEventPublishing();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    var correlationContext = context.RequestServices.GetRequiredService<CorrelationContext>();
    var correlationId = context.Request.Headers.TryGetValue(CorrelationNames.HeaderName, out var requestCorrelationId) &&
        !string.IsNullOrWhiteSpace(requestCorrelationId)
        ? requestCorrelationId.ToString()
        : Guid.NewGuid().ToString("N");

    correlationContext.CorrelationId = correlationId;
    context.TraceIdentifier = correlationId;
    context.Response.Headers[CorrelationNames.HeaderName] = correlationId;

    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.MapHealthChecks("/health");

try
{
    Log.Information("Starting TrackMyDelivery API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "TrackMyDelivery API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
