using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Agent.Models;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Trading;

namespace TradePilot.Application.Trading.Services;

public sealed class GridStrategyEngine : IStrategyEngine
{
    private readonly IExecutionLogger _executionLogger;

    public GridStrategyEngine(IExecutionLogger? executionLogger = null)
    {
        _executionLogger = executionLogger ?? NullExecutionLogger.Instance;
    }

    public Task<StrategyEvaluationResult> EvaluateAsync(MarketContext context, IStrategyConfig strategyConfig, CancellationToken cancellationToken = default)
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

            return Task.FromResult(Fail(
                "grid.configuration",
                "Grid configuration",
                "Grid configuration is incomplete.",
                actualValue: $"levels={config.Grid?.Levels ?? 0}, spacing={config.Grid?.Spacing ?? 0m}, positionSize={config.Risk.PositionSizeValue}",
                expectedValue: "levels > 0, spacing > 0, position size > 0"));
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

            return Task.FromResult(new StrategyEvaluationResult
            {
                SetupDetected = false,
                Reason = "Higher timeframe context is not available yet.",
                EvaluationShortCircuited = true,
                Rules =
                [
                    Pass("grid.configuration", "Grid configuration", "Grid configuration is complete."),
                    new RuleEvaluationResult(
                        "market.higher_timeframes",
                        "Higher timeframe context",
                        TradePilot.Domain.Enums.RuleCategory.Entry,
                        false,
                        "Higher timeframe context is not available yet.",
                        true,
                        ActualValue: $"1h={context.LatestOneHourCandle is not null}, 4h={context.LatestFourHourCandle is not null}",
                        ExpectedValue: "1h and 4h candles available")
                ]
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

            return Task.FromResult(new StrategyEvaluationResult
            {
                SetupDetected = false,
                Regime = regime,
                Reason = "Regime is RiskOff — new grid entries are blocked.",
                EvaluationShortCircuited = true,
                Rules =
                [
                    Pass("grid.configuration", "Grid configuration", "Grid configuration is complete."),
                    Pass("market.higher_timeframes", "Higher timeframe context", "Higher timeframe context is available."),
                    new RuleEvaluationResult(
                        "entry.market_regime",
                        "Market regime",
                        TradePilot.Domain.Enums.RuleCategory.Entry,
                        false,
                        "Regime is RiskOff — new grid entries are blocked.",
                        true,
                        ActualValue: regime.ToString(),
                        ExpectedValue: "not RiskOff")
                ]
            });
        }

        _executionLogger.LogDetail(
            ExecutionLogCategory.EntryGate,
            $"Gate PASSED: Regime is {regime}",
            new Dictionary<string, object> { ["gate"] = "Regime", ["passed"] = true, ["regime"] = regime.ToString() });

        return Task.FromResult(new StrategyEvaluationResult
        {
            SetupDetected = true,
            Regime = regime,
            Reason = $"Grid setup available. Regime: {regime}.",
            Rules =
            [
                Pass("grid.configuration", "Grid configuration", "Grid configuration is complete."),
                Pass("market.higher_timeframes", "Higher timeframe context", "Higher timeframe context is available."),
                new RuleEvaluationResult(
                    "entry.market_regime",
                    "Market regime",
                    TradePilot.Domain.Enums.RuleCategory.Entry,
                    true,
                    $"Regime {regime} permits new grid entries.",
                    true,
                    ActualValue: regime.ToString(),
                    ExpectedValue: "not RiskOff")
            ]
        });
    }

    private static StrategyEvaluationResult Fail(
        string ruleId,
        string name,
        string reason,
        string? actualValue = null,
        string? expectedValue = null)
    {
        return new StrategyEvaluationResult
        {
            SetupDetected = false,
            Reason = reason,
            EvaluationShortCircuited = true,
            Rules =
            [
                new RuleEvaluationResult(
                    ruleId,
                    name,
                    TradePilot.Domain.Enums.RuleCategory.Entry,
                    false,
                    reason,
                    true,
                    ActualValue: actualValue,
                    ExpectedValue: expectedValue)
            ]
        };
    }

    private static RuleEvaluationResult Pass(string ruleId, string name, string reason)
    {
        return new RuleEvaluationResult(
            ruleId,
            name,
            TradePilot.Domain.Enums.RuleCategory.Entry,
            true,
            reason,
            true);
    }
}
