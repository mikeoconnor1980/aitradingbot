namespace TradingApp.Application.Backtesting.Models;

public sealed class CandleCoverageResponse
{
    public required Dictionary<string, IntervalCoverage> Coverage { get; init; }
}