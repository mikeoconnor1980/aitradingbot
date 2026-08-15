namespace TradePilot.Application.MarketAnalysis.Models;

/// <summary>Summarises the requested and available completed-candle range.</summary>
public sealed record ChartRangeSummary(DateTimeOffset FromOpenTimeUtc, DateTimeOffset ToOpenTimeUtc);

/// <summary>Represents one selected candle and the server-provided indicator values available for it.</summary>
public sealed record SelectedChartCandle(
    DateTimeOffset OpenTimeUtc,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume,
    IReadOnlyDictionary<string, decimal> AvailableIndicatorValues);

/// <summary>Contains bounded deterministic evidence for an immutable chart snapshot.</summary>
public sealed record AnalyseChartContextResult(
    string Symbol,
    string Timeframe,
    Exchange Exchange,
    ChartRangeSummary RequestedRange,
    ChartRangeSummary? ActualRange,
    bool IsComplete,
    int CandleCount,
    decimal? StartClose,
    decimal? EndClose,
    decimal? AbsoluteChange,
    decimal? PercentChange,
    decimal? HighestHigh,
    DateTimeOffset? HighestHighOpenTimeUtc,
    decimal? LowestLow,
    DateTimeOffset? LowestLowOpenTimeUtc,
    decimal TotalVolume,
    decimal AverageVolume,
    MarketAnalysisResult? EndOfRangeMarketAnalysis,
    SelectedChartCandle? SelectedCandle);