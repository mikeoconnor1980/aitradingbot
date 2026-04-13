<!-- markdownlint-disable-file -->

# Task Details: Volatility-Scaled Initial Stop Loss (ATR-Based)

## Phase 3: Trigger Order Management

## Standards and Knowledge References

- **csharp.instructions.md**: `internal static` for pure calculation methods, `async Task` for async methods with `CancellationToken`.
- **testing.instructions.md**: MSTest + FluentAssertions v6 + Moq. `GivenX_WhenY_ThenZ` naming.
- **31-atr-calculation.md**: ATR populated via `IncrementalAtr(14)` in context builders, available as `context.Indicators?.Atr`.

### Task 3.1: Add `AtrInitial` Case to `CalculateStopLossPrice` {#task-31-add-atrinitial-case-to-calculatestoplossprice}

Add an `AtrInitial` branch in `TriggerOrderManager.CalculateStopLossPrice` that uses `positionState.AverageEntryPrice` as the reference price (not candle high like `AtrTrailing`). This places the exchange-native stop-loss trigger at the ATR-derived price.

- **Complexity**: Medium
- **Risk Factors**: Must use entry price as reference, not candle high. Must handle short positions correctly.
- **Files**:
  - `src/TradingApp.Application/Trading/Services/TriggerOrderManager.cs` - Add `AtrInitial` case in `CalculateStopLossPrice`
- **Success**:
  - Long: SL price = `entryPrice - (ATR × multiplier)`
  - Short: SL price = `entryPrice + (ATR × multiplier)`
  - Returns null when ATR <= 0
- **Dependencies**: Phase 1 (enum exists)

#### Implementation Details

```csharp
// src/TradingApp.Application/Trading/Services/TriggerOrderManager.cs — modification
// In CalculateStopLossPrice, add after the AtrTrailing block (before the Value fallback):

if (stopLossConfig.Type == ExitRuleType.AtrInitial)
{
    var atr = context.Indicators?.Atr ?? 0m;
    var multiplier = stopLossConfig.AtrMultiplier ?? 2m;

    if (atr <= 0m)
    {
        // Fallback: use Value as fixed percent if available
        if (stopLossConfig.Value.HasValue)
        {
            var percent = Math.Abs(stopLossConfig.Value.Value);
            return isLong
                ? positionState.AverageEntryPrice * (1m - (percent / 100m))
                : positionState.AverageEntryPrice * (1m + (percent / 100m));
        }

        return null;
    }

    // AtrInitial anchors to entry price (not candle high like AtrTrailing)
    var entryPrice = positionState.AverageEntryPrice;
    return isLong
        ? entryPrice - (atr * multiplier)
        : entryPrice + (atr * multiplier);
}
```

##### Pattern References

- `src/TradingApp.Application/Trading/Services/TriggerOrderManager.cs` lines 152–170 — existing `AtrTrailing` branch uses `context.CurrentCandle.High` as reference

### Task 3.2: Skip SL Update for `AtrInitial` in `UpdateProtectionOrdersAsync` {#task-32-skip-sl-update-for-atrinitial-in-updateprotectionordersasync}

For `AtrInitial`, the stop-loss price is locked at entry and should NOT be updated on subsequent candles. Add a guard to skip the SL modification when `StopLoss.Type == AtrInitial`.

- **Complexity**: Low
- **Risk Factors**: None — simple guard condition
- **Files**:
  - `src/TradingApp.Application/Trading/Services/TriggerOrderManager.cs` - Add type guard in `UpdateProtectionOrdersAsync`
- **Success**:
  - `AtrInitial` SL trigger is not modified after initial placement
  - `AtrTrailing` and `FixedPercent` updates continue to work
- **Dependencies**: Task 1.1

#### Implementation Details

```csharp
// src/TradingApp.Application/Trading/Services/TriggerOrderManager.cs — modification
// In UpdateProtectionOrdersAsync, change the SL update guard (around line 92):

// Before:
if (exitConfig.StopLoss.Enabled && protectionState.HasStopLoss)

// After:
if (exitConfig.StopLoss.Enabled && protectionState.HasStopLoss
    && exitConfig.StopLoss.Type != ExitRuleType.AtrInitial)
```

##### Pattern References

- `src/TradingApp.Application/Trading/Services/TriggerOrderManager.cs` lines 92–102 — existing SL update block

### Task 3.3: Unit Tests for Trigger Order Management {#task-33-unit-tests-for-trigger-order-management}

Add tests for `CalculateStopLossPrice` with `AtrInitial` type and verify `UpdateProtectionOrdersAsync` skips SL updates for `AtrInitial`.

- **Complexity**: Medium
- **Risk Factors**: None — follows existing test patterns exactly
- **Files**:
  - `tests/TradingApp.Application.Tests/Trading/Services/TriggerOrderManagerTests.cs` - Add test methods
- **Success**:
  - Long `AtrInitial` SL price calculated correctly from entry price
  - Short `AtrInitial` SL price calculated correctly from entry price
  - Fallback to Value when ATR unavailable
  - `UpdateProtectionOrdersAsync` does not modify SL for `AtrInitial`
  - All tests pass
- **Dependencies**: Tasks 3.1–3.2

#### Implementation Details

```csharp
// tests/TradingApp.Application.Tests/Trading/Services/TriggerOrderManagerTests.cs

[TestMethod]
public void GivenLongPosition_WhenCalculateAtrInitialSL_ThenUsesEntryMinusAtrMultiple()
{
    var position = CreateLongPosition(entryPrice: 50_000m);
    var config = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.AtrInitial, AtrMultiplier = 2m };
    var context = CreateContext(atr: 500m);

    var result = TriggerOrderManager.CalculateStopLossPrice(position, config, context);

    result.Should().Be(49_000m); // 50000 - (500 * 2)
}

[TestMethod]
public void GivenShortPosition_WhenCalculateAtrInitialSL_ThenUsesEntryPlusAtrMultiple()
{
    var position = CreateShortPosition(entryPrice: 50_000m);
    var config = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.AtrInitial, AtrMultiplier = 2m };
    var context = CreateContext(atr: 500m);

    var result = TriggerOrderManager.CalculateStopLossPrice(position, config, context);

    result.Should().Be(51_000m); // 50000 + (500 * 2)
}

[TestMethod]
public void GivenAtrInitialWithZeroAtrAndFallbackValue_WhenCalculateSL_ThenUsesFixedPercent()
{
    var position = CreateLongPosition(entryPrice: 50_000m);
    var config = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.AtrInitial, AtrMultiplier = 2m, Value = 2m };
    var context = CreateContext(atr: 0m);

    var result = TriggerOrderManager.CalculateStopLossPrice(position, config, context);

    result.Should().Be(49_000m); // 50000 * (1 - 0.02) = 49000
}

[TestMethod]
public void GivenAtrInitialWithZeroAtrAndNoFallback_WhenCalculateSL_ThenReturnsNull()
{
    var position = CreateLongPosition(entryPrice: 50_000m);
    var config = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.AtrInitial, AtrMultiplier = 2m };
    var context = CreateContext(atr: 0m);

    var result = TriggerOrderManager.CalculateStopLossPrice(position, config, context);

    result.Should().BeNull();
}

[TestMethod]
public async Task GivenAtrInitialStop_WhenUpdateProtectionOrders_ThenStopLossNotModified()
{
    // Arrange: configure AtrInitial stop, set up protectionState with existing SL
    // Act: call UpdateProtectionOrdersAsync
    // Assert: ModifyTriggerAsync NOT called for SL (Verify Times.Never)
}
```

##### Pattern References

- `tests/TradingApp.Application.Tests/Trading/Services/TriggerOrderManagerTests.cs` — existing `CreateLongPosition`, `CreateShortPosition`, `CreateContext`, `CreateExitConfig` helpers

### Task 3.4: Build and Run Tests {#task-34-build-and-run-tests}

Build and run trigger order manager tests.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (build/test verification)
- **Success**:
  - `dotnet build TradingApp.sln` succeeds
  - `dotnet test tests/TradingApp.Application.Tests --filter "FullyQualifiedName~TriggerOrderManager"` passes
  - All existing tests continue to pass
- **Dependencies**: Tasks 3.1–3.3

## Phase Success Criteria

- `CalculateStopLossPrice` returns correct ATR-derived price for `AtrInitial` (long and short)
- `CalculateStopLossPrice` falls back to `Value` percent when ATR unavailable
- `UpdateProtectionOrdersAsync` does not modify SL trigger for `AtrInitial`
- All trigger order manager unit tests pass
