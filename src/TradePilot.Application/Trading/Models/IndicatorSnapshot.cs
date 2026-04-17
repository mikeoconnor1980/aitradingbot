namespace TradePilot.Application.Trading.Models;

/// <summary>
/// Computed technical indicator values at a point in time.
/// Will be expanded as more indicators are added.
/// </summary>
public sealed class IndicatorSnapshot
{
    public decimal EmaFast { get; init; }
    public decimal EmaSlow { get; init; }
    public decimal EmaTrend { get; init; }
    public decimal Rsi { get; init; }
    public decimal Atr { get; init; }
}
