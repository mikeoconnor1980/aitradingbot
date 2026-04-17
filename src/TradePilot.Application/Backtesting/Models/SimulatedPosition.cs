namespace TradePilot.Application.Backtesting.Models;

public sealed class SimulatedPosition
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Size { get; set; }
    public decimal AverageEntryPrice { get; set; }
    public int Leverage { get; set; }
    public decimal MarginUsed { get; set; }
    public decimal LiquidationPrice { get; set; }
    public decimal UnrealisedPnL { get; set; }
    public decimal RealisedPnL { get; set; }

    public bool IsOpen => Size != 0;
}
