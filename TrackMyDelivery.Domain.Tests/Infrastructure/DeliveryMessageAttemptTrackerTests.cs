using TrackMyDelivery.Infrastructure.Constants;
using TrackMyDelivery.Infrastructure.Messaging;
using Xunit;

namespace TrackMyDelivery.Domain.Tests.Infrastructure;

public sealed class DeliveryMessageAttemptTrackerTests
{
    [Fact]
    public void ReadAttemptCount_ShouldReturnZero_WhenHeadersAreMissing()
    {
        var attemptCount = DeliveryMessageAttemptTracker.ReadAttemptCount(null);

        Assert.Equal(0, attemptCount);
    }

    [Fact]
    public void ReadAttemptCount_ShouldReadIntegerHeader()
    {
        var headers = new Dictionary<string, object?>
        {
            [MessagingHeaders.DeliveryAttemptCount] = 2
        };

        var attemptCount = DeliveryMessageAttemptTracker.ReadAttemptCount(headers);

        Assert.Equal(2, attemptCount);
    }

    [Fact]
    public void CreateHeaders_ShouldIncludeAttemptCountAndFailureReason()
    {
        var headers = DeliveryMessageAttemptTracker.CreateHeaders(3, "projection failed");

        Assert.Equal(3, headers[MessagingHeaders.DeliveryAttemptCount]);
        Assert.Equal("projection failed", headers[MessagingHeaders.LastFailure]);
    }
}
