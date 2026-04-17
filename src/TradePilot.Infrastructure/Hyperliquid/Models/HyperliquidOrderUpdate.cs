using System.Text.Json.Serialization;

namespace TradePilot.Infrastructure.Hyperliquid.Models;

/// <summary>
/// Single order update record from the Hyperliquid userEvents WebSocket stream.
/// Note: Field names to be verified against Hyperliquid API documentation.
/// </summary>
internal sealed class HyperliquidOrderUpdate
{
    [JsonPropertyName("order")]
    public HyperliquidOrderInfo Order { get; set; } = new();

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("statusTimestamp")]
    public long StatusTimestamp { get; set; }
}
