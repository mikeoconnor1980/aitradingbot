namespace TradePilot.Application.Candles.Models;

public sealed class IntervalResult
{
    public required string Interval { get; init; }
    public int Fetched { get; init; }
    public int Inserted { get; init; }
    public int Skipped { get; init; }
    public string? EarliestCandle { get; init; }
    public string? LatestCandle { get; init; }
    public string? Error { get; init; }
}