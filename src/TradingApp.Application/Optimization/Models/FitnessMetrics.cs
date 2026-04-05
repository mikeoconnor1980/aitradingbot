namespace TradingApp.Application.Optimization.Models;

public sealed record FitnessMetrics
{
    public decimal SharpeRatio { get; init; }
    public decimal SortinoRatio { get; init; }
    public decimal ProfitFactor { get; init; }
    public decimal CalmarRatio { get; init; }
}
