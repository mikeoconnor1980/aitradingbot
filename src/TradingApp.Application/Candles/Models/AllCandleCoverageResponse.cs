namespace TradingApp.Application.Candles.Models;

public sealed class AllCandleCoverageResponse
{
    public required IReadOnlyList<SymbolCoverage> Symbols { get; init; }
}

public sealed class SymbolCoverage
{
    public required string Symbol { get; init; }
    public required IReadOnlyList<IntervalCoverageDetail> Intervals { get; init; }
}

public sealed class IntervalCoverageDetail
{
    public required string Interval { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public int CandleCount { get; init; }
}
