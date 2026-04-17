using System.Text.Json;
using System.Text.Json.Serialization;
using TradePilot.Application.StrategyAuthoring.Models;

namespace TradePilot.Application.StrategyAuthoring.Serialization;

public sealed class EntryConditionConfigConverter : JsonConverter<EntryConditionConfig>
{
    public override EntryConditionConfig? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        var type = root.TryGetProperty("type", out var typeElement)
            ? ParseType(typeElement.GetString())
            : EntryConditionType.Unknown;

        IEntryConditionParams? entryParams = null;
        if (root.TryGetProperty("params", out var paramsElement) && paramsElement.ValueKind != JsonValueKind.Null)
        {
            entryParams = EntryConditionParamsConverter.DeserializeForType(paramsElement, type, options);
        }

        return new EntryConditionConfig
        {
            Id = root.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? string.Empty : string.Empty,
            Enabled = root.TryGetProperty("enabled", out var enabledElement) && enabledElement.GetBoolean(),
            Type = type,
            Label = root.TryGetProperty("label", out var labelElement) ? labelElement.GetString() ?? string.Empty : string.Empty,
            Params = entryParams,
        };
    }

    public override void Write(Utf8JsonWriter writer, EntryConditionConfig value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();
        writer.WriteString("id", value.Id);
        writer.WriteBoolean("enabled", value.Enabled);
        writer.WritePropertyName("type");
        JsonSerializer.Serialize(writer, value.Type, options);
        writer.WriteString("label", value.Label);
        writer.WritePropertyName("params");

        if (value.Params is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            JsonSerializer.Serialize(writer, value.Params, value.Params.GetType(), options);
        }

        writer.WriteEndObject();
    }

    private static EntryConditionType ParseType(string? type)
    {
        return type switch
        {
            "rsi" => EntryConditionType.Rsi,
            "price_vs_ema" => EntryConditionType.PriceVsEma,
            "macd" => EntryConditionType.Macd,
            "support_resistance" => EntryConditionType.SupportResistance,
            _ => EntryConditionType.Unknown,
        };
    }
}