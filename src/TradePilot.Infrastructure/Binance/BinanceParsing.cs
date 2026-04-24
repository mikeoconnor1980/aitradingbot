using System.Globalization;

namespace TradePilot.Infrastructure.Binance;

/// <summary>
/// Shared parsing helpers for Binance API payloads.
/// NumberStyles.Any is required because Binance can return scientific notation.
/// </summary>
internal static class BinanceParsing
{
    private const NumberStyles DecimalStyles = NumberStyles.Any;
    private const NumberStyles IntegerStyles = NumberStyles.Integer;

    public static decimal ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException("Cannot parse a null or empty Binance decimal value.");
        }

        if (!decimal.TryParse(value, DecimalStyles, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new FormatException($"Cannot parse Binance decimal value '{value}'.");
        }

        return parsed;
    }

    public static bool TryParseDecimal(string? value, out decimal result)
    {
        result = 0m;

        return !string.IsNullOrWhiteSpace(value)
            && decimal.TryParse(value, DecimalStyles, CultureInfo.InvariantCulture, out result);
    }

    public static int ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException("Cannot parse a null or empty Binance integer value.");
        }

        if (!int.TryParse(value, IntegerStyles, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new FormatException($"Cannot parse Binance integer value '{value}'.");
        }

        return parsed;
    }

    public static long ParseOrderId(string? orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new FormatException("Order ID cannot be null or empty.");
        }

        if (!long.TryParse(orderId, IntegerStyles, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new FormatException($"Invalid Binance order ID '{orderId}'.");
        }

        return parsed;
    }
}