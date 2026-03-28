namespace TradingApp.Application.Backtesting.Models;

public sealed class GridStrategyConfig
{
    public int GridLevels { get; set; }
    public decimal GridSpacing { get; set; }
    public decimal TakeProfitPercent { get; set; }
    public decimal BreakdownThreshold { get; set; }
    public decimal MakerFee { get; set; }
    public decimal TakerFee { get; set; }
    public decimal Slippage { get; set; }
    public decimal PositionSize { get; set; }
    public decimal Leverage { get; set; }
    public decimal StopLossPercent { get; set; }
}