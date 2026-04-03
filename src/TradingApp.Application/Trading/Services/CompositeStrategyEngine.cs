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

    public CompositeStrategyEngine(GridStrategyEngine gridEngine, IConditionEvaluator conditionEvaluator)
    {
        _gridEngine = gridEngine ?? throw new ArgumentNullException(nameof(gridEngine));
        _conditionEvaluator = conditionEvaluator ?? throw new ArgumentNullException(nameof(conditionEvaluator));
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
        var result = _conditionEvaluator.Evaluate(config, context);

        return new StrategyEvaluation
        {
            SetupDetected = result.SetupDetected,
            Reason = result.OverallReason
        };
    }
}