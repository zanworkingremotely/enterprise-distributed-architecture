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
    public string DeliveryEventRoutePrefix { get; set; } = MessagingDefaults.DeliveryEventRoutePrefix;
    public ushort MaxInFlightDeliveryEvents { get; set; } = 10;
}
