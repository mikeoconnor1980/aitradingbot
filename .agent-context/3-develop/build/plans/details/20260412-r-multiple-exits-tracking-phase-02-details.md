<!-- markdownlint-disable-file -->

# Task Details: R-Multiple Exit Types & Trade Tracking

## Phase 2: R-Multiple TP Price Calculation

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — sealed classes, guards
- `.github/instructions/testing.instructions.md` — MSTest, FluentAssertions, Given_When_Then
- `.agent-context/0-knowledge/33-risk-management-and-trade-sizing.md` — R-multiple TP formula: `tpPrice = entry ± (slDistance × rMultiple)`

## Design References

**R-Multiple TP Price Formula:**
- Long: `tpPrice = entryPrice × (1 + (slPercent / 100 × rMultiple))`
- Short: `tpPrice = entryPrice × (1 - (slPercent / 100 × rMultiple))`
- Example: entry=$50,000, SL=2%, R-multiple=2R → TP = $50,000 × 1.04 = $52,000

### Task 2.1: Extend `CalculateTakeProfitPrice` for RMultiple {#task-21-extend-calculatetakeprofitprice-for-rmultiple}

Extend `TriggerOrderManager.CalculateTakeProfitPrice` to handle `ExitRuleType.RMultiple`. The method needs an additional `stopLossPercent` parameter to compute the SL distance for the R-multiple conversion.

- **Complexity**: Medium
- **Risk Factors**: Method signature change — callers must be updated
- **Files**:
  - `src/TradingApp.Application/Trading/Services/TriggerOrderManager.cs` — extend `CalculateTakeProfitPrice`
- **Success**:
  - RMultiple TP returns correct price for long and short positions
  - FixedPercent TP continues to work unchanged (ignores stopLossPercent)
  - Returns null if stopLossPercent is null/zero when type is RMultiple
- **Dependencies**: Phase 1

#### Implementation Details

```csharp
// src/TradingApp.Application/Trading/Services/TriggerOrderManager.cs — modification
// Update CalculateTakeProfitPrice signature and add RMultiple branch:

internal static decimal? CalculateTakeProfitPrice(
    PositionState positionState,
    ExitRuleConfig takeProfitConfig,
    decimal? stopLossPercent = null)
{
    if (!takeProfitConfig.Enabled || !takeProfitConfig.Value.HasValue || !positionState.IsOpen)
    {
        return null;
    }

    var isLong = positionState.Size > 0;

    if (takeProfitConfig.Type == ExitRuleType.RMultiple)
    {
        if (!stopLossPercent.HasValue || stopLossPercent.Value <= 0m)
        {
            return null;
        }

        var rMultiple = Math.Abs(takeProfitConfig.Value.Value);
        var effectivePercent = stopLossPercent.Value * rMultiple;

        return isLong
            ? positionState.AverageEntryPrice * (1m + (effectivePercent / 100m))
            : positionState.AverageEntryPrice * (1m - (effectivePercent / 100m));
    }

    var percent = Math.Abs(takeProfitConfig.Value.Value);

    return isLong
        ? positionState.AverageEntryPrice * (1m + (percent / 100m))
        : positionState.AverageEntryPrice * (1m - (percent / 100m));
}
```

Also update the two call sites in `PlaceProtectionOrdersAsync` and `UpdateProtectionOrdersAsync` to pass the stopLossPercent. Use the existing `StopLossDistanceResolver.Resolve()` to avoid duplicating SL resolution logic:

```csharp
// In PlaceProtectionOrdersAsync / UpdateProtectionOrdersAsync:
// Resolve stopLossPercent for RMultiple TP using the existing resolver
decimal? slPercentForTp = null;
if (exitConfig.TakeProfit.Type == ExitRuleType.RMultiple)
{
    slPercentForTp = StopLossDistanceResolver.Resolve(
        exitConfig.StopLoss,
        context.Indicators?.Atr,
        positionState.AverageEntryPrice);
}

var tpPrice = CalculateTakeProfitPrice(positionState, exitConfig.TakeProfit, slPercentForTp);
```

##### Pattern References

- `src/TradingApp.Application/Trading/Services/TriggerOrderManager.cs` — existing `CalculateTakeProfitPrice` (percent-only)
- `src/TradingApp.Application/Trading/Services/StopLossDistanceResolver.cs` — SL % resolution logic pattern

### Task 2.2: Update GridController inline TP calculation for RMultiple {#task-22-update-gridcontroller-inline-tp-calculation}

Update the inline TP price calculation in `GridController.ProcessAsync` (partially-filled and fully-filled branches) to support `ExitRuleType.RMultiple`. The SL distance % is resolved using `StopLossDistanceResolver.Resolve`.

- **Complexity**: Medium
- **Risk Factors**: Two TP evaluation branches must be updated consistently
- **Files**:
  - `src/TradingApp.Application/Trading/Services/GridController.cs` — update TP evaluation in ProcessAsync
- **Success**:
  - Partially-filled grid with RMultiple TP evaluates correctly
  - Fully-filled grid with RMultiple TP places correct limit order
  - Non-RMultiple TP types unaffected
- **Dependencies**: Task 2.1

#### Implementation Details

Extract a helper to compute the TP trigger price from the exit config:

```csharp
// src/TradingApp.Application/Trading/Services/GridController.cs — modification
// Add private static helper method:

// NOTE: Currently handles long positions only (uses 1m + ...), matching the existing
// GridController TP evaluation pattern. Short-side support is a future enhancement.
private static decimal ComputeTakeProfitTrigger(
    decimal averageEntryPrice,
    ExitConfig exitConfig,
    decimal? atr,
    decimal? gridBreakdownThreshold)
{
    var tpConfig = exitConfig.TakeProfit;
    if (!tpConfig.Enabled || !tpConfig.Value.HasValue)
    {
        return 0m;
    }

    if (tpConfig.Type == ExitRuleType.RMultiple)
    {
        var slPercent = StopLossDistanceResolver.Resolve(
            exitConfig.StopLoss,
            atr,
            averageEntryPrice,
            gridBreakdownThreshold);

        if (!slPercent.HasValue || slPercent.Value <= 0m)
        {
            return 0m;
        }

        var rMultiple = Math.Abs(tpConfig.Value.Value);
        return averageEntryPrice * (1m + (slPercent.Value * rMultiple / 100m));
    }

    var percent = Math.Abs(tpConfig.Value.Value);
    return averageEntryPrice * (1m + (percent / 100m));
}
```

Then replace the inline TP calculations in both branches:

```csharp
// Partially-filled branch (around line 48-53):
// BEFORE:
// var takeProfitPercent = config.Exit.TakeProfit.Enabled && config.Exit.TakeProfit.Value.HasValue
//     ? Math.Abs(config.Exit.TakeProfit.Value.Value) : 0m;
// var takeProfitTrigger = positionState.AverageEntryPrice * (1m + (takeProfitPercent / 100m));
// AFTER:
var takeProfitTrigger = ComputeTakeProfitTrigger(
    positionState.AverageEntryPrice,
    config.Exit,
    context.Indicators?.Atr,
    config.Grid?.BreakdownThreshold);

if (takeProfitTrigger > 0m && context.CurrentCandle.Close >= takeProfitTrigger)
{
    // ... existing TP signal emission ...
}

// Fully-filled branch (around line 81-84):
// Same replacement pattern:
var tpTrigger = ComputeTakeProfitTrigger(
    positionState.AverageEntryPrice,
    config.Exit,
    context.Indicators?.Atr,
    config.Grid?.BreakdownThreshold);
```

##### Pattern References

- `src/TradingApp.Application/Trading/Services/GridController.cs` — existing inline TP calculation (lines 48-53, 81-84)
- `src/TradingApp.Application/Trading/Services/StopLossDistanceResolver.cs` — `Resolve()` method

### Task 2.3: Unit tests for R-multiple TP price calculation {#task-23-unit-tests-for-r-multiple-tp-price-calculation}

Write unit tests for `CalculateTakeProfitPrice` with `RMultiple` type.

- **Complexity**: Medium
- **Risk Factors**: None
- **Files**:
  - `tests/TradingApp.Application.Tests/Trading/Services/TriggerOrderManagerTests.cs` — add RMultiple TP test cases
- **Success**:
  - Long position + 2R target + 2% SL → TP at entry × 1.04
  - Short position + 3R target + 2% SL → TP at entry × 0.94
  - RMultiple with null stopLossPercent → returns null
  - RMultiple with zero stopLossPercent → returns null
  - Existing FixedPercent tests unaffected
- **Dependencies**: Task 2.1

#### Implementation Details

```csharp
// tests/TradingApp.Application.Tests/Trading/Services/TriggerOrderManagerTests.cs — modification
// Add new test section:

// ──── R-Multiple Take Profit ────

[TestMethod]
public void GivenLongPosition_WhenCalculateRMultipleTP_ThenReturnsCorrectPrice()
{
    // Arrange
    var position = CreateLongPosition(entryPrice: 50_000m);
    var tpConfig = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.RMultiple, Value = 2m };

    // Act
    var result = TriggerOrderManager.CalculateTakeProfitPrice(position, tpConfig, stopLossPercent: 2m);

    // Assert
    result.Should().Be(52_000m); // 50000 * (1 + 2% * 2) = 50000 * 1.04
}

[TestMethod]
public void GivenShortPosition_WhenCalculateRMultipleTP_ThenReturnsCorrectPrice()
{
    // Arrange
    var position = CreateShortPosition(entryPrice: 50_000m);
    var tpConfig = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.RMultiple, Value = 3m };

    // Act
    var result = TriggerOrderManager.CalculateTakeProfitPrice(position, tpConfig, stopLossPercent: 2m);

    // Assert
    result.Should().Be(47_000m); // 50000 * (1 - 2% * 3) = 50000 * 0.94
}

[TestMethod]
public void GivenRMultipleTP_WhenStopLossPercentNull_ThenReturnsNull()
{
    // Arrange
    var position = CreateLongPosition(entryPrice: 50_000m);
    var tpConfig = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.RMultiple, Value = 2m };

    // Act
    var result = TriggerOrderManager.CalculateTakeProfitPrice(position, tpConfig, stopLossPercent: null);

    // Assert
    result.Should().BeNull();
}

[TestMethod]
public void GivenRMultipleTP_WhenStopLossPercentZero_ThenReturnsNull()
{
    // Arrange
    var position = CreateLongPosition(entryPrice: 50_000m);
    var tpConfig = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.RMultiple, Value = 2m };

    // Act
    var result = TriggerOrderManager.CalculateTakeProfitPrice(position, tpConfig, stopLossPercent: 0m);

    // Assert
    result.Should().BeNull();
}
```

##### Pattern References

- `tests/TradingApp.Application.Tests/Trading/Services/TriggerOrderManagerTests.cs` — existing `GivenLongPosition_WhenCalculateFixedPercentTP_*` test pattern, `CreateLongPosition`/`CreateShortPosition` factory helpers

### Task 2.4: Build and verify {#task-24-build-and-verify}

Build solution and run all tests.

- **Complexity**: Low
- **Risk Factors**: Signature change in `CalculateTakeProfitPrice` may require updating call sites
- **Files**: None
- **Success**:
  - `dotnet build TradingApp.sln` succeeds
  - `dotnet test TradingApp.sln` — all tests pass
- **Dependencies**: Task 2.3

## Phase Success Criteria

- `CalculateTakeProfitPrice` handles `RMultiple` type using SL distance
- GridController TP evaluation supports RMultiple for both partially-filled and fully-filled grids
- Long/short R-multiple TP prices match PBI acceptance criteria
- All existing FixedPercent and AtrTrailing tests continue to pass
