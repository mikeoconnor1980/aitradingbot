using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Agent.Models;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Trading;

namespace TradingApp.Application.Trading.Services;

public sealed class GridStrategyEngine : IStrategyEngine
{
    private readonly IExecutionLogger _executionLogger;

    public GridStrategyEngine(IExecutionLogger? executionLogger = null)
    {
        _executionLogger = executionLogger ?? NullExecutionLogger.Instance;
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

        if (config.Grid is null
            || config.Grid.Levels <= 0
            || config.Grid.Spacing <= 0m
            || config.Risk.PositionSizeValue <= 0m)
        {
            _executionLogger.LogDetail(
                ExecutionLogCategory.EntryGate,
                "Gate FAILED: Grid configuration is incomplete.",
                new Dictionary<string, object>
                {
                    ["gate"] = "GridConfig",
                    ["passed"] = false,
                });

            return Task.FromResult(new StrategyEvaluation
            {
                SetupDetected = false,
                Reason = "Grid configuration is incomplete."
            });
        }

        _executionLogger.LogDetail(
            ExecutionLogCategory.EntryGate,
            $"Gate PASSED: Grid config valid (Levels={config.Grid.Levels}, Spacing={config.Grid.Spacing:F4})",
            new Dictionary<string, object> { ["gate"] = "GridConfig", ["passed"] = true });

        if (context.LatestOneHourCandle is null || context.LatestFourHourCandle is null)
        {
            _executionLogger.LogDetail(
                ExecutionLogCategory.EntryGate,
                $"Gate FAILED: Higher TF candles missing (1H={context.LatestOneHourCandle is not null}, 4H={context.LatestFourHourCandle is not null})",
                new Dictionary<string, object> { ["gate"] = "HigherTFCandles", ["passed"] = false });

            return Task.FromResult(new StrategyEvaluation
            {
                SetupDetected = false,
                Reason = "Higher timeframe context is not available yet."
            });
        }

        _executionLogger.LogDetail(
            ExecutionLogCategory.EntryGate,
            "Gate PASSED: Higher TF candles available",
            new Dictionary<string, object> { ["gate"] = "HigherTFCandles", ["passed"] = true });

        var regime = context.LlmContext?.DerivedRegime ?? MarketRegime.Normal;

        if (regime == MarketRegime.RiskOff)
        {
            _executionLogger.LogDetail(
                ExecutionLogCategory.EntryGate,
                $"Gate FAILED: Regime is RiskOff — new grid entries blocked",
                new Dictionary<string, object> { ["gate"] = "Regime", ["passed"] = false, ["regime"] = regime.ToString() });

            return Task.FromResult(new StrategyEvaluation
            {
                SetupDetected = false,
                Regime = regime,
                Reason = "Regime is RiskOff — new grid entries are blocked."
            });
        }

        _executionLogger.LogDetail(
            ExecutionLogCategory.EntryGate,
            $"Gate PASSED: Regime is {regime}",
            new Dictionary<string, object> { ["gate"] = "Regime", ["passed"] = true, ["regime"] = regime.ToString() });

        return Task.FromResult(new StrategyEvaluation
        {
            SetupDetected = true,
            Regime = regime,
            Reason = $"Grid setup available. Regime: {regime}."
        });
    }
}