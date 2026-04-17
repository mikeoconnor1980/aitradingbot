using System.Text.Json.Serialization;

namespace TradePilot.Infrastructure.Hyperliquid.Models;

/// <summary>
/// Data payload within a userEvents WebSocket message containing fills and order updates.
/// </summary>
internal sealed class HyperliquidUserEventsData
{
    [JsonPropertyName("fills")]
    public List<HyperliquidUserFill> Fills { get; set; } = [];

    [JsonPropertyName("orderUpdates")]
    public List<HyperliquidOrderUpdate> OrderUpdates { get; set; } = [];
}
