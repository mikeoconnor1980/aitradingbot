using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradePilot.Infrastructure.Hyperliquid.Models;

public sealed class HyperliquidExchangeResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = default!;

    [JsonPropertyName("response")]
    [JsonConverter(typeof(ExchangeResponseConverter))]
    public HyperliquidExchangeResponseData? Response { get; set; }
}

/// <summary>
/// Handles the polymorphic "response" field from Hyperliquid.
/// On success: { "type": "order", "data": { ... } }
/// On error:   a plain string like "Some error message"
/// </summary>
internal sealed class ExchangeResponseConverter : JsonConverter<HyperliquidExchangeResponseData?>
{
    public override HyperliquidExchangeResponseData? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var errorMessage = reader.GetString();
            return new HyperliquidExchangeResponseData
            {
                Type = "error",
                ErrorMessage = errorMessage,
            };
        }

        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<HyperliquidExchangeResponseData>(ref reader, options);
    }

    public override void Write(Utf8JsonWriter writer, HyperliquidExchangeResponseData? value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, options);
    }
}

public sealed class HyperliquidExchangeResponseData
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = default!;

    [JsonPropertyName("data")]
    public HyperliquidOrderResponseData? Data { get; set; }

    /// <summary>
    /// Populated when Hyperliquid returns the response field as a plain string (error case).
    /// </summary>
    [JsonIgnore]
    public string? ErrorMessage { get; set; }
}

public sealed class HyperliquidOrderResponseData
{
    [JsonPropertyName("statuses")]
    public List<HyperliquidOrderStatus>? Statuses { get; set; }
}

public sealed class HyperliquidOrderStatus
{
    [JsonPropertyName("resting")]
    public HyperliquidRestingOrder? Resting { get; set; }

    [JsonPropertyName("filled")]
    public HyperliquidFilledOrder? Filled { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public sealed class HyperliquidRestingOrder
{
    [JsonPropertyName("oid")]
    public long Oid { get; set; }
}

public sealed class HyperliquidFilledOrder
{
    [JsonPropertyName("totalSz")]
    public string TotalSz { get; set; } = default!;

    [JsonPropertyName("avgPx")]
    public string AvgPx { get; set; } = default!;

    [JsonPropertyName("oid")]
    public long Oid { get; set; }
}
