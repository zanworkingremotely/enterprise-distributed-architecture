using TrackMyDelivery.Infrastructure.Constants;

namespace TrackMyDelivery.Infrastructure.Messaging;

public static class DeliveryMessageAttemptTracker
{
    public static int ReadAttemptCount(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue(MessagingHeaders.DeliveryAttemptCount, out var rawAttemptCount))
        {
            return 0;
        }

        return rawAttemptCount switch
        {
            byte byteValue => byteValue,
            sbyte sbyteValue => sbyteValue,
            short shortValue => shortValue,
            ushort ushortValue => ushortValue,
            int intValue => intValue,
            long longValue => (int)longValue,
            byte[] byteArray when int.TryParse(System.Text.Encoding.UTF8.GetString(byteArray), out var parsedCount) => parsedCount,
            _ => 0
        };
    }

    public static Dictionary<string, object?> CreateHeaders(int attemptCount, string? lastFailure = null)
    {
        return new Dictionary<string, object?>
        {
            [MessagingHeaders.DeliveryAttemptCount] = attemptCount,
            [MessagingHeaders.LastFailure] = lastFailure ?? string.Empty
        };
    }
}
