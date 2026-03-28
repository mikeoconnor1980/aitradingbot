using System.Globalization;
using System.Text.Json.Serialization;
using TradingApp.Application.FundingRates.Models;

namespace TradingApp.Infrastructure.Binance.Models;

public sealed class BinanceFundingRate
{
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
        FundingRate = decimal.Parse(FundingRateValue, CultureInfo.InvariantCulture),
        MarkPrice = decimal.Parse(MarkPriceValue, CultureInfo.InvariantCulture),
    };
}