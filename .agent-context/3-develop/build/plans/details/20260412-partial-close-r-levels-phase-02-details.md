<!-- markdownlint-disable-file -->

# Task Details: Partial Close at R-Levels

## Phase 2: Backtest Simulation

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — sealed classes, async patterns
- `.github/instructions/testing.instructions.md` — MSTest, FluentAssertions ≤ v6, Given_When_Then naming
- `.agent-context/0-knowledge/18-backtesting-architecture.md` — Backtest loop, SimulatedExecutionEngine, trade pairing
- `.agent-context/0-knowledge/33-risk-management-and-trade-sizing.md` — R-multiple partial close concept

## Design References

- `SimulatedExecutionEngine.TryProcessProtectionOrLiquidation` currently processes only one protection fill per candle and returns early. Must be extended to process all matching TP triggers.
- `BacktestPositionManager` translates signals → simulated orders. Currently places one full-size TP trigger. Must place multiple partial TP triggers from `ExitConfig.PartialCloses`.
- `BacktestRunner.CloseCompatibleTrades` already handles partial fills (FIFO). The partial-size matching is structurally sound — but the MFE/MAE tracker assignment for split trades needs validation.
- R-multiple result per partial close: `pnl_of_tranche / InitialRDollars` is correct because `InitialRDollars` represents 1R for the full position.

### Task 2.1: Extend `SimulatedExecutionEngine` to process multiple TP triggers per candle {#task-21-extend-simulatedexecutionengine-for-multiple-tp-triggers}

Modify `TryProcessProtectionOrLiquidation` to process ALL matching TP trigger orders on a candle, not just the first one. SL still takes priority (processes first). Multiple TP triggers at different R-levels may fire on the same candle if the price range covers multiple levels.

- **Complexity**: High
- **Risk Factors**: Order of fill processing matters — lower R-levels must fill before higher ones on the same candle. Position size must reduce correctly after each partial fill.
- **Files**:
  - `src/TradePilot.Application/Backtesting/Services/SimulatedExecutionEngine.cs` — Modify `TryProcessProtectionOrLiquidation`
- **Success**:
  - SL triggers still take priority over TP triggers
  - All TP triggers whose conditions are met on a candle fire (not just the first)
  - TP triggers fire in ascending price order (for longs) / descending order (for shorts) to correctly sequence partial fills
  - Position size decrements correctly after each partial fill
  - Fills list returns all partial fills for the candle

#### Implementation Details

```csharp
// SimulatedExecutionEngine.cs — modification to TryProcessProtectionOrLiquidation
// Current: returns single SimulatedFill? on first match
// New: returns List<SimulatedFill> with all matching triggers

// Pseudocode for the change:
private List<SimulatedFill> ProcessProtectionOrders(Candle candle)
{
    var fills = new List<SimulatedFill>();

    // 1. Check liquidation first (unchanged)
    // 2. Check SL — if SL fires, cancel all TP triggers and return (position is fully closed)

    // 3. Collect all TP triggers that match the candle
    var matchingTpTriggers = _openOrders
        .Where(o => o.TriggerPrice.HasValue && o.CloseReason == CancellationReason.TakeProfitTriggered)
        .Where(o => IsTriggered(o, candle))
        .OrderBy(o => o.TriggerPrice) // ascending for longs (fill lower R first)
        .ToList();

    foreach (var trigger in matchingTpTriggers)
    {
        if (_position.Size == 0) break; // fully closed by earlier tranches

        var fillSize = Math.Min(Math.Abs(trigger.Size), Math.Abs(_position.Size));
        var fill = CreateFill(trigger, candle, fillSize);
        UpdatePositionForSell(fill);
        _openOrders.Remove(trigger);
        fills.Add(fill);
    }

    // 4. If position fully closed, cancel remaining TP triggers
    if (_position.Size == 0)
    {
        _openOrders.RemoveAll(o => o.TriggerPrice.HasValue);
    }

    return fills;
}
```

> **Note**: Pseudocode above is illustrative — adapt to actual `SimulatedExecutionEngine` internal field names and patterns.

##### Pattern References

- `src/TradePilot.Application/Backtesting/Services/SimulatedExecutionEngine.cs` — existing `TryProcessProtectionOrLiquidation` method (single-fill version)

### Task 2.2: Extend `BacktestPositionManager` to place partial TP triggers {#task-22-extend-backtestpositionmanager-to-place-partial-tp-triggers}

When a signal-mode entry fills and `ExitConfig.PartialCloses` is configured, place one TP trigger order per tranche with fractional size instead of a single full-size TP.

- **Complexity**: Medium
- **Risk Factors**: Must correctly calculate trigger price per tranche using existing R-multiple formula. Must handle the case where `PartialCloses` sums to < 100 (no TP trigger for the remainder — managed by SL/trailing only).
- **Files**:
  - `src/TradePilot.Application/Trading/Services/BacktestPositionManager.cs` — Modify TP trigger placement logic
- **Success**:
  - When `PartialCloses` is configured, places N triggers (one per tranche) with fractional sizes
  - Each tranche trigger price uses `CalculateTakeProfitPrice` with the tranche's `AtRMultiple`
  - When `PartialCloses` is null/empty, existing single-TP logic is unchanged
  - SL trigger placement is unchanged (always full remaining size, re-sized on each fill in Phase 3 live only — in backtest SL is simulated differently)

#### Implementation Details

```csharp
// BacktestPositionManager — modification to the TP placement section after entry fill

if (exitConfig.PartialCloses is { Count: > 0 } partialCloses)
{
    var totalSize = Math.Abs(positionState.Size);
    foreach (var tranche in partialCloses.OrderBy(pc => pc.AtRMultiple))
    {
        var trancheSize = totalSize * (tranche.ClosePercent / 100m);
        var triggerPrice = CalculateRMultipleTriggerPrice(
            positionState.AverageEntryPrice,
            stopLossPercent,
            tranche.AtRMultiple,
            isLong);
        await _executionEngine.PlaceTriggerOrderAsync(
            symbol, closeSide, trancheSize, triggerPrice, "tp");
    }
}
else
{
    // existing single-TP logic unchanged
}
```

##### Pattern References

- `src/TradePilot.Application/Trading/Services/BacktestPositionManager.cs` — existing TP placement after signal entry
- `src/TradePilot.Application/Trading/Services/TriggerOrderManager.cs` — `CalculateTakeProfitPrice` for `RMultiple` type

### Task 2.3: Extend `BacktestRunner` R-metric tracking for partial closes {#task-23-extend-backtestrunner-r-metric-tracking-for-partial-closes}

Ensure `RecordFill` and `CloseCompatibleTrades` correctly handle partial TP fills from R-level tranches. Each partial close should be recorded as a separate trade close with its own R-multiple result.

- **Complexity**: Medium
- **Risk Factors**: FIFO trade pairing in `CloseCompatibleTrades` already supports partial fills. The main risk is MFE/MAE tracker management for split trades.
- **Files**:
  - `src/TradePilot.Application/Backtesting/Services/BacktestRunner.cs` — Review and extend `RecordFill` / `CloseCompatibleTrades` if needed
- **Success**:
  - Partial TP fills correctly pair with open trades using FIFO partial matching
  - R-multiple result for each tranche = `tranche_pnl / InitialRDollars` (correct since InitialR is for the full position)
  - MFE/MAE tracking continues correctly for the remaining open portion
  - Total blended R-multiple across all tranches is mathematically correct

#### Implementation Details

`CloseCompatibleTrades` already handles partial fills. The key change is ensuring partial TP fills carry `TradeType.TakeProfit` and `CancellationReason.TakeProfitTriggered` so they route correctly through `RecordFill`. Verify and add `TradeType.PartialTakeProfit` if differentiation is needed for reporting.

Reuse the existing `TradeType.TakeProfit` for both full and partial closes. No new enum value needed — the fill size and remaining position size are sufficient to distinguish partial from full closes in reporting.

##### Pattern References

- `src/TradePilot.Application/Backtesting/Services/BacktestRunner.cs` — `CloseCompatibleTrades` FIFO partial matching

### Task 2.4: Add unit tests for backtest partial close simulation {#task-24-add-unit-tests-for-backtest-partial-close-simulation}

Add tests to `SimulatedExecutionEngineTests` and `BacktestRunnerTests` covering partial close scenarios.

- **Complexity**: Medium
- **Risk Factors**: None
- **Files**:
  - `tests/TradePilot.Application.Tests/Backtesting/Services/SimulatedExecutionEngineTests.cs` — Add tests
  - `tests/TradePilot.Application.Tests/Backtesting/Services/BacktestRunnerTests.cs` — Add tests
- **Success**:
  - Test: multiple TP triggers at different R-levels all fire when candle covers all levels
  - Test: partial fills reduce position size correctly in simulation
  - Test: SL takes priority over TP triggers on same candle
  - Test: R-multiple result per tranche is correct (tranche_pnl / InitialR)
  - Test: blended R-multiple across all tranches matches expected value
  - Test: MFE/MAE tracking works with partial closes
  - All tests pass

#### Implementation Details

```csharp
// SimulatedExecutionEngineTests — new tests

[TestMethod]
public async Task GivenMultiplePartialTpTriggers_WhenCandleCoversAllLevels_ThenAllFire()
{
    // Arrange — long position, 3 TP triggers at 1R ($51k), 2R ($52k), 3R ($53k)
    await _sut.PlaceOrderAsync("BTC", "buy", "market", null, 100m);
    _sut.ProcessCandle(new Candle { Close = 50000m, Low = 49000m, High = 51000m }); // entry fill

    await _sut.PlaceTriggerOrderAsync("BTC", "sell", 25m, 51000m, "tp");  // 1R
    await _sut.PlaceTriggerOrderAsync("BTC", "sell", 25m, 52000m, "tp");  // 2R
    await _sut.PlaceTriggerOrderAsync("BTC", "sell", 50m, 53000m, "tp");  // 3R

    // Act — candle covers all 3 levels
    var fills = _sut.ProcessCandle(new Candle { Open = 50500m, High = 54000m, Low = 50000m, Close = 53500m });

    // Assert
    fills.Should().HaveCount(3);
    fills[0].Size.Should().Be(25m); // 1R tranche
    fills[1].Size.Should().Be(25m); // 2R tranche
    fills[2].Size.Should().Be(50m); // 3R tranche
}

[TestMethod]
public async Task GivenPartialTpAndSl_WhenSlTriggeredFirst_ThenAllTpCancelled()
{
    // SL takes priority — all TP triggers should be cancelled
    // ... setup and assert
}
```

##### Pattern References

- `tests/TradePilot.Application.Tests/Backtesting/Services/SimulatedExecutionEngineTests.cs` — existing trigger fill test patterns

### Task 2.5: Run architecture tests {#task-25-run-architecture-tests}

Run the solution's architecture tests to ensure no layer violations were introduced.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (test execution only)
- **Success**:
  - All architecture tests pass
  - All existing tests continue to pass

## Phase Success Criteria

- Backtest correctly simulates multiple TP trigger orders firing on same or different candles
- Partial TP fills reduce position size correctly in simulation
- R-multiple tracking is accurate for partial closes
- SL priority is maintained
- All new and existing tests pass
