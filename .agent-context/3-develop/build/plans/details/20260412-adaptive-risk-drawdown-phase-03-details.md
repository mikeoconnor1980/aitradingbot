<!-- markdownlint-disable-file -->

# Task Details: Adaptive Risk (Drawdown-Adjusted)

## Phase 3: Backtest Support

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — sealed classes, async patterns
- `.github/instructions/testing.instructions.md` — MSTest, Moq, FluentAssertions, Given_When_Then naming
- `.agent-context/0-knowledge/18-backtesting-architecture.md` — BacktestRiskEngine, replay loop, equity snapshots
- `.agent-context/0-knowledge/33-risk-management-and-trade-sizing.md` — backtest must enforce same drawdown logic as live

---

### Task 3.1: Add drawdown tracking to BacktestRiskEngine {#task-31-add-drawdown-tracking-to-backtestriskengine}

Extend `BacktestRiskEngine` to track in-memory HWM, evaluate drawdown tiers, and block entry signals when the halt tier is reached. Uses the same `DrawdownEvaluator` as live trading.

- **Complexity**: Medium
- **Risk Factors**: BacktestRiskEngine manages its own equity state via `UpdatePortfolioState` — drawdown evaluation must use the same equity. HWM is in-memory only (no DB persistence during backtest).
- **Files**:
  - `src/TradingApp.Application/Backtesting/Services/BacktestRiskEngine.cs` — modification
- **Success**:
  - `BacktestRiskEngine` tracks HWM starting from initial equity (first `UpdatePortfolioState` call)
  - `DrawdownEvaluator.Evaluate()` called on each `UpdatePortfolioState`
  - Drawdown CB blocks entry signals in `ValidateAsync` (same as live)
  - Risk-reducing signals pass through even when drawdown CB is active
  - Drawdown CB auto-resets when equity recovers above halt threshold
  - `DrawdownScalingFactor` and `IsDrawdownCircuitBreakerTripped` properties exposed
- **Dependencies**: Phase 2 (DrawdownEvaluator, IRiskEngine extensions)

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/Services/BacktestRiskEngine.cs — modifications

// New fields:
private readonly IReadOnlyList<DrawdownTier> _drawdownTiers;
private decimal _highWaterMark;
private decimal _drawdownScalingFactor = 1.0m;
private bool _drawdownCircuitBreakerTripped;
private int _drawdownBlockedSignalCount;

// Properties:
public decimal DrawdownScalingFactor => _drawdownScalingFactor;
public bool IsDrawdownCircuitBreakerTripped => _drawdownCircuitBreakerTripped;
public int DrawdownBlockedSignalCount => _drawdownBlockedSignalCount;

// Constructor — accept drawdown tiers (from RiskLimitsConfig):
public BacktestRiskEngine(RiskLimitsConfig limits)
{
    // ... existing constructor code ...
    _drawdownTiers = limits.DrawdownTiers;
}

// UpdatePortfolioState — add drawdown evaluation after existing equity update:
public void UpdatePortfolioState(decimal accountEquity)
{
    _accountEquity = Math.Max(0m, accountEquity);

    // Initialize HWM on first call
    if (_highWaterMark == 0m)
        _highWaterMark = _accountEquity;

    var drawdownResult = DrawdownEvaluator.Evaluate(
        _accountEquity, _highWaterMark, _drawdownTiers);
    _highWaterMark = drawdownResult.NewHighWaterMark;
    _drawdownScalingFactor = drawdownResult.ScalingFactor;
    _drawdownCircuitBreakerTripped = drawdownResult.IsHalted;
}

// ValidateAsync — add drawdown CB check alongside existing heat check:
// After existing portfolio heat check:
if (_drawdownCircuitBreakerTripped)
{
    _drawdownBlockedSignalCount++;
    continue; // Skip entry signal
}
```

Note: The implementing agent must inspect the existing `BacktestRiskEngine` constructor to see how `RiskLimitsConfig` is currently received (it may already take it or need it added). The `DrawdownTiers` come from `RiskLimitsConfig` which is already used for `MaxPortfolioHeatPercent`.

##### Pattern References

- `src/TradingApp.Application/Backtesting/Services/BacktestRiskEngine.cs` — existing `_accountEquity`, `UpdatePortfolioState`, portfolio heat check in `ValidateAsync`
- `src/TradingApp.Application/Trading/Services/DrawdownEvaluator.cs` — stateless evaluator (from Phase 2)

---

### Task 3.2: Track drawdown-blocked signals in backtest metrics {#task-32-track-drawdown-blocked-signals-in-backtest-metrics}

Expose the count of signals blocked by the drawdown CB so backtest results can report them.

- **Complexity**: Low
- **Risk Factors**: None — additive metric
- **Files**:
  - `src/TradingApp.Application/Backtesting/Services/BacktestRiskEngine.cs` — already added `DrawdownBlockedSignalCount` in Task 3.1
  - `src/TradingApp.Application/Backtesting/Models/BacktestResult.cs` or equivalent — add drawdown-blocked count (if such a result model exists)
- **Success**:
  - `DrawdownBlockedSignalCount` is accessible from `BacktestRiskEngine` after a run
  - Backtest runner or metrics calculator includes the count in results
- **Dependencies**: Task 3.1

#### Implementation Details

The implementing agent should search for the backtest result/summary model and add the `DrawdownBlockedSignalCount` alongside the existing `HeatBlockedSignalCount`. Follow the exact same pattern.

##### Pattern References

- `src/TradingApp.Application/Backtesting/Services/BacktestRiskEngine.cs` — existing `HeatBlockedSignalCount` metric

---

### Task 3.3: Unit and integration tests for Phase 3 {#task-33-unit-and-integration-tests-for-phase-3}

Write tests for `BacktestRiskEngine` drawdown tracking, including a backtest run where equity enters the halt tier.

- **Complexity**: Medium
- **Risk Factors**: Integration test may need specific equity curve data to trigger drawdown tiers
- **Files**:
  - `tests/TradingApp.Application.Tests/Backtesting/Services/BacktestRiskEngineTests.cs` — add drawdown tests
  - `tests/TradingApp.Application.Tests/Backtesting/Services/BacktestRunnerTests.cs` — add integration test (optional)
- **Success**:
  - Test: drawdown CB blocks entry signals when equity drops below halt threshold
  - Test: drawdown CB auto-resets when equity recovers
  - Test: risk-reducing signals pass through during drawdown CB
  - Test: `DrawdownBlockedSignalCount` increments correctly
  - Test: HWM ratchets up during backtest equity growth
  - All tests pass
- **Dependencies**: Tasks 3.1–3.2

#### Implementation Details

```csharp
// tests/TradingApp.Application.Tests/Backtesting/Services/BacktestRiskEngineTests.cs — add tests

[TestMethod]
public async Task GivenEquityDropsIntoHaltTier_WhenEntrySignalValidated_ThenBlocked()
{
    // Setup: HWM at 10000, equity drops to 8400 (16% drawdown → halt tier)
    _sut.UpdatePortfolioState(10_000m); // Set initial HWM
    _sut.UpdatePortfolioState(8_400m);  // Drop into halt

    _sut.IsDrawdownCircuitBreakerTripped.Should().BeTrue();

    var signals = new[] { CreateEntrySignal("BTC", 100m) };
    var approved = await _sut.ValidateAsync(signals, CancellationToken.None);
    approved.Should().BeEmpty();
    _sut.DrawdownBlockedSignalCount.Should().Be(1);
}

[TestMethod]
public async Task GivenEquityRecoversFromHalt_WhenEntrySignalValidated_ThenApproved()
{
    _sut.UpdatePortfolioState(10_000m);
    _sut.UpdatePortfolioState(8_400m);  // Halt
    _sut.IsDrawdownCircuitBreakerTripped.Should().BeTrue();

    _sut.UpdatePortfolioState(8_600m);  // Recover to 14% (below 15% halt)
    _sut.IsDrawdownCircuitBreakerTripped.Should().BeFalse();
    _sut.DrawdownScalingFactor.Should().Be(0.50m); // In 10-15% tier

    var signals = new[] { CreateEntrySignal("BTC", 100m) };
    var approved = await _sut.ValidateAsync(signals, CancellationToken.None);
    approved.Should().HaveCount(1);
}

[TestMethod]
public void GivenEquityGrowth_WhenUpdated_ThenHwmRatchetsUp()
{
    _sut.UpdatePortfolioState(10_000m);
    _sut.UpdatePortfolioState(10_500m);
    _sut.UpdatePortfolioState(10_200m); // Decline but still above original

    _sut.DrawdownScalingFactor.Should().Be(1.0m); // ~2.8% drawdown from 10500 — below 5% tier
}
```

##### Pattern References

- `tests/TradingApp.Application.Tests/Backtesting/Services/BacktestRiskEngineTests.cs` — existing heat-blocking test pattern

---

### Task 3.4: Run architecture tests {#task-34-run-architecture-tests}

Build the solution and run all tests to verify no regressions.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification only)
- **Success**:
  - `dotnet build TradingApp.sln` succeeds
  - `dotnet test TradingApp.sln` — all tests pass including new Phase 3 tests
- **Dependencies**: Tasks 3.1–3.3

## Phase Success Criteria

- `BacktestRiskEngine` tracks in-memory HWM and evaluates drawdown tiers using `DrawdownEvaluator`
- Entry signals blocked during halt tier in backtest — same behavior as live trading
- Drawdown-blocked signal count tracked and accessible in backtest results
- Auto-reset works correctly when equity recovers during a backtest run
- All existing backtest tests continue to pass
