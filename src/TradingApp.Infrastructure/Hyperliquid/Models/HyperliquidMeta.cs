using System.Text.Json.Serialization;

namespace TradingApp.Infrastructure.Hyperliquid.Models;

public sealed class HyperliquidMeta
{
    [JsonPropertyName("universe")]
    public List<HyperliquidAssetMeta> Universe { get; set; } = new();
}
