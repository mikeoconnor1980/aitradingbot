using System.Text.Json.Serialization;

namespace TradingApp.Infrastructure.Hyperliquid.Models;

internal sealed class HyperliquidTradesMessage
{
    [JsonPropertyName("channel")]
    public string Channel { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public List<HyperliquidTrade> Data { get; set; } = [];
}