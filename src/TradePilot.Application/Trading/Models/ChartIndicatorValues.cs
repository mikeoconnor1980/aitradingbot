namespace TradePilot.Application.Trading.Models;

public sealed class ChartIndicatorValues
{
    public decimal? EmaFast { get; init; }
    public decimal? EmaSlow { get; init; }
    public decimal? EmaTrend { get; init; }
    public decimal? Rsi { get; init; }
    public decimal? Atr { get; init; }
    public decimal? MacdLine { get; init; }
    public decimal? MacdSignal { get; init; }
    public decimal? MacdHistogram { get; init; }
    public decimal? BollingerUpper { get; init; }
    public decimal? BollingerMiddle { get; init; }
    public decimal? BollingerLower { get; init; }
}