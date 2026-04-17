<!-- markdownlint-disable-file -->

# Task Details: F6.75 — Signal Runtime Wiring

## Phase 1: Indicator Context Wiring in StrategyScheduler

## Standards and Knowledge References

- **csharp.instructions.md**: `sealed` classes, `_camelCase` private fields, `async/await` with `CancellationToken`
- **testing.instructions.md**: MSTest, FluentAssertions ≤ v6, Moq, `Given_When_Then` naming, builder pattern
- **dotnet-architecture.instructions.md**: Application layer services, `IStrategyConfig` marker interface
- **14-strategy-runtime-model.md**: `IStrategyEngine`, `IMarketContextBuilder`, `IGridController` pipeline
- **19-scheduling-architecture.md**: `StrategyScheduler` handles candle-close events, shared between live and backtest
- **16-signal-contracts.md**: Signal types as string constants, `TradingSignal` shape

### Task 1.1: Update `StrategyScheduler.HandleCandleClosedAsync` to extract indicator requirements and call 4-arg `Build` {#task-11-update-strategyschedulehandlecandleclosedasync}

Update the `HandleCandleClosedAsync` method to detect signal-mode strategies, extract their indicator requirements via `IndicatorExtractor.Extract()`, and call the 4-arg `IMarketContextBuilder.Build` overload that populates `IndicatorContext`.

- **Complexity**: Medium
- **Risk Factors**: Must not break grid-mode — for grid strategies, `requiredIndicators` will be `null`, preserving existing behavior (3-arg delegates to 4-arg with `null`)
- **Files**:
  - `src/TradePilot.Application/Scheduling/StrategyScheduler.cs` — modification
- **Success**:
  - Signal-mode strategies receive a `MarketContext` with populated `IndicatorContext`
  - Grid-mode strategies continue to receive `IndicatorContext = null` (unchanged behavior)
- **Important**: After this change, the scheduler always calls the 4-arg `Build` overload (with `null` requirements for grid mode). Existing test mock setups in `StrategySchedulerTests.Setup()` and `BacktestRunnerTests.Setup()` that only mock the 3-arg `Build` must be updated to also mock the 4-arg overload, otherwise they will return `null` `MarketContext` and fail.
- **Dependencies**:
  - `IndicatorExtractor.Extract()` — already implemented in `src/TradePilot.Application/StrategyAuthoring/Services/IndicatorExtractor.cs`

#### Implementation Details

```csharp
// src/TradePilot.Application/Scheduling/StrategyScheduler.cs — modification
// Add using at top of file:
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Services;

// Replace current Build call (lines 67-70):
// BEFORE:
//     var context = _contextBuilder.Build(
//         evt.Candle,
//         latestOneHourCandle,
//         latestFourHourCandle);

// AFTER:
        IReadOnlyList<IndicatorRequirement>? requiredIndicators = null;
        if (_strategyConfig is StrategyConfig typedConfig
            && typedConfig.StrategyMode == StrategyMode.Signal)
        {
            requiredIndicators = IndicatorExtractor.Extract(typedConfig);
        }

        var context = _contextBuilder.Build(
            evt.Candle,
            latestOneHourCandle,
            latestFourHourCandle,
            requiredIndicators);
```

##### Pattern References

- `src/TradePilot.Application/StrategyAuthoring/Services/IndicatorExtractor.cs` — static `Extract(StrategyConfig)` method
- `src/TradePilot.Application/Abstractions/Services/IMarketContextBuilder.cs` — 4-arg `Build` overload signature
- `src/TradePilot.Application/Trading/Services/BacktestMarketContextBuilder.cs` — implementation that populates `IndicatorContext` from requirements

### Task 1.2: Add signal-mode scheduler tests proving indicator requirements are passed to market-context builder {#task-12-add-signal-mode-scheduler-tests}

Update the default mock setup in `StrategySchedulerTests` and add tests that verify:
1. **Update default `[TestInitialize]` mock**: Replace the 3-arg `Build` mock with a 4-arg `Build` mock (accepting `IReadOnlyList<IndicatorRequirement>?`) so all existing tests continue to receive a valid `MarketContext`
2. For signal-mode configs, the 4-arg `Build` overload is called with correct `IndicatorRequirement[]`
3. For grid-mode configs, the 4-arg `Build` overload is called with `null` requirements (regression)
4. Signal-mode evaluation receives a `MarketContext` with populated `IndicatorContext`

- **Complexity**: Medium
- **Risk Factors**: Must mock the 4-arg overload correctly; existing tests mock only 3-arg
- **Files**:
  - `tests/TradePilot.Application.Tests/Scheduling/StrategySchedulerTests.cs` — modification
- **Success**:
  - New tests pass: `GivenSignalModeConfig_WhenHandleCandleClosed_ThenFourArgBuildCalledWithIndicatorRequirements`
  - New tests pass: `GivenGridModeConfig_WhenHandleCandleClosed_ThenThreeArgBuildCalledWithoutIndicators`
  - New tests pass: `GivenSignalModeWithRsi_WhenHandleCandleClosed_ThenIndicatorContextPopulated`
- **Dependencies**:
  - Task 1.1 completed

#### Implementation Details

```csharp
// tests/TradePilot.Application.Tests/Scheduling/StrategySchedulerTests.cs — modification
// Add a signal-mode config alongside the existing TestConfig:

    private static readonly StrategyConfig SignalTestConfig = new()
    {
        SchemaVersion = 1,
        StrategyMode = StrategyMode.Signal,
        StrategyName = "Test RSI Signal",
        Market = "BTC-USD",
        EntryLogic = EntryLogic.All,
        EntryConditions =
        [
            new EntryConditionConfig
            {
                Id = "rsi-entry",
                Type = EntryConditionType.Rsi,
                Enabled = true,
                Params = new RsiParams { Period = 14, Operator = "lt", Threshold = 30m },
            }
        ],
        Exit = new ExitConfig
        {
            TakeProfit = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = 2m },
            StopLoss = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = 5m },
        },
        Risk = new RiskConfig
        {
            PositionSizeValue = 100m,
            Leverage = 1m,
            MaxOpenTrades = 1,
        },
    };

    [TestMethod]
    public async Task GivenSignalModeConfig_WhenHandleCandleClosed_ThenFourArgBuildCalledWithIndicatorRequirements()
    {
        // Arrange
        var sut = new StrategyScheduler(
            _contextBuilderMock.Object,
            _strategyEngineMock.Object,
            _gridControllerMock.Object,
            _riskEngineMock.Object,
            _positionManagerMock.Object,
            SignalTestConfig);

        var indicatorContext = new IndicatorContext();
        indicatorContext.SetRsi(14, 25m);

        _contextBuilderMock
            .Setup(builder => builder.Build(
                It.IsAny<Candle>(),
                It.IsAny<Candle?>(),
                It.IsAny<Candle?>(),
                It.IsAny<IReadOnlyList<IndicatorRequirement>?>()))
            .Returns((Candle trigger, Candle? oneHour, Candle? fourHour, IReadOnlyList<IndicatorRequirement>? _) =>
                new MarketContext
                {
                    Symbol = trigger.Symbol,
                    TimestampUtc = trigger.Timestamp,
                    CurrentCandle = trigger,
                    LatestOneHourCandle = oneHour,
                    LatestFourHourCandle = fourHour,
                    Indicators = new IndicatorSnapshot(),
                    IndicatorContext = indicatorContext,
                });

        var evt = CreateEvent("15m");

        // Act
        await sut.HandleCandleClosedAsync(evt, null, null);

        // Assert
        _contextBuilderMock.Verify(
            builder => builder.Build(
                It.IsAny<Candle>(),
                It.IsAny<Candle?>(),
                It.IsAny<Candle?>(),
                It.Is<IReadOnlyList<IndicatorRequirement>?>(reqs =>
                    reqs != null && reqs.Count == 1 && reqs[0].Type == "RSI" && reqs[0].Period == 14)),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenGridModeConfig_WhenHandleCandleClosed_ThenFourArgBuildCalledWithNullRequirements()
    {
        // Arrange — uses default _sut with grid-mode TestConfig
        var evt = CreateEvent("15m");

        // Act
        await _sut.HandleCandleClosedAsync(evt, null, null);

        // Assert
        _contextBuilderMock.Verify(
            builder => builder.Build(
                It.IsAny<Candle>(),
                It.IsAny<Candle?>(),
                It.IsAny<Candle?>(),
                It.Is<IReadOnlyList<IndicatorRequirement>?>(reqs => reqs == null)),
            Times.Once);
    }
```

##### Pattern References

- `tests/TradePilot.Application.Tests/Scheduling/StrategySchedulerTests.cs` — existing test class structure, `_sut` pattern, `CreateEvent` helper
- `tests/TradePilot.Application.Tests/StrategyAuthoring/Services/ConditionEvaluatorTests.cs` — `IndicatorContext` inline construction pattern

### Task 1.3: Run all existing scheduler and strategy tests to verify no grid-mode regression {#task-13-run-regression-tests}

Run the full test suite for scheduling and strategy-related test classes to confirm no regressions.

- **Complexity**: Low
- **Risk Factors**: None — read-only verification step
- **Files**:
  - `tests/TradePilot.Application.Tests/Scheduling/StrategySchedulerTests.cs`
  - `tests/TradePilot.Application.Tests/Trading/Services/CompositeStrategyEngineTests.cs`
  - `tests/TradePilot.Application.Tests/StrategyAuthoring/Services/ConditionEvaluatorTests.cs`
  - `tests/TradePilot.Application.Tests/StrategyAuthoring/Services/RsiConditionHandlerTests.cs`
  - `tests/TradePilot.Application.Tests/StrategyAuthoring/Services/IndicatorExtractorTests.cs`
  - `tests/TradePilot.Application.Tests/Trading/Services/GridControllerTests.cs`
- **Success**:
  - All existing tests pass
  - All new Phase 1 tests pass
- **Dependencies**:
  - Tasks 1.1 and 1.2 completed

## Phase Success Criteria

- `StrategyScheduler` calls 4-arg `Build` with extracted `IndicatorRequirement[]` for signal-mode strategies
- Grid-mode strategies continue to call `Build` with `null` requirements (unchanged behavior)
- New tests verify indicator wiring for signal-mode; existing grid tests pass unchanged
