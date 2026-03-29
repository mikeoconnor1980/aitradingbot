using System.Text.Json.Serialization;

namespace TradingApp.Infrastructure.Hyperliquid.Models;

public sealed class HyperliquidModifyAction
{
    [JsonPropertyName("type")]
    public string Type { get; } = "batchModifyOrders";

    [JsonPropertyName("modifies")]
    public List<HyperliquidModifyEntry> Modifies { get; set; } = [];
}

public sealed class HyperliquidModifyEntry
{
    [JsonPropertyName("oid")]
    public long OrderId { get; set; }

    [JsonPropertyName("order")]
    public required HyperliquidModifyOrderParams Order { get; set; }
}

public sealed class HyperliquidModifyOrderParams
{
    [JsonPropertyName("a")]
    public int AssetIndex { get; set; }

    [JsonPropertyName("b")]
    public bool IsBuy { get; set; }

    [JsonPropertyName("p")]
    public required string Price { get; set; }

    [JsonPropertyName("s")]
    public required string Size { get; set; }

    [JsonPropertyName("r")]
    public bool ReduceOnly { get; set; }

    [JsonPropertyName("t")]
    public HyperliquidOrderType OrderType { get; set; } = new();
}

public sealed class HyperliquidOrderType
{
    [JsonPropertyName("limit")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HyperliquidLimitParams? Limit { get; set; }

    [JsonPropertyName("trigger")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HyperliquidTriggerParams? Trigger { get; set; }
}

public sealed class HyperliquidLimitParams
{
    [JsonPropertyName("tif")]
    public string Tif { get; set; } = "Gtc";
}

public sealed class HyperliquidTriggerParams
{
    [JsonPropertyName("triggerPx")]
    public string TriggerPx { get; set; } = default!;

    [JsonPropertyName("isMarket")]
    public bool IsMarket { get; set; } = true;

    [JsonPropertyName("tpsl")]
    public string Tpsl { get; set; } = default!;
}