using System.Text.Json.Serialization;

namespace TradingApp.Infrastructure.Hyperliquid.Models;

public sealed class HyperliquidAssetMeta
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("szDecimals")]
    public int SzDecimals { get; set; }
}
