<!-- markdownlint-disable-file -->

# Task Details: F5 — Indicator Infrastructure & Condition Evaluator (RSI)

## Phase 2: Condition Evaluator Engine & RSI Handler

## Standards and Knowledge References

- **csharp.instructions.md**: sealed classes, interfaces in same file as Application service impl, Given_When_Then tests
- **testing.instructions.md**: MSTest, Moq, FluentAssertions 6, private static helpers
- **Knowledge**: `16-signal-contracts.md` (signal types), `14-strategy-runtime-model.md` (pipeline flow)
- **PBI**: `cross_above`/`cross_below` operators require current + previous RSI values; `entryLogic` All/Any combinator; unknown types produce warning, don't block

---

### Task 2.1: Create condition evaluation models {#task-21-create-condition-evaluation-models}

Create the result models for condition evaluation: `ConditionResult` (per-condition) and `ConditionEvaluationResult` (overall).

- **Complexity**: Low
- **Risk Factors**: None — straightforward model creation
- **Files**:
  - `src/TradePilot.Application/StrategyAuthoring/Models/ConditionResult.cs` — **New**
  - `src/TradePilot.Application/StrategyAuthoring/Models/ConditionEvaluationResult.cs` — **New**
- **Success**:
  - `ConditionResult` has `Passed` (bool), `Reason` (string), `ConditionId` (string)
  - `ConditionEvaluationResult` has `SetupDetected` (bool), `TrendFilterPassed` (bool?), `ConditionResults` (list), `OverallReason` (string)

#### Implementation Details

```csharp
// src/TradePilot.Application/StrategyAuthoring/Models/ConditionResult.cs — new file
namespace TradePilot.Application.StrategyAuthoring.Models;

/// <summary>
/// Result of evaluating a single entry condition.
/// </summary>
public sealed class ConditionResult
{
    public required string ConditionId { get; init; }
    public required bool Passed { get; init; }
    public required string Reason { get; init; }
}
```

```csharp
// src/TradePilot.Application/StrategyAuthoring/Models/ConditionEvaluationResult.cs — new file
namespace TradePilot.Application.StrategyAuthoring.Models;

/// <summary>
/// Overall result of evaluating all entry conditions for a signal-mode strategy.
/// </summary>
public sealed class ConditionEvaluationResult
{
    public required bool SetupDetected { get; init; }
    public bool? TrendFilterPassed { get; init; }
    public required IReadOnlyList<ConditionResult> ConditionResults { get; init; }
    public required string OverallReason { get; init; }
}
```

##### Pattern References

- `src/TradePilot.Application/Trading/Models/StrategyEvaluation.cs` — similar result pattern (`SetupDetected` + `Reason`)

---

### Task 2.2: Create `IConditionHandler` and `RsiConditionHandler` {#task-22-create-iconditionhandler-and-rsiconditionhandler}

Create the handler interface and the first concrete handler for RSI conditions. The handler evaluates RSI(period) from `IndicatorContext` against the configured operator and value.

- **Complexity**: High
- **Risk Factors**: Must handle all 6 operators (`lt`, `lte`, `gt`, `gte`, `cross_above`, `cross_below`); cross operators require previous RSI value; must handle missing indicator data gracefully
- **Files**:
  - `src/TradePilot.Application/StrategyAuthoring/Services/IConditionHandler.cs` — **New**
  - `src/TradePilot.Application/StrategyAuthoring/Services/RsiConditionHandler.cs` — **New**
- **Success**:
  - RSI(14)=35 with operator `lt`, value=40 → `Passed = true`, reason includes "RSI(14) = 35 < 40"
  - RSI(14)=45 with operator `lt`, value=40 → `Passed = false`
  - `cross_above` with prev=28, curr=32, value=30 → `Passed = true`
  - `cross_above` with prev=32, curr=35, value=30 → `Passed = false` (already above — no cross)
  - Unknown operator → `Passed = false` with descriptive reason
  - Missing indicator data → `Passed = false` with "not available" reason
- **Dependencies**:
  - Task 1.1 (IndicatorContext), Task 2.1 (ConditionResult)

#### Implementation Details

```csharp
// src/TradePilot.Application/StrategyAuthoring/Services/IConditionHandler.cs — new file
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;

namespace TradePilot.Application.StrategyAuthoring.Services;

/// <summary>
/// Evaluates a single entry condition against market context and indicator data.
/// </summary>
public interface IConditionHandler
{
    EntryConditionType ConditionType { get; }

    ConditionResult Evaluate(EntryConditionConfig condition, IndicatorContext indicatorContext, MarketContext marketContext);
}
```

```csharp
// src/TradePilot.Application/StrategyAuthoring/Services/RsiConditionHandler.cs — new file
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

        if (condition.Params is not RsiParams rsi)
        {
            return Fail(condition.Id, $"Expected RsiParams but received {condition.Params?.GetType().Name ?? "null"}.");
        }

        var currentRsi = indicatorContext.GetRsi(rsi.Period);
        if (!currentRsi.HasValue)
        {
            return Fail(condition.Id, $"RSI({rsi.Period}) not available in indicator context.");
        }

        return rsi.Operator.ToLowerInvariant() switch
        {
            "lt" => EvaluateComparison(condition.Id, currentRsi.Value, rsi.Value, rsi.Period, "<",
                (curr, threshold) => curr < threshold),

            "lte" => EvaluateComparison(condition.Id, currentRsi.Value, rsi.Value, rsi.Period, "<=",
                (curr, threshold) => curr <= threshold),

            "gt" => EvaluateComparison(condition.Id, currentRsi.Value, rsi.Value, rsi.Period, ">",
                (curr, threshold) => curr > threshold),

            "gte" => EvaluateComparison(condition.Id, currentRsi.Value, rsi.Value, rsi.Period, ">=",
                (curr, threshold) => curr >= threshold),

            "cross_above" => EvaluateCross(condition.Id, indicatorContext, rsi, crossAbove: true),

            "cross_below" => EvaluateCross(condition.Id, indicatorContext, rsi, crossAbove: false),

            _ => Fail(condition.Id, $"Unknown RSI operator: '{rsi.Operator}'.")
        };
    }

    private static ConditionResult EvaluateComparison(
        string conditionId, decimal currentRsi, decimal threshold, int period,
        string operatorSymbol, Func<decimal, decimal, bool> compare)
    {
        var passed = compare(currentRsi, threshold);
        var status = passed ? "condition met" : "condition not met";
        return new ConditionResult
        {
            ConditionId = conditionId,
            Passed = passed,
            Reason = $"RSI({period}) = {currentRsi:F2} {operatorSymbol} {threshold} — {status}"
        };
    }

    private static ConditionResult EvaluateCross(
        string conditionId, IndicatorContext indicatorContext, RsiParams rsi, bool crossAbove)
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
            Reason = $"RSI({rsi.Period}) prev={previousRsi.Value:F2} curr={currentRsi.Value:F2} {direction} {rsi.Value} — {status}"
        };
    }

    private static ConditionResult Fail(string conditionId, string reason) =>
        new() { ConditionId = conditionId, Passed = false, Reason = reason };
}
```

##### Pattern References

- `src/TradePilot.Application/StrategyAuthoring/Models/RsiParams.cs` — `Period`, `Operator`, `Value` consumed by handler
- `src/TradePilot.Application/Trading/Models/IndicatorContext.cs` (Phase 1) — `GetRsi(period)`, `GetPreviousRsi(period)`

---

### Task 2.3: Create `IConditionEvaluator` and `ConditionEvaluator` {#task-23-create-iconditionevaluator-and-conditionevaluator}

Create the evaluator orchestrator that resolves handlers by condition type and combines results using `EntryLogic` (All/Any).

- **Complexity**: High
- **Risk Factors**: Must correctly implement All/Any logic; must handle unknown types with warning (not failure); must handle no enabled conditions
- **Files**:
  - `src/TradePilot.Application/StrategyAuthoring/Services/ConditionEvaluator.cs` — **New** (interface `IConditionEvaluator` defined in same file per project convention)
- **Success**:
  - `entryLogic = All`: all enabled conditions must pass → `SetupDetected = true`
  - `entryLogic = Any`: at least one enabled condition passes → `SetupDetected = true`
  - No enabled conditions → `SetupDetected = false`
  - Unknown condition type → warning in results, doesn't block (skipped from pass/fail logic)
  - Disabled conditions excluded from evaluation
- **Dependencies**:
  - Task 2.1 (models), Task 2.2 (IConditionHandler)

#### Implementation Details

```csharp
// src/TradePilot.Application/StrategyAuthoring/Services/ConditionEvaluator.cs — new file
using Microsoft.Extensions.Logging;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;

namespace TradePilot.Application.StrategyAuthoring.Services;

/// <summary>
/// Evaluates strategy entry conditions by dispatching to registered condition handlers.
/// </summary>
public interface IConditionEvaluator
{
    ConditionEvaluationResult Evaluate(StrategyConfig config, MarketContext context);
}

public sealed class ConditionEvaluator : IConditionEvaluator
{
    private readonly Dictionary<EntryConditionType, IConditionHandler> _handlers;
    private readonly ILogger<ConditionEvaluator> _logger;

    public ConditionEvaluator(IEnumerable<IConditionHandler> handlers, ILogger<ConditionEvaluator> logger)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _handlers = handlers.ToDictionary(h => h.ConditionType);
    }

    public ConditionEvaluationResult Evaluate(StrategyConfig config, MarketContext context)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(context);

        var enabledConditions = config.EntryConditions?
            .Where(c => c.Enabled)
            .ToList() ?? [];

        if (enabledConditions.Count == 0)
        {
            return new ConditionEvaluationResult
            {
                SetupDetected = false,
                ConditionResults = [],
                OverallReason = "No enabled entry conditions."
            };
        }

        var indicatorContext = context.IndicatorContext;
        if (indicatorContext is null)
        {
            return new ConditionEvaluationResult
            {
                SetupDetected = false,
                ConditionResults = [],
                OverallReason = "Indicator context not available."
            };
        }

        var results = new List<ConditionResult>();
        var evaluatedResults = new List<ConditionResult>();

        foreach (var condition in enabledConditions)
        {
            if (_handlers.TryGetValue(condition.Type, out var handler))
            {
                var result = handler.Evaluate(condition, indicatorContext, context);
                results.Add(result);
                evaluatedResults.Add(result);
            }
            else
            {
                _logger.LogWarning(
                    "No handler registered for condition type {ConditionType}. Skipping condition {ConditionId}.",
                    condition.Type, condition.Id);

                results.Add(new ConditionResult
                {
                    ConditionId = condition.Id,
                    Passed = true, // Unknown types don't block — forward compatibility
                    Reason = $"No handler for condition type '{condition.Type}' — skipped."
                });
            }
        }

        var entryLogic = config.EntryLogic ?? EntryLogic.All;

        bool setupDetected;
        string reason;

        if (evaluatedResults.Count == 0)
        {
            // All conditions were unknown types — treated as no evaluable conditions
            setupDetected = true;
            reason = "All conditions skipped (unknown types).";
        }
        else if (entryLogic == EntryLogic.All)
        {
            setupDetected = evaluatedResults.All(r => r.Passed);
            var failedCount = evaluatedResults.Count(r => !r.Passed);
            reason = setupDetected
                ? $"All {evaluatedResults.Count} conditions passed."
                : $"{failedCount}/{evaluatedResults.Count} conditions failed.";
        }
        else // EntryLogic.Any
        {
            setupDetected = evaluatedResults.Any(r => r.Passed);
            var passedCount = evaluatedResults.Count(r => r.Passed);
            reason = setupDetected
                ? $"{passedCount}/{evaluatedResults.Count} conditions passed (any mode)."
                : $"No conditions passed out of {evaluatedResults.Count} (any mode).";
        }

        return new ConditionEvaluationResult
        {
            SetupDetected = setupDetected,
            ConditionResults = results,
            OverallReason = reason
        };
    }
}
```

##### Pattern References

- `src/TradePilot.Application/Trading/Services/GridStrategyEngine.cs` — existing strategy engine pattern returning `StrategyEvaluation`
- `src/TradePilot.Application/StrategyAuthoring/Validation/CompositeStrategyValidator.cs` — composite pattern delegating to sub-validators

---

### Task 2.4: Write unit tests for Phase 2 {#task-24-write-unit-tests-for-phase-2}

Write comprehensive tests for `RsiConditionHandler` and `ConditionEvaluator`.

- **Complexity**: Medium
- **Risk Factors**: Must cover all 6 RSI operators, All/Any logic, unknown types, disabled conditions, no enabled conditions, missing indicator data
- **Files**:
  - `tests/TradePilot.Application.Tests/StrategyAuthoring/Services/RsiConditionHandlerTests.cs` — **New**
  - `tests/TradePilot.Application.Tests/StrategyAuthoring/Services/ConditionEvaluatorTests.cs` — **New**
- **Success**:
  - All acceptance criteria covered
  - Edge cases for cross detection, missing data, unknown operators tested
- **Dependencies**:
  - Tasks 2.1–2.3

#### Implementation Details

```csharp
// tests/TradePilot.Application.Tests/StrategyAuthoring/Services/RsiConditionHandlerTests.cs — new file
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Services;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Tests.StrategyAuthoring.Services;

[TestClass]
public sealed class RsiConditionHandlerTests
{
    private RsiConditionHandler _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new RsiConditionHandler();
    }

    [TestMethod]
    public void GivenRsiBelow40_WhenOperatorLt40_ThenPassed()
    {
        var condition = CreateRsiCondition("lt", 40m, 14);
        var indicators = CreateIndicatorContext(rsiPeriod: 14, currentRsi: 35m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeTrue();
        result.Reason.Should().Contain("RSI(14) = 35");
    }

    [TestMethod]
    public void GivenRsiAbove40_WhenOperatorLt40_ThenFailed()
    {
        var condition = CreateRsiCondition("lt", 40m, 14);
        var indicators = CreateIndicatorContext(rsiPeriod: 14, currentRsi: 45m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeFalse();
    }

    [TestMethod]
    public void GivenCrossAboveThreshold_WhenPreviousBelowCurrentAbove_ThenPassed()
    {
        var condition = CreateRsiCondition("cross_above", 30m, 14);
        var indicators = CreateIndicatorContext(rsiPeriod: 14, currentRsi: 32m, previousRsi: 28m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeTrue();
    }

    [TestMethod]
    public void GivenCrossAboveThreshold_WhenBothAbove_ThenFailed()
    {
        var condition = CreateRsiCondition("cross_above", 30m, 14);
        var indicators = CreateIndicatorContext(rsiPeriod: 14, currentRsi: 35m, previousRsi: 32m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeFalse();
    }

    [TestMethod]
    public void GivenCrossBelowThreshold_WhenPreviousAboveCurrentBelow_ThenPassed()
    {
        var condition = CreateRsiCondition("cross_below", 70m, 14);
        var indicators = CreateIndicatorContext(rsiPeriod: 14, currentRsi: 68m, previousRsi: 72m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeTrue();
    }

    [TestMethod]
    public void GivenMissingRsiData_WhenEvaluated_ThenFailed()
    {
        var condition = CreateRsiCondition("lt", 40m, 14);
        var indicators = new IndicatorContext(); // no RSI set

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("not available");
    }

    [TestMethod]
    public void GivenUnknownOperator_WhenEvaluated_ThenFailed()
    {
        var condition = CreateRsiCondition("invalid_op", 40m, 14);
        var indicators = CreateIndicatorContext(rsiPeriod: 14, currentRsi: 35m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("Unknown RSI operator");
    }

    private static EntryConditionConfig CreateRsiCondition(string op, decimal value, int period = 14)
    {
        return new EntryConditionConfig
        {
            Id = "rsi-1",
            Enabled = true,
            Type = EntryConditionType.Rsi,
            Label = $"RSI({period})",
            Params = new RsiParams { Period = period, Operator = op, Value = value }
        };
    }

    private static IndicatorContext CreateIndicatorContext(
        int rsiPeriod = 14, decimal currentRsi = 50m, decimal? previousRsi = null)
    {
        var ctx = new IndicatorContext();
        ctx.SetRsi(rsiPeriod, currentRsi, previousRsi);
        return ctx;
    }

    private static MarketContext CreateMarketContext()
    {
        return new MarketContext
        {
            Symbol = "BTC-USD",
            TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            CurrentCandle = new Candle
            {
                Symbol = "BTC-USD",
                Interval = "15m",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Open = 100m, High = 105m, Low = 95m, Close = 102m, Volume = 1000m
            },
            Indicators = new IndicatorSnapshot()
        };
    }
}
```

```csharp
// tests/TradePilot.Application.Tests/StrategyAuthoring/Services/ConditionEvaluatorTests.cs — new file
using Microsoft.Extensions.Logging;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Services;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Tests.StrategyAuthoring.Services;

[TestClass]
public sealed class ConditionEvaluatorTests
{
    private ConditionEvaluator _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        var handlers = new IConditionHandler[] { new RsiConditionHandler() };
        var logger = Mock.Of<ILogger<ConditionEvaluator>>();
        _sut = new ConditionEvaluator(handlers, logger);
    }

    [TestMethod]
    public void GivenSignalModeRsiLt40_WhenRsi35_ThenSetupDetected()
    {
        var config = CreateSignalConfig(EntryLogic.All, CreateRsiCondition("lt", 40m));
        var context = CreateMarketContextWithIndicators(rsiPeriod: 14, currentRsi: 35m);

        var result = _sut.Evaluate(config, context);

        result.SetupDetected.Should().BeTrue();
    }

    [TestMethod]
    public void GivenSignalModeRsiLt40_WhenRsi45_ThenNoSetup()
    {
        var config = CreateSignalConfig(EntryLogic.All, CreateRsiCondition("lt", 40m));
        var context = CreateMarketContextWithIndicators(rsiPeriod: 14, currentRsi: 45m);

        var result = _sut.Evaluate(config, context);

        result.SetupDetected.Should().BeFalse();
    }

    [TestMethod]
    public void GivenEntryLogicAll_WhenRsiPassesAndUnknownType_ThenSetupDetected()
    {
        var rsiCondition = CreateRsiCondition("lt", 40m);
        var unknownCondition = new EntryConditionConfig
        {
            Id = "unknown-1", Enabled = true,
            Type = EntryConditionType.Unknown, Label = "Unknown",
            Params = null
        };
        var config = CreateSignalConfig(EntryLogic.All, rsiCondition, unknownCondition);
        var context = CreateMarketContextWithIndicators(rsiPeriod: 14, currentRsi: 35m);

        var result = _sut.Evaluate(config, context);

        result.SetupDetected.Should().BeTrue();
    }

    [TestMethod]
    public void GivenEntryLogicAny_WhenRsiFails_ThenNoSetup()
    {
        var config = CreateSignalConfig(EntryLogic.Any, CreateRsiCondition("lt", 40m));
        var context = CreateMarketContextWithIndicators(rsiPeriod: 14, currentRsi: 45m);

        var result = _sut.Evaluate(config, context);

        result.SetupDetected.Should().BeFalse();
    }

    [TestMethod]
    public void GivenDisabledConditionOnly_WhenEntryLogicAll_ThenNoSetup()
    {
        var disabledCondition = CreateRsiCondition("lt", 40m);
        disabledCondition = disabledCondition with { Enabled = false };
        var config = CreateSignalConfig(EntryLogic.All, disabledCondition);
        var context = CreateMarketContextWithIndicators(rsiPeriod: 14, currentRsi: 35m);

        var result = _sut.Evaluate(config, context);

        result.SetupDetected.Should().BeFalse();
        result.OverallReason.Should().Contain("No enabled");
    }

    [TestMethod]
    public void GivenNoEntryConditions_WhenEvaluated_ThenNoSetup()
    {
        var config = new StrategyConfig
        {
            StrategyMode = StrategyMode.Signal,
            StrategyName = "Test",
            Market = "BTC-USD",
            EntryLogic = EntryLogic.All,
            EntryConditions = null,
            Risk = new RiskConfig { PositionSizeValue = 100m }
        };
        var context = CreateMarketContextWithIndicators(rsiPeriod: 14, currentRsi: 35m);

        var result = _sut.Evaluate(config, context);

        result.SetupDetected.Should().BeFalse();
    }

    [TestMethod]
    public void GivenCrossAboveRsi_WhenPreviousBelowCurrentAbove_ThenSetupDetected()
    {
        var config = CreateSignalConfig(EntryLogic.All, CreateRsiCondition("cross_above", 30m));
        var context = CreateMarketContextWithIndicators(rsiPeriod: 14, currentRsi: 32m, previousRsi: 28m);

        var result = _sut.Evaluate(config, context);

        result.SetupDetected.Should().BeTrue();
    }

    // Private static factory helpers
    private static StrategyConfig CreateSignalConfig(EntryLogic logic, params EntryConditionConfig[] conditions)
    {
        return new StrategyConfig
        {
            StrategyMode = StrategyMode.Signal,
            StrategyName = "Test Signal Strategy",
            Market = "BTC-USD",
            EntryLogic = logic,
            EntryConditions = conditions.ToList(),
            Risk = new RiskConfig { PositionSizeValue = 100m }
        };
    }

    private static EntryConditionConfig CreateRsiCondition(string op, decimal value, int period = 14)
    {
        return new EntryConditionConfig
        {
            Id = "rsi-1",
            Enabled = true,
            Type = EntryConditionType.Rsi,
            Label = $"RSI({period})",
            Params = new RsiParams { Period = period, Operator = op, Value = value }
        };
    }

    private static MarketContext CreateMarketContextWithIndicators(
        int rsiPeriod = 14, decimal currentRsi = 50m, decimal? previousRsi = null)
    {
        var indicatorContext = new IndicatorContext();
        indicatorContext.SetRsi(rsiPeriod, currentRsi, previousRsi);

        return new MarketContext
        {
            Symbol = "BTC-USD",
            TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            CurrentCandle = new Candle
            {
                Symbol = "BTC-USD",
                Interval = "15m",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Open = 100m, High = 105m, Low = 95m, Close = 102m, Volume = 1000m
            },
            Indicators = new IndicatorSnapshot(),
            IndicatorContext = indicatorContext
        };
    }
}
```

##### Pattern References

- `tests/TradePilot.Application.Tests/Trading/Services/GridControllerTests.cs` — private static `Create*` helpers, MSTest [TestInitialize] pattern
- `tests/TradePilot.Application.Tests/Scheduling/StrategySchedulerTests.cs` — Moq mock setup pattern

---

### Task 2.5: Build and run tests {#task-25-build-and-run-tests}

Build and run all Phase 2 tests plus existing tests.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification only)
- **Success**:
  - `dotnet build` succeeds
  - All new Phase 2 tests pass
  - All existing tests pass

```bash
dotnet build tests/TradePilot.Application.Tests/TradePilot.Application.Tests.csproj
dotnet test tests/TradePilot.Application.Tests --no-build
```

## Phase Success Criteria

- `ConditionResult` and `ConditionEvaluationResult` models created
- `IConditionHandler` interface and `RsiConditionHandler` created with all 6 operators
- `IConditionEvaluator` and `ConditionEvaluator` created with All/Any logic, unknown type handling
- All acceptance criteria for RSI evaluation covered by tests
- All existing tests pass unchanged
