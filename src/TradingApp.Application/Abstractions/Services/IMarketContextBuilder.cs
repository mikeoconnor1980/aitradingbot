using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Abstractions.Services;

/// <summary>
/// Builds market context from candle data and indicator state.
/// Shared between live and backtest modes.
/// </summary>
public interface IMarketContextBuilder
{
    void UpdateIndicators(Candle candle);

    MarketContext Build(Candle triggerCandle, Candle? latestOneHourCandle, Candle? latestFourHourCandle);

    MarketContext Build(
        Candle triggerCandle,
        Candle? latestOneHourCandle,
        Candle? latestFourHourCandle,
        IReadOnlyList<IndicatorRequirement>? requiredIndicators);
}
