using System.Text.Json.Serialization;

namespace TradingApp.Infrastructure.Hyperliquid.Models;

public sealed class HyperliquidCancelAction
{
    [JsonPropertyName("type")]
    public string Type { get; } = "cancel";

    [JsonPropertyName("cancels")]
    public List<HyperliquidCancelEntry> Cancels { get; set; } = [];
}

public sealed class HyperliquidCancelEntry
{
    [JsonPropertyName("a")]
    public int AssetIndex { get; set; }

    [JsonPropertyName("o")]
    public long OrderId { get; set; }
}