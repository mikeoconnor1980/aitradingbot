using TradingApp.Domain.Entities;

namespace TradingApp.Application.Trading.Models;

/// <summary>
/// Market context provided to the strategy engine at each candle close.
/// Contains the trigger candle, higher-timeframe context, and computed indicators.
/// </summary>
public sealed class MarketContext
{
    public required string Symbol { get; init; }
    public required long TimestampUtc { get; init; }
    public required Candle CurrentCandle { get; init; }
    public Candle? PreviousCandle { get; init; }
    public Candle? LatestOneHourCandle { get; init; }
    public Candle? LatestFourHourCandle { get; init; }
    public required IndicatorSnapshot Indicators { get; init; }
    public IndicatorContext? IndicatorContext { get; init; }
    public LlmContext? LlmContext { get; init; }
    public decimal AccountEquity { get; set; }
    public int? MaxLeverage { get; init; }
}
