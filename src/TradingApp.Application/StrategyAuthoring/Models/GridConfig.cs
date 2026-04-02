namespace TradingApp.Application.StrategyAuthoring.Models;

public sealed record GridConfig
{
    public int Levels { get; init; }
    public decimal Spacing { get; init; }
    public string EntryMode { get; init; } = "auto_from_signal_candle";
    public decimal? AnchorPrice { get; init; }
    public decimal BreakdownThreshold { get; init; }
}