namespace TradePilot.Application.Backtesting.Models;

public sealed class RegimeSegmentationSummary
{
    public int CompletedGridCyclesAnalysed { get; init; }
    public string? UnavailableReason { get; init; }
    public IReadOnlyList<RegimeSegmentStat> TrendSegments { get; init; } = [];
    public IReadOnlyList<RegimeSegmentStat> AtrPercentileSegments { get; init; } = [];
    public IReadOnlyList<RegimeSegmentStat> VolatilitySegments { get; init; } = [];
    public IReadOnlyList<RegimeSegmentStat> FundingSegments { get; init; } = [];
    public IReadOnlyList<RegimeSegmentStat> SessionSegments { get; init; } = [];
    public string OpenInterestTrendNote { get; init; } = string.Empty;
    public bool HasAnySegmentData => CompletedGridCyclesAnalysed > 0;
}