<!-- markdownlint-disable-file -->

# Task Details: Volatility-Scaled Initial Stop Loss (ATR-Based)

## Phase 2: SL Distance Resolution & Exit Evaluation

## Standards and Knowledge References

- **csharp.instructions.md**: `sealed` classes, `static` methods for pure calculations, `CancellationToken` on async methods.
- **testing.instructions.md**: MSTest + FluentAssertions v6. `GivenX_WhenY_ThenZ` naming. Each phase must include tests.
- **dotnet-architecture.instructions.md**: Application services contain business logic. Domain models are immutable where possible.
- **33-risk-management-and-trade-sizing.md**: SL distance = `ATR × multiplier / entryPrice × 100`. Position size = `R / (SL% / 100)`.
- **15-grid-controller.md**: `StopLossDistanceResolver` integration, `DeployNewGridAsync` flow.

### Task 2.1: Add `AtrInitial` Case to `StopLossDistanceResolver` {#task-21-add-atrinitial-case-to-stoplossdistanceresolver}

Add an `AtrInitial` case to the switch expression in `StopLossDistanceResolver.Resolve`. The formula is mathematically identical to `AtrTrailing`: `(ATR × multiplier) / anchorPrice × 100`. This is the key integration point — the returned percentage feeds directly into `PositionSizeResolver.CalculateRiskBased`.

When ATR is unavailable (null or zero) and `AtrInitial` is configured, fall back to `Value` if present (same as `FixedPercent` fallback).

- **Complexity**: Medium
- **Risk Factors**: The fallback logic must handle the case where ATR is not seeded yet (insufficient candle history)
- **Files**:
  - `src/TradingApp.Application/Trading/Services/StopLossDistanceResolver.cs` - Add `AtrInitial` case to switch expression
- **Success**:
  - `AtrInitial` with valid ATR returns ATR-derived percentage
  - `AtrInitial` with zero/null ATR falls back to `Value` if present
  - `AtrInitial` with zero/null ATR and no `Value` returns null (grid breakdown threshold used)
  - Position sizing inversely scales with ATR (high ATR → smaller position)
- **Dependencies**: Phase 1 (enum exists)

#### Implementation Details

```csharp
// src/TradingApp.Application/Trading/Services/StopLossDistanceResolver.cs — modification
// Add new case to the switch expression, before the default `_` case:

var resolved = stopLossConfig.Type switch
{
    ExitRuleType.FixedPercent when stopLossConfig.Value.HasValue && stopLossConfig.Value.Value > 0m
        => stopLossConfig.Value.Value,

    ExitRuleType.AtrTrailing when atr.HasValue && atr.Value > 0m && anchorPrice > 0m
        => (atr.Value * (stopLossConfig.AtrMultiplier ?? 3m)) / anchorPrice * 100m,

    ExitRuleType.AtrInitial when atr.HasValue && atr.Value > 0m && anchorPrice > 0m
        => (atr.Value * (stopLossConfig.AtrMultiplier ?? 2m)) / anchorPrice * 100m,

    // Fallback: AtrInitial with unavailable ATR uses Value as FixedPercent fallback
    ExitRuleType.AtrInitial when stopLossConfig.Value.HasValue && stopLossConfig.Value.Value > 0m
        => stopLossConfig.Value.Value,

    _ => (decimal?)null,
};
```

> **Note**: Default multiplier for `AtrInitial` is `2m` (per PBI), vs `3m` for `AtrTrailing`. The `AtrMultiplier` field on the config overrides in both cases.

##### Pattern References

- `src/TradingApp.Application/Trading/Services/StopLossDistanceResolver.cs` — existing switch expression with `FixedPercent` and `AtrTrailing` cases

### Task 2.2: Capture `AtrAtEntry` in `GridController` at Grid Deployment {#task-22-capture-atratentry-in-gridcontroller-at-grid-deployment}

Set `gridState.AtrAtEntry` when deploying a new grid with `AtrInitial` stop-loss type. The ATR value is captured from `context.Indicators?.Atr` at the entry candle close and locked for the duration of the position. Clear `AtrAtEntry` at all the same points where `InitialRDollars` is cleared (position close/reset).

- **Complexity**: Medium
- **Risk Factors**: Must clear `AtrAtEntry` at all lifecycle reset points to prevent stale values
- **Files**:
  - `src/TradingApp.Application/Trading/Services/GridController.cs` - Set `AtrAtEntry` at deployment, clear at all close/reset points
- **Success**:
  - `AtrAtEntry` is set to current ATR when `StopLoss.Type == AtrInitial` at deployment
  - `AtrAtEntry` is null when `StopLoss.Type != AtrInitial`
  - `AtrAtEntry` is cleared to null at all the same points as `InitialRDollars`
- **Dependencies**: Task 1.4 (GridState field exists)

#### Implementation Details

```csharp
// src/TradingApp.Application/Trading/Services/GridController.cs — modification

// At grid deployment (near line 183 where InitialRDollars is set):
gridState.InitialRDollars = PositionSizeResolver.ResolveInitialR(config.Risk, context.AccountEquity);
gridState.AtrAtEntry = config.Exit.StopLoss.Type == ExitRuleType.AtrInitial
    ? context.Indicators?.Atr
    : null;

// At every point where InitialRDollars is set to null in GridController (lines 59, 97, 275, 310):
// Add gridState.AtrAtEntry = null; alongside each gridState.InitialRDollars = null;
// Note: SignalController does not clear InitialRDollars or AtrAtEntry — only GridController manages these fields.
```

##### Pattern References

- `src/TradingApp.Application/Trading/Services/GridController.cs` line 183 — `InitialRDollars` set at deployment
- `src/TradingApp.Application/Trading/Services/GridController.cs` lines 59, 97, 275, 310 — `InitialRDollars` cleared at close/reset

### Task 2.3: Add `AtrInitial` Exit Evaluation Branch in `GridController` {#task-23-add-atrinitial-exit-evaluation-branch-in-gridcontroller}

Add a new `isAtrInitial` branch in `GridController.EvaluateExitConditions` that computes the stop price from `AverageEntryPrice - (AtrAtEntry × multiplier)` for longs and triggers when candle close breaches it.

- **Complexity**: Medium
- **Risk Factors**: Must handle long/short direction correctly. Must handle null `AtrAtEntry` gracefully.
- **Files**:
  - `src/TradingApp.Application/Trading/Services/GridController.cs` - Add `isAtrInitial` branch in `EvaluateExitConditions`
- **Success**:
  - `AtrInitial` stop triggers when candle close <= stop price (long)
  - Stop price computed from locked `AtrAtEntry`, not live ATR
  - Lifecycle transitions correctly on trigger
  - `AtrAtEntry` cleared on trigger
- **Dependencies**: Tasks 2.2, 1.1

#### Implementation Details

```csharp
// src/TradingApp.Application/Trading/Services/GridController.cs — modification
// In EvaluateExitConditions, after the isFixedStopLoss block and before the return null:

var isAtrInitial = stopLossConfig.Enabled
    && stopLossConfig.Type == ExitRuleType.AtrInitial
    && gridState.AtrAtEntry.HasValue
    && gridState.AtrAtEntry.Value > 0m;

if (isAtrInitial)
{
    var multiplier = stopLossConfig.AtrMultiplier ?? 2m;
    var isLong = positionState.Size > 0;
    var stopPrice = isLong
        ? positionState.AverageEntryPrice - (gridState.AtrAtEntry!.Value * multiplier)
        : positionState.AverageEntryPrice + (gridState.AtrAtEntry!.Value * multiplier);

    var triggered = isLong
        ? context.CurrentCandle.Close <= stopPrice
        : context.CurrentCandle.Close >= stopPrice;

    if (triggered)
    {
        gridState.Lifecycle = GridLifecycle.Closing;
        gridState.TrailingStopHighWatermark = null;
        gridState.CandlesSinceEntry = 0;
        gridState.InitialRDollars = null;
        gridState.AtrAtEntry = null;

        return new TradingSignal
        {
            SignalType = "TakeProfit",
            Symbol = context.Symbol,
            Reason = $"ATR initial stop triggered (stop: {stopPrice:F2}).",
            Parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["targetPrice"] = context.CurrentCandle.Close,
                ["size"] = Math.Abs(positionState.Size),
                ["orderType"] = OrderType.Market.ToString(),
                ["gridCycleId"] = gridCycleId,
                ["cancellationReason"] = CancellationReason.StopLossTriggered.ToString()
            }
        };
    }
}
```

##### Pattern References

- `src/TradingApp.Application/Trading/Services/GridController.cs` lines 248–312 — existing `isAtrTrailing` and `isFixedStopLoss` branches

### Task 2.4: Add `AtrInitial` Exit Evaluation Branch in `SignalController` {#task-24-add-atrinitial-exit-evaluation-branch-in-signalcontroller}

Mirror the `GridController` `isAtrInitial` branch in `SignalController.EvaluateExitConditions`. `SignalController` returns `IReadOnlyList<TradingSignal>` instead of `TradingSignal?`.

- **Complexity**: Medium
- **Risk Factors**: Must stay in sync with `GridController` logic
- **Files**:
  - `src/TradingApp.Application/Trading/Services/SignalController.cs` - Add `isAtrInitial` branch
- **Success**:
  - `AtrInitial` stop triggers correctly in `SignalController`
  - Same logic and state clearing as `GridController`
- **Dependencies**: Tasks 2.2, 2.3

#### Implementation Details

```csharp
// src/TradingApp.Application/Trading/Services/SignalController.cs — modification
// In EvaluateExitConditions, after the isFixedStopLoss block:

var isAtrInitial = stopLossConfig.Enabled
    && stopLossConfig.Type == ExitRuleType.AtrInitial
    && gridState.AtrAtEntry.HasValue
    && gridState.AtrAtEntry.Value > 0m;

if (isAtrInitial)
{
    var multiplier = stopLossConfig.AtrMultiplier ?? 2m;
    var isLong = positionState.Size > 0;
    var stopPrice = isLong
        ? positionState.AverageEntryPrice - (gridState.AtrAtEntry!.Value * multiplier)
        : positionState.AverageEntryPrice + (gridState.AtrAtEntry!.Value * multiplier);

    var triggered = isLong
        ? context.CurrentCandle.Close <= stopPrice
        : context.CurrentCandle.Close >= stopPrice;

    if (triggered)
    {
        gridState.TrailingStopHighWatermark = null;
        gridState.CandlesSinceEntry = 0;

        return
        [
            new TradingSignal
            {
                SignalType = "TakeProfit",
                Symbol = context.Symbol,
                Reason = $"ATR initial stop triggered (stop: {stopPrice:F2}).",
                Parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["targetPrice"] = context.CurrentCandle.Close,
                    ["size"] = Math.Abs(positionState.Size),
                    ["orderType"] = OrderType.Market.ToString(),
                    ["cancellationReason"] = CancellationReason.StopLossTriggered.ToString(),
                    ["gridCycleId"] = "signal"
                }
            }
        ];
    }
}
```

##### Pattern References

- `src/TradingApp.Application/Trading/Services/SignalController.cs` lines 98–180 — existing `isAtrTrailing` and `isFixedStopLoss` branches

### Task 2.5: Fix `isFixedStopLoss` Guard to Exclude `AtrInitial` {#task-25-fix-isfixedstoploss-guard-to-exclude-atrinitial}

The current `isFixedStopLoss` condition in both `GridController` and `SignalController` is:
```csharp
var isFixedStopLoss = stopLossConfig.Enabled
    && stopLossConfig.Type != ExitRuleType.AtrTrailing
    && stopLossConfig.Value.HasValue;
```

`AtrInitial` has no `Value`, so it won't match `isFixedStopLoss` today. However, the condition is fragile — it catches any non-`AtrTrailing` type with a `Value`. If someone sets `Value` on an `AtrInitial` config (e.g., as the fallback percent), it would incorrectly match `isFixedStopLoss`. Add explicit exclusion.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Application/Trading/Services/GridController.cs` - Update `isFixedStopLoss` guard
  - `src/TradingApp.Application/Trading/Services/SignalController.cs` - Update `isFixedStopLoss` guard
- **Success**:
  - `AtrInitial` with `Value` set (fallback percent) does not match `isFixedStopLoss`
- **Dependencies**: Task 1.1

#### Implementation Details

```csharp
// In both GridController.cs and SignalController.cs:
var isFixedStopLoss = stopLossConfig.Enabled
    && stopLossConfig.Type != ExitRuleType.AtrTrailing
    && stopLossConfig.Type != ExitRuleType.AtrInitial
    && stopLossConfig.Value.HasValue;
```

##### Pattern References

- `src/TradingApp.Application/Trading/Services/GridController.cs` line 245 — current `isFixedStopLoss`
- `src/TradingApp.Application/Trading/Services/SignalController.cs` line 99 — current `isFixedStopLoss`

### Task 2.6: Unit Tests for SL Distance Resolution and Exit Evaluation {#task-26-unit-tests-for-sl-distance-resolution-and-exit-evaluation}

Add comprehensive unit tests covering:
1. `StopLossDistanceResolver` with `AtrInitial` type
2. `GridController` `AtrInitial` exit evaluation (locked ATR, trigger, fallback)
3. Position size varying inversely with ATR

- **Complexity**: Medium
- **Risk Factors**: `GridControllerTests.CreateMarketContext` doesn't currently populate `Indicators.Atr` — must extend or override inline
- **Files**:
  - `tests/TradingApp.Application.Tests/Trading/Services/StopLossDistanceResolverTests.cs` - Add or create test class
  - `tests/TradingApp.Application.Tests/Trading/Services/GridControllerTests.cs` - Add `AtrInitial` test methods
- **Success**:
  - ATR-derived SL distance calculated correctly: `(ATR × multiplier) / price × 100`
  - Fallback to `FixedPercent` `Value` when ATR unavailable
  - ATR initial stop triggers at correct price
  - ATR value locked at entry (doesn't change on subsequent candles)
  - Position size halves when ATR doubles (AC from PBI)
  - All new tests pass
- **Dependencies**: Tasks 2.1–2.5

#### Implementation Details

```csharp
// StopLossDistanceResolverTests — key tests:

[TestMethod]
public void GivenAtrInitialWithAtr500AndMultiplier2AndPrice50000_WhenResolved_ThenReturns2Percent()
{
    var config = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.AtrInitial, AtrMultiplier = 2.0m };

    var result = StopLossDistanceResolver.Resolve(config, atr: 500m, anchorPrice: 50_000m);

    result.Should().Be(2.0m); // (500 * 2) / 50000 * 100 = 2%
}

[TestMethod]
public void GivenAtrInitialWithNoAtrAndFallbackValue_WhenResolved_ThenReturnsFallbackPercent()
{
    var config = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.AtrInitial, AtrMultiplier = 2.0m, Value = 3.0m };

    var result = StopLossDistanceResolver.Resolve(config, atr: 0m, anchorPrice: 50_000m);

    result.Should().Be(3.0m);
}

[TestMethod]
public void GivenAtrInitialWithNoAtrAndNoFallback_WhenResolved_ThenReturnsNull()
{
    var config = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.AtrInitial, AtrMultiplier = 2.0m };

    var result = StopLossDistanceResolver.Resolve(config, atr: null, anchorPrice: 50_000m);

    result.Should().BeNull();
}

// GridControllerTests — key tests:

[TestMethod]
public async Task GivenAtrInitialStopAndPriceBelowStop_WhenProcessed_ThenStopLossTriggered()
{
    // Arrange: AtrAtEntry = 500, multiplier = 2, entry = 50000, stop = 49000
    // Act: candle close = 48900 (below stop)
    // Assert: exit signal emitted with CancellationReason.StopLossTriggered
}

[TestMethod]
public async Task GivenAtrInitialStopAndPriceAboveStop_WhenProcessed_ThenNoExit()
{
    // Arrange: AtrAtEntry = 500, multiplier = 2, entry = 50000, stop = 49000
    // Act: candle close = 49500 (above stop)
    // Assert: no exit signal
}

[TestMethod]
public async Task GivenAtrInitialStopWithLockedAtr_WhenAtrChanges_ThenStopPriceUnchanged()
{
    // Arrange: AtrAtEntry = 500 (locked), live ATR = 800
    // Act: evaluate exit with live ATR different from locked
    // Assert: stop price still uses 500, not 800
}
```

##### Pattern References

- `tests/TradingApp.Application.Tests/Trading/Services/GridControllerTests.cs` — existing test patterns with `DefaultConfig`, `CreateMarketContext` helpers
- `tests/TradingApp.Application.Tests/Trading/Services/PositionSizeResolverTests.cs` — existing tests for `ResolveNotional` with `stopLossPercent`

### Task 2.7: Build and Run Tests {#task-27-build-and-run-tests}

Build and run all affected test projects to verify Phase 2 changes.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (build/test verification)
- **Success**:
  - `dotnet build TradingApp.sln` succeeds
  - `dotnet test tests/TradingApp.Application.Tests --filter "FullyQualifiedName~StopLossDistance|FullyQualifiedName~GridController|FullyQualifiedName~AtrInitial"` passes
  - All existing tests continue to pass
- **Dependencies**: Tasks 2.1–2.6

## Phase Success Criteria

- `StopLossDistanceResolver` returns ATR-derived SL% for `AtrInitial`
- `StopLossDistanceResolver` falls back to `Value` when ATR unavailable
- `GridController` captures `AtrAtEntry` at deployment and uses it for exit evaluation
- `GridController` and `SignalController` trigger `AtrInitial` stop at correct price
- `isFixedStopLoss` guard excludes `AtrInitial`
- Position size inversely scales with ATR through the `StopLossDistanceResolver` → `PositionSizeResolver` chain
- All unit tests pass
