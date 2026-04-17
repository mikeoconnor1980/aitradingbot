using System.Globalization;
using Microsoft.Extensions.Logging;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;

namespace TradePilot.Application.StrategyAuthoring.Services;

public sealed class PriceVsEmaConditionHandler : IConditionHandler
{
    private readonly ILogger<PriceVsEmaConditionHandler> _logger;

    public PriceVsEmaConditionHandler(ILogger<PriceVsEmaConditionHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public EntryConditionType ConditionType => EntryConditionType.PriceVsEma;

    public ConditionResult Evaluate(
        EntryConditionConfig condition,
        IndicatorContext indicatorContext,
        MarketContext marketContext)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(indicatorContext);
        ArgumentNullException.ThrowIfNull(marketContext);

        if (condition.Params is not PriceVsEmaParams emaParams)
        {
            return Fail(
                condition.Id,
                $"Expected {nameof(PriceVsEmaParams)} but received {condition.Params?.GetType().Name ?? "null"}.");
        }

        var emaValue = indicatorContext.GetEma(emaParams.Period);
        if (!emaValue.HasValue)
        {
            return Fail(condition.Id, $"EMA({emaParams.Period}) not available in indicator context.");
        }

        var closePrice = marketContext.CurrentCandle.Close;
        var normalizedOperator = emaParams.Operator.Trim().ToLowerInvariant();

        return normalizedOperator switch
        {
            "near" => EvaluateNear(condition.Id, closePrice, emaValue.Value, emaParams),
            "above" => EvaluateComparison(condition.Id, closePrice, emaValue.Value, emaParams.Period, ">", (price, ema) => price > ema),
            "below" => EvaluateComparison(condition.Id, closePrice, emaValue.Value, emaParams.Period, "<", (price, ema) => price < ema),
            "cross_above" => EvaluateCross(condition.Id, marketContext, indicatorContext, emaParams.Period, crossAbove: true),
            "cross_below" => EvaluateCross(condition.Id, marketContext, indicatorContext, emaParams.Period, crossAbove: false),
            "touch" => EvaluateTouch(condition.Id, marketContext, emaValue.Value, emaParams.Period),
            _ => Fail(condition.Id, $"Unknown price_vs_ema operator: '{emaParams.Operator}'."),
        };
    }

    private ConditionResult EvaluateNear(string conditionId, decimal closePrice, decimal emaValue, PriceVsEmaParams emaParams)
    {
        if (emaValue == 0m)
        {
            return Fail(conditionId, $"EMA({emaParams.Period}) is zero - cannot evaluate distance.");
        }

        var distanceType = emaParams.DistanceType.Trim().ToLowerInvariant();
        var distanceValue = emaParams.DistanceValue ?? 0m;
        var absoluteDistance = Math.Abs(closePrice - emaValue);
        bool passed;
        string description;

        switch (distanceType)
        {
            case "percent":
                var percentDistance = absoluteDistance / emaValue * 100m;
                passed = percentDistance <= distanceValue;
                description = $"distance {Format(percentDistance)}% vs threshold {Format(distanceValue)}%";
                break;

            case "absolute":
                passed = absoluteDistance <= distanceValue;
                description = $"distance {Format(absoluteDistance)} vs threshold {Format(distanceValue)}";
                break;

            case "atr_multiple":
                _logger.LogWarning(
                    "ATR-based distance is not available for price_vs_ema near evaluation on condition {ConditionId}.",
                    conditionId);
                return Fail(conditionId, "ATR-based distance not yet supported for price_vs_ema near operator.");

            default:
                return Fail(conditionId, $"Unknown distance type: '{emaParams.DistanceType}'.");
        }

        var status = passed ? "condition met" : "condition not met";
        return new ConditionResult
        {
            ConditionId = conditionId,
            Passed = passed,
            Reason =
                $"Price {Format(closePrice)} near EMA({emaParams.Period}) = {Format(emaValue)} - {description} - {status}",
        };
    }

    private static ConditionResult EvaluateComparison(
        string conditionId,
        decimal closePrice,
        decimal emaValue,
        int period,
        string operatorSymbol,
        Func<decimal, decimal, bool> compare)
    {
        var passed = compare(closePrice, emaValue);
        var status = passed ? "condition met" : "condition not met";

        return new ConditionResult
        {
            ConditionId = conditionId,
            Passed = passed,
            Reason = $"Price {Format(closePrice)} {operatorSymbol} EMA({period}) = {Format(emaValue)} - {status}",
        };
    }

    private static ConditionResult EvaluateCross(
        string conditionId,
        MarketContext marketContext,
        IndicatorContext indicatorContext,
        int period,
        bool crossAbove)
    {
        var previousCandle = marketContext.PreviousCandle;
        if (previousCandle is null)
        {
            return Fail(conditionId, "Previous candle not available for price_vs_ema cross detection.");
        }

        var currentEma = indicatorContext.GetEma(period);
        var previousEma = indicatorContext.GetPreviousEma(period);
        if (!currentEma.HasValue || !previousEma.HasValue)
        {
            return Fail(conditionId, $"EMA({period}) previous value not available for cross detection.");
        }

        var previousClose = previousCandle.Close;
        var currentClose = marketContext.CurrentCandle.Close;
        var passed = crossAbove
            ? previousClose < previousEma.Value && currentClose > currentEma.Value
            : previousClose > previousEma.Value && currentClose < currentEma.Value;
        var direction = crossAbove ? "cross_above" : "cross_below";
        var status = passed ? "condition met" : "condition not met";

        return new ConditionResult
        {
            ConditionId = conditionId,
            Passed = passed,
            Reason =
                $"Price prev={Format(previousClose)} curr={Format(currentClose)} {direction} EMA({period}) prev={Format(previousEma.Value)} curr={Format(currentEma.Value)} - {status}",
        };
    }

    private static ConditionResult EvaluateTouch(string conditionId, MarketContext marketContext, decimal emaValue, int period)
    {
        var candle = marketContext.CurrentCandle;
        var passed = candle.High >= emaValue && candle.Low <= emaValue;
        var status = passed ? "condition met" : "condition not met";

        return new ConditionResult
        {
            ConditionId = conditionId,
            Passed = passed,
            Reason =
                $"Candle [low={Format(candle.Low)}, high={Format(candle.High)}] {(passed ? "touches" : "does not touch")} EMA({period}) = {Format(emaValue)} - {status}",
        };
    }

    private static ConditionResult Fail(string conditionId, string reason)
    {
        return new ConditionResult
        {
            ConditionId = conditionId,
            Passed = false,
            Reason = reason,
        };
    }

    private static string Format(decimal value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}