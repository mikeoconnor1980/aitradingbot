<!-- markdownlint-disable-file -->

# Task Details: F7 — Trend Filter + EMA Condition Handler + UI

## Phase 2: TrendFilterEvaluator & PriceVsEmaConditionHandler

## Standards and Knowledge References

- **csharp.instructions.md**: sealed classes, interface-driven DI, PascalCase naming
- **testing.instructions.md**: MSTest, Moq, FluentAssertions v6, Given_When_Then naming
- **dotnet-architecture.instructions.md**: services in Application/{BoundedContext}/Services/
- **01-trading-strategy.md**: trend filter before entry, price > EMA, EMA cross logic
- **14-strategy-runtime-model.md**: pipeline interfaces, MarketContext.IndicatorContext
- **16-signal-contracts.md**: signal type boundaries

## Design References

- F7 PBI spec: trend filter runs before entry conditions; if filter fails, conditions skipped entirely
- Cross detection: compare previous confirmed candle vs current confirmed candle

### Task 2.1: Create ITrendFilterEvaluator interface and TrendFilterEvaluator {#task-21-create-itrendfilterevaluator-interface-and-trendfilterevaluator}

Create a new service that evaluates trend filter configuration against indicator context. Returns a `TrendFilterResult` with `Passed` and `Reason`.

- **Complexity**: High
- **Risk Factors**: Cross detection logic; insufficient data handling; unknown type handling
- **Files**:
  - `src/TradePilot.Application/StrategyAuthoring/Services/ITrendFilterEvaluator.cs` — new interface
  - `src/TradePilot.Application/StrategyAuthoring/Services/TrendFilterEvaluator.cs` — new implementation
  - `src/TradePilot.Application/StrategyAuthoring/Models/TrendFilterResult.cs` — new result model
- **Success**:
  - Handles `ema_cross`, `sma_cross`, `price_above_ema` types
  - Handles `gt`, `lt`, `gte`, `lte`, `cross_above`, `cross_below`, `above`, `below` operators
  - Returns `Passed = true` when `Enabled = false` or `AppliesTo` doesn't match direction
  - Returns `Passed = false` when indicator data insufficient
  - Returns `Passed = false` with warning for unknown types
  - Solution builds
- **Dependencies**:
  - Phase 1 complete (enums, IndicatorContext SMA support)
  - **F6.75 complete** — the signal execution path must be established before wiring the trend filter evaluator into it

#### Implementation Details

```csharp
// src/TradePilot.Application/StrategyAuthoring/Models/TrendFilterResult.cs — new file
namespace TradePilot.Application.StrategyAuthoring.Models;

public sealed class TrendFilterResult
{
    public required bool Passed { get; init; }
    public required string Reason { get; init; }

    public static TrendFilterResult Pass(string reason) => new() { Passed = true, Reason = reason };
    public static TrendFilterResult Fail(string reason) => new() { Passed = false, Reason = reason };
}
```

```csharp
// src/TradePilot.Application/StrategyAuthoring/Services/ITrendFilterEvaluator.cs — new file
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;

namespace TradePilot.Application.StrategyAuthoring.Services;

public interface ITrendFilterEvaluator
{
    TrendFilterResult Evaluate(TrendFilterConfig? filter, Direction strategyDirection, IndicatorContext indicatorContext, MarketContext marketContext);
}
```

```csharp
// src/TradePilot.Application/StrategyAuthoring/Services/TrendFilterEvaluator.cs — new file
using System.Globalization;
using Microsoft.Extensions.Logging;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;

namespace TradePilot.Application.StrategyAuthoring.Services;

public sealed class TrendFilterEvaluator : ITrendFilterEvaluator
{
    private readonly ILogger<TrendFilterEvaluator> _logger;

    public TrendFilterEvaluator(ILogger<TrendFilterEvaluator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public TrendFilterResult Evaluate(
        TrendFilterConfig? filter,
        Direction strategyDirection,
        IndicatorContext indicatorContext,
        MarketContext marketContext)
    {
        if (filter is null || !filter.Enabled)
        {
            return TrendFilterResult.Pass("Trend filter disabled — skipped.");
        }

        if (!AppliesToDirection(filter.AppliesTo, strategyDirection))
        {
            return TrendFilterResult.Pass(
                $"Trend filter appliesTo={filter.AppliesTo} does not match direction={strategyDirection} — skipped.");
        }

        return filter.Type switch
        {
            TrendFilterType.EmaCross => EvaluateMaCross(filter, indicatorContext, isEma: true),
            TrendFilterType.SmaCross => EvaluateMaCross(filter, indicatorContext, isEma: false),
            TrendFilterType.PriceAboveEma => EvaluatePriceAboveEma(filter, indicatorContext, marketContext),
            _ => HandleUnknownType(filter)
        };
    }

    private static bool AppliesToDirection(Direction appliesTo, Direction strategyDirection)
    {
        if (appliesTo == Direction.Both)
            return true;

        return appliesTo == strategyDirection;
    }

    private TrendFilterResult EvaluateMaCross(TrendFilterConfig filter, IndicatorContext context, bool isEma)
    {
        var typeName = isEma ? "EMA" : "SMA";

        decimal? fastCurrent, fastPrevious, slowCurrent, slowPrevious;

        if (isEma)
        {
            fastCurrent = context.GetEma(filter.FastPeriod);
            fastPrevious = context.GetPreviousEma(filter.FastPeriod);
            slowCurrent = context.GetEma(filter.SlowPeriod);
            slowPrevious = context.GetPreviousEma(filter.SlowPeriod);
        }
        else
        {
            fastCurrent = context.GetSma(filter.FastPeriod);
            fastPrevious = context.GetPreviousSma(filter.FastPeriod);
            slowCurrent = context.GetSma(filter.SlowPeriod);
            slowPrevious = context.GetPreviousSma(filter.SlowPeriod);
        }

        if (!fastCurrent.HasValue || !slowCurrent.HasValue)
        {
            return TrendFilterResult.Fail(
                $"{typeName}({filter.FastPeriod}) or {typeName}({filter.SlowPeriod}) not available — insufficient data.");
        }

        return filter.Operator switch
        {
            TrendOperator.Gt => EvaluateComparison(fastCurrent.Value, slowCurrent.Value, typeName, filter, ">",
                (f, s) => f > s),
            TrendOperator.Lt => EvaluateComparison(fastCurrent.Value, slowCurrent.Value, typeName, filter, "<",
                (f, s) => f < s),
            TrendOperator.Gte => EvaluateComparison(fastCurrent.Value, slowCurrent.Value, typeName, filter, ">=",
                (f, s) => f >= s),
            TrendOperator.Lte => EvaluateComparison(fastCurrent.Value, slowCurrent.Value, typeName, filter, "<=",
                (f, s) => f <= s),
            TrendOperator.CrossAbove => EvaluateMaCrossover(fastCurrent.Value, fastPrevious, slowCurrent.Value,
                slowPrevious, typeName, filter, crossAbove: true),
            TrendOperator.CrossBelow => EvaluateMaCrossover(fastCurrent.Value, fastPrevious, slowCurrent.Value,
                slowPrevious, typeName, filter, crossAbove: false),
            _ => TrendFilterResult.Fail($"Unknown operator '{filter.Operator}' for {typeName} cross filter.")
        };
    }

    private static TrendFilterResult EvaluateComparison(
        decimal fast, decimal slow, string typeName, TrendFilterConfig filter,
        string opSymbol, Func<decimal, decimal, bool> compare)
    {
        var passed = compare(fast, slow);
        var status = passed ? "filter passed" : "filter failed";

        return new TrendFilterResult
        {
            Passed = passed,
            Reason = $"{typeName}({filter.FastPeriod}) = {Format(fast)} {opSymbol} {typeName}({filter.SlowPeriod}) = {Format(slow)} — {status}"
        };
    }

    private static TrendFilterResult EvaluateMaCrossover(
        decimal fastCurrent, decimal? fastPrevious, decimal slowCurrent, decimal? slowPrevious,
        string typeName, TrendFilterConfig filter, bool crossAbove)
    {
        if (!fastPrevious.HasValue || !slowPrevious.HasValue)
        {
            return TrendFilterResult.Fail(
                $"{typeName}({filter.FastPeriod}) or {typeName}({filter.SlowPeriod}) previous values not available for cross detection.");
        }

        bool passed;
        string direction;

        if (crossAbove)
        {
            passed = fastPrevious.Value <= slowPrevious.Value && fastCurrent > slowCurrent;
            direction = "cross_above";
        }
        else
        {
            passed = fastPrevious.Value >= slowPrevious.Value && fastCurrent < slowCurrent;
            direction = "cross_below";
        }

        var status = passed ? "filter passed" : "filter failed";
        return new TrendFilterResult
        {
            Passed = passed,
            Reason = $"{typeName}({filter.FastPeriod}) {direction} {typeName}({filter.SlowPeriod}) — prev fast={Format(fastPrevious.Value)} slow={Format(slowPrevious.Value)}, curr fast={Format(fastCurrent)} slow={Format(slowCurrent)} — {status}"
        };
    }

    private TrendFilterResult EvaluatePriceAboveEma(TrendFilterConfig filter, IndicatorContext context, MarketContext marketContext)
    {
        var period = filter.Period ?? 0;
        if (period <= 0)
        {
            return TrendFilterResult.Fail("PriceAboveEma filter has invalid period.");
        }

        var currentEma = context.GetEma(period);
        if (!currentEma.HasValue)
        {
            return TrendFilterResult.Fail($"EMA({period}) not available — insufficient data.");
        }

        var closePrice = marketContext.CurrentCandle.Close;

        return filter.Operator switch
        {
            TrendOperator.Above => EvaluatePriceComparison(closePrice, currentEma.Value, period, "above",
                (price, ema) => price > ema),
            TrendOperator.Below => EvaluatePriceComparison(closePrice, currentEma.Value, period, "below",
                (price, ema) => price < ema),
            TrendOperator.Gt => EvaluatePriceComparison(closePrice, currentEma.Value, period, ">",
                (price, ema) => price > ema),
            TrendOperator.Lt => EvaluatePriceComparison(closePrice, currentEma.Value, period, "<",
                (price, ema) => price < ema),
            TrendOperator.CrossAbove => EvaluatePriceCross(closePrice, context, period, marketContext, crossAbove: true),
            TrendOperator.CrossBelow => EvaluatePriceCross(closePrice, context, period, marketContext, crossAbove: false),
            _ => TrendFilterResult.Fail($"Unknown operator '{filter.Operator}' for PriceAboveEma filter.")
        };
    }

    private static TrendFilterResult EvaluatePriceComparison(
        decimal closePrice, decimal emaValue, int period,
        string opLabel, Func<decimal, decimal, bool> compare)
    {
        var passed = compare(closePrice, emaValue);
        var status = passed ? "filter passed" : "filter failed";

        return new TrendFilterResult
        {
            Passed = passed,
            Reason = $"Price {Format(closePrice)} {opLabel} EMA({period}) = {Format(emaValue)} — {status}"
        };
    }

    private static TrendFilterResult EvaluatePriceCross(
        decimal closePrice, IndicatorContext context, int period,
        MarketContext marketContext, bool crossAbove)
    {
        var currentEma = context.GetEma(period);
        var previousEma = context.GetPreviousEma(period);

        if (!currentEma.HasValue || !previousEma.HasValue)
        {
            return TrendFilterResult.Fail($"EMA({period}) previous value not available for cross detection.");
        }

        // Use previous candle's close for cross detection
        // We compare price relative to EMA: prev close vs prev EMA, current close vs current EMA
        // Approximate: use previous EMA as both the "level" that was crossed
        bool passed;
        string direction;

        // NOTE: Full cross detection requires previous candle close, not yet in MarketContext.
        // Approximation: compare current close vs current EMA, use previousEma as directional proxy.
        // The implementer should add MarketContext.PreviousCandle for proper cross detection.
        if (crossAbove)
        {
            passed = closePrice > currentEma.Value && previousEma.Value >= currentEma.Value;
            direction = "cross_above";
        }
        else
        {
            passed = closePrice < currentEma.Value && previousEma.Value <= currentEma.Value;
            direction = "cross_below";
        }
        var status = passed ? "filter passed" : "filter failed";
        return new TrendFilterResult
        {
            Passed = passed,
            Reason = $"Price {Format(closePrice)} {direction} EMA({period}) = {Format(currentEma.Value)} — {status}"
        };
    }

    private TrendFilterResult HandleUnknownType(TrendFilterConfig filter)
    {
        _logger.LogWarning("Unknown trend filter type: {TrendFilterType}. Filter fails closed.", filter.Type);
        return TrendFilterResult.Fail($"Unknown trend filter type '{filter.Type}' — filter fails closed.");
    }

    private static string Format(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
```

**IMPORTANT NOTE on cross detection for PriceAboveEma**: The `cross_above`/`cross_below` logic for `price_above_ema` requires knowing the **previous candle's close price**, which is not currently stored in `MarketContext`. The implementer should add a `PreviousCandle` property to `MarketContext` (or reuse an existing mechanism) to enable proper cross detection. The implementation above uses an approximation. A cleaner approach:

```csharp
// If MarketContext.PreviousCandle is available:
if (crossAbove)
{
    var previousClose = marketContext.PreviousCandle?.Close;
    if (previousClose is null)
        return TrendFilterResult.Fail("Previous candle not available for cross detection.");

    passed = previousClose.Value < previousEma.Value && closePrice > currentEma.Value;
}
```

If adding `PreviousCandle` is complex, an alternative is to track the previous candle close in `IndicatorContext` (e.g., `SetPreviousClose(decimal value)`). The implementer should choose the simplest approach that fits the existing pipeline — check how `BacktestMarketContextBuilder.Build` is called to see if the previous candle is accessible.

##### Pattern References

- `RsiConditionHandler.cs` — handler structure, operator switch, cross detection pattern, `Fail` helper, `FormatValue` helper

---

### Task 2.2: Create PriceVsEmaConditionHandler {#task-22-create-pricevsemaconditionhandler}

Create a condition handler for `price_vs_ema` entry conditions. Handles operators: `near`, `above`, `below`, `cross_above`, `cross_below`, `touch`.

- **Complexity**: High
- **Risk Factors**: Distance calculation for different distance types; touch detection using candle high/low
- **Files**:
  - `src/TradePilot.Application/StrategyAuthoring/Services/PriceVsEmaConditionHandler.cs` — new file
- **Success**:
  - Implements `IConditionHandler` with `ConditionType = EntryConditionType.PriceVsEma`
  - Handles all 6 operators correctly
  - `near` with `percent` distance: `|close - ema| / ema <= distanceValue / 100`
  - `near` with `atr_multiple`: requires ATR value (not in scope for full implementation — log warning if ATR not available)
  - `near` with `absolute`: `|close - ema| <= distanceValue`
  - `touch`: candle high >= EMA and candle low <= EMA
  - `above`/`below`: close > EMA / close < EMA
  - `cross_above`/`cross_below`: previous close on opposite side of EMA vs current close
  - Returns `ConditionResult` with descriptive reason
  - Solution builds
- **Dependencies**:
  - Phase 1 (PriceVsEmaParams expansion)

#### Implementation Details

```csharp
// src/TradePilot.Application/StrategyAuthoring/Services/PriceVsEmaConditionHandler.cs — new file
using System.Globalization;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;

namespace TradePilot.Application.StrategyAuthoring.Services;

/// <summary>
/// Evaluates price vs EMA conditions: near, above, below, cross_above, cross_below, touch.
/// </summary>
public sealed class PriceVsEmaConditionHandler : IConditionHandler
{
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
            return Fail(condition.Id,
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
            "above" => EvaluateSimple(condition.Id, closePrice, emaValue.Value, emaParams.Period,
                "above", (price, ema) => price > ema),
            "below" => EvaluateSimple(condition.Id, closePrice, emaValue.Value, emaParams.Period,
                "below", (price, ema) => price < ema),
            "cross_above" => EvaluateCross(condition.Id, closePrice, indicatorContext, emaParams, crossAbove: true),
            "cross_below" => EvaluateCross(condition.Id, closePrice, indicatorContext, emaParams, crossAbove: false),
            "touch" => EvaluateTouch(condition.Id, marketContext, emaValue.Value, emaParams.Period),
            _ => Fail(condition.Id, $"Unknown price_vs_ema operator: '{emaParams.Operator}'.")
        };
    }

    private static ConditionResult EvaluateNear(
        string conditionId, decimal closePrice, decimal emaValue, PriceVsEmaParams emaParams)
    {
        if (emaValue == 0m)
        {
            return Fail(conditionId, $"EMA({emaParams.Period}) is zero — cannot evaluate distance.");
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
                // ATR-based distance requires ATR value — not yet available in IndicatorContext
                // Fall through with a descriptive reason
                return Fail(conditionId,
                    $"ATR-based distance not yet supported for price_vs_ema near operator.");

            default:
                return Fail(conditionId, $"Unknown distance type: '{emaParams.DistanceType}'.");
        }

        var status = passed ? "condition met" : "condition not met";
        return new ConditionResult
        {
            ConditionId = conditionId,
            Passed = passed,
            Reason = $"Price {Format(closePrice)} near EMA({emaParams.Period}) = {Format(emaValue)} — {description} — {status}"
        };
    }

    private static ConditionResult EvaluateSimple(
        string conditionId, decimal closePrice, decimal emaValue, int period,
        string opLabel, Func<decimal, decimal, bool> compare)
    {
        var passed = compare(closePrice, emaValue);
        var status = passed ? "condition met" : "condition not met";

        return new ConditionResult
        {
            ConditionId = conditionId,
            Passed = passed,
            Reason = $"Price {Format(closePrice)} {opLabel} EMA({period}) = {Format(emaValue)} — {status}"
        };
    }

    private static ConditionResult EvaluateCross(
        string conditionId, decimal closePrice, IndicatorContext indicatorContext,
        PriceVsEmaParams emaParams, bool crossAbove)
    {
        var currentEma = indicatorContext.GetEma(emaParams.Period);
        var previousEma = indicatorContext.GetPreviousEma(emaParams.Period);

        if (!currentEma.HasValue || !previousEma.HasValue)
        {
            return Fail(conditionId,
                $"EMA({emaParams.Period}) previous value not available for cross detection.");
        }

        // Cross detection: previous close vs previous EMA, current close vs current EMA
        // Since we don't have previous candle close, we approximate:
        // For cross_above: if current close > current EMA and previous EMA > current EMA direction
        // indicates upward price movement across EMA
        // Better: use previousEma as reference for where price was relative to EMA
        // The implementer should refine this if PreviousCandle is available in MarketContext

        bool passed;
        string direction;

        if (crossAbove)
        {
            // Current close above EMA and we assume previous was below
            // Approximation: closePrice > currentEma and previousEma was closer (price was below)
            passed = closePrice > currentEma.Value;
            direction = "cross_above";
        }
        else
        {
            passed = closePrice < currentEma.Value;
            direction = "cross_below";
        }

        var status = passed ? "condition met" : "condition not met";
        return new ConditionResult
        {
            ConditionId = conditionId,
            Passed = passed,
            Reason = $"Price {Format(closePrice)} {direction} EMA({emaParams.Period}) = {Format(currentEma.Value)} — {status}"
        };
    }

    private static ConditionResult EvaluateTouch(
        string conditionId, MarketContext marketContext, decimal emaValue, int period)
    {
        var candle = marketContext.CurrentCandle;
        var passed = candle.High >= emaValue && candle.Low <= emaValue;
        var status = passed ? "condition met" : "condition not met";

        return new ConditionResult
        {
            ConditionId = conditionId,
            Passed = passed,
            Reason = $"Candle [low={Format(candle.Low)}, high={Format(candle.High)}] {(passed ? "touches" : "does not touch")} EMA({period}) = {Format(emaValue)} — {status}"
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

    private static string Format(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);
}
```

**NOTE**: The `cross_above`/`cross_below` operators need proper previous candle close access. The implementer should check if `MarketContext` or the `BacktestMarketContextBuilder` can provide the previous candle close, and refine accordingly. See Task 2.1 note.

##### Pattern References

- `RsiConditionHandler.cs` — canonical IConditionHandler implementation with operator switch, Fail helper, FormatValue

---

### Task 2.3: Wire TrendFilterEvaluator into CompositeStrategyEngine {#task-23-wire-trendfilterevaluator-into-compositestrategyengine}

Inject `ITrendFilterEvaluator` into `CompositeStrategyEngine` and call it before `ConditionEvaluator.Evaluate` in signal mode. If the trend filter fails, skip condition evaluation and return `SetupDetected = false`.

- **Complexity**: Medium
- **Risk Factors**: Must preserve existing grid mode behavior; must propagate `TrendFilterPassed` into result; **F6.75 may restructure signal mode flow** — injection point may shift from `CompositeStrategyEngine` to a new signal orchestrator
- **Files**:
  - `src/TradePilot.Application/Trading/Services/CompositeStrategyEngine.cs` — inject and wire
- **Success**:
  - Grid mode unaffected
  - Signal mode: trend filter evaluated before conditions
  - If trend filter fails: `SetupDetected = false`, conditions not evaluated
  - If trend filter passes or is disabled: conditions evaluated normally
  - `ConditionEvaluationResult.TrendFilterPassed` populated correctly

**NOTE on TrendFilterPassed**: After evaluating the trend filter and before returning the `StrategyEvaluation`, the implementer must ensure `TrendFilterPassed` is propagated. When the trend filter passes and conditions are evaluated, set `result.TrendFilterPassed = trendResult.Passed` on the `ConditionEvaluationResult` (or pass it through to `StrategyEvaluation`). When the trend filter fails (early return), `TrendFilterPassed` is implicitly `false` via the `SetupDetected = false` return.

#### Implementation Details

```csharp
// src/TradePilot.Application/Trading/Services/CompositeStrategyEngine.cs — modification
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Services;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Trading;

namespace TradePilot.Application.Trading.Services;

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
        // Evaluate trend filter first — if it fails, skip conditions entirely
        if (context.IndicatorContext is not null)
        {
            var trendResult = _trendFilterEvaluator.Evaluate(
                config.TrendFilter,
                config.Direction,
                context.IndicatorContext,
                context);

            if (!trendResult.Passed)
            {
                return new StrategyEvaluation
                {
                    SetupDetected = false,
                    Reason = $"Trend filter failed: {trendResult.Reason}"
                };
            }
        }

        var result = _conditionEvaluator.Evaluate(config, context);

        return new StrategyEvaluation
        {
            SetupDetected = result.SetupDetected,
            Reason = result.OverallReason
        };
    }
}
```

##### Pattern References

- Existing `CompositeStrategyEngine.cs` — `EvaluateSignalMode` method, constructor injection pattern

---

### Task 2.4: Update CrossFieldValidator {#task-24-update-crossfieldvalidator}

Remove or update the `TREND_FILTER_NOT_EVALUATED` info message now that trend filter evaluation is wired in.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradePilot.Application/StrategyAuthoring/Validation/CrossFieldValidator.cs` — remove placeholder
- **Success**:
  - `TREND_FILTER_NOT_EVALUATED` info message no longer emitted
  - Other cross-field validations unchanged

#### Implementation Details

```csharp
// src/TradePilot.Application/StrategyAuthoring/Validation/CrossFieldValidator.cs — modification
// Remove the entire EmitV1InfoMessages method and its call from Validate:

public sealed class CrossFieldValidator
{
    public void Validate(StrategyConfig config, ValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(result);

        ValidateStrategyModeConsistency(config, result);
        // EmitV1InfoMessages call removed
    }

    // ... ValidateStrategyModeConsistency unchanged ...
    // EmitV1InfoMessages method removed entirely
}
```

##### Pattern References

- Existing `CrossFieldValidator.cs`

---

### Task 2.5: Register new services in DI {#task-25-register-new-services-in-di}

Register `PriceVsEmaConditionHandler` and `TrendFilterEvaluator` in `Program.cs`.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradePilot.Api/Program.cs` — add registrations
- **Success**:
  - `PriceVsEmaConditionHandler` registered as `IConditionHandler`
  - `TrendFilterEvaluator` registered as `ITrendFilterEvaluator`
  - Existing registrations unchanged

#### Implementation Details

```csharp
// src/TradePilot.Api/Program.cs — add after existing RsiConditionHandler registration
builder.Services.AddScoped<IConditionHandler, RsiConditionHandler>();
builder.Services.AddScoped<IConditionHandler, PriceVsEmaConditionHandler>();  // NEW
builder.Services.AddScoped<ITrendFilterEvaluator, TrendFilterEvaluator>();    // NEW
builder.Services.AddScoped<IConditionEvaluator, ConditionEvaluator>();
```

##### Pattern References

- `Program.cs` — existing `AddScoped<IConditionHandler, RsiConditionHandler>()` pattern

---

### Task 2.6: TrendFilterEvaluator tests {#task-26-trendfilterevaluator-tests}

Create comprehensive unit tests for `TrendFilterEvaluator` covering all filter types, operators, edge cases, and failure modes.

- **Complexity**: High
- **Risk Factors**: Must cover all acceptance criteria from F7 PBI
- **Files**:
  - `tests/TradePilot.Application.Tests/StrategyAuthoring/Services/TrendFilterEvaluatorTests.cs` — new file
- **Success**:
  - Tests for ema_cross: gt (pass/fail), lt, cross_above, cross_below
  - Tests for sma_cross: gt (pass)
  - Tests for price_above_ema: above (pass/fail), cross_above
  - Tests for edge cases: disabled, appliesTo mismatch, insufficient data, unknown type
  - All tests pass
- **Dependencies**:
  - Tasks 2.1, Phase 1

#### Implementation Details

```csharp
// tests/TradePilot.Application.Tests/StrategyAuthoring/Services/TrendFilterEvaluatorTests.cs — new file
using Microsoft.Extensions.Logging;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Services;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Tests.StrategyAuthoring.Services;

[TestClass]
public sealed class TrendFilterEvaluatorTests
{
    private const long CandleTimestamp = 1_000_000;

    private TrendFilterEvaluator _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        var logger = new Mock<ILogger<TrendFilterEvaluator>>();
        _sut = new TrendFilterEvaluator(logger.Object);
    }

    // --- ema_cross ---

    [TestMethod]
    public void GivenEmaCrossGt_WhenFastAboveSlow_ThenPassed()
    {
        var filter = CreateEmaCrossFilter(TrendOperator.Gt, 50, 200);
        var context = CreateEmaContext(ema50: 42500m, ema200: 42000m);

        var result = _sut.Evaluate(filter, Direction.Long, context, CreateMarketContext());

        result.Passed.Should().BeTrue();
    }

    [TestMethod]
    public void GivenEmaCrossGt_WhenFastBelowSlow_ThenFailed()
    {
        var filter = CreateEmaCrossFilter(TrendOperator.Gt, 50, 200);
        var context = CreateEmaContext(ema50: 41500m, ema200: 42000m);

        var result = _sut.Evaluate(filter, Direction.Long, context, CreateMarketContext());

        result.Passed.Should().BeFalse();
    }

    [TestMethod]
    public void GivenEmaCrossCrossAbove_WhenPrevBelowCurrAbove_ThenPassed()
    {
        var filter = CreateEmaCrossFilter(TrendOperator.CrossAbove, 50, 200);
        var context = CreateEmaContextWithPrevious(
            ema50Current: 42500m, ema50Previous: 41800m,
            ema200Current: 42000m, ema200Previous: 42100m);

        var result = _sut.Evaluate(filter, Direction.Long, context, CreateMarketContext());

        result.Passed.Should().BeTrue();
    }

    // --- sma_cross ---

    [TestMethod]
    public void GivenSmaCrossGt_WhenFastAboveSlow_ThenPassed()
    {
        var filter = CreateSmaCrossFilter(TrendOperator.Gt, 20, 50);
        var context = CreateSmaContext(sma20: 42500m, sma50: 42000m);

        var result = _sut.Evaluate(filter, Direction.Long, context, CreateMarketContext());

        result.Passed.Should().BeTrue();
    }

    // --- price_above_ema ---

    [TestMethod]
    public void GivenPriceAboveEmaAbove_WhenPriceAbove_ThenPassed()
    {
        var filter = CreatePriceAboveEmaFilter(TrendOperator.Above, 200);
        var context = CreateEmaContext(ema200: 42000m);
        var market = CreateMarketContext(closePrice: 42500m);

        var result = _sut.Evaluate(filter, Direction.Long, context, market);

        result.Passed.Should().BeTrue();
    }

    [TestMethod]
    public void GivenPriceAboveEmaAbove_WhenPriceBelow_ThenFailed()
    {
        var filter = CreatePriceAboveEmaFilter(TrendOperator.Above, 200);
        var context = CreateEmaContext(ema200: 42000m);
        var market = CreateMarketContext(closePrice: 41500m);

        var result = _sut.Evaluate(filter, Direction.Long, context, market);

        result.Passed.Should().BeFalse();
    }

    // --- Edge cases ---

    [TestMethod]
    public void GivenDisabledFilter_WhenEvaluated_ThenPassed()
    {
        var filter = new TrendFilterConfig { Enabled = false, Type = TrendFilterType.EmaCross };
        var context = new IndicatorContext();

        var result = _sut.Evaluate(filter, Direction.Long, context, CreateMarketContext());

        result.Passed.Should().BeTrue();
        result.Reason.Should().Contain("disabled");
    }

    [TestMethod]
    public void GivenNullFilter_WhenEvaluated_ThenPassed()
    {
        var context = new IndicatorContext();

        var result = _sut.Evaluate(null, Direction.Long, context, CreateMarketContext());

        result.Passed.Should().BeTrue();
    }

    [TestMethod]
    public void GivenAppliesToShort_WhenDirectionLong_ThenPassed()
    {
        var filter = CreateEmaCrossFilter(TrendOperator.Gt, 50, 200) with { AppliesTo = Direction.Short };
        var context = new IndicatorContext();

        var result = _sut.Evaluate(filter, Direction.Long, context, CreateMarketContext());

        result.Passed.Should().BeTrue();
        result.Reason.Should().Contain("skipped");
    }

    [TestMethod]
    public void GivenInsufficientData_WhenEmaCrossEvaluated_ThenFailed()
    {
        var filter = CreateEmaCrossFilter(TrendOperator.Gt, 50, 200);
        var context = new IndicatorContext(); // no EMA values set

        var result = _sut.Evaluate(filter, Direction.Long, context, CreateMarketContext());

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("not available");
    }

    // --- Private factory helpers ---

    private static TrendFilterConfig CreateEmaCrossFilter(TrendOperator op, int fast, int slow)
    {
        return new TrendFilterConfig
        {
            Enabled = true,
            Type = TrendFilterType.EmaCross,
            FastPeriod = fast,
            SlowPeriod = slow,
            Operator = op,
            AppliesTo = Direction.Long,
        };
    }

    private static TrendFilterConfig CreateSmaCrossFilter(TrendOperator op, int fast, int slow)
    {
        return new TrendFilterConfig
        {
            Enabled = true,
            Type = TrendFilterType.SmaCross,
            FastPeriod = fast,
            SlowPeriod = slow,
            Operator = op,
            AppliesTo = Direction.Long,
        };
    }

    private static TrendFilterConfig CreatePriceAboveEmaFilter(TrendOperator op, int period)
    {
        return new TrendFilterConfig
        {
            Enabled = true,
            Type = TrendFilterType.PriceAboveEma,
            Period = period,
            Operator = op,
            AppliesTo = Direction.Long,
        };
    }

    private static IndicatorContext CreateEmaContext(
        decimal? ema50 = null, decimal? ema200 = null)
    {
        var context = new IndicatorContext();
        if (ema50.HasValue) context.SetEma(50, ema50.Value);
        if (ema200.HasValue) context.SetEma(200, ema200.Value);
        return context;
    }

    private static IndicatorContext CreateEmaContextWithPrevious(
        decimal ema50Current, decimal ema50Previous,
        decimal ema200Current, decimal ema200Previous)
    {
        var context = new IndicatorContext();
        context.SetEma(50, ema50Current, ema50Previous);
        context.SetEma(200, ema200Current, ema200Previous);
        return context;
    }

    private static IndicatorContext CreateSmaContext(
        decimal? sma20 = null, decimal? sma50 = null)
    {
        var context = new IndicatorContext();
        if (sma20.HasValue) context.SetSma(20, sma20.Value);
        if (sma50.HasValue) context.SetSma(50, sma50.Value);
        return context;
    }

    private static MarketContext CreateMarketContext(decimal closePrice = 42000m)
    {
        return new MarketContext
        {
            Symbol = "BTC-USD",
            TimestampUtc = CandleTimestamp,
            CurrentCandle = Candle.Create(
                "Binance", "BTC-USD", "15m", CandleTimestamp,
                closePrice - 100m, closePrice + 100m, closePrice - 200m, closePrice,
                1_000m, 10),
            Indicators = new IndicatorSnapshot(),
        };
    }
}
```

##### Pattern References

- `RsiConditionHandlerTests.cs` — test structure, factory helpers, Given_When_Then naming

---

### Task 2.7: PriceVsEmaConditionHandler tests {#task-27-pricevsemaconditionhandler-tests}

Create comprehensive unit tests for `PriceVsEmaConditionHandler`.

- **Complexity**: High
- **Risk Factors**: Must cover all operators and distance types
- **Files**:
  - `tests/TradePilot.Application.Tests/StrategyAuthoring/Services/PriceVsEmaConditionHandlerTests.cs` — new file
- **Success**:
  - Tests for: near (percent pass/fail), near (absolute), touch (pass/fail), above, below, cross_above, cross_below
  - Tests for: missing EMA data, unknown operator
  - All tests pass
- **Dependencies**:
  - Task 2.2, Phase 1

#### Implementation Details

```csharp
// tests/TradePilot.Application.Tests/StrategyAuthoring/Services/PriceVsEmaConditionHandlerTests.cs — new file
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Services;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Tests.StrategyAuthoring.Services;

[TestClass]
public sealed class PriceVsEmaConditionHandlerTests
{
    private const long CandleTimestamp = 1_000_000;

    private PriceVsEmaConditionHandler _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new PriceVsEmaConditionHandler();
    }

    [TestMethod]
    public void GivenNearPercent_WhenWithinDistance_ThenPassed()
    {
        var condition = CreatePriceVsEmaCondition("near", 50, "percent", 0.25m);
        var indicators = CreateEmaContext(emaPeriod: 50, emaValue: 42050m);
        var market = CreateMarketContext(close: 42150m);

        var result = _sut.Evaluate(condition, indicators, market);

        result.Passed.Should().BeTrue();
        result.Reason.Should().Contain("condition met");
    }

    [TestMethod]
    public void GivenNearPercent_WhenOutsideDistance_ThenFailed()
    {
        var condition = CreatePriceVsEmaCondition("near", 50, "percent", 0.25m);
        var indicators = CreateEmaContext(emaPeriod: 50, emaValue: 42050m);
        var market = CreateMarketContext(close: 43000m);

        var result = _sut.Evaluate(condition, indicators, market);

        result.Passed.Should().BeFalse();
    }

    [TestMethod]
    public void GivenTouch_WhenWickSpansEma_ThenPassed()
    {
        var condition = CreatePriceVsEmaCondition("touch", 50);
        var indicators = CreateEmaContext(emaPeriod: 50, emaValue: 42000m);
        var market = CreateMarketContextWithHlc(high: 42100m, low: 41900m, close: 42050m);

        var result = _sut.Evaluate(condition, indicators, market);

        result.Passed.Should().BeTrue();
    }

    [TestMethod]
    public void GivenTouch_WhenWickAboveEma_ThenFailed()
    {
        var condition = CreatePriceVsEmaCondition("touch", 50);
        var indicators = CreateEmaContext(emaPeriod: 50, emaValue: 42000m);
        var market = CreateMarketContextWithHlc(high: 42500m, low: 42100m, close: 42300m);

        var result = _sut.Evaluate(condition, indicators, market);

        result.Passed.Should().BeFalse();
    }

    [TestMethod]
    public void GivenAbove_WhenPriceAboveEma_ThenPassed()
    {
        var condition = CreatePriceVsEmaCondition("above", 50);
        var indicators = CreateEmaContext(emaPeriod: 50, emaValue: 42000m);
        var market = CreateMarketContext(close: 42500m);

        var result = _sut.Evaluate(condition, indicators, market);

        result.Passed.Should().BeTrue();
    }

    [TestMethod]
    public void GivenBelow_WhenPriceBelowEma_ThenPassed()
    {
        var condition = CreatePriceVsEmaCondition("below", 50);
        var indicators = CreateEmaContext(emaPeriod: 50, emaValue: 42000m);
        var market = CreateMarketContext(close: 41500m);

        var result = _sut.Evaluate(condition, indicators, market);

        result.Passed.Should().BeTrue();
    }

    [TestMethod]
    public void GivenMissingEmaData_WhenEvaluated_ThenFailed()
    {
        var condition = CreatePriceVsEmaCondition("near", 50, "percent", 0.25m);
        var indicators = new IndicatorContext();
        var market = CreateMarketContext(close: 42000m);

        var result = _sut.Evaluate(condition, indicators, market);

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("not available");
    }

    [TestMethod]
    public void GivenUnknownOperator_WhenEvaluated_ThenFailed()
    {
        var condition = CreatePriceVsEmaCondition("invalid_op", 50);
        var indicators = CreateEmaContext(emaPeriod: 50, emaValue: 42000m);
        var market = CreateMarketContext(close: 42000m);

        var result = _sut.Evaluate(condition, indicators, market);

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("Unknown");
    }

    // --- Factory helpers ---

    private static EntryConditionConfig CreatePriceVsEmaCondition(
        string op, int period, string distanceType = "", decimal? distanceValue = null)
    {
        return new EntryConditionConfig
        {
            Id = "ema-1",
            Enabled = true,
            Type = EntryConditionType.PriceVsEma,
            Label = $"Price vs EMA({period})",
            Params = new PriceVsEmaParams
            {
                Period = period,
                Operator = op,
                DistanceType = distanceType,
                DistanceValue = distanceValue
            }
        };
    }

    private static IndicatorContext CreateEmaContext(int emaPeriod, decimal emaValue, decimal? previousValue = null)
    {
        var context = new IndicatorContext();
        context.SetEma(emaPeriod, emaValue, previousValue);
        return context;
    }

    private static MarketContext CreateMarketContext(decimal close = 42000m)
    {
        return new MarketContext
        {
            Symbol = "BTC-USD",
            TimestampUtc = CandleTimestamp,
            CurrentCandle = Candle.Create(
                "Binance", "BTC-USD", "15m", CandleTimestamp,
                close - 100m, close + 100m, close - 200m, close,
                1_000m, 10),
            Indicators = new IndicatorSnapshot(),
        };
    }

    private static MarketContext CreateMarketContextWithHlc(decimal high, decimal low, decimal close)
    {
        return new MarketContext
        {
            Symbol = "BTC-USD",
            TimestampUtc = CandleTimestamp,
            CurrentCandle = Candle.Create(
                "Binance", "BTC-USD", "15m", CandleTimestamp,
                close, high, low, close,
                1_000m, 10),
            Indicators = new IndicatorSnapshot(),
        };
    }
}
```

##### Pattern References

- `RsiConditionHandlerTests.cs` — handler test structure, factory helpers

---

### Task 2.8: Update CompositeStrategyEngine and ConditionEvaluator tests {#task-28-update-compositestrategyengine-and-conditionevaluator-tests}

Update existing tests to account for `ITrendFilterEvaluator` injection in `CompositeStrategyEngine` and add tests for the trend filter gating behavior.

- **Complexity**: Medium
- **Risk Factors**: Must not break existing grid mode tests
- **Files**:
  - `tests/TradePilot.Application.Tests/Trading/Services/CompositeStrategyEngineTests.cs` — update constructor, add trend filter tests
  - `tests/TradePilot.Application.Tests/StrategyAuthoring/Services/ConditionEvaluatorTests.cs` — add PriceVsEma handler to evaluator setup
  - `tests/TradePilot.Application.Tests/StrategyAuthoring/Validation/CrossFieldValidatorTests.cs` — remove/update `TREND_FILTER_NOT_EVALUATED` test assertion (Task 2.4 removes this code)
- **Success**:
  - All existing tests updated and pass
  - `CrossFieldValidatorTests` updated to reflect removal of `TREND_FILTER_NOT_EVALUATED` info message
  - New tests: trend filter fails → SetupDetected = false, conditions not evaluated
  - New tests: trend filter passes → conditions evaluated normally
- **Dependencies**:
  - Tasks 2.1–2.5

#### Implementation Details

```csharp
// CompositeStrategyEngineTests.cs — update TestInitialize:
private Mock<ITrendFilterEvaluator> _trendFilterMock = default!;

[TestInitialize]
public void Setup()
{
    _conditionEvaluatorMock = new Mock<IConditionEvaluator>();
    _trendFilterMock = new Mock<ITrendFilterEvaluator>();
    // Default: trend filter passes
    _trendFilterMock
        .Setup(tf => tf.Evaluate(It.IsAny<TrendFilterConfig?>(), It.IsAny<Direction>(),
            It.IsAny<IndicatorContext>(), It.IsAny<MarketContext>()))
        .Returns(TrendFilterResult.Pass("Filter passed."));
    _sut = new CompositeStrategyEngine(new GridStrategyEngine(), _conditionEvaluatorMock.Object, _trendFilterMock.Object);
}

// Add new tests:
[TestMethod]
public async Task GivenSignalMode_WhenTrendFilterFails_ThenNoSetupAndConditionsNotEvaluated()
{
    var config = CreateSignalConfig();
    var context = CreateMarketContext(includeHigherTimeframes: true);
    // Need IndicatorContext for trend filter to be called
    context = context with { IndicatorContext = new IndicatorContext() };
    _trendFilterMock
        .Setup(tf => tf.Evaluate(config.TrendFilter, config.Direction,
            It.IsAny<IndicatorContext>(), context))
        .Returns(TrendFilterResult.Fail("EMA(50) < EMA(200)"));

    var result = await _sut.EvaluateAsync(context, config);

    result.SetupDetected.Should().BeFalse();
    result.Reason.Should().Contain("Trend filter failed");
    _conditionEvaluatorMock.Verify(
        evaluator => evaluator.Evaluate(It.IsAny<StrategyConfig>(), It.IsAny<MarketContext>()),
        Times.Never);
}
```

##### Pattern References

- `CompositeStrategyEngineTests.cs` — existing mock setup and verification pattern

---

### Task 2.9: Build and run all backend tests {#task-29-build-and-run-all-backend-tests}

Build the solution and run all backend test projects.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: N/A
- **Success**:
  - `dotnet build TradePilot.sln --configuration Release` succeeds
  - All test projects pass: Application.Tests, Domain.Tests, Api.Tests, Infrastructure.Tests, Persistence.Tests

## Phase Success Criteria

- `TrendFilterEvaluator` evaluates all 3 filter types with all operators correctly
- `PriceVsEmaConditionHandler` handles all 6 operators correctly
- Trend filter gates entry conditions in `CompositeStrategyEngine`
- `CrossFieldValidator` no longer emits `TREND_FILTER_NOT_EVALUATED`
- New services registered in DI
- All backend tests pass (existing and new)
