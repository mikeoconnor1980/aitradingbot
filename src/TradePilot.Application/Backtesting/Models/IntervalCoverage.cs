namespace TradePilot.Application.Backtesting.Models;

public sealed class IntervalCoverage
{
    public required DateTime? From { get; init; }
    public required DateTime? To { get; init; }
    public required int CandleCount { get; init; }
}