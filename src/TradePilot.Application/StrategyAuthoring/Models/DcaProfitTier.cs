namespace TradePilot.Application.StrategyAuthoring.Models;

public sealed record DcaProfitTier
{
    public decimal TargetMultiple { get; init; }
    public decimal SellPercent { get; init; }
}