using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradingApp.Application.StrategyAuthoring.Serialization;

public sealed class FlexibleStringJsonConverter : JsonConverter<string>
{
    public override string? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt64(out var integerValue)
                ? integerValue.ToString(CultureInfo.InvariantCulture)
                : reader.GetDouble().ToString(CultureInfo.InvariantCulture),
            JsonTokenType.True => bool.TrueString,
            JsonTokenType.False => bool.FalseString,
            JsonTokenType.Null => string.Empty,
            _ => JsonDocument.ParseValue(ref reader).RootElement.ToString(),
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        string value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}