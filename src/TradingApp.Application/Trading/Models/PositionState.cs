namespace TradingApp.Application.Trading.Models;

/// <summary>
/// Represents the current position state for a symbol.
/// Minimal definition and will be expanded by PositionManager work.
/// </summary>
public sealed class PositionState
{
    public string Symbol { get; init; } = string.Empty;
    public decimal Size { get; init; }
    public decimal AverageEntryPrice { get; init; }
    public decimal UnrealisedPnL { get; init; }
    public bool IsOpen => Size != 0;
}
