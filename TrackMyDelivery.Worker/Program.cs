using Serilog;
using TrackMyDelivery.Application.DependencyInjection;
using TrackMyDelivery.Infrastructure.DependencyInjection;
using TrackMyDelivery.Worker;

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true, reloadOnChange: true)
        .Build())
    .WriteTo.Console()
    .WriteTo.File("logs/worker-log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureServices((context, services) =>
        {
            services.AddApplication();
            services.AddInfrastructure(context.Configuration);
            services.AddHostedService<TrackingTimelineWorker>();
        })
        .Build();

    Log.Information("Starting TrackMyDelivery worker");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "TrackMyDelivery worker terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
