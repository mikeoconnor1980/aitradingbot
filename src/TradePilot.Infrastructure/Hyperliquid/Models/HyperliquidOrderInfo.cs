using System.Text.Json.Serialization;

namespace TradePilot.Infrastructure.Hyperliquid.Models;

/// <summary>
/// Order detail within an order update from the Hyperliquid userEvents WebSocket stream.
/// </summary>
internal sealed class HyperliquidOrderInfo
{
    [JsonPropertyName("coin")]
    public string Coin { get; set; } = string.Empty;

    [JsonPropertyName("side")]
    public string Side { get; set; } = string.Empty;

    [JsonPropertyName("limitPx")]
    public string LimitPrice { get; set; } = string.Empty;

    [JsonPropertyName("sz")]
    public string Size { get; set; } = string.Empty;

    [JsonPropertyName("oid")]
    public long OrderId { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("origSz")]
    public string OriginalSize { get; set; } = string.Empty;
}
