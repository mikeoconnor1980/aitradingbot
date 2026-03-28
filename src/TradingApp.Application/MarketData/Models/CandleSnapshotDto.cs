namespace TradingApp.Application.MarketData.Models;

public sealed class CandleSnapshotDto
{
    public long Timestamp { get; init; }
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Close { get; init; }
    public decimal Volume { get; init; }
    public int NumTrades { get; init; }
}
