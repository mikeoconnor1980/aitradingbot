namespace TradingApp.Application.Optimization.Models;

public sealed class OptimizationResultResponse
{
    public required int Rank { get; init; }
    public required decimal FitnessScore { get; init; }
    public required string SignalDescription { get; init; }
    public required string StrategyConfigJson { get; init; }
    public required decimal TotalPnl { get; init; }
    public required decimal WinRate { get; init; }
    public required decimal MaxDrawdown { get; init; }
    public required int TotalTrades { get; init; }
    public required int WinningTrades { get; init; }
    public required int LosingTrades { get; init; }
    public required decimal TotalFeesPaid { get; init; }
    public required decimal AverageTradePnl { get; init; }
    public required double AverageHoldTimeMinutes { get; init; }
}