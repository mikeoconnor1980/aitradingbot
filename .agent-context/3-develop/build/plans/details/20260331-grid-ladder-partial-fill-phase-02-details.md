<!-- markdownlint-disable-file -->

# Task Details: Grid Ladder Remains Active After Partial Fill

## Phase 2: Integration Tests + Knowledge Documentation

## Standards and Knowledge References

- `.github/instructions/testing.instructions.md` — MSTest, FluentAssertions v6, `Given_When_Then` naming, run tests per phase
- `.github/instructions/csharp.instructions.md` — sealed classes, guard clauses, naming
- `.agent-context/0-knowledge/15-grid-controller.md` — Grid lifecycle documentation
- `.agent-context/0-knowledge/24-backtesting-grid-engine-explained.md` — Grid engine walkthrough (to be updated)
- `tests/TradePilot.Application.Tests/Backtesting/Services/RealBacktestRunnerTests.cs` — Existing integration test patterns

## Design References

The integration tests must exercise the full pipeline: `BacktestRunner` → `StrategyScheduler` → `GridController` → `BacktestPositionManager` → `SimulatedExecutionEngine` — using real (non-mocked) implementations, just like the existing `RealBacktestRunnerTests`. The tests need to:

1. Deploy a multi-level grid (e.g., 5 levels)
2. Construct candle sequences where price progressively drops into 2–3 levels
3. Verify the remaining levels stay open after partial fills
4. Verify that a subsequent price recovery triggers TP correctly
5. Verify stop-loss from a partially filled grid cancels remaining levels

---

### Task 2.1: Add multi-level grid integration tests to `RealBacktestRunnerTests` {#task-21-add-multi-level-grid-integration-tests}

Add new integration test methods that exercise the corrected multi-level grid lifecycle. These tests use the real `GridController`, `BacktestPositionManager`, and `SimulatedExecutionEngine` — same as existing tests but with `gridLevels > 1` and candle sequences designed to fill multiple levels.

- **Complexity**: Medium
- **Risk Factors**: Candle sequences must be carefully designed so limit buy orders fill at predictable levels; requires understanding of `SimulatedExecutionEngine.ProcessCandle` fill logic
- **Files**:
  - `tests/TradePilot.Application.Tests/Backtesting/Services/RealBacktestRunnerTests.cs` — Add new test methods
- **Success**:
  - Test proves ladder stays active after first fill (multiple levels fill across candles)
  - Test proves TP triggers from partial fill when candle close reaches TP level
  - Test proves stop-loss from partial fill correctly closes the cycle
  - Test proves fully filled grid transitions to Closing and TP is placed
  - All tests are deterministic over the same candle data
- **Dependencies**: Phase 1 (GridController fix must be in place)

#### Implementation Details

```csharp
// tests/TradePilot.Application.Tests/Backtesting/Services/RealBacktestRunnerTests.cs — additions

    [TestMethod]
    public async Task GivenMultiLevelGrid_WhenPartialFills_ThenLadderRemainsActiveAndAdditionalLevelsFill()
    {
        // 5-level grid with 0.5% spacing from anchor ~100
        // Levels: L1=99.5, L2=99.0, L3=98.5, L4=98.0, L5=97.5
        // Candle sequence: deploy, then progressively lower candles to fill L1,L2,L3
        // Then price reverses above TP to close the cycle
        var config = new BacktestConfig
        {
            Symbol = "BTC",
            Intervals = ["15m", "1h", "4h"],
            StartDateUtc = 12 * OneHourMs,
            EndDateUtc = (12 * OneHourMs) + (8 * FifteenMinutesMs),
            InitialCapital = 10_000m,
            FeeModel = FeeModel.Default,
            WarmupPeriod = 2,
            StrategyConfigJson = "{\"gridLevels\":5,\"gridSpacing\":0.5,\"takeProfitPercent\":1," +
                "\"breakdownThreshold\":2,\"makerFee\":0.0001,\"takerFee\":0.00035,\"slippage\":0," +
                "\"positionSize\":100,\"leverage\":3,\"stopLossPercent\":5}",
            EnableAuditLog = true,
        };

        // Warmup candles
        // Deploy candle (close=100, grid deploys)
        // Fill L1: low dips to 99.4 (fills 99.5 level)
        // Fill L2,L3: low dips to 98.4 (fills 99.0 and 98.5 levels)
        // Recovery: close above TP trigger (avg entry ~99.0, TP = ~99.99)
        SetupCandles("15m",
        [
            CreateCandle("15m", config.StartDateUtc - (2 * FifteenMinutesMs), 100m, 101m, 99.5m, 100m),
            CreateCandle("15m", config.StartDateUtc - FifteenMinutesMs, 100m, 100.5m, 99.8m, 100m),
            CreateCandle("15m", config.StartDateUtc, 100m, 100.2m, 99.9m, 100m),           // deploy
            CreateCandle("15m", config.StartDateUtc + FifteenMinutesMs, 100m, 100.1m, 99.4m, 99.6m),  // fills L1
            CreateCandle("15m", config.StartDateUtc + (2 * FifteenMinutesMs), 99.6m, 99.7m, 98.4m, 98.6m), // fills L2,L3
            CreateCandle("15m", config.StartDateUtc + (3 * FifteenMinutesMs), 98.7m, 99.5m, 98.5m, 99.4m), // partial recovery
            CreateCandle("15m", config.StartDateUtc + (4 * FifteenMinutesMs), 99.4m, 100.5m, 99.3m, 100.2m), // close above TP
            CreateCandle("15m", config.StartDateUtc + (5 * FifteenMinutesMs), 100.3m, 101.0m, 100.1m, 100.8m), // TP sell fills
            CreateCandle("15m", config.StartDateUtc + (6 * FifteenMinutesMs), 100.8m, 101.2m, 100.5m, 101.0m),
            CreateCandle("15m", config.StartDateUtc + (7 * FifteenMinutesMs), 101.0m, 101.5m, 100.8m, 101.2m),
        ]);

        // {{1h and 4h candles covering the full range — use broader candles}}
        SetupCandles("1h",
        [
            CreateCandle("1h", 10 * OneHourMs, 100m, 101m, 98m, 100m),
            CreateCandle("1h", 11 * OneHourMs, 100m, 101m, 98m, 100m),
            CreateCandle("1h", 12 * OneHourMs, 100m, 101m, 98m, 100m),
            CreateCandle("1h", 13 * OneHourMs, 99m, 101m, 98m, 100m),
        ]);

        SetupCandles("4h",
        [
            CreateCandle("4h", OneHourMs * 4, 100m, 101m, 98m, 100m),
            CreateCandle("4h", OneHourMs * 8, 100m, 101m, 98m, 100m),
            CreateCandle("4h", OneHourMs * 12, 100m, 101m, 98m, 100m),
        ]);

        var result = await _sut.RunAsync(config);

        // Multiple levels filled — not just 1
        result.GridCycleLog.Should().ContainSingle();
        result.GridCycleLog![0].LevelsPlaced.Should().Be(5);
        result.GridCycleLog[0].LevelsFilled.Should().BeGreaterThanOrEqualTo(3);

        // Cycle completed with at least one closed trade
        result.TradeLog.Should().Contain(trade => trade.ExitTimeUtc.HasValue);

        // Remaining unfilled levels should be cancelled with TakeProfitTriggered reason
        result.OrderEventLog.Should().Contain(entry =>
            entry.EventType == OrderEventType.Cancelled &&
            entry.CancellationReason == CancellationReason.TakeProfitTriggered);
    }

    [TestMethod]
    public async Task GivenMultiLevelGrid_WhenStopLossFromPartialFill_ThenRemainingLevelsCancelledAndCycleCloses()
    {
        // 5-level grid, price drops past SL before all levels fill
        var config = new BacktestConfig
        {
            Symbol = "BTC",
            Intervals = ["15m", "1h", "4h"],
            StartDateUtc = 12 * OneHourMs,
            EndDateUtc = (12 * OneHourMs) + (6 * FifteenMinutesMs),
            InitialCapital = 10_000m,
            FeeModel = FeeModel.Default,
            WarmupPeriod = 2,
            StrategyConfigJson = "{\"gridLevels\":5,\"gridSpacing\":0.5,\"takeProfitPercent\":1," +
                "\"breakdownThreshold\":2,\"makerFee\":0.0001,\"takerFee\":0.00035,\"slippage\":0," +
                "\"positionSize\":100,\"leverage\":3,\"stopLossPercent\":3}",
            EnableAuditLog = true,
        };

        // Deploy, fill 2 levels, then crash below SL
        // SL at 3% from avg entry (~99.25) = ~96.27
        SetupCandles("15m",
        [
            CreateCandle("15m", config.StartDateUtc - (2 * FifteenMinutesMs), 100m, 101m, 99.5m, 100m),
            CreateCandle("15m", config.StartDateUtc - FifteenMinutesMs, 100m, 100.5m, 99.8m, 100m),
            CreateCandle("15m", config.StartDateUtc, 100m, 100.2m, 99.9m, 100m),             // deploy
            CreateCandle("15m", config.StartDateUtc + FifteenMinutesMs, 100m, 100.1m, 99.4m, 99.6m),   // fills L1
            CreateCandle("15m", config.StartDateUtc + (2 * FifteenMinutesMs), 99.6m, 99.7m, 98.9m, 99.1m), // fills L2
            CreateCandle("15m", config.StartDateUtc + (3 * FifteenMinutesMs), 99.0m, 99.1m, 95.0m, 95.5m), // crash below SL
            CreateCandle("15m", config.StartDateUtc + (4 * FifteenMinutesMs), 95.5m, 96.0m, 95.0m, 95.8m), // SL market sell fills
            CreateCandle("15m", config.StartDateUtc + (5 * FifteenMinutesMs), 95.8m, 96.5m, 95.5m, 96.0m),
        ]);

        SetupCandles("1h",
        [
            CreateCandle("1h", 10 * OneHourMs, 100m, 101m, 95m, 100m),
            CreateCandle("1h", 11 * OneHourMs, 100m, 101m, 95m, 100m),
            CreateCandle("1h", 12 * OneHourMs, 100m, 101m, 95m, 96m),
        ]);

        SetupCandles("4h",
        [
            CreateCandle("4h", OneHourMs * 4, 100m, 101m, 95m, 100m),
            CreateCandle("4h", OneHourMs * 8, 100m, 101m, 95m, 100m),
            CreateCandle("4h", OneHourMs * 12, 100m, 101m, 95m, 96m),
        ]);

        var result = await _sut.RunAsync(config);

        // Verify SL was triggered (negative PnL)
        result.TotalPnL.Should().BeLessThan(0m);
        result.GridCycleLog.Should().ContainSingle();
        result.GridCycleLog![0].LevelsFilled.Should().BeGreaterThanOrEqualTo(2);

        // Verify remaining orders were cancelled
        result.OrderEventLog.Should().Contain(entry =>
            entry.EventType == OrderEventType.Cancelled &&
            entry.CancellationReason == CancellationReason.StopLossTriggered);
    }
```

> **Note**: The exact candle prices and grid fill behavior need validation during implementation. The implementing agent should trace through the fill logic to confirm which levels fill on which candles, and adjust prices if needed. The key assertion is that `LevelsFilled >= 3` (not just 1 as in current tests).

##### Pattern References

- Existing integration tests: `tests/TradePilot.Application.Tests/Backtesting/Services/RealBacktestRunnerTests.cs`
- `BacktestConfig` model: `src/TradePilot.Application/Backtesting/Models/BacktestConfig.cs`
- `GridCycleEntry` audit model: search for `GridCycleEntry` in `src/TradePilot.Application/Backtesting/`
- `SimulatedExecutionEngine.ProcessCandle` fill logic: `src/TradePilot.Application/Backtesting/Services/SimulatedExecutionEngine.cs`

---

### Task 2.2: Update existing integration tests for corrected behavior {#task-22-update-existing-integration-tests}

Review and update existing `RealBacktestRunnerTests` methods if their assertions are affected by the corrected lifecycle behavior. The `GivenInitialMarketThenGridEntryMode` test currently asserts `LevelsFilled = 1` with `gridLevels = 2` — this may now fill more levels if the second level's price is reached by the candle sequence.

- **Complexity**: Low
- **Risk Factors**: Existing candle sequences may produce different fill counts with the corrected controller. Need to trace through each test's candle data.
- **Files**:
  - `tests/TradePilot.Application.Tests/Backtesting/Services/RealBacktestRunnerTests.cs` — Adjust assertions if needed
- **Success**:
  - All existing integration tests pass with the corrected controller
  - Assertions accurately reflect expected behavior under the corrected lifecycle
- **Dependencies**: Phase 1 (GridController fix)

---

### Task 2.3: Update knowledge documentation {#task-23-update-knowledge-documentation}

Update the knowledge files to reflect the corrected grid lifecycle behavior. The key file is `24-backtesting-grid-engine-explained.md` which documents the current (buggy) cancel-on-first-fill behavior as if it were intended. Also verify `15-grid-controller.md` is accurate.

- **Complexity**: Low
- **Risk Factors**: None — documentation-only changes
- **Files**:
  - `.agent-context/0-knowledge/24-backtesting-grid-engine-explained.md` — Update sections that describe cancel-on-first-fill behavior. Search for: "cancel", "first fill", "PositionOpened", "position opened", "remaining buy", "all open orders". Replace references to immediate cancellation with the corrected multi-level lifecycle.
  - `.agent-context/0-knowledge/15-grid-controller.md` — Verify lifecycle state transitions match corrected implementation, update if needed
- **Success**:
  - Documentation accurately describes the corrected multi-level grid lifecycle
  - No references to "cancel remaining buys on first fill" remain in documentation
  - Lifecycle state transitions documented: `Deploying → PartiallyFilled → FullyFilled → Closing → Closed`
  - Controller-checked TP behavior for partial fills is documented
- **Dependencies**: Phase 1 (know the final implementation to document)

---

### Task 2.4: Run all tests and verify {#task-24-run-all-tests-and-verify}

Build the full solution and run all test projects to verify no regressions.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification step)
- **Success**:
  - `dotnet build TradePilot.sln` succeeds
  - `dotnet test tests/TradePilot.Application.Tests` — all tests pass
  - `dotnet test tests/TradePilot.Domain.Tests` — all tests pass
  - `dotnet test tests/TradePilot.Api.Tests` — all tests pass
  - `dotnet test tests/TradePilot.Infrastructure.Tests` — all tests pass
  - `dotnet test tests/TradePilot.Persistence.Tests` — all tests pass
  - All 6 PBI acceptance criteria satisfied
- **Dependencies**: Tasks 2.1, 2.2, 2.3

## Phase Success Criteria

- Multi-level integration tests demonstrate ladder staying active after partial fills
- Multi-level integration tests demonstrate correct TP from partial fill (candle close check)
- Multi-level integration tests demonstrate SL from partial fill with remaining level cancellation
- Existing integration tests pass without regression
- Knowledge documentation reflects corrected lifecycle behavior
- Full test suite passes across all test projects
