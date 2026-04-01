<!-- markdownlint-disable-file -->

# Task Details: Grid Ladder Remains Active After Partial Fill

## Phase 1: GridController Lifecycle Fix + Unit Tests

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — sealed classes, `_camelCase` fields, `Given_When_Then` test naming, `CancellationToken` handling, guard clauses
- `.github/instructions/testing.instructions.md` — MSTest, Moq, FluentAssertions ≤ v6, builder pattern, run tests per phase
- `.github/instructions/dotnet-architecture.instructions.md` — Clean Architecture, interfaces in `Abstractions/`, impl in feature folder
- `.agent-context/0-knowledge/15-grid-controller.md` — Grid lifecycle states and controller responsibilities
- `.agent-context/0-knowledge/16-signal-contracts.md` — TradingSignal model, signal types
- `.agent-context/0-knowledge/24-backtesting-grid-engine-explained.md` — Current (buggy) behavior documentation

### Task 1.1: Refactor `GridController.ProcessAsync` to be lifecycle-aware {#task-11-refactor-gridcontroller-processasync-to-be-lifecycle-aware}

Restructure the `positionState.IsOpen` branch in `GridController.ProcessAsync` to check `gridState.Lifecycle` before deciding whether to emit a `TakeProfit` signal. The controller must:
- **Stop-loss**: Check from any filled state (`PartiallyFilled`, `FullyFilled`, or falling through). If triggered → emit `TakeProfit` (Market), transition to `Closing`.
- **Already `Closing`**: Return empty signals — exit order is already in the engine.
- **`PartiallyFilled`**: Check candle close against TP level. If reached → emit `TakeProfit` (Market at close), transition to `Closing`. Otherwise → return empty (ladder stays active).
- **`FullyFilled`**: Emit `TakeProfit` (Limit), transition to `Closing`.
- **Fallback** (other states like `Deploying`): Emit `TakeProfit` (Limit), transition to `Closing` — preserves safety for edge cases.

- **Complexity**: Medium
- **Risk Factors**: This is the core bug fix; incorrect lifecycle branching could break the entire grid/backtest pipeline. Must handle all lifecycle states including edge cases.
- **Files**:
  - `src/TradingApp.Application/Trading/Services/GridController.cs` — Refactor the `if (positionState.IsOpen)` branch
- **Success**:
  - `GridController` no longer transitions to `Closing` when `lifecycle == PartiallyFilled` (unless SL or TP hit)
  - `GridController` correctly transitions to `Closing` when `lifecycle == FullyFilled`
  - Stop-loss check works from all filled states
  - Controller returns empty signals when `lifecycle == Closing` (no re-emission)
  - Candle-close TP check for partial fills uses `context.CurrentCandle.Close >= takeProfitTrigger`
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Application/Trading/Services/GridController.cs — modification
// Replace the entire `if (positionState.IsOpen) { ... }` block (lines 36–62) with:

        if (positionState.IsOpen)
        {
            var stopLossPercent = Math.Abs(config.StopLossPercent);
            var takeProfitPercent = Math.Abs(config.TakeProfitPercent);
            var stopLossTrigger = positionState.AverageEntryPrice * (1m - (stopLossPercent / 100m));
            var takeProfitTrigger = positionState.AverageEntryPrice * (1m + (takeProfitPercent / 100m));
            var shouldStopOut = stopLossPercent > 0m && context.CurrentCandle.Close <= stopLossTrigger;
            var gridCycleId = gridState.GridCycleId ?? "default";

            // Stop-loss from any filled state — immediate market close
            if (shouldStopOut)
            {
                gridState.Lifecycle = GridLifecycle.Closing;

                return Task.FromResult<IReadOnlyList<TradingSignal>>(
                [
                    new TradingSignal
                    {
                        SignalType = "TakeProfit",
                        Symbol = context.Symbol,
                        Reason = "Stop loss triggered.",
                        Parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["targetPrice"] = context.CurrentCandle.Close,
                            ["size"] = Math.Abs(positionState.Size),
                            ["orderType"] = OrderType.Market.ToString(),
                            ["gridCycleId"] = gridCycleId
                        }
                    }
                ]);
            }

            // Already closing — exit order in the engine, wait for fill
            if (gridState.Lifecycle == GridLifecycle.Closing)
            {
                return Task.FromResult<IReadOnlyList<TradingSignal>>(Array.Empty<TradingSignal>());
            }

            // Partially filled — ladder stays active, check TP at candle close
            if (gridState.Lifecycle == GridLifecycle.PartiallyFilled)
            {
                if (takeProfitPercent > 0m && context.CurrentCandle.Close >= takeProfitTrigger)
                {
                    gridState.Lifecycle = GridLifecycle.Closing;

                    return Task.FromResult<IReadOnlyList<TradingSignal>>(
                    [
                        new TradingSignal
                        {
                            SignalType = "TakeProfit",
                            Symbol = context.Symbol,
                            Reason = "Take profit triggered (partial fill).",
                            Parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["targetPrice"] = context.CurrentCandle.Close,
                                ["size"] = Math.Abs(positionState.Size),
                                ["orderType"] = OrderType.Market.ToString(),
                                ["gridCycleId"] = gridCycleId
                            }
                        }
                    ]);
                }

                return Task.FromResult<IReadOnlyList<TradingSignal>>(Array.Empty<TradingSignal>());
            }

            // Fully filled or fallback — place limit take-profit, transition to Closing
            gridState.Lifecycle = GridLifecycle.Closing;
            var orderType = OrderType.Limit;
            var targetPrice = takeProfitTrigger;

            return Task.FromResult<IReadOnlyList<TradingSignal>>(
            [
                new TradingSignal
                {
                    SignalType = "TakeProfit",
                    Symbol = context.Symbol,
                    Reason = "Take profit active.",
                    Parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["targetPrice"] = targetPrice,
                        ["size"] = Math.Abs(positionState.Size),
                        ["orderType"] = orderType.ToString(),
                        ["gridCycleId"] = gridCycleId
                    }
                }
            ]);
        }
```

##### Pattern References

- Current `GridController.ProcessAsync`: `src/TradingApp.Application/Trading/Services/GridController.cs` (lines 36–62)
- `GridLifecycle` enum: `src/TradingApp.Application/Trading/Models/GridLifecycle.cs`
- `GridState` model: `src/TradingApp.Application/Trading/Models/GridState.cs`
- `PositionState.IsOpen`: `src/TradingApp.Application/Trading/Models/PositionState.cs` (line 13)

---

### Task 1.2: Rename `CancellationReason.PositionOpened` to `TakeProfitTriggered` {#task-12-rename-cancellationreason-positionopened}

Rename the misleading `PositionOpened` enum value to `TakeProfitTriggered`. After the fix, this reason is only used when a genuine take-profit exit occurs — the name should reflect that. Update all usages in both C# and TypeScript.

- **Complexity**: Low
- **Risk Factors**: Simple rename; must update all references including the frontend TypeScript enum
- **Files**:
  - `src/TradingApp.Application/Backtesting/Models/CancellationReason.cs` — Rename enum value
  - `src/TradingApp.Application/Trading/Services/BacktestPositionManager.cs` — Update usage in `PlaceTakeProfitAsync`
  - `frontend/trading-ui/src/app/core/models/backtest-debug.model.ts` — Update TypeScript enum
  - `tests/TradingApp.Api.Tests/Controllers/BacktestsControllerTests.cs` — Update test assertion using `CancellationReason.PositionOpened` (line 752)
- **Success**:
  - `CancellationReason.PositionOpened` no longer exists
  - `CancellationReason.TakeProfitTriggered` is used in `PlaceTakeProfitAsync`
  - Frontend TypeScript enum matches
  - All usages compile cleanly (including test projects)
- **Dependencies**: None (can be done in parallel with Task 1.1)

---

### Task 1.3: Create `GridControllerTests.cs` with comprehensive unit tests {#task-13-create-gridcontrollertests}

Create a new unit test file for `GridController` covering all lifecycle transition paths. Currently no unit tests exist for this class. Follow the project's test conventions: MSTest, Moq, FluentAssertions v6, `Given_When_Then` naming.

- **Complexity**: Medium
- **Risk Factors**: First time the controller is unit-tested in isolation; need to carefully construct `GridState`, `PositionState`, `StrategyEvaluation`, and `MarketContext` inputs
- **Files**:
  - `tests/TradingApp.Application.Tests/Trading/Services/GridControllerTests.cs` — New file
- **Success**:
  - All lifecycle transition paths are covered
  - Tests pass with the refactored controller
  - Tests would FAIL with the old (buggy) controller behavior
- **Dependencies**: Task 1.1 (refactored controller)

#### Implementation Details

```csharp
// tests/TradingApp.Application.Tests/Trading/Services/GridControllerTests.cs — new file

using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Application.Trading.Services;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Tests.Trading.Services;

[TestClass]
public sealed class GridControllerTests
{
    private GridController _sut = default!;
    private const string DefaultConfigJson =
        "{\"gridLevels\":5,\"gridSpacing\":0.5,\"takeProfitPercent\":1,\"breakdownThreshold\":2," +
        "\"makerFee\":0.0001,\"takerFee\":0.00035,\"slippage\":0,\"positionSize\":100," +
        "\"leverage\":3,\"stopLossPercent\":5}";

    [TestInitialize]
    public void Setup()
    {
        _sut = new GridController();
    }

    // --- PartiallyFilled: ladder stays active ---

    [TestMethod]
    public async Task GivenPartiallyFilledGrid_WhenPositionOpenAndNoExitCondition_ThenReturnsEmptySignals()
    {
        var gridState = CreateGridState(GridLifecycle.PartiallyFilled, filledLevels: 2, totalLevels: 5);
        var positionState = CreatePositionState(size: 2m, avgEntry: 99.5m);
        var context = CreateMarketContext(close: 99.0m); // above SL, below TP

        var signals = await _sut.ProcessAsync(
            CreateSetupDetected(), context, gridState, positionState, DefaultConfigJson);

        signals.Should().BeEmpty();
        gridState.Lifecycle.Should().Be(GridLifecycle.PartiallyFilled);
    }

    [TestMethod]
    public async Task GivenPartiallyFilledGrid_WhenCandleCloseReachesTakeProfit_ThenEmitsTakeProfitAndClosing()
    {
        var gridState = CreateGridState(GridLifecycle.PartiallyFilled, filledLevels: 2, totalLevels: 5);
        var positionState = CreatePositionState(size: 2m, avgEntry: 99.5m);
        // TP trigger = 99.5 * 1.01 = 100.495 — close above that
        var context = CreateMarketContext(close: 101.0m);

        var signals = await _sut.ProcessAsync(
            CreateSetupDetected(), context, gridState, positionState, DefaultConfigJson);

        signals.Should().ContainSingle();
        signals[0].SignalType.Should().Be("TakeProfit");
        signals[0].Parameters!["orderType"].Should().Be("Market");
        signals[0].Reason.Should().Contain("partial fill");
        gridState.Lifecycle.Should().Be(GridLifecycle.Closing);
    }

    [TestMethod]
    public async Task GivenPartiallyFilledGrid_WhenStopLossTriggered_ThenEmitsMarketTakeProfitAndClosing()
    {
        var gridState = CreateGridState(GridLifecycle.PartiallyFilled, filledLevels: 2, totalLevels: 5);
        var positionState = CreatePositionState(size: 2m, avgEntry: 100m);
        // SL trigger = 100 * (1 - 5/100) = 95 — close below that
        var context = CreateMarketContext(close: 94.0m);

        var signals = await _sut.ProcessAsync(
            CreateSetupDetected(), context, gridState, positionState, DefaultConfigJson);

        signals.Should().ContainSingle();
        signals[0].SignalType.Should().Be("TakeProfit");
        signals[0].Parameters!["orderType"].Should().Be("Market");
        signals[0].Reason.Should().Contain("Stop loss");
        gridState.Lifecycle.Should().Be(GridLifecycle.Closing);
    }

    // --- FullyFilled: emit limit TP ---

    [TestMethod]
    public async Task GivenFullyFilledGrid_WhenPositionOpen_ThenEmitsLimitTakeProfitAndClosing()
    {
        var gridState = CreateGridState(GridLifecycle.FullyFilled, filledLevels: 5, totalLevels: 5);
        var positionState = CreatePositionState(size: 5m, avgEntry: 99.0m);
        var context = CreateMarketContext(close: 99.5m);

        var signals = await _sut.ProcessAsync(
            CreateSetupDetected(), context, gridState, positionState, DefaultConfigJson);

        signals.Should().ContainSingle();
        signals[0].SignalType.Should().Be("TakeProfit");
        signals[0].Parameters!["orderType"].Should().Be("Limit");
        gridState.Lifecycle.Should().Be(GridLifecycle.Closing);
    }

    [TestMethod]
    public async Task GivenFullyFilledGrid_WhenStopLossTriggered_ThenEmitsMarketSellOverLimitTp()
    {
        var gridState = CreateGridState(GridLifecycle.FullyFilled, filledLevels: 5, totalLevels: 5);
        var positionState = CreatePositionState(size: 5m, avgEntry: 100m);
        var context = CreateMarketContext(close: 94.0m);

        var signals = await _sut.ProcessAsync(
            CreateSetupDetected(), context, gridState, positionState, DefaultConfigJson);

        signals.Should().ContainSingle();
        signals[0].Parameters!["orderType"].Should().Be("Market");
        signals[0].Reason.Should().Contain("Stop loss");
        gridState.Lifecycle.Should().Be(GridLifecycle.Closing);
    }

    // --- Closing: no re-emission ---

    [TestMethod]
    public async Task GivenClosingLifecycle_WhenPositionStillOpen_ThenReturnsEmptySignals()
    {
        var gridState = CreateGridState(GridLifecycle.Closing, filledLevels: 5, totalLevels: 5);
        var positionState = CreatePositionState(size: 5m, avgEntry: 99.0m);
        var context = CreateMarketContext(close: 99.5m);

        var signals = await _sut.ProcessAsync(
            CreateSetupDetected(), context, gridState, positionState, DefaultConfigJson);

        signals.Should().BeEmpty();
        gridState.Lifecycle.Should().Be(GridLifecycle.Closing);
    }

    // --- Deploy guard still works ---

    [TestMethod]
    public async Task GivenPartiallyFilledGrid_WhenSetupDetectedAndNoPosition_ThenDoesNotRedeploy()
    {
        var gridState = CreateGridState(GridLifecycle.PartiallyFilled, filledLevels: 2, totalLevels: 5);
        var positionState = CreatePositionState(size: 0m, avgEntry: 0m);
        var context = CreateMarketContext(close: 100m);

        var signals = await _sut.ProcessAsync(
            CreateSetupDetected(), context, gridState, positionState, DefaultConfigJson);

        signals.Should().BeEmpty();
        gridState.Lifecycle.Should().Be(GridLifecycle.PartiallyFilled);
    }

    // --- Inactive/Closed: deploy works ---

    [TestMethod]
    public async Task GivenInactiveGrid_WhenSetupDetectedAndNoPosition_ThenEmitsDeployGrid()
    {
        var gridState = CreateGridState(GridLifecycle.Inactive, filledLevels: 0, totalLevels: 0);
        var positionState = CreatePositionState(size: 0m, avgEntry: 0m);
        var context = CreateMarketContext(close: 100m);

        var signals = await _sut.ProcessAsync(
            CreateSetupDetected(), context, gridState, positionState, DefaultConfigJson);

        signals.Should().ContainSingle();
        signals[0].SignalType.Should().Be("DeployGrid");
        gridState.Lifecycle.Should().Be(GridLifecycle.Deploying);
        // Optional: also verify signal parameters (anchorPrice, gridLevels, etc.) for thoroughness
    }

    // --- Stop-loss from Closing state still emits (SL takes priority) ---

    [TestMethod]
    public async Task GivenClosingLifecycle_WhenStopLossTriggered_ThenStillEmitsMarketExit()
    {
        var gridState = CreateGridState(GridLifecycle.Closing, filledLevels: 5, totalLevels: 5);
        var positionState = CreatePositionState(size: 5m, avgEntry: 100m);
        var context = CreateMarketContext(close: 94.0m);

        var signals = await _sut.ProcessAsync(
            CreateSetupDetected(), context, gridState, positionState, DefaultConfigJson);

        signals.Should().ContainSingle();
        signals[0].Parameters!["orderType"].Should().Be("Market");
        signals[0].Reason.Should().Contain("Stop loss");
    }

    // --- Helpers ---

    private static GridState CreateGridState(
        GridLifecycle lifecycle, int filledLevels = 0, int totalLevels = 5)
    {
        return new GridState
        {
            Lifecycle = lifecycle,
            GridCycleId = "test-cycle-001",
            FilledLevels = filledLevels,
            TotalLevels = totalLevels
        };
    }

    private static PositionState CreatePositionState(decimal size, decimal avgEntry)
    {
        return new PositionState
        {
            Symbol = "BTC",
            Size = size,
            AverageEntryPrice = avgEntry,
            UnrealisedPnL = 0m
        };
    }

    private static MarketContext CreateMarketContext(decimal close)
    {
        return new MarketContext
        {
            Symbol = "BTC",
            TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            CurrentCandle = Candle.Create(
                "Binance", "BTC", "15m",
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                close, close + 1m, close - 1m, close, 1_000m, 10),
            Indicators = new IndicatorSnapshot()
        };
    }

    private static StrategyEvaluation CreateSetupDetected()
    {
        return new StrategyEvaluation { SetupDetected = true, Reason = "Test setup" };
    }
}
```

> **Note**: The helper uses `Candle.Create(...)` (domain entity factory method) and initialises all `required` `MarketContext` properties (`Symbol`, `TimestampUtc`, `CurrentCandle`, `Indicators`). The implementing agent should verify the `Candle.Create` parameter order matches the domain entity.

##### Pattern References

- Test conventions: `tests/TradingApp.Application.Tests/Backtesting/Services/RealBacktestRunnerTests.cs` (MSTest, FluentAssertions, `Given_When_Then`)
- Test project: `tests/TradingApp.Application.Tests/TradingApp.Application.Tests.csproj`
- Global usings: `tests/TradingApp.Application.Tests/Usings.cs` (FluentAssertions, MSTest, Moq)
- `GridController` implementation: `src/TradingApp.Application/Trading/Services/GridController.cs`
- `MarketContext` / `StrategyEvaluation` types: search `src/TradingApp.Application/Trading/Models/`

---

### Task 1.4: Run tests and verify {#task-14-run-tests-and-verify}

Build the solution and run the affected test projects to verify the changes compile and all tests pass.

- **Complexity**: Low
- **Risk Factors**: Existing integration tests may need minor adjustments if their assertions depended on the old (buggy) lifecycle behavior
- **Files**: None (verification step)
- **Success**:
  - `dotnet build` succeeds with no errors
  - `dotnet test tests/TradingApp.Application.Tests --filter "FullyQualifiedName~GridController"` — all new unit tests pass
  - `dotnet test tests/TradingApp.Application.Tests --filter "FullyQualifiedName~RealBacktestRunner"` — existing integration tests still pass
  - `dotnet test` for all test projects succeeds
- **Dependencies**: Tasks 1.1, 1.2, 1.3

## Phase Success Criteria

- `GridController.ProcessAsync` correctly handles `PartiallyFilled`, `FullyFilled`, `Closing` lifecycle states
- No premature `TakeProfit` signal emission when grid is `PartiallyFilled`
- Stop-loss works from all filled states
- `CancellationReason.TakeProfitTriggered` replaces `PositionOpened`
- 10+ unit tests cover all lifecycle transition paths
- All existing and new tests pass
