namespace TrackMyDelivery.Infrastructure.Configuration;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string DatabasePath { get; set; } = "../TrackMyDelivery.SharedData/track-my-delivery.db";
}
