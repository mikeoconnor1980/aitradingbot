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

    /// <summary>
    /// Async overload that supports LLM context enrichment.
    /// Default implementation delegates to the synchronous <see cref="Build"/> method.
    /// </summary>
    Task<MarketContext> BuildAsync(
        Candle triggerCandle,
        Candle? latestOneHourCandle,
        Candle? latestFourHourCandle,
        IReadOnlyList<IndicatorRequirement>? requiredIndicators,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Build(triggerCandle, latestOneHourCandle, latestFourHourCandle, requiredIndicators));
    }
}
