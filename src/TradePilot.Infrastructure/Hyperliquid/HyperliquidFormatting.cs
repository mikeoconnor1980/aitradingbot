using System.Globalization;
using System.Text.Json;

namespace TradePilot.Infrastructure.Hyperliquid;

public static class HyperliquidFormatting
{
    public static string ToWireDecimal(decimal value)
    {
        var formatted = value.ToString("0.############################", CultureInfo.InvariantCulture);
        return formatted.Contains('.')
            ? formatted.TrimEnd('0').TrimEnd('.')
            : formatted;
    }

    public static string MapOrderSide(string side)
    {
        return side.ToUpperInvariant() switch
        {
            "B" => "Buy",
            "A" => "Sell",
            _ => side,
        };
    }

    public static decimal ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0m;
        }

        return decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0m;
    }

    public static decimal ParseDecimal(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.GetDecimal();
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            return ParseDecimal(element.GetString());
        }

        return 0m;
    }
}