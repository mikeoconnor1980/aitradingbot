using System.Text.Json.Serialization;

namespace TradingApp.Infrastructure.Hyperliquid.Models;

public sealed class HyperliquidAssetCtx
{
    [JsonPropertyName("funding")]
    public string Funding { get; set; } = "0";

    [JsonPropertyName("openInterest")]
    public string OpenInterest { get; set; } = "0";

    [JsonPropertyName("prevDayPx")]
    public string PrevDayPx { get; set; } = "0";

    [JsonPropertyName("dayNtlVlm")]
    public string DayNtlVlm { get; set; } = "0";

    [JsonPropertyName("markPx")]
    public string MarkPx { get; set; } = "0";

    [JsonPropertyName("midPx")]
    public string MidPx { get; set; } = "0";

    [JsonPropertyName("oraclePx")]
    public string OraclePx { get; set; } = "0";
}
