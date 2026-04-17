using System.Text.Json.Serialization;

namespace TradePilot.Infrastructure.Hyperliquid.Models;

public sealed class HyperliquidUpdateLeverageAction
{
    [JsonPropertyName("type")]
    public string Type { get; } = "updateLeverage";

    [JsonPropertyName("asset")]
    public int Asset { get; set; }

    [JsonPropertyName("isCross")]
    public bool IsCross { get; set; }

    [JsonPropertyName("leverage")]
    public int Leverage { get; set; }
}
