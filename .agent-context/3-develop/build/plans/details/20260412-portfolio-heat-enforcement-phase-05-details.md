<!-- markdownlint-disable-file -->

# Task Details: Portfolio Heat Enforcement

## Phase 5: Backtest Heat Enforcement

## Standards and Knowledge References

- **csharp.instructions.md**: Sealed classes, `internal` visibility for implementation classes, async/await
- **testing.instructions.md**: MSTest, Moq, FluentAssertions ≤ v6, Given_When_Then naming
- **dotnet-architecture.instructions.md**: DI registration patterns, interface implementations
- **18-backtesting-architecture.md**: Replay engine, simulated execution, reuses same strategy/grid engine
- **33-risk-management-and-trade-sizing.md**: Heat enforcement should apply in backtests

## Design References

**Approach**: Create a `BacktestRiskEngine` that:
1. Only checks portfolio heat (no circuit breaker, no order size, no open order limits)
2. Uses the same `PortfolioHeatCalculator` as the live engine
3. Tracks position R internally from approved signal parameters (same as `LiveRiskEngine`)
4. Counts blocked signals for reporting in `BacktestResult`
5. Is registered as `IRiskEngine` for backtest scope when `MaxPortfolioHeatPercent > 0`

The backtest pipeline already uses `StrategyScheduler` → `IRiskEngine.ValidateAsync` → `SimulatedExecutionEngine`. By replacing `PassThroughRiskEngine` with `BacktestRiskEngine`, heat enforcement integrates naturally.

---

### Task 5.1: Create `BacktestRiskEngine` {#task-51-create-backtestriskengine}

Create a risk engine implementation that only enforces portfolio heat for backtesting.

- **Complexity**: High
- **Risk Factors**: Must handle all the same edge cases as `LiveRiskEngine` heat check; must not interfere with other risk checks
- **Files**:
  - `src/TradingApp.Application/Backtesting/Services/BacktestRiskEngine.cs` — New file
- **Success**:
  - Implements `IRiskEngine`
  - Only checks portfolio heat (no other risk checks)
  - Tracks position R from approved signals (same as `LiveRiskEngine`)
  - Provides `HeatBlockedSignalCount` property for backtest results
  - Heat check skipped when `MaxPortfolioHeatPercent = 0`
  - Risk-reducing signals always pass and update tracked R
- **Dependencies**: Phase 1

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/Services/BacktestRiskEngine.cs — new file
using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Application.Trading.Services;

namespace TradingApp.Application.Backtesting.Services;

/// <summary>
/// Risk engine for backtesting that enforces portfolio heat limits only.
/// Does not check circuit breaker, order size, or open order limits.
/// </summary>
public sealed class BacktestRiskEngine : IRiskEngine
{
    private readonly RiskLimitsConfig _limits;
    private readonly ConcurrentDictionary<string, decimal> _positionRisks = new();
    private decimal _accountEquity;
    private int _heatBlockedSignalCount;

    public BacktestRiskEngine(IOptions<RiskLimitsConfig> limits)
    {
        _limits = limits?.Value ?? throw new ArgumentNullException(nameof(limits));
    }

    /// <summary>Number of signals blocked due to portfolio heat limit during the backtest.</summary>
    public int HeatBlockedSignalCount => _heatBlockedSignalCount;

    public Task<IReadOnlyList<TradingSignal>> ValidateAsync(
        IReadOnlyList<TradingSignal> signals,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(signals);

        if (signals.Count == 0 || _limits.MaxPortfolioHeatPercent <= 0)
        {
            return Task.FromResult(signals);
        }

        var approved = new List<TradingSignal>(signals.Count);

        foreach (var signal in signals)
        {
            if (IsRiskReducing(signal))
            {
                approved.Add(signal);
                TrackPositionCloseFromSignal(signal);
                continue;
            }

            if (!CheckPortfolioHeat(signal))
            {
                Interlocked.Increment(ref _heatBlockedSignalCount);
                continue;
            }

            approved.Add(signal);
            TrackPositionOpenFromSignal(signal);
        }

        return Task.FromResult<IReadOnlyList<TradingSignal>>(approved);
    }

    public void UpdatePortfolioState(decimal accountEquity)
    {
        _accountEquity = accountEquity;
    }

    public void RecordPositionOpened(string symbol, decimal riskUsd)
    {
        _positionRisks[symbol] = riskUsd;
    }

    public void RecordPositionClosed(string symbol)
    {
        _positionRisks.TryRemove(symbol, out _);
    }

    private static bool IsRiskReducing(TradingSignal signal)
    {
        return signal.SignalType is "TakeProfit" or "CancelGrid" or "FlattenPosition" or "CloseHedge";
    }

    private bool CheckPortfolioHeat(TradingSignal signal)
    {
        if (_accountEquity <= 0)
        {
            return true;
        }

        if (signal.Parameters is not null
            && signal.Parameters.TryGetValue("estimatedRiskUsd", out var rObj))
        {
            var newTradeRiskUsd = Convert.ToDecimal(rObj);
            var currentHeatUsd = _positionRisks.Values.Sum();
            var maxHeatUsd = _accountEquity * (_limits.MaxPortfolioHeatPercent / 100m);

            if (currentHeatUsd + newTradeRiskUsd > maxHeatUsd)
            {
                return false;
            }
        }

        return true;
    }

    private void TrackPositionOpenFromSignal(TradingSignal signal)
    {
        if (signal.Parameters is not null
            && signal.Parameters.TryGetValue("estimatedRiskUsd", out var rObj))
        {
            var riskUsd = Convert.ToDecimal(rObj);
            if (riskUsd > 0)
            {
                _positionRisks[signal.Symbol] = riskUsd;
            }
        }
    }

    private void TrackPositionCloseFromSignal(TradingSignal signal)
    {
        if (signal.SignalType is "FlattenPosition" or "CloseHedge")
        {
            _positionRisks.TryRemove(signal.Symbol, out _);
        }
    }
}
```

##### Pattern References

- `src/TradingApp.Application/Trading/Services/LiveRiskEngine.cs` — `IsRiskReducing`, `ValidateAsync` structure, `ConcurrentDictionary` usage
- `src/TradingApp.Application/Trading/Services/PassThroughRiskEngine.cs` — simple `IRiskEngine` implementation

---

### Task 5.2: Register `BacktestRiskEngine` for backtest runs {#task-52-register-backtestriskengine-for-backtest-runs}

Replace `PassThroughRiskEngine` with `BacktestRiskEngine` in the API project's DI registration, so backtests get heat enforcement.

- **Complexity**: Medium
- **Risk Factors**: Must not break existing backtest functionality; need to understand how `IRiskEngine` is resolved in backtest scope; `PassThroughRiskEngine` may be used elsewhere (verify)
- **Files**:
  - `src/TradingApp.Api/Program.cs` — Change `IRiskEngine` registration from `PassThroughRiskEngine` to `BacktestRiskEngine`
  - OR: `src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs` — Create scoped `BacktestRiskEngine` per run
- **Success**:
  - Backtests use `BacktestRiskEngine` instead of `PassThroughRiskEngine`
  - Heat enforcement applies when `MaxPortfolioHeatPercent > 0`
  - When `MaxPortfolioHeatPercent = 0`, `BacktestRiskEngine` passes all signals (same as `PassThroughRiskEngine`)
  - `HeatBlockedSignalCount` is accessible from `BacktestRunner` after run completes
- **Dependencies**: Task 5.1

#### Implementation Details

There are two possible approaches — **Option B is recommended** because it provides per-run isolation (each backtest gets a fresh engine with its own state) and makes `HeatBlockedSignalCount` easily accessible after run completion (required by Task 5.3):

**Option A — Replace DI registration in `Program.cs`:**
```csharp
// src/TradingApp.Api/Program.cs — modification
// Replace: builder.Services.AddScoped<IRiskEngine, PassThroughRiskEngine>();
// With:
builder.Services.AddScoped<IRiskEngine, BacktestRiskEngine>();
```
This works if `BacktestRiskEngine` passes all signals when heat is disabled (≤ 0), which it does.

**Option B — Create per-run in `BacktestRunner`:**
If `BacktestRunner` constructs the `StrategyScheduler` manually (not via DI), create `BacktestRiskEngine` directly:
```csharp
var riskEngine = new BacktestRiskEngine(Options.Create(riskLimits));
// Pass to StrategyScheduler constructor
```

> **Note**: Read `BacktestRunner` to determine which approach fits. Option A is simpler but Option B gives more control over per-run state (e.g., accessing `HeatBlockedSignalCount` after the run).

##### Pattern References

- `src/TradingApp.Api/Program.cs` — existing `IRiskEngine` registration
- `src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs` — how `IRiskEngine` is used in backtest runs

---

### Task 5.3: Add `HeatBlockedSignalCount` to `BacktestResult` {#task-53-add-heatblockedsignalcount-to-backtestresult}

Add a field to the backtest result model to report how many signals were blocked by heat limits.

- **Complexity**: Low
- **Risk Factors**: Need to find `BacktestResult` class and the point where it's populated
- **Files**:
  - `src/TradingApp.Application/Backtesting/Models/BacktestResult.cs` — Add property
  - `src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs` — Populate from `BacktestRiskEngine`
- **Success**:
  - `BacktestResult.HeatBlockedSignalCount` property exists
  - Populated from `BacktestRiskEngine.HeatBlockedSignalCount` after backtest completes
  - Value appears in backtest result response
- **Dependencies**: Tasks 5.1, 5.2

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/Models/BacktestResult.cs — modification
// Add property:

    /// <summary>Number of entry signals blocked due to portfolio heat limit.</summary>
    public int HeatBlockedSignalCount { get; set; }
```

```csharp
// src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs — modification
// After backtest loop completes, extract the count:

    if (riskEngine is BacktestRiskEngine backtestEngine)
    {
        result.HeatBlockedSignalCount = backtestEngine.HeatBlockedSignalCount;
    }
```

##### Pattern References

- `src/TradingApp.Application/Backtesting/Models/BacktestResult.cs` — existing result model properties
- `src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs` — result population pattern

---

### Task 5.4: Unit tests for backtest heat enforcement {#task-54-unit-tests-for-backtest-heat-enforcement}

Test the `BacktestRiskEngine` heat enforcement logic.

- **Complexity**: Medium
- **Risk Factors**: None — follows established test pattern
- **Files**:
  - `tests/TradingApp.Application.Tests/Backtesting/Services/BacktestRiskEngineTests.cs` — New file
- **Success**:
  - Test: entry allowed when heat below limit
  - Test: entry blocked when heat exceeds limit and `HeatBlockedSignalCount` incremented
  - Test: risk-reducing signals always pass
  - Test: heat disabled (0) passes all signals
  - Test: position close reduces heat
  - All tests pass: `dotnet test tests/TradingApp.Application.Tests/ --filter "FullyQualifiedName~BacktestRiskEngine"`
- **Dependencies**: Task 5.1

#### Implementation Details

```csharp
// tests/TradingApp.Application.Tests/Backtesting/Services/BacktestRiskEngineTests.cs — new file
using Microsoft.Extensions.Options;
using TradingApp.Application.Backtesting.Services;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.Tests.Backtesting.Services;

[TestClass]
public sealed class BacktestRiskEngineTests
{
    private RiskLimitsConfig _limits = null!;
    private BacktestRiskEngine _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _limits = new RiskLimitsConfig { MaxPortfolioHeatPercent = 6m };
        _sut = new BacktestRiskEngine(Options.Create(_limits));
    }

    [TestMethod]
    public async Task GivenHeatBelowLimit_WhenEntrySignal_ThenAllowed()
    {
        // Arrange
        _sut.UpdatePortfolioState(10_000m);
        _sut.RecordPositionOpened("BTC", 100m); // 1% heat

        var signal = CreateEntrySignal("ETH", 100m);

        // Act
        var approved = await _sut.ValidateAsync([signal]);

        // Assert
        approved.Should().HaveCount(1);
        _sut.HeatBlockedSignalCount.Should().Be(0);
    }

    [TestMethod]
    public async Task GivenHeatExceedsLimit_WhenEntrySignal_ThenBlockedAndCounted()
    {
        // Arrange
        _sut.UpdatePortfolioState(10_000m);
        for (int i = 0; i < 6; i++)
            _sut.RecordPositionOpened($"TOKEN{i}", 100m); // 6% heat

        var signal = CreateEntrySignal("NEW", 100m);

        // Act
        var approved = await _sut.ValidateAsync([signal]);

        // Assert
        approved.Should().BeEmpty();
        _sut.HeatBlockedSignalCount.Should().Be(1);
    }

    [TestMethod]
    public async Task GivenHeatDisabled_WhenEntrySignal_ThenAllowed()
    {
        // Arrange
        _limits = _limits with { MaxPortfolioHeatPercent = 0m };
        _sut = new BacktestRiskEngine(Options.Create(_limits));
        _sut.UpdatePortfolioState(10_000m);

        var signal = CreateEntrySignal("BTC", 1000m);

        // Act
        var approved = await _sut.ValidateAsync([signal]);

        // Assert
        approved.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task GivenHeatAtLimit_WhenRiskReducingSignal_ThenAllowed()
    {
        // Arrange
        _sut.UpdatePortfolioState(10_000m);
        for (int i = 0; i < 6; i++)
            _sut.RecordPositionOpened($"TOKEN{i}", 100m);

        var signal = new TradingSignal { SignalType = "TakeProfit", Symbol = "TOKEN0" };

        // Act
        var approved = await _sut.ValidateAsync([signal]);

        // Assert
        approved.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task GivenPositionClosed_WhenHeatDrops_ThenEntryAllowed()
    {
        // Arrange
        _sut.UpdatePortfolioState(10_000m);
        for (int i = 0; i < 6; i++)
            _sut.RecordPositionOpened($"TOKEN{i}", 100m);

        _sut.RecordPositionClosed("TOKEN0"); // 6% → 5%

        var signal = CreateEntrySignal("NEW", 100m); // +1% = 6%

        // Act
        var approved = await _sut.ValidateAsync([signal]);

        // Assert
        approved.Should().HaveCount(1);
    }

    private static TradingSignal CreateEntrySignal(string symbol, decimal estimatedRiskUsd)
    {
        return new TradingSignal
        {
            SignalType = "DeployGrid",
            Symbol = symbol,
            Parameters = new Dictionary<string, object>
            {
                ["notionalUsd"] = 1000m,
                ["gridLevels"] = 1,
                ["estimatedRiskUsd"] = estimatedRiskUsd
            }
        };
    }
}
```

##### Pattern References

- `tests/TradingApp.Application.Tests/Trading/Services/LiveRiskEngineTests.cs` — signal construction, Options.Create, test setup

---

### Task 5.5: Build and test verification {#task-55-build-and-test-verification}

Build all projects and run all tests to verify no regressions.

- **Complexity**: Low
- **Risk Factors**: Potential DI resolution issues if `PassThroughRiskEngine` was used elsewhere
- **Files**: None (verification only)
- **Success**:
  - `dotnet build TradingApp.sln` succeeds
  - `dotnet test tests/TradingApp.Application.Tests/ --filter "FullyQualifiedName~BacktestRiskEngine"` — all tests pass
  - `dotnet test TradingApp.sln --no-build` — all tests pass
- **Dependencies**: Tasks 5.1–5.4

## Phase Success Criteria

- `BacktestRiskEngine` enforces portfolio heat in backtests
- Blocked signals counted and reported in `BacktestResult.HeatBlockedSignalCount`
- Heat disabled (`MaxPortfolioHeatPercent = 0`) results in pass-through behaviour
- All backtest tests pass
- All existing tests pass without regression
