using System.Globalization;
using System.Text.Json;
using TradePilot.Application.MarketData.Models;

namespace TradePilot.Infrastructure.Binance.Models;

public sealed class BinanceKline
{
    public long OpenTime { get; init; }
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Close { get; init; }
    public decimal Volume { get; init; }
    public long CloseTime { get; init; }
    public int NumberOfTrades { get; init; }

    public static BinanceKline FromJsonArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() < 9)
        {
            throw new JsonException("Binance kline payload must be an array with at least 9 elements.");
        }

        return new BinanceKline
        {
            OpenTime = element[0].GetInt64(),
            Open = ParseDecimal(element[1]),
            High = ParseDecimal(element[2]),
            Low = ParseDecimal(element[3]),
            Close = ParseDecimal(element[4]),
            Volume = ParseDecimal(element[5]),
            CloseTime = element[6].GetInt64(),
            NumberOfTrades = element[8].GetInt32(),
        };
    }

    public CandleSnapshotDto ToCandleSnapshotDto() => new()
    {
        Timestamp = OpenTime,
        Open = Open,
        High = High,
        Low = Low,
        Close = Close,
        Volume = Volume,
        NumTrades = NumberOfTrades,
    };

    private static decimal ParseDecimal(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new JsonException($"Expected string for Binance decimal value, got {value.ValueKind}.");
        }

        var stringValue = value.GetString();
        if (!decimal.TryParse(stringValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new JsonException($"Unable to parse Binance decimal value '{stringValue}'.");
        }

        return parsed;
    }
}