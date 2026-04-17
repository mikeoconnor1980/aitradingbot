using System.Globalization;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;

namespace TradePilot.Application.StrategyAuthoring.Services;

public sealed class MacdConditionHandler : IConditionHandler
{
    public EntryConditionType ConditionType => EntryConditionType.Macd;

    public ConditionResult Evaluate(
        EntryConditionConfig condition,
        IndicatorContext indicatorContext,
        MarketContext marketContext)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(indicatorContext);
        ArgumentNullException.ThrowIfNull(marketContext);

        if (condition.Params is not MacdParams macd)
        {
            return Fail(
                condition.Id,
                $"Expected {nameof(MacdParams)} but received {condition.Params?.GetType().Name ?? "null"}.");
        }

        var line = indicatorContext.GetMacd(macd.FastPeriod, macd.SlowPeriod, macd.SignalPeriod);
        var signal = indicatorContext.GetMacdSignal(macd.FastPeriod, macd.SlowPeriod, macd.SignalPeriod);
        var histogram = indicatorContext.GetMacdHistogram(macd.FastPeriod, macd.SlowPeriod, macd.SignalPeriod);

        if (!line.HasValue || !signal.HasValue || !histogram.HasValue)
        {
            return Fail(
                condition.Id,
                $"MACD({macd.FastPeriod},{macd.SlowPeriod},{macd.SignalPeriod}) data not available.");
        }

        var normalizedOperator = macd.Operator.Trim().ToLowerInvariant();

        return normalizedOperator switch
        {
            "cross_above_signal" => EvaluateSignalCross(condition.Id, indicatorContext, macd, crossAbove: true),
            "cross_below_signal" => EvaluateSignalCross(condition.Id, indicatorContext, macd, crossAbove: false),
            "above_zero" => EvaluateZeroLine(condition.Id, line.Value, macd, aboveZero: true),
            "below_zero" => EvaluateZeroLine(condition.Id, line.Value, macd, aboveZero: false),
            "histogram_rising" => EvaluateHistogramDirection(condition.Id, indicatorContext, macd, rising: true),
            "histogram_falling" => EvaluateHistogramDirection(condition.Id, indicatorContext, macd, rising: false),
            _ => Fail(condition.Id, $"Unknown MACD operator: '{macd.Operator}'."),
        };
    }

    private static ConditionResult EvaluateSignalCross(
        string conditionId,
        IndicatorContext indicatorContext,
        MacdParams macd,
        bool crossAbove)
    {
        var currentLine = indicatorContext.GetMacd(macd.FastPeriod, macd.SlowPeriod, macd.SignalPeriod);
        var previousLine = indicatorContext.GetPreviousMacd(macd.FastPeriod, macd.SlowPeriod, macd.SignalPeriod);
        var currentSignal = indicatorContext.GetMacdSignal(macd.FastPeriod, macd.SlowPeriod, macd.SignalPeriod);
        var previousSignal = indicatorContext.GetPreviousMacdSignal(macd.FastPeriod, macd.SlowPeriod, macd.SignalPeriod);

        if (!currentLine.HasValue || !previousLine.HasValue || !currentSignal.HasValue || !previousSignal.HasValue)
        {
            return Fail(
                conditionId,
                $"MACD({macd.FastPeriod},{macd.SlowPeriod},{macd.SignalPeriod}) previous values not available for cross detection.");
        }

        var passed = crossAbove
            ? previousLine.Value < previousSignal.Value && currentLine.Value >= currentSignal.Value
            : previousLine.Value > previousSignal.Value && currentLine.Value <= currentSignal.Value;

        var direction = crossAbove ? "cross_above_signal" : "cross_below_signal";
        var status = passed ? "condition met" : "condition not met";

        return new ConditionResult
        {
            ConditionId = conditionId,
            Passed = passed,
            Reason =
                $"MACD({macd.FastPeriod},{macd.SlowPeriod},{macd.SignalPeriod}) prev_line={Format(previousLine.Value)} curr_line={Format(currentLine.Value)} prev_signal={Format(previousSignal.Value)} curr_signal={Format(currentSignal.Value)} {direction} - {status}",
        };
    }

    private static ConditionResult EvaluateZeroLine(
        string conditionId,
        decimal line,
        MacdParams macd,
        bool aboveZero)
    {
        var passed = aboveZero ? line > 0m : line < 0m;
        var direction = aboveZero ? "above_zero" : "below_zero";
        var status = passed ? "condition met" : "condition not met";

        return new ConditionResult
        {
            ConditionId = conditionId,
            Passed = passed,
            Reason = $"MACD({macd.FastPeriod},{macd.SlowPeriod},{macd.SignalPeriod}) line={Format(line)} {direction} - {status}",
        };
    }

    private static ConditionResult EvaluateHistogramDirection(
        string conditionId,
        IndicatorContext indicatorContext,
        MacdParams macd,
        bool rising)
    {
        var currentHistogram = indicatorContext.GetMacdHistogram(macd.FastPeriod, macd.SlowPeriod, macd.SignalPeriod);
        var previousHistogram = indicatorContext.GetPreviousMacdHistogram(macd.FastPeriod, macd.SlowPeriod, macd.SignalPeriod);

        if (!currentHistogram.HasValue || !previousHistogram.HasValue)
        {
            return Fail(
                conditionId,
                $"MACD({macd.FastPeriod},{macd.SlowPeriod},{macd.SignalPeriod}) previous histogram not available for direction detection.");
        }

        var passed = rising
            ? currentHistogram.Value > previousHistogram.Value
            : currentHistogram.Value < previousHistogram.Value;

        var direction = rising ? "histogram_rising" : "histogram_falling";
        var status = passed ? "condition met" : "condition not met";

        return new ConditionResult
        {
            ConditionId = conditionId,
            Passed = passed,
            Reason =
                $"MACD({macd.FastPeriod},{macd.SlowPeriod},{macd.SignalPeriod}) curr_hist={Format(currentHistogram.Value)} prev_hist={Format(previousHistogram.Value)} {direction} - {status}",
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