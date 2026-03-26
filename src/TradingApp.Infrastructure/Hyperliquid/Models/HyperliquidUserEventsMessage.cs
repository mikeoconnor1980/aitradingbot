using System.Text.Json.Serialization;

namespace TradingApp.Infrastructure.Hyperliquid.Models;

/// <summary>
/// Inbound WebSocket message for the userEvents channel.
/// Expected format: { "channel": "user", "data": { "fills": [...], "orderUpdates": [...] } }
/// Note: Exact channel name and data shape to be verified against Hyperliquid API.
/// </summary>
internal sealed class HyperliquidUserEventsMessage
{
    [JsonPropertyName("channel")]
    public string Channel { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public HyperliquidUserEventsData Data { get; set; } = new();
}
