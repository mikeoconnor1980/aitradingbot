using System.Globalization;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;

namespace TradePilot.Application.StrategyAuthoring.Services;

/// <summary>
/// Evaluates RSI conditions: compares RSI(period) to a threshold using the configured operator.
/// Supports: lt, lte, gt, gte, cross_above, cross_below.
/// </summary>
public sealed class RsiConditionHandler : IConditionHandler
{
    public EntryConditionType ConditionType => EntryConditionType.Rsi;

    public ConditionResult Evaluate(
        EntryConditionConfig condition,
        IndicatorContext indicatorContext,
        MarketContext marketContext)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(indicatorContext);
        ArgumentNullException.ThrowIfNull(marketContext);

        if (condition.Params is not RsiParams rsi)
        {
            return Fail(condition.Id, $"Expected {nameof(RsiParams)} but received {condition.Params?.GetType().Name ?? "null"}.");
        }

        var currentRsi = indicatorContext.GetRsi(rsi.Period);
        if (!currentRsi.HasValue)
        {
            return Fail(condition.Id, $"RSI({rsi.Period}) not available in indicator context.");
        }

        var normalizedOperator = rsi.Operator.Trim().ToLowerInvariant();

        return normalizedOperator switch
        {
            "lt" => EvaluateComparison(condition.Id, currentRsi.Value, rsi.Value, rsi.Period, "<", (current, threshold) => current < threshold),
            "lte" => EvaluateComparison(condition.Id, currentRsi.Value, rsi.Value, rsi.Period, "<=", (current, threshold) => current <= threshold),
            "gt" => EvaluateComparison(condition.Id, currentRsi.Value, rsi.Value, rsi.Period, ">", (current, threshold) => current > threshold),
            "gte" => EvaluateComparison(condition.Id, currentRsi.Value, rsi.Value, rsi.Period, ">=", (current, threshold) => current >= threshold),
            "cross_above" => EvaluateCross(condition.Id, indicatorContext, rsi, crossAbove: true),
            "cross_below" => EvaluateCross(condition.Id, indicatorContext, rsi, crossAbove: false),
            _ => Fail(condition.Id, $"Unknown RSI operator: '{rsi.Operator}'.")
        };
    }

    private static ConditionResult EvaluateComparison(
        string conditionId,
        decimal currentRsi,
        decimal threshold,
        int period,
        string operatorSymbol,
        Func<decimal, decimal, bool> compare)
    {
        var passed = compare(currentRsi, threshold);
        var status = passed ? "condition met" : "condition not met";

        return new ConditionResult
        {
            ConditionId = conditionId,
            Passed = passed,
            Reason = $"RSI({period}) = {FormatValue(currentRsi)} {operatorSymbol} {FormatValue(threshold)} - {status}"
        };
    }

    private static ConditionResult EvaluateCross(
        string conditionId,
        IndicatorContext indicatorContext,
        RsiParams rsi,
        bool crossAbove)
    {
        var currentRsi = indicatorContext.GetRsi(rsi.Period);
        var previousRsi = indicatorContext.GetPreviousRsi(rsi.Period);

        if (!currentRsi.HasValue || !previousRsi.HasValue)
        {
            return Fail(conditionId, $"RSI({rsi.Period}) previous value not available for cross detection.");
        }

        bool passed;
        string direction;

        if (crossAbove)
        {
            passed = previousRsi.Value <= rsi.Value && currentRsi.Value > rsi.Value;
            direction = "cross_above";
        }
        else
        {
            passed = previousRsi.Value >= rsi.Value && currentRsi.Value < rsi.Value;
            direction = "cross_below";
        }

        var status = passed ? "condition met" : "condition not met";

        return new ConditionResult
        {
            ConditionId = conditionId,
            Passed = passed,
            Reason = $"RSI({rsi.Period}) prev={FormatValue(previousRsi.Value)} curr={FormatValue(currentRsi.Value)} {direction} {FormatValue(rsi.Value)} - {status}"
        };
    }

    private static ConditionResult Fail(string conditionId, string reason)
    {
        return new ConditionResult
        {
            ConditionId = conditionId,
            Passed = false,
            Reason = reason
        };
    }

    private static string FormatValue(decimal value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}