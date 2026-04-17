namespace TradePilot.Domain.Entities;

public sealed class FearGreedReading
{
    public int Id { get; private set; }
    public int Value { get; private set; }
    public string Classification { get; private set; } = string.Empty;
    public long Timestamp { get; private set; }
    public long FetchedAtUtc { get; private set; }

    private FearGreedReading()
    {
    }

    public static FearGreedReading Create(
        int value,
        string classification,
        long timestamp,
        long fetchedAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 100);
        ArgumentException.ThrowIfNullOrWhiteSpace(classification);

        return new FearGreedReading
        {
            Value = value,
            Classification = classification,
            Timestamp = timestamp,
            FetchedAtUtc = fetchedAtUtc,
        };
    }
}
