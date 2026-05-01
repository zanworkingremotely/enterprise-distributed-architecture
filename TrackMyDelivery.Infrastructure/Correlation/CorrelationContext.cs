namespace TrackMyDelivery.Infrastructure.Correlation;

public sealed class CorrelationContext
{
    private static readonly AsyncLocal<string?> CurrentCorrelationId = new();

    public string? CorrelationId
    {
        get => CurrentCorrelationId.Value;
        set => CurrentCorrelationId.Value = value;
    }
}
