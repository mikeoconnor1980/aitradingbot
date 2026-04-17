namespace TradePilot.Application.Candles.Models;

public sealed class IngestionRequest
{
    public required string Symbol { get; init; }
    public required string[] Intervals { get; init; }
    public long? StartTime { get; init; }
    public long? EndTime { get; init; }
    public bool IncludeMarkPrice { get; init; }
}