using System.Globalization;
using System.Text.Json.Serialization;
using TradingApp.Application.FundingRates.Models;

namespace TradingApp.Infrastructure.Binance.Models;

public sealed class BinanceFundingRate
{
    private const decimal DefaultMarkPrice = 0m;

    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("fundingTime")]
    public long FundingTime { get; init; }

    [JsonPropertyName("fundingRate")]
    public string FundingRateValue { get; init; } = string.Empty;

    [JsonPropertyName("markPrice")]
    public string MarkPriceValue { get; init; } = string.Empty;

    public FundingRateDto ToDto() => new()
    {
        FundingTime = FundingTime,
        Rate = ParseRequiredDecimal(FundingRateValue, nameof(FundingRateValue)),
        MarkPrice = ParseOptionalDecimal(MarkPriceValue, nameof(MarkPriceValue), DefaultMarkPrice),
    };

    private static decimal ParseRequiredDecimal(string value, string fieldName)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new System.Text.Json.JsonException($"Unable to parse Binance funding rate field '{fieldName}' value '{value}'.");
        }

        return parsed;
    }

    private static decimal ParseOptionalDecimal(string value, string fieldName, decimal fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new System.Text.Json.JsonException($"Unable to parse Binance funding rate field '{fieldName}' value '{value}'.");
        }

        return parsed;
    }
}