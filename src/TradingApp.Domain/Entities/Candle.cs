namespace TradingApp.Domain.Entities;

public sealed class Candle
{
    public long Id { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public string Symbol { get; private set; } = string.Empty;
    public string Interval { get; private set; } = string.Empty;
    public long Timestamp { get; private set; }
    public decimal Open { get; private set; }
    public decimal High { get; private set; }
    public decimal Low { get; private set; }
    public decimal Close { get; private set; }
    public decimal Volume { get; private set; }
    public int NumTrades { get; private set; }

    private Candle()
    {
    }

    public static Candle Create(
        string source,
        string symbol,
        string interval,
        long timestamp,
        decimal open,
        decimal high,
        decimal low,
        decimal close,
        decimal volume,
        int numTrades)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(interval);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timestamp);
        ArgumentOutOfRangeException.ThrowIfNegative(open);
        ArgumentOutOfRangeException.ThrowIfNegative(high);
        ArgumentOutOfRangeException.ThrowIfNegative(low);
        ArgumentOutOfRangeException.ThrowIfNegative(close);
        ArgumentOutOfRangeException.ThrowIfNegative(volume);
        ArgumentOutOfRangeException.ThrowIfNegative(numTrades);

        if (high < low)
        {
            throw new ArgumentException("High must be >= Low.", nameof(high));
        }

        return new Candle
        {
            Source = source,
            Symbol = symbol,
            Interval = interval,
            Timestamp = timestamp,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Volume = volume,
            NumTrades = numTrades
        };
    }

    public static Candle Create(
        string symbol,
        string interval,
        long timestamp,
        decimal open,
        decimal high,
        decimal low,
        decimal close,
        decimal volume,
        int numTrades,
        string source = "Hyperliquid")
        => Create(source, symbol, interval, timestamp, open, high, low, close, volume, numTrades);
}
