using System.Text.Json.Serialization;

namespace TradePilot.Infrastructure.Hyperliquid.Models;

internal sealed class HyperliquidTrade
{
    [JsonPropertyName("coin")]
    public string Coin { get; set; } = string.Empty;

    [JsonPropertyName("side")]
    public string Side { get; set; } = string.Empty;

    [JsonPropertyName("px")]
    public string Px { get; set; } = string.Empty;

    [JsonPropertyName("sz")]
    public string Sz { get; set; } = string.Empty;

    [JsonPropertyName("time")]
    public long Time { get; set; }

    [JsonPropertyName("hash")]
    public string Hash { get; set; } = string.Empty;

    [JsonPropertyName("tid")]
    public long Tid { get; set; }
}