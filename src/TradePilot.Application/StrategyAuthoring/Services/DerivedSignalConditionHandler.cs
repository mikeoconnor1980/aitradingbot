using System.Globalization;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;
using TradePilot.Application.Trading.Signals;
using TradePilot.Application.Trading.Signals.Abstractions;
using TradePilot.Application.Trading.Signals.Models;

namespace TradePilot.Application.StrategyAuthoring.Services;

public sealed class DerivedSignalConditionHandler : IConditionHandler
{
    private static readonly IReadOnlyDictionary<EntryConditionType, string> SignalNames =
        new Dictionary<EntryConditionType, string>
        {
            [EntryConditionType.CandlePattern] = "candle_pattern",
            [EntryConditionType.LiquiditySweep] = "liquidity_sweep",
            [EntryConditionType.StructureShift] = "structure_shift",
        };

    private static readonly IReadOnlyCollection<EntryConditionType> DerivedConditionTypes = SignalNames.Keys.ToArray();

    private readonly IDerivedSignalRegistry _registry;

    public DerivedSignalConditionHandler(IDerivedSignalRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public EntryConditionType ConditionType => EntryConditionType.CandlePattern;

    public IReadOnlyCollection<EntryConditionType> SupportedConditionTypes => DerivedConditionTypes;

    public ConditionResult Evaluate(EntryConditionConfig condition, IndicatorContext indicatorContext, MarketContext marketContext)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(indicatorContext);
        ArgumentNullException.ThrowIfNull(marketContext);

        if (!SignalNames.TryGetValue(condition.Type, out var signalName))
        {
            return Fail(condition.Id, $"Unsupported derived signal condition type '{condition.Type}'.");
        }

        SignalRequest request;
        try
        {
            request = BuildRequest(condition, signalName, marketContext);
        }
        catch (InvalidOperationException ex)
        {
            return Fail(condition.Id, ex.Message);
        }

        try
        {
            var context = new MarketContextSignalContextAdapter(marketContext);
            var evaluation = _registry.Get(signalName).Evaluate(context, request);

            return new ConditionResult
            {
                ConditionId = condition.Id,
                Passed = evaluation.IsMatch,
                Reason = BuildReason(signalName, evaluation),
                Score = evaluation.Score,
                Metadata = evaluation.Metadata,
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            return Fail(condition.Id, ex.Message);
        }
    }

    private static SignalRequest BuildRequest(EntryConditionConfig condition, string signalName, MarketContext marketContext)
    {
        var timeframe = string.IsNullOrWhiteSpace(marketContext.CurrentCandle.Interval)
            ? "trigger"
            : marketContext.CurrentCandle.Interval;

        return condition.Params switch
        {
            CandlePatternParams candlePattern => new SignalRequest(
                signalName,
                timeframe,
                new Dictionary<string, object?>
                {
                    ["pattern"] = candlePattern.Pattern,
                }),
            LiquiditySweepParams liquiditySweep => new SignalRequest(
                signalName,
                timeframe,
                new Dictionary<string, object?>
                {
                    ["lookback_bars"] = liquiditySweep.LookbackBars,
                    ["pivot_bars"] = liquiditySweep.PivotBars,
                    ["side"] = liquiditySweep.Side,
                }),
            StructureShiftParams structureShift => new SignalRequest(
                signalName,
                timeframe,
                new Dictionary<string, object?>
                {
                    ["pivot_bars"] = structureShift.PivotBars,
                    ["direction"] = structureShift.Direction,
                }),
            null => throw new InvalidOperationException($"Condition '{condition.Id}' is missing params for derived signal '{signalName}'."),
            _ => throw new InvalidOperationException(
                $"Condition '{condition.Id}' has incompatible params type '{condition.Params.GetType().Name}' for derived signal '{signalName}'."),
        };
    }

    private static string BuildReason(string signalName, SignalEvaluationResult evaluation)
    {
        if (evaluation.Metadata.TryGetValue("reason", out var reasonValue) && reasonValue is string reason && !string.IsNullOrWhiteSpace(reason))
        {
            return reason;
        }

        var metadataSummary = string.Join(
            ", ",
            evaluation.Metadata
                .Where(entry => !string.Equals(entry.Key, "reason", StringComparison.OrdinalIgnoreCase))
                .Select(entry => $"{entry.Key}={FormatMetadataValue(entry.Value)}"));

        if (evaluation.IsMatch)
        {
            return string.IsNullOrWhiteSpace(metadataSummary)
                ? $"Derived signal '{signalName}' matched with score {evaluation.Score.ToString("0.##", CultureInfo.InvariantCulture)}."
                : $"Derived signal '{signalName}' matched with score {evaluation.Score.ToString("0.##", CultureInfo.InvariantCulture)} ({metadataSummary}).";
        }

        return string.IsNullOrWhiteSpace(metadataSummary)
            ? $"Derived signal '{signalName}' did not match."
            : $"Derived signal '{signalName}' did not match ({metadataSummary}).";
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

    private static string FormatMetadataValue(object? value)
    {
        return value switch
        {
            null => "null",
            decimal decimalValue => decimalValue.ToString("0.####", CultureInfo.InvariantCulture),
            double doubleValue => doubleValue.ToString("0.####", CultureInfo.InvariantCulture),
            float floatValue => floatValue.ToString("0.####", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
        };
    }
}