using System.Text.Json.Serialization;

namespace TradingApp.Infrastructure.Hyperliquid.Models;

public sealed class HyperliquidCandle
{
    [JsonPropertyName("t")]
    public long OpenTime { get; set; }

    [JsonPropertyName("T")]
    public long CloseTime { get; set; }

    [JsonPropertyName("s")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("i")]
    public string Interval { get; set; } = string.Empty;

    [JsonPropertyName("o")]
    public string Open { get; set; } = "0";

    [JsonPropertyName("h")]
    public string High { get; set; } = "0";

    [JsonPropertyName("l")]
    public string Low { get; set; } = "0";

    [JsonPropertyName("c")]
    public string Close { get; set; } = "0";

    [JsonPropertyName("v")]
    public string Volume { get; set; } = "0";

    [JsonPropertyName("n")]
    public int NumTrades { get; set; }
}