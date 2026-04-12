<!-- markdownlint-disable-file -->

# Task Details: Portfolio Heat Enforcement

## Phase 2: LiveRiskEngine Heat Enforcement

## Standards and Knowledge References

- **csharp.instructions.md**: Sealed classes, async/await for I/O, CancellationToken passing, `_camelCase` private fields
- **testing.instructions.md**: MSTest, Moq, FluentAssertions ≤ v6, Given_When_Then naming
- **dotnet-architecture.instructions.md**: Interface default implementations, CQRS handler kept in same file
- **33-risk-management-and-trade-sizing.md**: Heat enforcement rules, risk-reducing signal bypass
- **16-signal-contracts.md**: `DeployGrid`, `TakeProfit`, `FlattenPosition`, `CancelGrid`, `CloseHedge` signal types
- **30-worker-execution-pipeline.md**: Signal → RiskEngine → PositionManager flow

## Design References

**Enforcement Rule**: Block new entries when `currentHeatPercent + newTradeRPercent > MaxPortfolioHeatPercent`.

**State tracking approach**: Follows existing `RecordLoss`/`RecordOrdersPlaced`/`RecordOrdersClosed` pattern — external callers update the singleton engine's state, and `ValidateAsync` uses the state during validation.

**R tracking in ValidateAsync**: When a `DeployGrid`/`OpenPosition` signal is approved and contains `estimatedRiskUsd`, the engine records it in `_positionRisks`. When a `FlattenPosition`/`CloseHedge` passes through, the engine removes the tracked R for that symbol. `RecordPositionClosed` provides authoritative external cleanup.

---

### Task 2.1: Add position/equity tracking methods to `IRiskEngine` {#task-21-add-positionequity-tracking-methods-to-iriskengine}

Add default-implemented methods for position lifecycle tracking and equity updates.

- **Complexity**: Low
- **Risk Factors**: Interface change affects all implementors — but default implementations mean no breaking changes
- **Files**:
  - `src/TradingApp.Application/Abstractions/Services/IRiskEngine.cs` — Add 3 default-implemented methods
- **Success**:
  - `UpdatePortfolioState(decimal accountEquity)` method with empty default
  - `RecordPositionOpened(string symbol, decimal riskUsd)` method with empty default
  - `RecordPositionClosed(string symbol)` method with empty default
  - `PassThroughRiskEngine` compiles without changes (defaults apply)
- **Dependencies**: Phase 1

#### Implementation Details

```csharp
// src/TradingApp.Application/Abstractions/Services/IRiskEngine.cs — modification
// Add after existing RecordOrdersClosed method:

    /// <summary>Update the engine's knowledge of current account equity (for heat calculation).</summary>
    void UpdatePortfolioState(decimal accountEquity) { }

    /// <summary>Record that a position was opened with the given risk amount.</summary>
    void RecordPositionOpened(string symbol, decimal riskUsd) { }

    /// <summary>Record that a position was fully closed.</summary>
    void RecordPositionClosed(string symbol) { }
```

##### Pattern References

- `src/TradingApp.Application/Abstractions/Services/IRiskEngine.cs` — existing default-implemented methods (`RecordLoss`, `RecordOrdersPlaced`, `RecordOrdersClosed`)

---

### Task 2.2: Implement heat tracking state in `LiveRiskEngine` {#task-22-implement-heat-tracking-state-in-liveriskengine}

Add internal state to track position risks and account equity. Implement the new interface methods.

- **Complexity**: Medium
- **Risk Factors**: Thread safety — engine is a singleton accessed from multiple strategy schedulers
- **Files**:
  - `src/TradingApp.Application/Trading/Services/LiveRiskEngine.cs` — Add fields and method implementations
- **Success**:
  - `_positionRisks` ConcurrentDictionary tracks symbol → R USD
  - `_accountEquity` field with thread-safe access via `_lock` stores latest known equity
  - `UpdatePortfolioState` updates equity
  - `RecordPositionOpened` adds/updates position risk
  - `RecordPositionClosed` removes position risk
  - All logging includes relevant values
- **Dependencies**: Task 2.1

#### Implementation Details

```csharp
// src/TradingApp.Application/Trading/Services/LiveRiskEngine.cs — modification
// Add new fields after existing fields:

    private readonly ConcurrentDictionary<string, decimal> _positionRisks = new();
    private decimal _accountEquity;

// ... existing code ...

// Add new method implementations after ResetCircuitBreaker():

    /// <summary>
    /// Update the engine's knowledge of current account equity.
    /// Called by StrategyScheduler before ValidateAsync on each candle evaluation.
    /// </summary>
    public void UpdatePortfolioState(decimal accountEquity)
    {
        lock (_lock) { _accountEquity = accountEquity; }
    }

    /// <summary>
    /// Record that a position was opened with the given risk amount.
    /// </summary>
    public void RecordPositionOpened(string symbol, decimal riskUsd)
    {
        _positionRisks[symbol] = riskUsd;
        _logger.LogInformation(
            "RISK: Position opened — Symbol={Symbol}, R=${RiskUsd:N2}, TotalHeat={HeatCount} positions",
            symbol, riskUsd, _positionRisks.Count);
    }

    /// <summary>
    /// Record that a position was fully closed. Removes tracked risk.
    /// </summary>
    public void RecordPositionClosed(string symbol)
    {
        if (_positionRisks.TryRemove(symbol, out var removedRisk))
        {
            _logger.LogInformation(
                "RISK: Position closed — Symbol={Symbol}, R=${RiskUsd:N2} removed, TotalHeat={HeatCount} positions",
                symbol, removedRisk, _positionRisks.Count);
        }
    }

    /// <summary>Current number of tracked position risks (for testing).</summary>
    internal int TrackedPositionCount => _positionRisks.Count;

    /// <summary>Current tracked account equity (for testing).</summary>
    internal decimal TrackedEquity { get { lock (_lock) return _accountEquity; } }
```

##### Pattern References

- `src/TradingApp.Application/Trading/Services/LiveRiskEngine.cs` — existing `RecordLoss`, `RecordOrdersPlaced`, `RecordOrdersClosed` implementations, `ConcurrentQueue` usage, logging pattern

---

### Task 2.3: Add `CheckPortfolioHeat` to `ValidateAsync` pipeline {#task-23-add-checkportfolioheat-to-validateasync-pipeline}

Add portfolio heat check as Step 5 in the `ValidateAsync` pipeline, after order size and count checks. Also handle R tracking on signal approval and position close tracking for risk-reducing signals.

- **Complexity**: High
- **Risk Factors**: Must preserve existing risk-reducing signal bypass; must handle `estimatedRiskUsd` parameter extraction; must handle disabled state (MaxPortfolioHeatPercent = 0)
- **Files**:
  - `src/TradingApp.Application/Trading/Services/LiveRiskEngine.cs` — Modify `ValidateAsync` and add helper
- **Success**:
  - Entry signals blocked when `currentHeat + newTradeR > equity × MaxPortfolioHeatPercent / 100`
  - Risk-reducing signals always pass AND `FlattenPosition`/`CloseHedge` remove tracked R
  - Heat check skipped when `MaxPortfolioHeatPercent = 0`
  - Heat check skipped when `_accountEquity <= 0`
  - Approved entry signals with `estimatedRiskUsd` parameter are tracked in `_positionRisks`
  - Blocked signals logged with heat details
- **Dependencies**: Tasks 2.1, 2.2

#### Implementation Details

```csharp
// src/TradingApp.Application/Trading/Services/LiveRiskEngine.cs — modification

// Modify the ValidateAsync method. Add after IsRiskReducing check (risk-reducing signals):
        foreach (var signal in signals)
        {
            // CancelGrid and TakeProfit always pass — they reduce risk
            if (IsRiskReducing(signal))
            {
                approved.Add(signal);
                // Track position closes for heat accounting
                TrackPositionCloseFromSignal(signal);
                continue;
            }

            if (_circuitBreakerTripped)
            {
                // ... existing circuit breaker check ...
                continue;
            }

            if (!CheckOrderSize(signal))
            {
                continue;
            }

            if (!CheckOpenOrderLimit(signal))
            {
                continue;
            }

            // Check portfolio heat limit
            if (!CheckPortfolioHeat(signal))
            {
                continue;
            }

            approved.Add(signal);

            // Track R for approved entry signals
            TrackPositionOpenFromSignal(signal);
        }

// Add new private methods:

    private bool CheckPortfolioHeat(TradingSignal signal)
    {
        decimal equity;
        lock (_lock) { equity = _accountEquity; }

        if (_limits.MaxPortfolioHeatPercent <= 0 || equity <= 0)
        {
            return true; // Heat check disabled or no equity info
        }

        if (!TryGetEstimatedRisk(signal, out var newTradeRiskUsd))
        {
            return true; // No risk info on signal — allow (can't compute heat)
        }

        var currentHeatUsd = _positionRisks.Values.Sum();
        var maxHeatUsd = equity * (_limits.MaxPortfolioHeatPercent / 100m);

        if (currentHeatUsd + newTradeRiskUsd > maxHeatUsd)
        {
            var currentHeatPct = PortfolioHeatCalculator.CalculateHeatPercent(
                _positionRisks.Values, equity);
            var newTradePct = (newTradeRiskUsd / equity) * 100m;

            _logger.LogWarning(
                "RISK: Signal BLOCKED by portfolio heat — CurrentHeat={CurrentHeat:N2}% + NewR={NewR:N2}% = {Total:N2}% > Max={Max:N2}%. " +
                "Type={SignalType}, Symbol={Symbol}",
                currentHeatPct, newTradePct, currentHeatPct + newTradePct,
                _limits.MaxPortfolioHeatPercent, signal.SignalType, signal.Symbol);
            return false;
        }

        return true;
    }

    private static bool TryGetEstimatedRisk(TradingSignal signal, out decimal riskUsd)
    {
        riskUsd = 0m;
        if (signal.Parameters is not null
            && signal.Parameters.TryGetValue("estimatedRiskUsd", out var rObj))
        {
            riskUsd = Convert.ToDecimal(rObj);
            return riskUsd > 0;
        }

        return false;
    }

    private void TrackPositionOpenFromSignal(TradingSignal signal)
    {
        if (TryGetEstimatedRisk(signal, out var riskUsd))
        {
            _positionRisks[signal.Symbol] = riskUsd;
        }
    }

    private void TrackPositionCloseFromSignal(TradingSignal signal)
    {
        if (signal.SignalType is "FlattenPosition" or "CloseHedge")
        {
            _positionRisks.TryRemove(signal.Symbol, out _);
        }
    }
```

##### Pattern References

- `src/TradingApp.Application/Trading/Services/LiveRiskEngine.cs` — existing `CheckOrderSize`, `CheckOpenOrderLimit`, `IsRiskReducing` methods — pipeline check pattern

---

### Task 2.4: Add `estimatedRiskUsd` to signal parameters in `GridController` {#task-24-add-estimatedriskusd-to-signal-parameters-in-gridcontroller}

When the `GridController` emits `DeployGrid` signals, include the estimated R (risk in USD) in the signal parameters. This enables the risk engine to compute heat impact without needing access to strategy config.

- **Complexity**: Medium
- **Risk Factors**: Need to find where `DeployGrid` signal parameters are constructed; need to handle all `PositionSizeType` modes
- **Files**:
  - `src/TradingApp.Application/Trading/Services/GridController.cs` — Add `estimatedRiskUsd` parameter
- **Success**:
  - `DeployGrid` signals include `estimatedRiskUsd` parameter
  - R computed correctly for `RiskBased`: `equity × riskPerTradePercent / 100`
  - R computed correctly for `PercentWallet`/`FixedNotional` with SL: `notionalUsd × stopLossPercent / 100`
  - R fallback (no SL): `notionalUsd / leverage` (margin at risk)
- **Dependencies**: Phase 1

#### Implementation Details

Search `GridController` for where `DeployGrid` signal parameters dictionary is constructed (where `notionalUsd`, `gridLevels`, etc. are added). Add `estimatedRiskUsd` to the dictionary:

```csharp
// In the DeployGrid signal construction, add estimatedRiskUsd:
// The exact location varies — find where Parameters dictionary is built for DeployGrid signals

var estimatedRiskUsd = EstimateSignalRisk(risk, notionalPerLevel, equity, stopLossPercent);

// Add to Parameters dictionary:
["estimatedRiskUsd"] = estimatedRiskUsd,

// New private helper method:
private static decimal EstimateSignalRisk(
    RiskConfig risk, decimal notionalUsd, decimal equity, decimal? stopLossPercent)
{
    if (risk.PositionSizeType == PositionSizeType.RiskBased
        && risk.RiskPerTradePercent.HasValue
        && risk.RiskPerTradePercent.Value > 0)
    {
        return Math.Max(0m, equity) * (risk.RiskPerTradePercent.Value / 100m);
    }

    if (stopLossPercent.HasValue && stopLossPercent.Value > 0)
    {
        return notionalUsd * (stopLossPercent.Value / 100m);
    }

    // Fallback: use margin (notional / leverage) as conservative proxy
    var leverage = Math.Max(1m, risk.Leverage);
    return notionalUsd / leverage;
}
```

##### Pattern References

- `src/TradingApp.Application/Trading/Services/GridController.cs` — existing signal parameter construction with `notionalUsd`, `gridLevels`, etc.
- `src/TradingApp.Application/Trading/Services/PositionSizeResolver.cs` — R calculation reference

---

### Task 2.5: Wire `StrategyScheduler` to call `UpdatePortfolioState` {#task-25-wire-strategyscheduler-to-call-updateportfoliostate}

Before calling `ValidateAsync`, the scheduler must update the risk engine with current equity so the heat check has correct values.

- **Complexity**: Low
- **Risk Factors**: None — additive call before existing `ValidateAsync`
- **Files**:
  - `src/TradingApp.Application/Scheduling/StrategyScheduler.cs` — Add `UpdatePortfolioState` call
- **Success**:
  - `_riskEngine.UpdatePortfolioState(marketContext.AccountEquity)` called before `ValidateAsync`
  - Existing signal flow unchanged
- **Dependencies**: Task 2.1

#### Implementation Details

```csharp
// src/TradingApp.Application/Scheduling/StrategyScheduler.cs — modification
// Find the line: var approvedSignals = await _riskEngine.ValidateAsync(signals, cancellationToken);
// Add before it:

        _riskEngine.UpdatePortfolioState(marketContext.AccountEquity);
        var approvedSignals = await _riskEngine.ValidateAsync(signals, cancellationToken);
```

Note: `marketContext` is the `MarketContext` variable in scope at the point where `ValidateAsync` is called. Verify the exact variable name by reading the surrounding context in `StrategyScheduler.EvaluateCandleAsync`.

##### Pattern References

- `src/TradingApp.Application/Scheduling/StrategyScheduler.cs` — existing `ValidateAsync` call site at line 134

---

### Task 2.6: Wire `FillProcessor` to call `RecordPositionClosed` {#task-26-wire-fillprocessor-to-call-recordpositionclosed}

When the `FillProcessor` detects that a position has been fully closed (size = 0 after fill processing), notify the risk engine to remove tracked risk.

- **Complexity**: Medium
- **Risk Factors**: Need to find the correct location in `FillProcessor` where position close is detected
- **Files**:
  - `src/TradingApp.Application/Trading/Services/FillProcessor.cs` — Add `RecordPositionClosed` call (or nearest equivalent)
- **Success**:
  - When a fill reduces position size to 0, `_riskEngine.RecordPositionClosed(symbol)` is called
  - Works as authoritative cleanup for R tracking (corrects any inaccuracies from signal-based tracking)
- **Dependencies**: Task 2.1

#### Implementation Details

Search `FillProcessor` for where it calls `_riskEngine.RecordLoss()` or detects position close (size = 0). Add a `RecordPositionClosed` call at the same point:

```csharp
// In FillProcessor, after detecting a position has fully closed:
// Look for the code path that calls RecordLoss (which runs on losing closes)
// At the same decision point (position fully closed, win or loss):

_riskEngine.RecordPositionClosed(symbol);
```

##### Pattern References

- `src/TradingApp.Application/Trading/Services/FillProcessor.cs` — existing `RecordLoss`, `RecordOrdersClosed` call sites

---

### Task 2.7: Unit tests for heat enforcement {#task-27-unit-tests-for-heat-enforcement}

Add tests to `LiveRiskEngineTests` covering all heat enforcement scenarios.

- **Complexity**: Medium
- **Risk Factors**: None — follows existing test patterns
- **Files**:
  - `tests/TradingApp.Application.Tests/Trading/Services/LiveRiskEngineTests.cs` — Add new test methods
- **Success**:
  - Test: entry allowed when heat + new R ≤ limit
  - Test: entry blocked when heat + new R > limit
  - Test: risk-reducing signals pass regardless of heat
  - Test: heat check skipped when limit = 0 (disabled)
  - Test: position close reduces heat and enables entry
  - Test: FlattenPosition signal removes tracked R
  - Test: signal without `estimatedRiskUsd` is allowed (can't compute heat)
  - All tests pass: `dotnet test tests/TradingApp.Application.Tests/ --filter "FullyQualifiedName~LiveRiskEngine"`
- **Dependencies**: Tasks 2.2, 2.3

#### Implementation Details

```csharp
// tests/TradingApp.Application.Tests/Trading/Services/LiveRiskEngineTests.cs — modification
// Add new test methods to the existing class:

    [TestMethod]
    public async Task GivenHeatBelowLimit_WhenEntrySignal_ThenAllowed()
    {
        // Arrange — 5% heat, 1% new entry, 6% limit
        _limits = _limits with { MaxPortfolioHeatPercent = 6m };
        _sut = new LiveRiskEngine(Options.Create(_limits), new Mock<ILogger<LiveRiskEngine>>().Object);
        _sut.UpdatePortfolioState(10_000m); // $10k equity
        _sut.RecordPositionOpened("BTC", 100m);  // 1% heat per position
        _sut.RecordPositionOpened("ETH", 100m);
        _sut.RecordPositionOpened("SOL", 100m);
        _sut.RecordPositionOpened("AVAX", 100m);
        _sut.RecordPositionOpened("LINK", 100m);  // total 5% = $500

        var signal = new TradingSignal
        {
            SignalType = "DeployGrid",
            Symbol = "DOGE",
            Parameters = new Dictionary<string, object>
            {
                ["notionalUsd"] = 1000m,
                ["gridLevels"] = 1,
                ["estimatedRiskUsd"] = 100m  // +1% = 6% total ≤ 6% limit
            }
        };

        // Act
        var approved = await _sut.ValidateAsync([signal]);

        // Assert
        approved.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task GivenHeatAtLimit_WhenEntrySignal_ThenBlocked()
    {
        // Arrange — 6% heat, 1% new entry → 7% > 6% limit
        _limits = _limits with { MaxPortfolioHeatPercent = 6m };
        _sut = new LiveRiskEngine(Options.Create(_limits), new Mock<ILogger<LiveRiskEngine>>().Object);
        _sut.UpdatePortfolioState(10_000m);
        for (int i = 0; i < 6; i++)
            _sut.RecordPositionOpened($"TOKEN{i}", 100m);  // total 6% = $600

        var signal = new TradingSignal
        {
            SignalType = "DeployGrid",
            Symbol = "NEWTOKEN",
            Parameters = new Dictionary<string, object>
            {
                ["notionalUsd"] = 1000m,
                ["gridLevels"] = 1,
                ["estimatedRiskUsd"] = 100m  // +1% = 7% > 6% limit
            }
        };

        // Act
        var approved = await _sut.ValidateAsync([signal]);

        // Assert
        approved.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenHeatAtLimit_WhenTakeProfitSignal_ThenAllowed()
    {
        // Arrange — heat at limit
        _limits = _limits with { MaxPortfolioHeatPercent = 6m };
        _sut = new LiveRiskEngine(Options.Create(_limits), new Mock<ILogger<LiveRiskEngine>>().Object);
        _sut.UpdatePortfolioState(10_000m);
        for (int i = 0; i < 6; i++)
            _sut.RecordPositionOpened($"TOKEN{i}", 100m);

        var signal = new TradingSignal
        {
            SignalType = "TakeProfit",
            Symbol = "TOKEN0"
        };

        // Act
        var approved = await _sut.ValidateAsync([signal]);

        // Assert
        approved.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task GivenHeatDisabled_WhenEntrySignal_ThenAllowed()
    {
        // Arrange — MaxPortfolioHeatPercent = 0 (disabled)
        _limits = _limits with { MaxPortfolioHeatPercent = 0m };
        _sut = new LiveRiskEngine(Options.Create(_limits), new Mock<ILogger<LiveRiskEngine>>().Object);
        _sut.UpdatePortfolioState(10_000m);
        for (int i = 0; i < 10; i++)
            _sut.RecordPositionOpened($"TOKEN{i}", 100m);  // 10% heat

        var signal = new TradingSignal
        {
            SignalType = "DeployGrid",
            Symbol = "NEWTOKEN",
            Parameters = new Dictionary<string, object>
            {
                ["notionalUsd"] = 1000m,
                ["gridLevels"] = 1,
                ["estimatedRiskUsd"] = 100m
            }
        };

        // Act
        var approved = await _sut.ValidateAsync([signal]);

        // Assert — heat check disabled, so signal passes
        approved.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task GivenPositionClosed_WhenHeatDropsBelowLimit_ThenEntryAllowed()
    {
        // Arrange — 6% heat → close one → 5% → new entry allowed
        _limits = _limits with { MaxPortfolioHeatPercent = 6m };
        _sut = new LiveRiskEngine(Options.Create(_limits), new Mock<ILogger<LiveRiskEngine>>().Object);
        _sut.UpdatePortfolioState(10_000m);
        for (int i = 0; i < 6; i++)
            _sut.RecordPositionOpened($"TOKEN{i}", 100m);

        _sut.RecordPositionClosed("TOKEN0"); // heat drops 6% → 5%

        var signal = new TradingSignal
        {
            SignalType = "DeployGrid",
            Symbol = "NEWTOKEN",
            Parameters = new Dictionary<string, object>
            {
                ["notionalUsd"] = 1000m,
                ["gridLevels"] = 1,
                ["estimatedRiskUsd"] = 100m  // +1% = 6% ≤ 6%
            }
        };

        // Act
        var approved = await _sut.ValidateAsync([signal]);

        // Assert
        approved.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task GivenFlattenPositionSignal_WhenProcessed_ThenTrackedRiskRemoved()
    {
        // Arrange
        _limits = _limits with { MaxPortfolioHeatPercent = 6m };
        _sut = new LiveRiskEngine(Options.Create(_limits), new Mock<ILogger<LiveRiskEngine>>().Object);
        _sut.UpdatePortfolioState(10_000m);
        _sut.RecordPositionOpened("BTC", 600m); // 6% heat

        var flatten = new TradingSignal { SignalType = "FlattenPosition", Symbol = "BTC" };

        // Act — FlattenPosition is risk-reducing, passes through
        var approved = await _sut.ValidateAsync([flatten]);

        // Assert — signal approved AND R removed
        approved.Should().HaveCount(1);
        _sut.TrackedPositionCount.Should().Be(0);
    }
```

##### Pattern References

- `tests/TradingApp.Application.Tests/Trading/Services/LiveRiskEngineTests.cs` — existing test setup with `_limits with { ... }` pattern, signal construction

---

### Task 2.8: Build and existing test verification {#task-28-build-and-existing-test-verification}

Build all affected projects and run all tests to verify no regressions.

- **Complexity**: Low
- **Risk Factors**: Potential compilation errors in `IRiskEngine` consumers if default implementations don't compile
- **Files**: None (verification only)
- **Success**:
  - `dotnet build TradingApp.sln` succeeds
  - `dotnet test tests/TradingApp.Application.Tests/ --filter "FullyQualifiedName~LiveRiskEngine"` — all tests pass
  - `dotnet test TradingApp.sln --no-build` — all tests pass (no regressions)
- **Dependencies**: Tasks 2.1–2.7

## Phase Success Criteria

- `IRiskEngine` has `UpdatePortfolioState`, `RecordPositionOpened`, `RecordPositionClosed` methods
- `LiveRiskEngine.ValidateAsync` blocks entry signals when portfolio heat exceeds configured limit
- Risk-reducing signals always pass regardless of heat level
- `GridController` adds `estimatedRiskUsd` to `DeployGrid` signal parameters
- `StrategyScheduler` updates equity before risk validation
- All heat enforcement unit tests pass
- All existing tests pass without regression
