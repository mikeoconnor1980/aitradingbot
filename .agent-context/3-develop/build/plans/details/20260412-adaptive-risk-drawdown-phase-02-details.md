<!-- markdownlint-disable-file -->

# Task Details: Adaptive Risk (Drawdown-Adjusted)

## Phase 2: Drawdown Tracking & Risk Engine Integration

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — sealed classes, static utility pattern, async I/O, CancellationToken
- `.github/instructions/testing.instructions.md` — MSTest, Moq, FluentAssertions, Given_When_Then naming
- `.github/instructions/dotnet-architecture.instructions.md` — Application layer services, interface abstractions
- `.agent-context/0-knowledge/33-risk-management-and-trade-sizing.md` — drawdown tier logic, CB halt/recovery
- `.agent-context/0-knowledge/14-strategy-runtime-model.md` — IRiskEngine contract
- `.agent-context/0-knowledge/15-grid-controller.md` — PositionSizeResolver call sites
- `.agent-context/0-knowledge/30-worker-execution-pipeline.md` — LiveRiskEngine singleton, UpdatePortfolioState call

---

### Task 2.1: Create DrawdownEvaluator static utility {#task-21-create-drawdownevaluator-static-utility}

Create a stateless static utility that computes drawdown state from inputs. This avoids singleton/scoped DI conflicts and produces a pure function.

- **Complexity**: Medium
- **Risk Factors**: Boundary conditions at tier thresholds must be precise (at threshold = lower tier's factor)
- **Files**:
  - `src/TradePilot.Application/Trading/Services/DrawdownEvaluator.cs` — new file
- **Success**:
  - `EvaluateDrawdown(equity, hwm, tiers)` returns `DrawdownResult` with: `NewHighWaterMark`, `DrawdownPercent`, `ScalingFactor`, `IsHalted`
  - HWM ratchets upward (new equity > old hwm → new hwm = equity)
  - Drawdown % calculated correctly: `(hwm - equity) / hwm * 100`
  - Active tier selected based on drawdown % (highest threshold not exceeded)
  - When no tiers configured, returns scaling factor 1.0
  - When drawdown < first tier, returns scaling factor 1.0
- **Dependencies**: Phase 1 (DrawdownTier record)

#### Implementation Details

```csharp
// src/TradePilot.Application/Trading/Services/DrawdownEvaluator.cs — new file
using TradePilot.Application.StrategyAuthoring.Models;

namespace TradePilot.Application.Trading.Services;

public sealed record DrawdownResult
{
    public required decimal NewHighWaterMark { get; init; }
    public required decimal DrawdownPercent { get; init; }
    public required decimal ScalingFactor { get; init; }
    public required bool IsHalted { get; init; }
}

internal static class DrawdownEvaluator
{
    public static DrawdownResult Evaluate(
        decimal currentEquity,
        decimal highWaterMark,
        IReadOnlyList<DrawdownTier> tiers)
    {
        // Ratchet HWM upward
        var newHwm = Math.Max(highWaterMark, currentEquity);

        // Calculate drawdown percentage
        var drawdownPercent = newHwm > 0
            ? (newHwm - currentEquity) / newHwm * 100m
            : 0m;

        // Find the active tier (highest threshold that drawdown has reached)
        var scalingFactor = 1.0m;
        for (var i = tiers.Count - 1; i >= 0; i--)
        {
            if (drawdownPercent >= tiers[i].ThresholdPercent)
            {
                scalingFactor = tiers[i].ScalingFactor;
                break;
            }
        }

        return new DrawdownResult
        {
            NewHighWaterMark = newHwm,
            DrawdownPercent = drawdownPercent,
            ScalingFactor = scalingFactor,
            IsHalted = scalingFactor == 0.0m,
        };
    }
}
```

##### Pattern References

- `src/TradePilot.Application/Trading/Services/PositionSizeResolver.cs` — existing `internal static class` utility pattern
- `src/TradePilot.Application/Trading/Services/PortfolioHeatCalculator.cs` — stateless calculator pattern

---

### Task 2.2: Extend IRiskEngine with drawdown state {#task-22-extend-iriskengine-with-drawdown-state}

Add default-body methods/properties to `IRiskEngine` for drawdown state, maintaining backward compatibility with `PassThroughRiskEngine`.

- **Complexity**: Low
- **Risk Factors**: Default body methods ensure no breaking changes for existing implementations
- **Files**:
  - `src/TradePilot.Application/Abstractions/Services/IRiskEngine.cs` — modification
- **Success**:
  - `UpdateDrawdownState(decimal scalingFactor, bool isHalted)` default no-op method added
  - `DrawdownScalingFactor` property with default return of `1.0m`
  - `IsDrawdownCircuitBreakerTripped` property with default return of `false`
  - Existing implementations continue to compile without changes
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradePilot.Application/Abstractions/Services/IRiskEngine.cs — add these members
public interface IRiskEngine
{
    // ... existing members ...

    /// <summary>Updates the drawdown state computed by the scheduler from equity vs HWM.</summary>
    void UpdateDrawdownState(decimal scalingFactor, bool isHalted) { }

    /// <summary>Current drawdown scaling factor (1.0 = full risk, 0.0 = halted).</summary>
    decimal DrawdownScalingFactor => 1.0m;

    /// <summary>Whether the drawdown circuit breaker is currently active.</summary>
    bool IsDrawdownCircuitBreakerTripped => false;
}
```

##### Pattern References

- `src/TradePilot.Application/Abstractions/Services/IRiskEngine.cs` — existing default-body method pattern (`void UpdatePortfolioState(decimal accountEquity) { }`)

---

### Task 2.3: Add drawdown CB to LiveRiskEngine.ValidateAsync {#task-23-add-drawdown-cb-to-liveriskenginevalidateasync}

Extend `LiveRiskEngine` to store drawdown state and enforce the drawdown circuit breaker in `ValidateAsync`.

- **Complexity**: Medium
- **Risk Factors**: Must not interfere with existing daily-loss CB; both operate independently
- **Files**:
  - `src/TradePilot.Application/Trading/Services/LiveRiskEngine.cs` — modification
- **Success**:
  - `_drawdownScalingFactor` and `_drawdownCircuitBreakerTripped` fields added
  - `UpdateDrawdownState` sets both fields
  - `ValidateAsync` blocks entry signals when `_drawdownCircuitBreakerTripped` (after daily-loss CB check)
  - Risk-reducing signals still pass through even when drawdown CB is tripped
  - Daily-loss CB and drawdown CB operate independently
  - Logging at CRITICAL level when drawdown CB trips, WARNING when it resets
- **Dependencies**: Task 2.2

#### Implementation Details

```csharp
// src/TradePilot.Application/Trading/Services/LiveRiskEngine.cs — modifications

// New fields (add alongside existing _circuitBreakerTripped):
private volatile bool _drawdownCircuitBreakerTripped;
private decimal _drawdownScalingFactor = 1.0m;

// New property implementations:
public decimal DrawdownScalingFactor => _drawdownScalingFactor;
public bool IsDrawdownCircuitBreakerTripped => _drawdownCircuitBreakerTripped;

// New method:
public void UpdateDrawdownState(decimal scalingFactor, bool isHalted)
{
    var wasHalted = _drawdownCircuitBreakerTripped;
    _drawdownScalingFactor = scalingFactor;
    _drawdownCircuitBreakerTripped = isHalted;

    if (isHalted && !wasHalted)
        _logger.LogCritical("Drawdown circuit breaker TRIPPED — all new entries halted");
    else if (!isHalted && wasHalted)
        _logger.LogWarning("Drawdown circuit breaker RESET — trading resumed at scaling factor {ScalingFactor}", scalingFactor);
}

// In ValidateAsync — add drawdown CB check after existing CB check:
// ... existing code ...
if (_circuitBreakerTripped)
{
    _logger.LogWarning("Signal {Type} blocked: daily-loss circuit breaker active", signal.Type);
    continue;
}

// NEW: drawdown circuit breaker check
if (_drawdownCircuitBreakerTripped)
{
    _logger.LogWarning("Signal {Type} blocked: drawdown circuit breaker active", signal.Type);
    continue;
}
// ... existing order size / open order / heat checks ...
```

##### Pattern References

- `src/TradePilot.Application/Trading/Services/LiveRiskEngine.cs` — existing `_circuitBreakerTripped` pattern, `ValidateAsync` signal filtering loop

---

### Task 2.4: Add DrawdownScalingFactor to MarketContext {#task-24-add-drawdownscalingfactor-to-marketcontext}

Add a `DrawdownScalingFactor` property to `MarketContext` so GridController and SignalController can apply it at PositionSizeResolver call sites.

- **Complexity**: Low
- **Risk Factors**: None — additive property with default value
- **Files**:
  - `src/TradePilot.Application/Trading/Models/MarketContext.cs` — modification
- **Success**:
  - `DrawdownScalingFactor` property exists with default value `1.0m`
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradePilot.Application/Trading/Models/MarketContext.cs — add property
public decimal DrawdownScalingFactor { get; set; } = 1.0m;
```

##### Pattern References

- `src/TradePilot.Application/Trading/Models/MarketContext.cs` — existing mutable property pattern (`AccountEquity`)

---

### Task 2.5: Wire drawdown evaluation into StrategyScheduler {#task-25-wire-drawdown-evaluation-into-strategyscheduler}

After `UpdatePortfolioState` is called, evaluate drawdown using `DrawdownEvaluator`, update the risk engine state, set the scaling factor on `MarketContext`, and persist HWM changes.

- **Complexity**: High
- **Risk Factors**: Singleton `LiveRiskEngine` lifetime vs scoped repository access; HWM persistence timing; must handle null HWM (first run)
- **Files**:
  - `src/TradePilot.Application/Scheduling/StrategyScheduler.cs` — modification
- **Success**:
  - `DrawdownEvaluator.Evaluate()` called after `UpdatePortfolioState`
  - `_riskEngine.UpdateDrawdownState(scalingFactor, isHalted)` called with result
  - `context.DrawdownScalingFactor` set with the result
  - HWM persisted to strategy entity when it changes
  - First-run HWM initialized from current equity
  - HWM loaded from strategy entity at start of cycle
- **Dependencies**: Tasks 2.1, 2.2, 2.4, Phase 1 (HWM on Strategy entity)

#### Implementation Details

```csharp
// src/TradePilot.Application/Scheduling/StrategyScheduler.cs — modification
// In the candle processing flow, after UpdatePortfolioState:

_riskEngine.UpdatePortfolioState(context.AccountEquity);

// NEW: Evaluate drawdown and apply scaling
var currentHwm = strategy.HighWaterMarkUsd ?? context.AccountEquity;
var drawdownResult = DrawdownEvaluator.Evaluate(
    context.AccountEquity,
    currentHwm,
    _riskLimits.DrawdownTiers);

_riskEngine.UpdateDrawdownState(drawdownResult.ScalingFactor, drawdownResult.IsHalted);
context.DrawdownScalingFactor = drawdownResult.ScalingFactor;

// Persist HWM if it changed
if (drawdownResult.NewHighWaterMark != currentHwm)
{
    strategy.UpdateHighWaterMark(drawdownResult.NewHighWaterMark);
    // Persist via existing strategy repository / unit of work
}
```

Note: The implementing agent should inspect `StrategyScheduler`'s constructor for the injected `IStrategyRepository` and follow the existing `UpdateAsync`/`SaveChangesAsync` pattern for persisting the HWM change.

##### Pattern References

- `src/TradePilot.Application/Scheduling/StrategyScheduler.cs` — existing `UpdatePortfolioState` call site, `ResolveAccountEquity` flow
- `src/TradePilot.Application/Trading/Services/DrawdownEvaluator.cs` — new utility from Task 2.1

---

### Task 2.6: Apply scaling factor at PositionSizeResolver call sites {#task-26-apply-scaling-factor-at-positionsizeresolver-call-sites}

Multiply the `PositionSizeResolver.ResolveNotional` result by `context.DrawdownScalingFactor` at both call sites.

- **Complexity**: Low
- **Risk Factors**: Must apply AFTER the resolver returns, not before; must not apply when scaling = 0.0 (that case is blocked by CB upstream)
- **Files**:
  - `src/TradePilot.Application/Trading/Services/GridController.cs` — modification
  - `src/TradePilot.Application/Trading/Services/SignalController.cs` — modification
- **Success**:
  - Both call sites multiply the resolved notional by `context.DrawdownScalingFactor`
  - When scaling factor is 1.0 (no drawdown), behavior is unchanged
  - When scaling factor is 0.5, position size is halved
- **Dependencies**: Task 2.4

#### Implementation Details

```csharp
// src/TradePilot.Application/Trading/Services/GridController.cs — modification (line ~148)
var positionSize = PositionSizeResolver.ResolveNotional(config.Risk, context.AccountEquity, stopLossPercent);
positionSize *= context.DrawdownScalingFactor; // Apply drawdown scaling overlay

// src/TradePilot.Application/Trading/Services/SignalController.cs — modification (line ~61)
var notional = PositionSizeResolver.ResolveNotional(config.Risk, context.AccountEquity, stopLossPercent);
notional *= context.DrawdownScalingFactor; // Apply drawdown scaling overlay
```

##### Pattern References

- `src/TradePilot.Application/Trading/Services/GridController.cs` — existing `PositionSizeResolver.ResolveNotional` call
- `src/TradePilot.Application/Trading/Services/SignalController.cs` — existing `PositionSizeResolver.ResolveNotional` call

---

### Task 2.7: Persist HWM changes via strategy repository {#task-27-persist-hwm-changes-via-strategy-repository}

Ensure the strategy repository can update the `HighWaterMarkUsd` column. Add a method if the existing repository pattern does not support partial updates.

- **Complexity**: Low
- **Risk Factors**: Existing repository may already support full entity updates; verify no unnecessary overwrites
- **Files**:
  - `src/TradePilot.Persistence/Repositories/StrategyRepository.cs` — modification (if needed)
  - or use existing `UpdateAsync` / `SaveChangesAsync` pattern
- **Success**:
  - HWM can be updated and persisted via the existing strategy repository
  - HWM is loaded from the database when a strategy is activated
- **Dependencies**: Phase 1 Task 1.4

#### Implementation Details

If the existing `IStrategyRepository` already has an `UpdateAsync` method that persists entity changes, no new method is needed — the scheduler can simply call `strategy.UpdateHighWaterMark(newHwm)` and use the existing save pattern.

If a lightweight update is preferred (avoiding full entity serialization), add:

```csharp
// In the strategy repository — optional targeted update
public async Task UpdateHighWaterMarkAsync(Guid strategyId, decimal highWaterMark, CancellationToken ct)
{
    await _context.Strategies
        .Where(s => s.Id == strategyId)
        .ExecuteUpdateAsync(s => s.SetProperty(x => x.HighWaterMarkUsd, highWaterMark), ct);
}
```

The implementing agent should inspect the existing repository pattern and choose the approach that matches.

##### Pattern References

- `src/TradePilot.Persistence/Repositories/` — existing repository update patterns

---

### Task 2.8: Unit tests for Phase 2 {#task-28-unit-tests-for-phase-2}

Write comprehensive unit tests for `DrawdownEvaluator`, `LiveRiskEngine` drawdown CB, and scaling factor application.

- **Complexity**: High
- **Risk Factors**: Many boundary conditions at tier thresholds
- **Files**:
  - `tests/TradePilot.Application.Tests/Trading/Services/DrawdownEvaluatorTests.cs` — new file
  - `tests/TradePilot.Application.Tests/Trading/Services/LiveRiskEngineTests.cs` — add drawdown CB tests
- **Success**:
  - DrawdownEvaluator tests cover: HWM ratchet up, HWM stays on decline, each tier boundary, between tiers, no tiers, halt tier, recovery
  - LiveRiskEngine tests cover: drawdown CB blocks entry, drawdown CB passes risk-reducing, daily-loss CB independent from drawdown CB, drawdown CB reset
  - All tests pass
- **Dependencies**: Tasks 2.1–2.6

#### Implementation Details

```csharp
// tests/TradePilot.Application.Tests/Trading/Services/DrawdownEvaluatorTests.cs — new file
using FluentAssertions;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Services;

namespace TradePilot.Application.Tests.Trading.Services;

[TestClass]
public sealed class DrawdownEvaluatorTests
{
    private static readonly IReadOnlyList<DrawdownTier> DefaultTiers = new[]
    {
        new DrawdownTier { ThresholdPercent = 5m, ScalingFactor = 0.75m },
        new DrawdownTier { ThresholdPercent = 10m, ScalingFactor = 0.50m },
        new DrawdownTier { ThresholdPercent = 15m, ScalingFactor = 0.0m },
    };

    [TestMethod]
    public void GivenEquityAboveHwm_WhenEvaluated_ThenHwmRatchetsUp()
    {
        var result = DrawdownEvaluator.Evaluate(10_500m, 10_000m, DefaultTiers);
        result.NewHighWaterMark.Should().Be(10_500m);
        result.DrawdownPercent.Should().Be(0m);
        result.ScalingFactor.Should().Be(1.0m);
        result.IsHalted.Should().BeFalse();
    }

    [TestMethod]
    public void GivenDrawdownAt7Percent_WhenEvaluated_ThenScalingIs075()
    {
        // 7% drawdown: equity = 9300, hwm = 10000
        var result = DrawdownEvaluator.Evaluate(9_300m, 10_000m, DefaultTiers);
        result.DrawdownPercent.Should().Be(7m);
        result.ScalingFactor.Should().Be(0.75m);
        result.IsHalted.Should().BeFalse();
    }

    [TestMethod]
    public void GivenDrawdownAt12Percent_WhenEvaluated_ThenScalingIs050()
    {
        var result = DrawdownEvaluator.Evaluate(8_800m, 10_000m, DefaultTiers);
        result.DrawdownPercent.Should().Be(12m);
        result.ScalingFactor.Should().Be(0.50m);
        result.IsHalted.Should().BeFalse();
    }

    [TestMethod]
    public void GivenDrawdownAt16Percent_WhenEvaluated_ThenIsHalted()
    {
        var result = DrawdownEvaluator.Evaluate(8_400m, 10_000m, DefaultTiers);
        result.DrawdownPercent.Should().Be(16m);
        result.ScalingFactor.Should().Be(0.0m);
        result.IsHalted.Should().BeTrue();
    }

    [TestMethod]
    public void GivenDrawdownExactlyAtTierThreshold_WhenEvaluated_ThenTierApplies()
    {
        // Exactly at 5% threshold
        var result = DrawdownEvaluator.Evaluate(9_500m, 10_000m, DefaultTiers);
        result.DrawdownPercent.Should().Be(5m);
        result.ScalingFactor.Should().Be(0.75m);
    }

    [TestMethod]
    public void GivenDrawdownBelowFirstTier_WhenEvaluated_ThenFullScaling()
    {
        var result = DrawdownEvaluator.Evaluate(9_700m, 10_000m, DefaultTiers);
        result.DrawdownPercent.Should().Be(3m);
        result.ScalingFactor.Should().Be(1.0m);
    }

    [TestMethod]
    public void GivenNoTiers_WhenEvaluated_ThenFullScaling()
    {
        var result = DrawdownEvaluator.Evaluate(8_000m, 10_000m, []);
        result.ScalingFactor.Should().Be(1.0m);
        result.IsHalted.Should().BeFalse();
    }

    [TestMethod]
    public void GivenEquityDecline_WhenEvaluated_ThenHwmDoesNotDecrease()
    {
        var result = DrawdownEvaluator.Evaluate(9_000m, 10_000m, DefaultTiers);
        result.NewHighWaterMark.Should().Be(10_000m);
    }

    [TestMethod]
    public void GivenRecoveryFromHaltToBelow15Percent_WhenEvaluated_ThenNotHalted()
    {
        // Recovery: was at 16%, now at 14%
        var result = DrawdownEvaluator.Evaluate(8_600m, 10_000m, DefaultTiers);
        result.DrawdownPercent.Should().Be(14m);
        result.ScalingFactor.Should().Be(0.50m);
        result.IsHalted.Should().BeFalse();
    }
}
```

Additional tests in `LiveRiskEngineTests.cs`:

```csharp
// tests/TradePilot.Application.Tests/Trading/Services/LiveRiskEngineTests.cs — add tests

[TestMethod]
public async Task GivenDrawdownCBTripped_WhenDeployGridSignal_ThenBlocked()
{
    _sut.UpdateDrawdownState(0.0m, isHalted: true);
    var signals = new[] { CreateDeployGridSignal() };
    var approved = await _sut.ValidateAsync(signals, CancellationToken.None);
    approved.Should().BeEmpty();
}

[TestMethod]
public async Task GivenDrawdownCBTripped_WhenTakeProfitSignal_ThenPassesThrough()
{
    _sut.UpdateDrawdownState(0.0m, isHalted: true);
    var signals = new[] { CreateTakeProfitSignal() };
    var approved = await _sut.ValidateAsync(signals, CancellationToken.None);
    approved.Should().HaveCount(1);
}

[TestMethod]
public async Task GivenDailyLossCBTrippedButDrawdownNot_WhenSignal_ThenBlockedByDailyLoss()
{
    // Trip daily-loss CB
    _sut.RecordLoss(_limits.MaxDailyLossUsd + 1);
    // Drawdown not tripped
    _sut.UpdateDrawdownState(1.0m, isHalted: false);

    var signals = new[] { CreateDeployGridSignal() };
    var approved = await _sut.ValidateAsync(signals, CancellationToken.None);
    approved.Should().BeEmpty();
}

[TestMethod]
public async Task GivenDrawdownCBTrippedButDailyLossNot_WhenSignal_ThenBlockedByDrawdown()
{
    _sut.UpdateDrawdownState(0.0m, isHalted: true);
    var signals = new[] { CreateDeployGridSignal() };
    var approved = await _sut.ValidateAsync(signals, CancellationToken.None);
    approved.Should().BeEmpty();
}
```

##### Pattern References

- `tests/TradePilot.Application.Tests/Trading/Services/LiveRiskEngineTests.cs` — existing CB test structure
- `tests/TradePilot.Application.Tests/Trading/Services/PositionSizeResolverTests.cs` — static utility test pattern

---

### Task 2.9: Run architecture tests {#task-29-run-architecture-tests}

Build the solution and run all tests to verify no regressions.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification only)
- **Success**:
  - `dotnet build TradePilot.sln` succeeds
  - `dotnet test TradePilot.sln` — all tests pass including new Phase 2 tests
- **Dependencies**: Tasks 2.1–2.8

## Phase Success Criteria

- `DrawdownEvaluator.Evaluate()` correctly computes HWM ratchet, drawdown %, scaling factor, and halt state
- `LiveRiskEngine` has independent drawdown CB that blocks entry signals when halted
- `MarketContext.DrawdownScalingFactor` carries scaling factor to controller call sites
- Both `PositionSizeResolver` call sites apply the drawdown scaling overlay
- `StrategyScheduler` evaluates drawdown each candle cycle and persists HWM changes
- All unit tests pass with full coverage of tier boundaries, CB activation/reset, and independence from daily-loss CB
