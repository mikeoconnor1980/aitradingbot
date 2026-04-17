namespace TradePilot.Application.Backtesting.Models;

public sealed class RegimeSegmentStat
{
    public string Segment { get; init; } = string.Empty;
    public int CycleCount { get; init; }
    public int WinningCycles { get; init; }
    public int LosingCycles { get; init; }
    public decimal WinRate { get; init; }
    public decimal AverageCyclePnl { get; init; }
    public decimal TotalCyclePnl { get; init; }
    public double AverageCycleDurationHours { get; init; }
}