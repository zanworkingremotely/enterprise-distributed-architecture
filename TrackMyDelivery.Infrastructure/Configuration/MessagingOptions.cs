using TrackMyDelivery.Infrastructure.Constants;

namespace TrackMyDelivery.Infrastructure.Configuration;

public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    public bool Enabled { get; set; }
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string DeliveryEventsExchange { get; set; } = MessagingDefaults.DeliveryEventsExchange;
    public string TrackingUpdatesQueue { get; set; } = MessagingDefaults.TrackingUpdatesQueue;
    public string FailedDeliveryEventsExchange { get; set; } = MessagingDefaults.FailedDeliveryEventsExchange;
    public string FailedTrackingUpdatesQueue { get; set; } = MessagingDefaults.FailedTrackingUpdatesQueue;
    public string DeliveryEventRoutePrefix { get; set; } = MessagingDefaults.DeliveryEventRoutePrefix;
    public string FailedDeliveryEventRoute { get; set; } = MessagingDefaults.FailedDeliveryEventRoute;
    public ushort MaxInFlightDeliveryEvents { get; set; } = 10;
    public int MaxDeliveryAttempts { get; set; } = 3;
}
