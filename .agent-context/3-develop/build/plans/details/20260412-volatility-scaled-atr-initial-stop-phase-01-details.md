<!-- markdownlint-disable-file -->

# Task Details: Volatility-Scaled Initial Stop Loss (ATR-Based)

## Phase 1: Domain Model, Configuration & Validation

## Standards and Knowledge References

- **csharp.instructions.md**: Enum values don't need guards — model binding handles validation. `sealed` classes, PascalCase properties.
- **testing.instructions.md**: MSTest + FluentAssertions v6 + Moq. `GivenX_WhenY_ThenZ` naming. Tests accompany each phase.
- **dotnet-architecture.instructions.md**: Value objects immutable, records for config DTOs, `sealed` classes.
- **33-risk-management-and-trade-sizing.md**: R-based sizing formula: `notional = R / (SL% / 100)`. SL distance = `ATR × multiplier / entryPrice × 100`.

### Task 1.1: Add `AtrInitial` to `ExitRuleType` Enum {#task-11-add-atrinitial-to-exitruletype-enum}

Add the `AtrInitial` variant to the `ExitRuleType` enum. This represents an initial stop-loss distance set by ATR at entry time, distinct from `AtrTrailing` which trails the high watermark.

- **Complexity**: Low
- **Risk Factors**: None — straightforward enum addition
- **Files**:
  - `src/TradingApp.Application/StrategyAuthoring/Models/ExitRuleType.cs` - Add new enum value
- **Success**:
  - `ExitRuleType.AtrInitial` compiles and serializes as `"atr_initial"` in JSON
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Application/StrategyAuthoring/Models/ExitRuleType.cs — modification
namespace TradingApp.Application.StrategyAuthoring.Models;

public enum ExitRuleType
{
    FixedPercent,
    SwingLow,
    AtrTrailing,
    RMultiple,
    AtrInitial,
}
```

##### Pattern References

- `src/TradingApp.Application/StrategyAuthoring/Models/ExitRuleType.cs` — existing enum with 4 values

### Task 1.2: Add `AtrPeriod` Field to `ExitRuleConfig` {#task-12-add-atrperiod-field-to-exitruleconfig}

Add an optional `AtrPeriod` field to `ExitRuleConfig` for future configurability. The ATR pipeline currently uses hardcoded period 14 — this field is for configuration completeness and optimizer sweeping.

- **Complexity**: Low
- **Risk Factors**: None — additive field, no breaking changes
- **Files**:
  - `src/TradingApp.Application/StrategyAuthoring/Models/ExitRuleConfig.cs` - Add `AtrPeriod` property
- **Success**:
  - `ExitRuleConfig.AtrPeriod` is available and nullable
  - Existing deserialization continues to work (null default)
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Application/StrategyAuthoring/Models/ExitRuleConfig.cs — modification
public sealed record ExitRuleConfig
{
    public bool Enabled { get; init; }
    public ExitRuleType Type { get; init; }
    public decimal? Value { get; init; }
    public int? Lookback { get; init; }
    public decimal? AtrMultiplier { get; init; }
    public int? AtrPeriod { get; init; }
    public int? TrailingStopWarmup { get; init; }
}
```

##### Pattern References

- `src/TradingApp.Application/StrategyAuthoring/Models/ExitRuleConfig.cs` — existing record with 6 properties

### Task 1.3: Add `AtrInitial` Validation to `BusinessRuleValidator` {#task-13-add-atrinitial-validation-to-businessrulevalidator}

Extend the exit validation in `BusinessRuleValidator` to validate `AtrMultiplier > 0` when `StopLoss.Type == AtrInitial`, matching the existing `AtrTrailing` validation. Also validate `AtrPeriod > 0` when specified.

- **Complexity**: Low
- **Risk Factors**: None — follows existing validation pattern exactly
- **Files**:
  - `src/TradingApp.Application/StrategyAuthoring/Validation/BusinessRuleValidator.cs` - Add `AtrInitial` validation rules
- **Success**:
  - `AtrInitial` with null/zero `AtrMultiplier` produces validation error
  - `AtrInitial` with negative `AtrPeriod` produces validation error
  - `AtrInitial` with valid `AtrMultiplier` passes validation
- **Dependencies**: Task 1.1

#### Implementation Details

Add validation after the existing `AtrTrailing` multiplier check (around line 136):

```csharp
// src/TradingApp.Application/StrategyAuthoring/Validation/BusinessRuleValidator.cs — modification
// After the existing AtrTrailing AtrMultiplier validation block:

if (exit.StopLoss.Enabled
    && exit.StopLoss.Type == ExitRuleType.AtrInitial
    && (exit.StopLoss.AtrMultiplier is null || exit.StopLoss.AtrMultiplier <= 0))
{
    result.Add(new ValidationError
    {
        Severity = ValidationSeverity.Error,
        FieldPath = "exit.stopLoss.atrMultiplier",
        Code = "SL_ATR_MULTIPLIER_REQUIRED",
        Message = "ATR multiplier must be > 0 when type is atr_initial.",
    });
}

if (exit.StopLoss.Enabled
    && (exit.StopLoss.Type == ExitRuleType.AtrInitial || exit.StopLoss.Type == ExitRuleType.AtrTrailing)
    && exit.StopLoss.AtrPeriod.HasValue
    && exit.StopLoss.AtrPeriod <= 0)
{
    result.Add(new ValidationError
    {
        Severity = ValidationSeverity.Error,
        FieldPath = "exit.stopLoss.atrPeriod",
        Code = "SL_ATR_PERIOD_INVALID",
        Message = "ATR period must be > 0 when specified.",
    });
}
```

##### Pattern References

- `src/TradingApp.Application/StrategyAuthoring/Validation/BusinessRuleValidator.cs` lines 125–136 — existing `AtrTrailing` multiplier validation

### Task 1.4: Add `AtrAtEntry` Field to `GridState` {#task-14-add-atratentry-field-to-gridstate}

Add `AtrAtEntry` property to `GridState` to capture the ATR value at grid deployment time. This is used by `AtrInitial` exit evaluation to compute the fixed stop price.

- **Complexity**: Low
- **Risk Factors**: In-memory only — not persisted across worker restarts. Exchange trigger orders remain in place as backstop.
- **Files**:
  - `src/TradingApp.Application/Trading/Models/GridState.cs` - Add `AtrAtEntry` property
- **Success**:
  - `GridState.AtrAtEntry` is available as `decimal?`
  - Default is null
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Application/Trading/Models/GridState.cs — modification
// Add after the InitialRDollars property:

/// <summary>
/// ATR value captured when the grid was deployed.
/// Used by AtrInitial stop to compute the fixed stop price from entry-time volatility.
/// Reset to null when position is closed.
/// </summary>
public decimal? AtrAtEntry { get; set; }
```

##### Pattern References

- `src/TradingApp.Application/Trading/Models/GridState.cs` — existing `InitialRDollars` property follows the same pattern (set at deployment, cleared on close)

### Task 1.5: Unit Tests for Validation Rules {#task-15-unit-tests-for-validation-rules}

Add unit tests for the new `AtrInitial` validation rules in `BusinessRuleValidator`.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `tests/TradingApp.Application.Tests/StrategyAuthoring/Validation/BusinessRuleValidatorTests.cs` - Add test methods (find existing file or create if needed)
- **Success**:
  - Tests verify `AtrMultiplier` required for `AtrInitial`
  - Tests verify `AtrPeriod` validation when specified
  - All tests pass
- **Dependencies**: Tasks 1.1–1.3

#### Implementation Details

```csharp
// Tests to add (follow existing test patterns in the file):

[TestMethod]
public void GivenAtrInitialStopLossWithNullMultiplier_WhenValidated_ThenReturnsError()
{
    // Arrange — create config with StopLoss.Type = AtrInitial, AtrMultiplier = null
    // Act — validate
    // Assert — error with code "SL_ATR_MULTIPLIER_REQUIRED"
}

[TestMethod]
public void GivenAtrInitialStopLossWithValidMultiplier_WhenValidated_ThenNoError()
{
    // Arrange — create config with StopLoss.Type = AtrInitial, AtrMultiplier = 2.0
    // Act — validate
    // Assert — no error for atrMultiplier field
}

[TestMethod]
public void GivenAtrStopLossWithNegativePeriod_WhenValidated_ThenReturnsError()
{
    // Arrange — create config with StopLoss.Type = AtrInitial, AtrPeriod = -1
    // Act — validate
    // Assert — error with code "SL_ATR_PERIOD_INVALID"
}
```

##### Pattern References

- `tests/TradingApp.Application.Tests/StrategyAuthoring/Validation/BusinessRuleValidatorTests.cs` — existing validation test patterns

### Task 1.6: Build and Run Architecture Tests {#task-16-build-and-run-architecture-tests}

Build the solution and run all tests to verify the domain model changes don't break anything.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (build/test verification)
- **Success**:
  - `dotnet build TradingApp.sln` succeeds
  - `dotnet test tests/TradingApp.Application.Tests --filter "FullyQualifiedName~BusinessRuleValidator"` passes
  - All existing tests continue to pass
- **Dependencies**: Tasks 1.1–1.5

## Phase Success Criteria

- `ExitRuleType.AtrInitial` exists and compiles
- `ExitRuleConfig.AtrPeriod` field exists
- `GridState.AtrAtEntry` field exists
- `BusinessRuleValidator` validates `AtrMultiplier` and `AtrPeriod` for `AtrInitial`
- All validation unit tests pass
- Solution builds cleanly
