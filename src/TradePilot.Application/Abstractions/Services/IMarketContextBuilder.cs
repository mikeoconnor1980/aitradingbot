using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Abstractions.Services;

/// <summary>
/// Builds market context from candle data and indicator state.
/// Shared between live and backtest modes.
/// </summary>
public interface IMarketContextBuilder
{
    void UpdateIndicators(Candle candle);

    /// <summary>
    /// Clears any live in-memory state so a new trading session starts from a clean slate.
    /// Backtest implementations can ignore this by using the default no-op behavior.
    /// </summary>
    void Reset()
    {
    }

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
