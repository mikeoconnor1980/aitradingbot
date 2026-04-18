namespace TradePilot.Application.StrategyAuthoring.Models;

public sealed record DcaGateConfig
{
    public decimal? MaxPriceUsd { get; init; }
    public int? MinFearGreedIndex { get; init; }
    public int? MaxFearGreedIndex { get; init; }
}