namespace TradePilot.Application.StrategyAuthoring.Models;

public sealed record DcaProfitTakingConfig
{
    public IReadOnlyList<DcaProfitTier> Tiers { get; init; } = [];
}