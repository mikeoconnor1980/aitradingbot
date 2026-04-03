using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Trading;

namespace TradingApp.Application.Trading.Services;

public sealed class GridStrategyEngine : IStrategyEngine
{
    public Task<StrategyEvaluation> EvaluateAsync(MarketContext context, IStrategyConfig strategyConfig, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(strategyConfig);

        if (strategyConfig is not StrategyConfig config)
        {
            throw new ArgumentException(
                $"Expected {nameof(StrategyConfig)} but received {strategyConfig.GetType().Name}.",
                nameof(strategyConfig));
        }

        if (config.Grid is null
            || config.Grid.Levels <= 0
            || config.Grid.Spacing <= 0m
            || config.Risk.PositionSizeValue <= 0m)
        {
            return Task.FromResult(new StrategyEvaluation
            {
                SetupDetected = false,
                Reason = "Grid configuration is incomplete."
            });
        }

        if (context.LatestOneHourCandle is null || context.LatestFourHourCandle is null)
        {
            return Task.FromResult(new StrategyEvaluation
            {
                SetupDetected = false,
                Reason = "Higher timeframe context is not available yet."
            });
        }

        return Task.FromResult(new StrategyEvaluation
        {
            SetupDetected = true,
            Reason = "Grid setup available."
        });
    }
}