using System.Text.Json;
using System.Text.Json.Serialization;
using TradePilot.Application.StrategyAuthoring.Models;

namespace TradePilot.Application.StrategyAuthoring.Serialization;

public sealed class EntryConditionParamsConverter : JsonConverter<IEntryConditionParams>
{
    public override IEntryConditionParams? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // This converter cannot resolve the polymorphic type without the parent's EntryConditionType.
        // Use EntryConditionConfigConverter which calls DeserializeForType with the correct discriminator.
        throw new NotSupportedException(
            "IEntryConditionParams cannot be deserialized directly. Use EntryConditionConfigConverter instead.");
    }

    public override void Write(Utf8JsonWriter writer, IEntryConditionParams value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }

    public static IEntryConditionParams? DeserializeForType(
        JsonElement element,
        EntryConditionType conditionType,
        JsonSerializerOptions options)
    {
        return conditionType switch
        {
            EntryConditionType.Rsi => element.Deserialize<RsiParams>(options),
            EntryConditionType.PriceVsEma => element.Deserialize<PriceVsEmaParams>(options),
            EntryConditionType.Macd => element.Deserialize<MacdParams>(options),
            EntryConditionType.SupportResistance => element.Deserialize<SupportResistanceParams>(options),
            EntryConditionType.CandlePattern => element.Deserialize<CandlePatternParams>(options),
            EntryConditionType.LiquiditySweep => element.Deserialize<LiquiditySweepParams>(options),
            EntryConditionType.StructureShift => element.Deserialize<StructureShiftParams>(options),
            _ => DeserializeUnknown(element),
        };
    }

    public static UnknownConditionParams DeserializeUnknown(JsonElement element)
    {
        var properties = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                properties[property.Name] = property.Value.Clone();
            }
        }

        return new UnknownConditionParams
        {
            RawProperties = properties,
        };
    }
}