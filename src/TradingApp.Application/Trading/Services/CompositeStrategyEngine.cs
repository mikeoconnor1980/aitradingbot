using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.StrategyAuthoring.Services;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Trading;

namespace TradingApp.Application.Trading.Services;

/// <summary>
/// Routes strategy evaluation to the grid or signal path based on the configured strategy mode.
/// </summary>
public sealed class CompositeStrategyEngine : IStrategyEngine
{
    private readonly GridStrategyEngine _gridEngine;
    private readonly IConditionEvaluator _conditionEvaluator;
    private readonly ITrendFilterEvaluator _trendFilterEvaluator;

    public CompositeStrategyEngine(
        GridStrategyEngine gridEngine,
        IConditionEvaluator conditionEvaluator,
        ITrendFilterEvaluator trendFilterEvaluator)
    {
        _gridEngine = gridEngine ?? throw new ArgumentNullException(nameof(gridEngine));
        _conditionEvaluator = conditionEvaluator ?? throw new ArgumentNullException(nameof(conditionEvaluator));
        _trendFilterEvaluator = trendFilterEvaluator ?? throw new ArgumentNullException(nameof(trendFilterEvaluator));
    }

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

        return config.StrategyMode switch
        {
            StrategyMode.Signal => Task.FromResult(EvaluateSignalMode(config, context)),
            _ => _gridEngine.EvaluateAsync(context, strategyConfig, cancellationToken)
        };
    }

    private StrategyEvaluation EvaluateSignalMode(StrategyConfig config, MarketContext context)
    {
        var trendFilter = config.TrendFilter;
        TrendFilterResult? trendResult = null;

        if (ShouldEvaluateTrendFilter(trendFilter, config.Direction))
        {
            if (context.IndicatorContext is null)
            {
                return new StrategyEvaluation
                {
                    SetupDetected = false,
                    TrendFilterPassed = false,
                    Reason = "Trend filter failed: Indicator context not available.",
                };
            }

            trendResult = _trendFilterEvaluator.Evaluate(
                trendFilter,
                config.Direction,
                context.IndicatorContext,
                context);

            if (!trendResult.Passed)
            {
                return new StrategyEvaluation
                {
                    SetupDetected = false,
                    TrendFilterPassed = false,
                    Reason = $"Trend filter failed: {trendResult.Reason}",
                };
            }
        }

        var result = _conditionEvaluator.Evaluate(config, context);

        return new StrategyEvaluation
        {
            SetupDetected = result.SetupDetected,
            TrendFilterPassed = trendResult?.Passed,
            Reason = result.OverallReason
        };
    }

    private static bool ShouldEvaluateTrendFilter(TrendFilterConfig? filter, Direction strategyDirection)
    {
        if (filter is null || !filter.Enabled)
        {
            return false;
        }

        return filter.AppliesTo == Direction.Both || filter.AppliesTo == strategyDirection;
    }
}