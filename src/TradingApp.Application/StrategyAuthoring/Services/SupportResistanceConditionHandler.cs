using System.Globalization;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.StrategyAuthoring.Services;

/// <summary>
/// Evaluates support/resistance conditions: compares current price to the nearest
/// swing-point support or resistance level using the configured operator and tolerance.
/// Supported operators: near_support, near_resistance, above_support, below_resistance, bounce_support, bounce_resistance.
/// </summary>
public sealed class SupportResistanceConditionHandler : IConditionHandler
{
    public EntryConditionType ConditionType => EntryConditionType.SupportResistance;

    public ConditionResult Evaluate(
        EntryConditionConfig condition,
        IndicatorContext indicatorContext,
        MarketContext marketContext)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(indicatorContext);
        ArgumentNullException.ThrowIfNull(marketContext);

        if (condition.Params is not SupportResistanceParams srParams)
        {
            return Fail(condition.Id, $"Expected {nameof(SupportResistanceParams)} but received {condition.Params?.GetType().Name ?? "null"}.");
        }

        var support = indicatorContext.GetSupport(srParams.Lookback);
        var resistance = indicatorContext.GetResistance(srParams.Lookback);
        var closePrice = marketContext.CurrentCandle.Close;
        var normalizedOperator = srParams.Operator.Trim().ToLowerInvariant();

        return normalizedOperator switch
        {
            "near_support" => EvaluateNear(condition.Id, closePrice, support, srParams, "support"),
            "near_resistance" => EvaluateNear(condition.Id, closePrice, resistance, srParams, "resistance"),
            "above_support" => EvaluateAboveBelow(condition.Id, closePrice, support, srParams.Lookback, "support", above: true),
            "below_resistance" => EvaluateAboveBelow(condition.Id, closePrice, resistance, srParams.Lookback, "resistance", above: false),
            "bounce_support" => EvaluateBounce(condition.Id, marketContext, support, srParams, "support"),
            "bounce_resistance" => EvaluateBounce(condition.Id, marketContext, resistance, srParams, "resistance"),
            _ => Fail(condition.Id, $"Unknown support_resistance operator: '{srParams.Operator}'.")
        };
    }

    private static ConditionResult EvaluateNear(
        string conditionId,
        decimal closePrice,
        decimal? level,
        SupportResistanceParams srParams,
        string levelName)
    {
        if (!level.HasValue)
        {
            return Fail(conditionId, $"No {levelName} level found within lookback {srParams.Lookback}.");
        }

        if (level.Value == 0m)
        {
            return Fail(conditionId, $"{levelName} level is zero — cannot evaluate distance.");
        }

        var percentDistance = Math.Abs(closePrice - level.Value) / level.Value * 100m;
        var passed = percentDistance <= srParams.Tolerance;
        var status = passed ? "condition met" : "condition not met";

        return new ConditionResult
        {
            ConditionId = conditionId,
            Passed = passed,
            Reason = $"Price {Format(closePrice)} near {levelName} {Format(level.Value)} — distance {Format(percentDistance)}% vs tolerance {Format(srParams.Tolerance)}% — {status}"
        };
    }

    private static ConditionResult EvaluateAboveBelow(
        string conditionId,
        decimal closePrice,
        decimal? level,
        int lookback,
        string levelName,
        bool above)
    {
        if (!level.HasValue)
        {
            return Fail(conditionId, $"No {levelName} level found within lookback {lookback}.");
        }

        var passed = above ? closePrice > level.Value : closePrice < level.Value;
        var direction = above ? "above" : "below";
        var status = passed ? "condition met" : "condition not met";

        return new ConditionResult
        {
            ConditionId = conditionId,
            Passed = passed,
            Reason = $"Price {Format(closePrice)} {direction} {levelName} {Format(level.Value)} — {status}"
        };
    }

    private static ConditionResult EvaluateBounce(
        string conditionId,
        MarketContext marketContext,
        decimal? level,
        SupportResistanceParams srParams,
        string levelName)
    {
        if (!level.HasValue)
        {
            return Fail(conditionId, $"No {levelName} level found within lookback {srParams.Lookback}.");
        }

        if (level.Value == 0m)
        {
            return Fail(conditionId, $"{levelName} level is zero — cannot evaluate bounce.");
        }

        var candle = marketContext.CurrentCandle;
        var closePrice = candle.Close;

        // A bounce occurs when the candle wick touched the level zone but closed away from it
        var toleranceAmount = level.Value * srParams.Tolerance / 100m;
        bool wicked;
        bool closedAway;

        if (levelName == "support")
        {
            wicked = candle.Low <= level.Value + toleranceAmount;
            closedAway = closePrice > level.Value;
        }
        else
        {
            wicked = candle.High >= level.Value - toleranceAmount;
            closedAway = closePrice < level.Value;
        }

        var passed = wicked && closedAway;
        var status = passed ? "condition met" : "condition not met";

        return new ConditionResult
        {
            ConditionId = conditionId,
            Passed = passed,
            Reason = $"Price bounce {levelName} {Format(level.Value)} — wick touched: {wicked}, closed away: {closedAway} — {status}"
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

    private static string Format(decimal value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
