namespace TradePilot.Application.StrategyAuthoring.Models;

public sealed record DcaScalingBand
{
    public decimal? PriceLowerUsd { get; init; }
    public decimal? PriceUpperUsd { get; init; }
    public decimal ScalingPercent { get; init; }
}