using System.Text.Json.Serialization;

namespace TradePilot.Infrastructure.Hyperliquid.Models;

/// <summary>
/// Single fill record from the Hyperliquid userEvents WebSocket stream.
/// Note: Field names to be verified against Hyperliquid API documentation.
/// </summary>
internal sealed class HyperliquidUserFill
{
    [JsonPropertyName("coin")]
    public string Coin { get; set; } = string.Empty;

    [JsonPropertyName("px")]
    public string Price { get; set; } = string.Empty;

    [JsonPropertyName("sz")]
    public string Size { get; set; } = string.Empty;

    [JsonPropertyName("side")]
    public string Side { get; set; } = string.Empty;

    [JsonPropertyName("time")]
    public long TimestampMs { get; set; }

    [JsonPropertyName("fee")]
    public string Fee { get; set; } = string.Empty;

    [JsonPropertyName("oid")]
    public long OrderId { get; set; }

    [JsonPropertyName("hash")]
    public string Hash { get; set; } = string.Empty;

    [JsonPropertyName("closedPnl")]
    public string ClosedPnl { get; set; } = string.Empty;

    [JsonPropertyName("dir")]
    public string Direction { get; set; } = string.Empty;
}
