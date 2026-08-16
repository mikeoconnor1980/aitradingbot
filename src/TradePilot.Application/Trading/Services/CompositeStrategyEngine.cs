using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Services;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Enums;
using TradePilot.Domain.Trading;

namespace TradePilot.Application.Trading.Services;

/// <summary>
/// Routes strategy evaluation to the configured strategy mode.
/// </summary>
public sealed class CompositeStrategyEngine : IStrategyEngine
{
    private readonly GridStrategyEngine _gridEngine;
    private readonly DcaStrategyEngine _dcaEngine;
    private readonly IConditionEvaluator _conditionEvaluator;
    private readonly ITrendFilterEvaluator _trendFilterEvaluator;

    public CompositeStrategyEngine(
        GridStrategyEngine gridEngine,
        DcaStrategyEngine dcaEngine,
        IConditionEvaluator conditionEvaluator,
        ITrendFilterEvaluator trendFilterEvaluator)
    {
        _gridEngine = gridEngine ?? throw new ArgumentNullException(nameof(gridEngine));
        _dcaEngine = dcaEngine ?? throw new ArgumentNullException(nameof(dcaEngine));
        _conditionEvaluator = conditionEvaluator ?? throw new ArgumentNullException(nameof(conditionEvaluator));
        _trendFilterEvaluator = trendFilterEvaluator ?? throw new ArgumentNullException(nameof(trendFilterEvaluator));
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

        return config.StrategyMode switch
        {
            StrategyMode.Signal => Task.FromResult(EvaluateSignalMode(config, context)),
            StrategyMode.Dca => _dcaEngine.EvaluateAsync(context, strategyConfig, cancellationToken),
            _ => _gridEngine.EvaluateAsync(context, strategyConfig, cancellationToken)
        };
    }

    private StrategyEvaluationResult EvaluateSignalMode(StrategyConfig config, MarketContext context)
    {
        var trendFilter = config.TrendFilter;
        TrendFilterResult? trendResult = null;
        var rules = new List<RuleEvaluationResult>();

        if (ShouldEvaluateTrendFilter(trendFilter, config.Direction))
        {
            if (context.IndicatorContext is null)
            {
                return new StrategyEvaluationResult
                {
                    SetupDetected = false,
                    TrendFilterPassed = false,
                    Reason = "Trend filter failed: Indicator context not available.",
                    EvaluationShortCircuited = true,
                    Rules =
                    [
                        new RuleEvaluationResult(
                            BuildTrendRuleId(trendFilter!),
                            "Trend filter",
                            RuleCategory.Trend,
                            false,
                            "Trend filter failed: Indicator context not available.",
                            true,
                            ActualValue: "unavailable",
                            ExpectedValue: "indicator context available")
                    ]
                };
            }

            trendResult = _trendFilterEvaluator.Evaluate(
                trendFilter,
                config.Direction,
                context.IndicatorContext,
                context);

            if (!trendResult.Passed)
            {
                return new StrategyEvaluationResult
                {
                    SetupDetected = false,
                    TrendFilterPassed = false,
                    Reason = $"Trend filter failed: {trendResult.Reason}",
                    EvaluationShortCircuited = true,
                    Rules =
                    [
                        new RuleEvaluationResult(
                            BuildTrendRuleId(trendFilter!),
                            "Trend filter",
                            RuleCategory.Trend,
                            false,
                            trendResult.Reason,
                            true,
                            ActualValue: trendResult.ActualValue,
                            ActualNumericValue: trendResult.ActualNumericValue,
                            ExpectedValue: trendResult.ExpectedValue,
                            ExpectedNumericValue: trendResult.ExpectedNumericValue)
                    ]
                };
            }

            rules.Add(new RuleEvaluationResult(
                BuildTrendRuleId(trendFilter!),
                "Trend filter",
                RuleCategory.Trend,
                true,
                trendResult.Reason,
                true,
                ActualValue: trendResult.ActualValue,
                ActualNumericValue: trendResult.ActualNumericValue,
                ExpectedValue: trendResult.ExpectedValue,
                ExpectedNumericValue: trendResult.ExpectedNumericValue));
        }

        var result = _conditionEvaluator.Evaluate(config, context);
        var conditionsById = config.EntryConditions?
            .Where(condition => condition.Enabled)
            .ToDictionary(condition => condition.Id, StringComparer.Ordinal) ?? [];
        var anyModePassed = config.EntryLogic == EntryLogic.Any && result.SetupDetected;

        foreach (var conditionResult in result.ConditionResults.Where(condition => condition.WasEvaluated))
        {
            conditionsById.TryGetValue(conditionResult.ConditionId, out var condition);
            var failedRuleBlocks = !conditionResult.Passed && !anyModePassed;
            rules.Add(new RuleEvaluationResult(
                BuildEntryRuleId(condition, conditionResult.ConditionId),
                string.IsNullOrWhiteSpace(condition?.Label)
                    ? condition?.Type.ToString() ?? conditionResult.ConditionId
                    : condition.Label,
                MapCategory(condition?.Type),
                conditionResult.Passed,
                conditionResult.Reason,
                failedRuleBlocks,
                failedRuleBlocks ? RuleEvaluationKind.Blocking : RuleEvaluationKind.Informational,
                conditionResult.ActualValue,
                conditionResult.ActualNumericValue,
                conditionResult.ExpectedValue,
                conditionResult.ExpectedNumericValue,
                conditionResult.Unit));
        }

        return new StrategyEvaluationResult
        {
            SetupDetected = result.SetupDetected,
            TrendFilterPassed = trendResult?.Passed,
            Reason = result.OverallReason,
            ConditionResults = result.ConditionResults,
            Rules = rules,
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

    private static string BuildTrendRuleId(TrendFilterConfig filter)
    {
        return $"trend.{ToStableToken(filter.Type.ToString())}";
    }

    private static string BuildEntryRuleId(EntryConditionConfig? condition, string conditionId)
    {
        var type = condition is null ? "condition" : ToStableToken(condition.Type.ToString());
        return $"entry.{type}.{ToStableToken(conditionId)}";
    }

    private static RuleCategory MapCategory(EntryConditionType? type)
    {
        return type switch
        {
            EntryConditionType.Rsi or EntryConditionType.Macd => RuleCategory.Momentum,
            EntryConditionType.PriceVsEma => RuleCategory.Trend,
            _ => RuleCategory.Entry,
        };
    }

    private static string ToStableToken(string value)
    {
        var characters = value.Trim().ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '.')
            .ToArray();
        return string.Join('.', new string(characters).Split('.', StringSplitOptions.RemoveEmptyEntries));
    }
}
