namespace TradePilot.Api.Models;

public sealed class TradableAssetDto
{
    public required string Symbol { get; init; }
    public required string Name { get; init; }
    public required int MaxLeverage { get; init; }
    public required int SzDecimals { get; init; }
}
