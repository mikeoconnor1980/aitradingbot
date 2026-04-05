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
    public decimal? SharpeRatio { get; init; }
    public decimal? SortinoRatio { get; init; }
    public decimal? ProfitFactor { get; init; }
    public decimal? CalmarRatio { get; init; }
    public decimal? OosTotalPnl { get; init; }
    public decimal? OosWinRate { get; init; }
    public decimal? OosMaxDrawdown { get; init; }
    public int? OosTotalTrades { get; init; }
    public decimal? OosFitnessScore { get; init; }
}