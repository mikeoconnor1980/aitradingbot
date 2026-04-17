namespace TradePilot.Application.Candles.Models;

public sealed class IngestionResult
{
    public int TotalFetched { get; init; }
    public int TotalInserted { get; init; }
    public int TotalSkipped { get; init; }
    public long ElapsedMs { get; init; }
    public required IReadOnlyList<IntervalResult> Intervals { get; init; }
}