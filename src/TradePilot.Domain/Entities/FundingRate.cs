namespace TradePilot.Domain.Entities;

public sealed class FundingRate
{
    public long Id { get; private set; }
    public string Symbol { get; private set; } = string.Empty;
    public long Timestamp { get; private set; }
    public decimal Rate { get; private set; }
    public decimal MarkPrice { get; private set; }

    private FundingRate()
    {
    }

    public static FundingRate Create(string symbol, long timestamp, decimal rate, decimal markPrice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timestamp);
        ArgumentOutOfRangeException.ThrowIfNegative(markPrice);

        return new FundingRate
        {
            Symbol = symbol,
            Timestamp = timestamp,
            Rate = rate,
            MarkPrice = markPrice,
        };
    }
}