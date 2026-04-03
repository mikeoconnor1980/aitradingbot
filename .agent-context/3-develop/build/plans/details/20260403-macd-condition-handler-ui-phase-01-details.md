<!-- markdownlint-disable-file -->

# Task Details: F8 — MACD Condition Handler + UI Card

## Phase 1: Backend — MacdConditionHandler + Validation + Tests

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — `sealed` classes, `_camelCase` fields, no regions
- `.github/instructions/testing.instructions.md` — MSTest only, FluentAssertions ≤ v6, Moq, `Given_When_Then` naming
- `.github/instructions/dotnet-architecture.instructions.md` — DI registration in `Program.cs`, handler pattern
- `.agent-context/0-knowledge/13-strategy-config-schema.md` — 4-step extension recipe for new condition types

## Design References

- Existing `MacdParams` already defines `FastPeriod`, `SlowPeriod`, `SignalPeriod`, `Operator` (string)
- `IndicatorContext` already has full MACD API: `GetMacd`, `GetPreviousMacd`, `GetMacdSignal`, `GetPreviousMacdSignal`, `GetMacdHistogram`, `GetPreviousMacdHistogram` — all accepting `(fast, slow, signal)` params and returning `decimal?`
- The PBI specifies 6 operators: `cross_above_signal`, `cross_below_signal`, `above_zero`, `below_zero`, `histogram_rising`, `histogram_falling`

### Task 1.1: Create `MacdConditionHandler` implementing `IConditionHandler` {#task-11-create-macdconditionhandler}

Create a new condition handler that evaluates MACD conditions using the 6 operators defined in the PBI.

- **Complexity**: Medium
- **Risk Factors**: Cross detection requires correct use of current + previous values; histogram comparison needs both current and previous histogram
- **Files**:
  - `src/TradingApp.Application/StrategyAuthoring/Services/MacdConditionHandler.cs` — new file
- **Success**:
  - Handler compiles and returns correct `ConditionResult` for all 6 operators
  - Missing data returns `Passed = false` with descriptive reason
  - Unknown operator returns `Passed = false` with descriptive reason

#### Implementation Details

```csharp
// src/TradingApp.Application/StrategyAuthoring/Services/MacdConditionHandler.cs — new file
using System.Globalization;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.StrategyAuthoring.Services;

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
            Reason = $"MACD({macd.FastPeriod},{macd.SlowPeriod},{macd.SignalPeriod}) prev_line={Format(previousLine.Value)} curr_line={Format(currentLine.Value)} prev_signal={Format(previousSignal.Value)} curr_signal={Format(currentSignal.Value)} {direction} - {status}",
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
            Reason = $"MACD({macd.FastPeriod},{macd.SlowPeriod},{macd.SignalPeriod}) curr_hist={Format(currentHistogram.Value)} prev_hist={Format(previousHistogram.Value)} {direction} - {status}",
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
```

##### Pattern References

- Based on `src/TradingApp.Application/StrategyAuthoring/Services/RsiConditionHandler.cs` — overall structure, `Fail` helper, `Format` helper, operator switch pattern
- Cross detection logic follows same `previousValue < threshold && currentValue >= threshold` pattern from `RsiConditionHandler.EvaluateCross`
- Uses `IndicatorContext` MACD API from `src/TradingApp.Application/Trading/Models/IndicatorContext.cs`

---

### Task 1.2: Enhance `BusinessRuleValidator` with MACD-specific validation rules {#task-12-enhance-businessrulevalidator}

Add three new validation rules: (1) max 1 MACD condition per strategy, (2) period range enforcement (fast ∈ [2, 50], slow ∈ [5, 200], signal ∈ [2, 50]), (3) fast < slow cross-field constraint. Enhance the existing `MACD_PERIODS_INVALID` rule area.

- **Complexity**: Medium
- **Risk Factors**: Must not break existing RSI/PriceVsEma validation; need to add duplicate detection loop above individual condition validation
- **Files**:
  - `src/TradingApp.Application/StrategyAuthoring/Validation/BusinessRuleValidator.cs` — modify
- **Success**:
  - Max 1 MACD condition enforced with `MACD_MAX_COUNT` error code
  - Period ranges validated with descriptive error messages
  - `FastPeriod < SlowPeriod` enforced with `MACD_FAST_SLOW_INVALID` error code
  - Existing MACD_PERIODS_INVALID rule still works (all > 0)
- **Dependencies**:
  - None (modifying existing file)

#### Implementation Details

```csharp
// src/TradingApp.Application/StrategyAuthoring/Validation/BusinessRuleValidator.cs — modification
// Add at the beginning of the entry conditions loop (before individual condition validation):

// ... existing code looping over conditions ...

// Add max-1 MACD check BEFORE the per-condition loop:
var macdCount = config.EntryConditions.Count(c => c.Type == EntryConditionType.Macd);
if (macdCount > 1)
{
    result.Add(new ValidationError
    {
        Severity = ValidationSeverity.Error,
        FieldPath = "entryConditions",
        Code = "MACD_MAX_COUNT",
        Message = "Only one MACD condition is allowed per strategy.",
    });
}

// Enhance the existing MacdParams block to add range and cross-field validation:
if (condition.Params is MacdParams macd)
{
    if (macd.FastPeriod <= 0 || macd.SlowPeriod <= 0 || macd.SignalPeriod <= 0)
    {
        result.Add(new ValidationError
        {
            Severity = ValidationSeverity.Error,
            FieldPath = $"entryConditions[{index}].params",
            Code = "MACD_PERIODS_INVALID",
            Message = "MACD fast, slow, and signal periods must all be greater than 0.",
        });
    }

    if (macd.FastPeriod < 2 || macd.FastPeriod > 50)
    {
        result.Add(new ValidationError
        {
            Severity = ValidationSeverity.Error,
            FieldPath = $"entryConditions[{index}].params.fastPeriod",
            Code = "MACD_FAST_PERIOD_RANGE",
            Message = "MACD fast period must be between 2 and 50.",
        });
    }

    if (macd.SlowPeriod < 5 || macd.SlowPeriod > 200)
    {
        result.Add(new ValidationError
        {
            Severity = ValidationSeverity.Error,
            FieldPath = $"entryConditions[{index}].params.slowPeriod",
            Code = "MACD_SLOW_PERIOD_RANGE",
            Message = "MACD slow period must be between 5 and 200.",
        });
    }

    if (macd.SignalPeriod < 2 || macd.SignalPeriod > 50)
    {
        result.Add(new ValidationError
        {
            Severity = ValidationSeverity.Error,
            FieldPath = $"entryConditions[{index}].params.signalPeriod",
            Code = "MACD_SIGNAL_PERIOD_RANGE",
            Message = "MACD signal period must be between 2 and 50.",
        });
    }

    if (macd.FastPeriod >= macd.SlowPeriod)
    {
        result.Add(new ValidationError
        {
            Severity = ValidationSeverity.Error,
            FieldPath = $"entryConditions[{index}].params.fastPeriod",
            Code = "MACD_FAST_SLOW_INVALID",
            Message = "MACD fast period must be less than slow period.",
        });
    }
}
// ... existing code ...
```

##### Pattern References

- Based on existing `MACD_PERIODS_INVALID` block in `src/TradingApp.Application/StrategyAuthoring/Validation/BusinessRuleValidator.cs` (lines 187-196)
- Error code naming follows existing conventions: `RSI_VALUE_INVALID`, `DISTANCE_VALUE_INVALID`

---

### Task 1.3: Register `MacdConditionHandler` in DI (`Program.cs`) {#task-13-register-macdconditionhandler-in-di}

Add DI registration for the new handler alongside existing RSI and PriceVsEma handler registrations.

- **Complexity**: Low
- **Risk Factors**: None — straightforward one-line addition
- **Files**:
  - `src/TradingApp.Api/Program.cs` — modify (line ~94)
- **Success**:
  - `MacdConditionHandler` is resolved by `ConditionEvaluator` at runtime
  - Application compiles and starts without errors

```csharp
// src/TradingApp.Api/Program.cs — modification
// After line 94 (existing PriceVsEmaConditionHandler registration):
builder.Services.AddScoped<IConditionHandler, RsiConditionHandler>();
builder.Services.AddScoped<IConditionHandler, PriceVsEmaConditionHandler>();
builder.Services.AddScoped<IConditionHandler, MacdConditionHandler>();  // ← add this line
```

##### Pattern References

- Based on existing registrations in `src/TradingApp.Api/Program.cs` lines 93-94

---

### Task 1.4: Create `MacdConditionHandlerTests` {#task-14-create-macdconditionhandlertests}

Create comprehensive test class covering all 6 operators, missing data paths, unknown operator, and wrong params type.

- **Complexity**: Medium
- **Risk Factors**: Must correctly set up `IndicatorContext` with MACD data including previous values for cross/histogram tests
- **Files**:
  - `tests/TradingApp.Application.Tests/StrategyAuthoring/Services/MacdConditionHandlerTests.cs` — new file
- **Success**:
  - All 6 operators tested for passing and failing cases
  - Missing data paths tested (line, signal, histogram, previous values)
  - Unknown operator tested
  - Wrong params type tested
  - All tests pass

#### Implementation Details

```csharp
// tests/TradingApp.Application.Tests/StrategyAuthoring/Services/MacdConditionHandlerTests.cs — new file
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.StrategyAuthoring.Services;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Tests.StrategyAuthoring.Services;

[TestClass]
public sealed class MacdConditionHandlerTests
{
    private const long CandleTimestamp = 1_000_000;

    private MacdConditionHandler _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new MacdConditionHandler();
    }

    // --- cross_above_signal ---

    [TestMethod]
    public void GivenCrossAboveSignal_WhenPreviousLineBelowSignalAndCurrentAbove_ThenPassed()
    {
        var condition = CreateMacdCondition("cross_above_signal");
        var indicators = CreateIndicatorContext(
            line: 0.5m, signal: 0.3m, histogram: 0.2m,
            previousLine: -0.1m, previousSignal: 0.1m, previousHistogram: -0.2m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeTrue();
        result.Reason.Should().Contain("cross_above_signal").And.Contain("condition met");
    }

    [TestMethod]
    public void GivenCrossAboveSignal_WhenBothAboveSignal_ThenFailed()
    {
        var condition = CreateMacdCondition("cross_above_signal");
        var indicators = CreateIndicatorContext(
            line: 0.5m, signal: 0.3m, histogram: 0.2m,
            previousLine: 0.4m, previousSignal: 0.2m, previousHistogram: 0.2m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeFalse();
    }

    // --- cross_below_signal ---

    [TestMethod]
    public void GivenCrossBelowSignal_WhenPreviousLineAboveSignalAndCurrentBelow_ThenPassed()
    {
        var condition = CreateMacdCondition("cross_below_signal");
        var indicators = CreateIndicatorContext(
            line: -0.1m, signal: 0.1m, histogram: -0.2m,
            previousLine: 0.5m, previousSignal: 0.3m, previousHistogram: 0.2m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeTrue();
        result.Reason.Should().Contain("cross_below_signal").And.Contain("condition met");
    }

    [TestMethod]
    public void GivenCrossBelowSignal_WhenBothBelowSignal_ThenFailed()
    {
        var condition = CreateMacdCondition("cross_below_signal");
        var indicators = CreateIndicatorContext(
            line: -0.3m, signal: -0.1m, histogram: -0.2m,
            previousLine: -0.2m, previousSignal: -0.1m, previousHistogram: -0.1m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeFalse();
    }

    // --- above_zero ---

    [TestMethod]
    public void GivenAboveZero_WhenLinePositive_ThenPassed()
    {
        var condition = CreateMacdCondition("above_zero");
        var indicators = CreateIndicatorContext(line: 0.5m, signal: 0.3m, histogram: 0.2m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeTrue();
        result.Reason.Should().Contain("above_zero").And.Contain("condition met");
    }

    [TestMethod]
    public void GivenAboveZero_WhenLineNegative_ThenFailed()
    {
        var condition = CreateMacdCondition("above_zero");
        var indicators = CreateIndicatorContext(line: -0.5m, signal: 0.3m, histogram: -0.8m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeFalse();
    }

    [TestMethod]
    public void GivenAboveZero_WhenLineExactlyZero_ThenFailed()
    {
        var condition = CreateMacdCondition("above_zero");
        var indicators = CreateIndicatorContext(line: 0m, signal: 0.3m, histogram: -0.3m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeFalse();
    }

    // --- below_zero ---

    [TestMethod]
    public void GivenBelowZero_WhenLineNegative_ThenPassed()
    {
        var condition = CreateMacdCondition("below_zero");
        var indicators = CreateIndicatorContext(line: -0.5m, signal: 0.3m, histogram: -0.8m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeTrue();
        result.Reason.Should().Contain("below_zero").And.Contain("condition met");
    }

    [TestMethod]
    public void GivenBelowZero_WhenLinePositive_ThenFailed()
    {
        var condition = CreateMacdCondition("below_zero");
        var indicators = CreateIndicatorContext(line: 0.5m, signal: 0.3m, histogram: 0.2m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeFalse();
    }

    // --- histogram_rising ---

    [TestMethod]
    public void GivenHistogramRising_WhenCurrentGreaterThanPrevious_ThenPassed()
    {
        var condition = CreateMacdCondition("histogram_rising");
        var indicators = CreateIndicatorContext(
            line: 0.5m, signal: 0.3m, histogram: 0.3m,
            previousLine: 0.4m, previousSignal: 0.3m, previousHistogram: 0.1m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeTrue();
        result.Reason.Should().Contain("histogram_rising").And.Contain("condition met");
    }

    [TestMethod]
    public void GivenHistogramRising_WhenCurrentEqualToPrevious_ThenFailed()
    {
        var condition = CreateMacdCondition("histogram_rising");
        var indicators = CreateIndicatorContext(
            line: 0.5m, signal: 0.3m, histogram: 0.2m,
            previousLine: 0.4m, previousSignal: 0.2m, previousHistogram: 0.2m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeFalse();
    }

    // --- histogram_falling ---

    [TestMethod]
    public void GivenHistogramFalling_WhenCurrentLessThanPrevious_ThenPassed()
    {
        var condition = CreateMacdCondition("histogram_falling");
        var indicators = CreateIndicatorContext(
            line: 0.3m, signal: 0.4m, histogram: -0.1m,
            previousLine: 0.5m, previousSignal: 0.3m, previousHistogram: 0.2m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeTrue();
        result.Reason.Should().Contain("histogram_falling").And.Contain("condition met");
    }

    [TestMethod]
    public void GivenHistogramFalling_WhenCurrentGreaterThanPrevious_ThenFailed()
    {
        var condition = CreateMacdCondition("histogram_falling");
        var indicators = CreateIndicatorContext(
            line: 0.5m, signal: 0.3m, histogram: 0.3m,
            previousLine: 0.4m, previousSignal: 0.3m, previousHistogram: 0.1m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeFalse();
    }

    // --- missing data ---

    [TestMethod]
    public void GivenMissingMacdData_WhenEvaluated_ThenFailed()
    {
        var condition = CreateMacdCondition("above_zero");
        var indicators = new IndicatorContext();

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("not available");
    }

    [TestMethod]
    public void GivenMissingPreviousDataForCross_WhenEvaluated_ThenFailed()
    {
        var condition = CreateMacdCondition("cross_above_signal");
        var indicators = CreateIndicatorContext(line: 0.5m, signal: 0.3m, histogram: 0.2m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("previous values not available");
    }

    [TestMethod]
    public void GivenMissingPreviousHistogram_WhenHistogramRisingEvaluated_ThenFailed()
    {
        var condition = CreateMacdCondition("histogram_rising");
        var indicators = CreateIndicatorContext(line: 0.5m, signal: 0.3m, histogram: 0.2m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("previous histogram not available");
    }

    // --- unknown operator ---

    [TestMethod]
    public void GivenUnknownOperator_WhenEvaluated_ThenFailed()
    {
        var condition = CreateMacdCondition("invalid_op");
        var indicators = CreateIndicatorContext(line: 0.5m, signal: 0.3m, histogram: 0.2m);

        var result = _sut.Evaluate(condition, indicators, CreateMarketContext());

        result.Passed.Should().BeFalse();
        result.Reason.Should().Contain("Unknown MACD operator");
    }

    // --- helpers ---

    private static EntryConditionConfig CreateMacdCondition(
        string op,
        int fast = 12,
        int slow = 26,
        int signal = 9)
    {
        return new EntryConditionConfig
        {
            Id = "macd-1",
            Enabled = true,
            Type = EntryConditionType.Macd,
            Label = $"MACD({fast},{slow},{signal})",
            Params = new MacdParams
            {
                FastPeriod = fast,
                SlowPeriod = slow,
                SignalPeriod = signal,
                Operator = op,
            },
        };
    }

    private static IndicatorContext CreateIndicatorContext(
        decimal line,
        decimal signal,
        decimal histogram,
        decimal? previousLine = null,
        decimal? previousSignal = null,
        decimal? previousHistogram = null,
        int fast = 12,
        int slow = 26,
        int signalPeriod = 9)
    {
        var context = new IndicatorContext();
        context.SetMacd(fast, slow, signalPeriod, line, signal, histogram, previousLine, previousSignal, previousHistogram);
        return context;
    }

    private static MarketContext CreateMarketContext()
    {
        return new MarketContext
        {
            Symbol = "BTC-USD",
            TimestampUtc = CandleTimestamp,
            CurrentCandle = Candle.Create(
                "Binance",
                "BTC-USD",
                "15m",
                CandleTimestamp,
                100m,
                105m,
                95m,
                102m,
                1_000m,
                10),
            Indicators = new IndicatorSnapshot(),
        };
    }
}
```

##### Pattern References

- Based on `tests/TradingApp.Application.Tests/StrategyAuthoring/Services/RsiConditionHandlerTests.cs` — test structure, factory helpers, assertion style

---

### Task 1.5: Add `BusinessRuleValidatorTests` for MACD enhancements {#task-15-add-businessrulevalidatortests-for-macd}

Add tests for the new validation rules: max 1 MACD per strategy, period range enforcement, and fast < slow cross-field constraint.

- **Complexity**: Low
- **Risk Factors**: None — straightforward validation assertions
- **Files**:
  - `tests/TradingApp.Application.Tests/StrategyAuthoring/Validation/BusinessRuleValidatorTests.cs` — modify (add new test methods)
- **Success**:
  - Max-count rule triggers with `MACD_MAX_COUNT` error code
  - Period range errors trigger with correct error codes
  - Fast >= slow triggers `MACD_FAST_SLOW_INVALID`
  - Valid MACD config produces no errors
  - All new tests pass
- **Dependencies**:
  - Task 1.2 (validator implementation)

---

### Task 1.6: Run all backend tests and architecture tests {#task-16-run-all-backend-tests}

Run the full backend test suite to verify no regressions.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification only)
- **Success**:
  - All existing tests pass
  - All new `MacdConditionHandler` tests pass
  - All new `BusinessRuleValidator` tests pass
  - Architecture tests pass

Run commands:
```powershell
dotnet test tests/TradingApp.Application.Tests/ --no-restore
dotnet test tests/TradingApp.Domain.Tests/ --no-restore
```

## Phase Success Criteria

- `MacdConditionHandler` correctly evaluates all 6 operators
- `BusinessRuleValidator` enforces max 1 MACD, period ranges, and fast < slow
- DI registration wires handler into `ConditionEvaluator` pipeline
- All backend tests pass (new + existing)
