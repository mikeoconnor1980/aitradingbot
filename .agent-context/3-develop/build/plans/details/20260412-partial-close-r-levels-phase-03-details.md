<!-- markdownlint-disable-file -->

# Task Details: Partial Close at R-Levels

## Phase 3: Live Execution

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — sealed classes, async/await, CancellationToken
- `.github/instructions/testing.instructions.md` — MSTest, FluentAssertions ≤ v6, Given_When_Then
- `.agent-context/0-knowledge/02-hyperliquid-integration.md` — Trigger order wire format, reduce-only orders
- `.agent-context/0-knowledge/30-worker-execution-pipeline.md` — Fill callback → protection order lifecycle
- `.agent-context/0-knowledge/10-architecture-decisions.md` — ADR 16: trigger orders not persisted in DB

## Design References

- `ProtectionOrderState` currently tracks one SL and one TP order ID. For partial closes, it needs to track multiple TP order IDs (one per tranche).
- `TriggerOrderManager.PlaceProtectionOrdersAsync` places one TP trigger for the full position size. Must be extended to place N triggers for N tranches.
- After a partial TP fill, the SL trigger order size must be modified to match the remaining position via `ModifyTriggerOrderAsync`.
- `FillProcessor` treats any TP fill as a full position close (transitions lifecycle to Closed). Must be updated to only close when position size reaches zero.
- Hyperliquid supports multiple reduce-only trigger orders simultaneously — no exchange-side limitation.

### Task 3.1: Extend `ProtectionOrderState` for multiple TP trigger orders {#task-31-extend-protectionorderstate-for-multiple-tp-triggers}

Extend `ProtectionOrderState` to track multiple TP trigger order IDs (one per R-level tranche) while maintaining backward compatibility for the single-TP case.

- **Complexity**: Medium
- **Risk Factors**: Must not break existing single-TP codepaths. `HasTakeProfit` check must work for both single and multi-TP.
- **Files**:
  - `src/TradePilot.Application/Trading/Models/ProtectionOrderState.cs` — Add multi-TP tracking
- **Success**:
  - Can track N partial TP order IDs with their associated R-levels
  - `HasTakeProfit` returns `true` when any TP triggers are active
  - Can remove individual TP orders when they fill (by order ID)
  - `Clear()` clears all TP orders
  - `IsProtectionOrderId` returns `true` for any TP order ID in the list
  - Single-TP case still works (backward compatible)

#### Implementation Details

```csharp
// src/TradePilot.Application/Trading/Models/ProtectionOrderState.cs — modification

public sealed class ProtectionOrderState
{
    // ... existing SL fields unchanged ...
    public string? StopLossOrderId { get; set; }
    public decimal? StopLossTriggerPrice { get; set; }

    // REPLACE single TP with list:
    // Keep existing TakeProfitOrderId/TakeProfitTriggerPrice for backward compat
    // or migrate to list-only. Recommended: migrate to list.

    private readonly List<PartialTpOrder> _takeProfitOrders = new();
    public IReadOnlyList<PartialTpOrder> TakeProfitOrders => _takeProfitOrders;

    public bool HasStopLoss => StopLossOrderId is not null;
    public bool HasTakeProfit => _takeProfitOrders.Count > 0;

    public void AddTakeProfitOrder(string orderId, decimal triggerPrice, decimal size, decimal atRMultiple)
    {
        _takeProfitOrders.Add(new PartialTpOrder(orderId, triggerPrice, size, atRMultiple));
    }

    public void RemoveTakeProfitOrder(string orderId)
    {
        _takeProfitOrders.RemoveAll(tp => tp.OrderId == orderId);
    }

    public void Clear()
    {
        StopLossOrderId = null;
        StopLossTriggerPrice = null;
        _takeProfitOrders.Clear();
    }

    // UPDATE: IsProtectionOrderId must check against TP list
    public bool IsProtectionOrderId(string orderId)
    {
        return string.Equals(orderId, StopLossOrderId, StringComparison.Ordinal)
            || _takeProfitOrders.Any(tp => string.Equals(tp.OrderId, orderId, StringComparison.Ordinal));
    }
}

public sealed record PartialTpOrder(
    string OrderId,
    decimal TriggerPrice,
    decimal Size,
    decimal AtRMultiple);
```

##### Pattern References

- `src/TradePilot.Application/Trading/Models/ProtectionOrderState.cs` — existing single-TP structure

### Task 3.2: Extend `TriggerOrderManager` to place partial TP triggers {#task-32-extend-triggerordermanager-for-partial-tp-triggers}

Modify `PlaceProtectionOrdersAsync` to place multiple TP trigger orders when `ExitConfig.PartialCloses` is configured.

- **Complexity**: High
- **Risk Factors**: Must correctly calculate trigger price per tranche. Must handle the case where SL percent changes (ATR trailing) — all TP trigger prices depend on SL distance × R-multiple. Must handle existing single-TP mode when `PartialCloses` is null.
- **Files**:
  - `src/TradePilot.Application/Trading/Services/TriggerOrderManager.cs` — Modify TP placement logic
  - `src/TradePilot.Application/Abstractions/Services/ITriggerOrderManager.cs` — Update interface if signature changes
- **Success**:
  - When `PartialCloses` is configured: places N TP triggers (one per tranche) with fractional sizes
  - When `PartialCloses` is null: existing single-TP logic unchanged
  - Each trigger price = `entryPrice × (1 ± SL% × atRMultiple / 100)`
  - Each trigger size = `totalPositionSize × (closePercent / 100)`
  - All TP order IDs tracked in `ProtectionOrderState`

#### Implementation Details

```csharp
// TriggerOrderManager.cs — modification to PlaceProtectionOrdersAsync

// After SL trigger placement (unchanged), modify TP section:

if (exitConfig.PartialCloses is { Count: > 0 } partialCloses
    && stopLossPercent.HasValue)
{
    var totalSize = Math.Abs(positionState.Size);
    var isLong = positionState.Size > 0;
    var closeSide = isLong ? "sell" : "buy";

    foreach (var tranche in partialCloses.OrderBy(pc => pc.AtRMultiple))
    {
        var trancheSize = totalSize * (tranche.ClosePercent / 100m);
        var triggerPrice = CalculateRMultipleTriggerPrice(
            positionState.AverageEntryPrice,
            stopLossPercent.Value,
            tranche.AtRMultiple,
            isLong);

        var orderId = await _executionEngine.PlaceTriggerOrderAsync(
            positionState.Symbol, closeSide, trancheSize, triggerPrice, "tp");

        protectionOrders.AddTakeProfitOrder(orderId, triggerPrice, trancheSize, tranche.AtRMultiple);
    }
}
else if (exitConfig.TakeProfit.Enabled)
{
    // existing single-TP logic (unchanged)
    // ... existing code ...
}

// Helper (may already exist in part):
private static decimal CalculateRMultipleTriggerPrice(
    decimal entryPrice, decimal stopLossPercent, decimal rMultiple, bool isLong)
{
    var effectivePercent = stopLossPercent * rMultiple;
    return isLong
        ? entryPrice * (1m + effectivePercent / 100m)
        : entryPrice * (1m - effectivePercent / 100m);
}
```

##### Pattern References

- `src/TradePilot.Application/Trading/Services/TriggerOrderManager.cs` — existing `PlaceProtectionOrdersAsync` and `CalculateTakeProfitPrice`

### Task 3.3: Extend `FillProcessor` for partial TP fill handling {#task-33-extend-fillprocessor-for-partial-tp-fills}

Currently, `FillProcessor` treats any TP fill as a full position close (lifecycle → Closed). Modify it to recognize partial TP fills and only close the lifecycle when the position is fully closed.

- **Complexity**: Medium
- **Risk Factors**: Must correctly distinguish partial vs full TP fills. Grid lifecycle transitions must only happen when position is fully exited.
- **Files**:
  - `src/TradePilot.Application/Trading/Services/FillProcessor.cs` — Modify TP fill handling
- **Success**:
  - Partial TP fill: records fill, removes the specific TP order from `ProtectionOrderState`, does NOT transition lifecycle to Closed
  - Final TP fill (position size = 0): transitions lifecycle to Closed
  - SL fill: unchanged (always closes full position)
  - Fill records saved with `TradeType.TakeProfit` for both partial and full

#### Implementation Details

```csharp
// FillProcessor.cs — modify ProcessTakeProfitFill or equivalent

// After processing a TP trigger fill:
protectionOrders.RemoveTakeProfitOrder(fill.OrderId);

// Only close lifecycle if position is fully closed
var remainingPosition = await _executionEngine.QueryPositionAsync(symbol);
if (remainingPosition.Size == 0)
{
    // Transition lifecycle to Closed (existing behavior)
    gridState.TransitionTo(GridLifecycle.Closed);
    // Cancel any remaining TP triggers (safety cleanup)
    // ... existing cleanup ...
}
// else: partial fill — position still open, remaining triggers still active
```

##### Pattern References

- `src/TradePilot.Application/Trading/Services/FillProcessor.cs` — existing `ProcessTakeProfitFill` logic

### Task 3.4: Update `TradingSession` fill callback for SL size adjustment {#task-34-update-tradingsession-fill-callback-for-sl-size-adjustment}

After a partial TP fill, the SL trigger order size must be reduced to match the remaining position. This is critical because Hyperliquid trigger orders have a fixed size — if the SL fires for the original full size after a partial close, it would try to close more than the remaining position.

- **Complexity**: Medium
- **Risk Factors**: Must correctly update the SL trigger order via `ModifyTriggerOrderAsync`. Must handle race conditions between fill callback and next candle evaluation.
- **Files**:
  - `src/TradePilot.Worker/Services/TradingSession.cs` — Modify fill callback
  - `src/TradePilot.Application/Trading/Services/TriggerOrderManager.cs` — Add `UpdateStopLossSizeAsync` method or extend `UpdateProtectionOrdersAsync`
- **Success**:
  - After partial TP fill, SL trigger order size is updated to remaining position size
  - `ModifyTriggerOrderAsync` called with new size and existing SL trigger price
  - Works correctly for multiple partial fills in sequence

#### Implementation Details

```csharp
// TradingSession.cs — in the fill callback, after QueryPositionStateAsync:

if (fill.TradeType == TradeType.TakeProfit && positionState.IsOpen)
{
    // This is a partial TP fill — position still open
    // Update SL trigger order size to match remaining position
    await _triggerOrderManager.UpdateStopLossSizeAsync(
        positionState,
        gridState.ProtectionOrders);
}

// TriggerOrderManager.cs — new method:
public async Task UpdateStopLossSizeAsync(
    PositionState positionState,
    ProtectionOrderState protectionOrders)
{
    if (!protectionOrders.HasStopLoss) return;

    var remainingSize = Math.Abs(positionState.Size);
    await _executionEngine.ModifyTriggerOrderAsync(
        protectionOrders.StopLossOrderId!,
        positionState.Symbol,
        remainingSize,
        protectionOrders.StopLossTriggerPrice!.Value);
}
```

##### Pattern References

- `src/TradePilot.Worker/Services/TradingSession.cs` — existing fill callback
- `src/TradePilot.Application/Trading/Services/TriggerOrderManager.cs` — existing `ModifyTriggerOrderAsync` usage

### Task 3.5: Add unit tests for live partial close execution {#task-35-add-unit-tests-for-live-partial-close-execution}

Add tests for `TriggerOrderManager`, `FillProcessor`, and related components.

- **Complexity**: Medium
- **Risk Factors**: None
- **Files**:
  - `tests/TradePilot.Application.Tests/Trading/Services/TriggerOrderManagerTests.cs` — Add or create tests
  - `tests/TradePilot.Application.Tests/Trading/Services/FillProcessorTests.cs` — Add or create tests
  - `tests/TradePilot.Api.Tests/Services/HyperliquidOrderServiceTests.cs` — Add partial trigger tests if needed
- **Success**:
  - Test: placing 3 partial TP triggers results in 3 `PlaceTriggerOrderAsync` calls with correct sizes
  - Test: removing a filled TP order from `ProtectionOrderState` by order ID
  - Test: SL size updated after partial TP fill
  - Test: lifecycle only transitions to Closed when position fully exits
  - Test: null `PartialCloses` falls back to single-TP behavior
  - All tests pass

#### Implementation Details

```csharp
[TestMethod]
public async Task GivenPartialCloses_WhenPlacingProtection_ThenPlacesMultipleTpTriggers()
{
    // Arrange
    var exitConfig = new ExitConfig
    {
        StopLoss = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = 2m },
        PartialCloses = new[]
        {
            new PartialCloseLevel { AtRMultiple = 1m, ClosePercent = 25 },
            new PartialCloseLevel { AtRMultiple = 2m, ClosePercent = 25 },
            new PartialCloseLevel { AtRMultiple = 3m, ClosePercent = 50 },
        }
    };
    var positionState = new PositionState { Symbol = "BTC", Size = 100m, AverageEntryPrice = 50000m };

    // Act
    await _sut.PlaceProtectionOrdersAsync(positionState, exitConfig, lastContext, protectionOrders);

    // Assert
    _mockExecutionEngine.Verify(
        e => e.PlaceTriggerOrderAsync("BTC", "sell", 25m, 51000m, "tp"), Times.Once);
    _mockExecutionEngine.Verify(
        e => e.PlaceTriggerOrderAsync("BTC", "sell", 25m, 52000m, "tp"), Times.Once);
    _mockExecutionEngine.Verify(
        e => e.PlaceTriggerOrderAsync("BTC", "sell", 50m, 53000m, "tp"), Times.Once);
    protectionOrders.TakeProfitOrders.Should().HaveCount(3);
}
```

##### Pattern References

- `tests/TradePilot.Api.Tests/Services/HyperliquidOrderServiceTests.cs` — existing trigger order test patterns

### Task 3.6: Run architecture tests {#task-36-run-architecture-tests}

Run the solution's architecture tests to ensure no layer violations were introduced.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (test execution only)
- **Success**:
  - All architecture tests pass
  - All existing tests continue to pass

## Phase Success Criteria

- Multiple TP trigger orders are placed on Hyperliquid for each tranche
- `ProtectionOrderState` correctly tracks multiple TP orders
- SL trigger order size is updated after each partial fill
- `FillProcessor` only closes lifecycle when position is fully exited
- All new and existing tests pass
