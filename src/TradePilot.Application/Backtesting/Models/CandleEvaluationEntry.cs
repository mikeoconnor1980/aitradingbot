using TradePilot.Application.Trading.Models;

namespace TradePilot.Application.Backtesting.Models;

/// <summary>
/// Per-candle audit entry capturing the full evaluation state at each 15m candle.
/// </summary>
public sealed record CandleEvaluationEntry
{
    public required long TimestampUtc { get; init; }
    public required decimal Open { get; init; }
    public required decimal High { get; init; }
    public required decimal Low { get; init; }
    public required decimal Close { get; init; }
    public required decimal Volume { get; init; }
    public required bool IsWarmup { get; init; }
    public required decimal EmaFast { get; init; }
    public required decimal EmaSlow { get; init; }
    public required decimal EmaTrend { get; init; }
    public required decimal Rsi { get; init; }
    public required decimal Atr { get; init; }
    public required bool SetupDetected { get; init; }
    public required string GridLifecycleState { get; init; }
    public required decimal PositionSize { get; init; }
    public required decimal PositionAvgEntry { get; init; }
    public required IReadOnlyList<string> SignalsEmitted { get; init; }
    public string? GridCycleId { get; init; }
    public ChartIndicatorValues? Indicators { get; init; }
    public MarketRegime? Regime { get; init; }
}