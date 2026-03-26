using System.Text.Json.Serialization;

namespace TradingApp.Infrastructure.Hyperliquid.Models;

/// <summary>
/// Subscription request for Hyperliquid userEvents WebSocket stream.
/// </summary>
internal sealed class HyperliquidUserEventSubscription
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "userEvents";

    [JsonPropertyName("user")]
    public string User { get; set; } = string.Empty;
}
