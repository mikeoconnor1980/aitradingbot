<!-- markdownlint-disable-file -->

# Task Details: F6.75 — Signal Runtime Wiring

## Phase 2: Signal Controller and Execution Branch

## Standards and Knowledge References

- **csharp.instructions.md**: `sealed` classes, interfaces in `Abstractions/Services/`, `_camelCase` private fields
- **testing.instructions.md**: MSTest, FluentAssertions ≤ v6, Moq, `Given_When_Then` naming
- **dotnet-architecture.instructions.md**: Application service interfaces in `Abstractions/Services/` when dependency-inverted
- **16-signal-contracts.md**: `TradingSignal` shape with string `SignalType`, `Parameters` dictionary
- **15-grid-controller.md**: `IGridController.ProcessAsync` contract — `SignalController` follows same signature pattern
- **14-strategy-runtime-model.md**: Pipeline: context → evaluate → controller → risk → position-manager

### Task 2.1: Create `ISignalController` interface in `Abstractions/Services/` {#task-21-create-isignalcontroller-interface}

Create a new interface for signal-mode post-evaluation processing, mirroring the `IGridController` contract shape.

- **Complexity**: Low
- **Risk Factors**: None — new file
- **Files**:
  - `src/TradePilot.Application/Abstractions/Services/ISignalController.cs` — new file
- **Success**:
  - Interface compiles and follows the same pattern as `IGridController`
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradePilot.Application/Abstractions/Services/ISignalController.cs — new file
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Trading;

namespace TradePilot.Application.Abstractions.Services;

/// <summary>
/// Processes signal-mode strategy evaluation results and emits trading signals
/// for position entry and exit. Replaces <see cref="IGridController"/> for
/// strategies that use <see cref="StrategyAuthoring.Models.StrategyMode.Signal"/>.
/// </summary>
public interface ISignalController
{
    Task<IReadOnlyList<TradingSignal>> ProcessAsync(
        StrategyEvaluation evaluation,
        MarketContext context,
        PositionState positionState,
        IStrategyConfig strategyConfig,
        CancellationToken cancellationToken = default);
}
```

##### Pattern References

- `src/TradePilot.Application/Abstractions/Services/IGridController.cs` — same return type and parameter pattern; `ISignalController` omits `GridState` since signal-mode has no grid lifecycle

### Task 2.2: Create `SignalController` implementation that emits `OpenPosition` and `TakeProfit` signals {#task-22-create-signalcontroller-implementation}

Create the signal-mode controller that:
- When `SetupDetected = true` and no position is open → emits `OpenPosition` signal
- When position is open → checks stop-loss and take-profit thresholds → emits `TakeProfit` signal (reuses existing signal type for position closing)
- When `SetupDetected = false` and no position → returns empty signals

- **Complexity**: High
- **Risk Factors**: Exit logic (SL/TP) must mirror `GridController`'s open-position branch to maintain parity; `PositionSizeValue` semantics differ between grid (notional per level) and signal (total position notional)
- **Files**:
  - `src/TradePilot.Application/Trading/Services/SignalController.cs` — new file
- **Success**:
  - `OpenPosition` signal emitted with correct sizing when `SetupDetected = true` and no position
  - `TakeProfit` signal emitted for SL/TP when position is open
  - No signal emitted when `SetupDetected = false` and no position
- **Dependencies**:
  - Task 2.1 (ISignalController interface)

#### Implementation Details

```csharp
// src/TradePilot.Application/Trading/Services/SignalController.cs — new file
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Trading;

namespace TradePilot.Application.Trading.Services;

/// <summary>
/// Processes signal-mode strategy evaluation and emits OpenPosition or TakeProfit signals.
/// </summary>
public sealed class SignalController : ISignalController
{
    public Task<IReadOnlyList<TradingSignal>> ProcessAsync(
        StrategyEvaluation evaluation,
        MarketContext context,
        PositionState positionState,
        IStrategyConfig strategyConfig,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(evaluation);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(positionState);
        ArgumentNullException.ThrowIfNull(strategyConfig);

        if (strategyConfig is not StrategyConfig config)
        {
            throw new ArgumentException(
                $"Expected {nameof(StrategyConfig)} but received {strategyConfig.GetType().Name}.",
                nameof(strategyConfig));
        }

        if (positionState.IsOpen)
        {
            return Task.FromResult(EvaluateExitConditions(context, positionState, config));
        }

        if (!evaluation.SetupDetected)
        {
            return Task.FromResult<IReadOnlyList<TradingSignal>>(Array.Empty<TradingSignal>());
        }

        return Task.FromResult(EmitOpenPosition(context, config, evaluation.Reason));
    }

    private static IReadOnlyList<TradingSignal> EmitOpenPosition(
        MarketContext context,
        StrategyConfig config,
        string? reason)
    {
        var notional = Math.Abs(config.Risk.PositionSizeValue);
        var entryPrice = context.CurrentCandle.Close;
        var size = entryPrice > 0m
            ? decimal.Round(notional / entryPrice, 8, MidpointRounding.AwayFromZero)
            : 0m;

        if (size <= 0m)
        {
            return Array.Empty<TradingSignal>();
        }

        return
        [
            new TradingSignal
            {
                SignalType = "OpenPosition",
                Symbol = context.Symbol,
                Reason = reason ?? "Signal condition met.",
                Parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["entryPrice"] = entryPrice,
                    ["size"] = size,
                    ["notional"] = notional,
                    ["orderType"] = OrderType.Market.ToString(),
                }
            }
        ];
    }

    private static IReadOnlyList<TradingSignal> EvaluateExitConditions(
        MarketContext context,
        PositionState positionState,
        StrategyConfig config)
    {
        var stopLossPercent = config.Exit.StopLoss.Enabled && config.Exit.StopLoss.Value.HasValue
            ? Math.Abs(config.Exit.StopLoss.Value.Value)
            : 0m;
        var takeProfitPercent = config.Exit.TakeProfit.Enabled && config.Exit.TakeProfit.Value.HasValue
            ? Math.Abs(config.Exit.TakeProfit.Value.Value)
            : 0m;

        var stopLossTrigger = positionState.AverageEntryPrice * (1m - (stopLossPercent / 100m));
        var takeProfitTrigger = positionState.AverageEntryPrice * (1m + (takeProfitPercent / 100m));

        if (stopLossPercent > 0m && context.CurrentCandle.Close <= stopLossTrigger)
        {
            return
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
                        ["cancellationReason"] = CancellationReason.StopLossTriggered.ToString(),
                    }
                }
            ];
        }

        if (takeProfitPercent > 0m && context.CurrentCandle.Close >= takeProfitTrigger)
        {
            return
            [
                new TradingSignal
                {
                    SignalType = "TakeProfit",
                    Symbol = context.Symbol,
                    Reason = "Take profit triggered.",
                    Parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["targetPrice"] = context.CurrentCandle.Close,
                        ["size"] = Math.Abs(positionState.Size),
                        ["orderType"] = OrderType.Market.ToString(),
                        ["cancellationReason"] = CancellationReason.TakeProfitTriggered.ToString(),
                    }
                }
            ];
        }

        return Array.Empty<TradingSignal>();
    }
}
```

##### Pattern References

- `src/TradePilot.Application/Trading/Services/GridController.cs` — exit condition evaluation (SL/TP) logic in `positionState.IsOpen` branch; `TradingSignal` construction with `Parameters` dictionary
- `src/TradePilot.Application/Trading/Models/TradingSignal.cs` — signal shape
- `src/TradePilot.Application/Backtesting/Models/CancellationReason.cs` — `StopLossTriggered`, `TakeProfitTriggered` enum values

### Task 2.3: Update `StrategyScheduler` to accept optional `ISignalController` and branch on `StrategyMode` {#task-23-update-strategyschedule-to-branch-on-strategymode}

Add an optional `ISignalController?` parameter to `StrategyScheduler`'s constructor. In `HandleCandleClosedAsync`, branch between `_gridController.ProcessAsync` (grid mode) and `_signalController.ProcessAsync` (signal mode) based on the strategy config.

- **Complexity**: Medium
- **Risk Factors**: Constructor change requires updating all call sites (`BacktestRunner`, any live host). Optional parameter with `null` default preserves backward compatibility.
- **Files**:
  - `src/TradePilot.Application/Scheduling/StrategyScheduler.cs` — modification
- **Success**:
  - Signal-mode strategies route through `ISignalController.ProcessAsync`
  - Grid-mode strategies continue routing through `IGridController.ProcessAsync`
  - Pipeline ordering preserved: context → evaluate → controller → risk → position
- **Dependencies**:
  - Task 2.1 (ISignalController interface)
  - Task 2.2 (SignalController implementation)
  - Task 1.1 (indicator wiring)

#### Implementation Details

```csharp
// src/TradePilot.Application/Scheduling/StrategyScheduler.cs — modification

// Add field after existing _positionManager field:
    private readonly ISignalController? _signalController;

// Add optional parameter to constructor (after auditCollector):
    public StrategyScheduler(
        IMarketContextBuilder contextBuilder,
        IStrategyEngine strategyEngine,
        IGridController gridController,
        IRiskEngine riskEngine,
        IPositionManager positionManager,
        IStrategyConfig strategyConfig,
        string triggerTimeframe = "15m",
        IBacktestAuditCollector? auditCollector = null,
        ISignalController? signalController = null)
    {
        // ... existing null checks ...
        _signalController = signalController;
    }

// Replace the gridController.ProcessAsync call in HandleCandleClosedAsync:
// BEFORE:
//     var signals = await _gridController.ProcessAsync(
//         evaluation, context, _gridState, _positionState, _strategyConfig, cancellationToken);

// AFTER:
        var signals = await ProcessEvaluationAsync(
            evaluation, context, cancellationToken);

// Add private method:
    private Task<IReadOnlyList<TradingSignal>> ProcessEvaluationAsync(
        StrategyEvaluation evaluation,
        MarketContext context,
        CancellationToken cancellationToken)
    {
        if (_signalController is not null
            && _strategyConfig is StrategyConfig { StrategyMode: StrategyMode.Signal })
        {
            return _signalController.ProcessAsync(
                evaluation, context, _positionState, _strategyConfig, cancellationToken);
        }

        return _gridController.ProcessAsync(
            evaluation, context, _gridState, _positionState, _strategyConfig, cancellationToken);
    }
```

##### Pattern References

- `src/TradePilot.Application/Scheduling/StrategyScheduler.cs` — existing constructor pattern, `HandleCandleClosedAsync` pipeline
- `src/TradePilot.Application/Abstractions/Services/IGridController.cs` — `ProcessAsync` signature for comparison

### Task 2.4: Register `ISignalController` in DI (`Program.cs`) {#task-24-register-isignalcontroller-in-di}

Register the `SignalController` implementation in the API's DI container alongside the existing grid controller registration.

- **Complexity**: Low
- **Risk Factors**: None — additive registration
- **Files**:
  - `src/TradePilot.Api/Program.cs` — modification
- **Success**:
  - `ISignalController` resolves to `SignalController` from DI
- **Dependencies**:
  - Task 2.2 (SignalController)

#### Implementation Details

```csharp
// src/TradePilot.Api/Program.cs — modification
// Add after the existing IGridController registration line:
// builder.Services.AddScoped<IGridController, GridController>();
builder.Services.AddScoped<ISignalController, SignalController>();
```

##### Pattern References

- `src/TradePilot.Api/Program.cs` — existing flat registration pattern (`AddScoped<IGridController, GridController>()` on line ~96)

### Task 2.5: Add `SignalController` unit tests {#task-25-add-signalcontroller-unit-tests}

Create a comprehensive test class for `SignalController` covering:
- `OpenPosition` signal emitted when `SetupDetected = true` and no position
- No signal when `SetupDetected = false` and no position
- `TakeProfit` signal (stop loss) when position open and price below SL trigger
- `TakeProfit` signal (take profit) when position open and price above TP trigger
- No signal when position open but price within SL/TP bands
- Correct sizing and parameter values in emitted signals

- **Complexity**: Medium
- **Risk Factors**: Must match `GridController` test patterns for consistency
- **Files**:
  - `tests/TradePilot.Application.Tests/Trading/Services/SignalControllerTests.cs` — new file
- **Success**:
  - All `SignalController` behavior paths are tested
  - Tests follow `Given_When_Then` naming and FluentAssertions
- **Dependencies**:
  - Task 2.2 (SignalController implementation)

#### Implementation Details

```csharp
// tests/TradePilot.Application.Tests/Trading/Services/SignalControllerTests.cs — new file
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;
using TradePilot.Application.Trading.Services;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Tests.Trading.Services;

[TestClass]
public sealed class SignalControllerTests
{
    private static readonly StrategyConfig DefaultConfig = new()
    {
        SchemaVersion = 1,
        StrategyMode = StrategyMode.Signal,
        StrategyName = "Test Signal",
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
            PositionSizeValue = 1000m,
            Leverage = 1m,
            MaxOpenTrades = 1,
        },
    };

    private SignalController _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new SignalController();
    }

    [TestMethod]
    public async Task GivenSetupDetectedAndNoPosition_WhenProcessAsync_ThenEmitsOpenPosition()
    {
        // Arrange
        var evaluation = new StrategyEvaluation { SetupDetected = true, Reason = "RSI below 30." };
        var context = CreateContext(close: 50000m);
        var positionState = new PositionState();

        // Act
        var signals = await _sut.ProcessAsync(evaluation, context, positionState, DefaultConfig);

        // Assert
        signals.Should().HaveCount(1);
        signals[0].SignalType.Should().Be("OpenPosition");
        signals[0].Symbol.Should().Be("BTC-USD");
        ((decimal)signals[0].Parameters!["entryPrice"]).Should().Be(50000m);
        ((decimal)signals[0].Parameters!["size"]).Should().BeGreaterThan(0m);
    }

    [TestMethod]
    public async Task GivenNoSetupAndNoPosition_WhenProcessAsync_ThenEmitsNoSignals()
    {
        // Arrange
        var evaluation = new StrategyEvaluation { SetupDetected = false, Reason = "RSI above 30." };
        var context = CreateContext(close: 50000m);
        var positionState = new PositionState();

        // Act
        var signals = await _sut.ProcessAsync(evaluation, context, positionState, DefaultConfig);

        // Assert
        signals.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenOpenPositionAndStopLossTriggered_WhenProcessAsync_ThenEmitsTakeProfitWithStopLoss()
    {
        // Arrange — entry at 50000, SL = 5% → trigger at 47500, close at 47000
        var evaluation = new StrategyEvaluation { SetupDetected = false };
        var context = CreateContext(close: 47000m);
        var positionState = new PositionState
        {
            Symbol = "BTC-USD",
            Size = 0.02m,
            AverageEntryPrice = 50000m,
        };

        // Act
        var signals = await _sut.ProcessAsync(evaluation, context, positionState, DefaultConfig);

        // Assert
        signals.Should().HaveCount(1);
        signals[0].SignalType.Should().Be("TakeProfit");
        signals[0].Reason.Should().Contain("Stop loss");
    }

    [TestMethod]
    public async Task GivenOpenPositionAndTakeProfitTriggered_WhenProcessAsync_ThenEmitsTakeProfitSignal()
    {
        // Arrange — entry at 50000, TP = 2% → trigger at 51000, close at 51500
        var evaluation = new StrategyEvaluation { SetupDetected = false };
        var context = CreateContext(close: 51500m);
        var positionState = new PositionState
        {
            Symbol = "BTC-USD",
            Size = 0.02m,
            AverageEntryPrice = 50000m,
        };

        // Act
        var signals = await _sut.ProcessAsync(evaluation, context, positionState, DefaultConfig);

        // Assert
        signals.Should().HaveCount(1);
        signals[0].SignalType.Should().Be("TakeProfit");
        signals[0].Reason.Should().Contain("Take profit");
    }

    [TestMethod]
    public async Task GivenOpenPositionWithinBands_WhenProcessAsync_ThenEmitsNoSignals()
    {
        // Arrange — entry at 50000, close at 50500 (within SL/TP bands)
        var evaluation = new StrategyEvaluation { SetupDetected = false };
        var context = CreateContext(close: 50500m);
        var positionState = new PositionState
        {
            Symbol = "BTC-USD",
            Size = 0.02m,
            AverageEntryPrice = 50000m,
        };

        // Act
        var signals = await _sut.ProcessAsync(evaluation, context, positionState, DefaultConfig);

        // Assert
        signals.Should().BeEmpty();
    }

    private static MarketContext CreateContext(decimal close = 50000m)
    {
        return new MarketContext
        {
            Symbol = "BTC-USD",
            TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            CurrentCandle = new Candle
            {
                Symbol = "BTC-USD",
                Interval = "15m",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Open = close,
                High = close + 100m,
                Low = close - 100m,
                Close = close,
                Volume = 1000m,
            },
            Indicators = new IndicatorSnapshot(),
        };
    }
}
```

##### Pattern References

- `tests/TradePilot.Application.Tests/Trading/Services/GridControllerTests.cs` — `_sut` pattern, `CreateContext` helper, signal assertion pattern
- `tests/TradePilot.Application.Tests/Scheduling/StrategySchedulerTests.cs` — `StrategyConfig` inline construction

### Task 2.6: Add scheduler signal-mode branching tests {#task-26-add-scheduler-signal-mode-branching-tests}

Add tests to `StrategySchedulerTests` that verify:
1. Signal-mode strategies route through `ISignalController.ProcessAsync` (not `IGridController`)
2. Grid-mode strategies still route through `IGridController.ProcessAsync`

- **Complexity**: Medium
- **Risk Factors**: Must mock both controllers and verify correct routing
- **Files**:
  - `tests/TradePilot.Application.Tests/Scheduling/StrategySchedulerTests.cs` — modification
- **Success**:
  - Signal-mode test: `_signalControllerMock.Verify(ProcessAsync, Times.Once)` and `_gridControllerMock.Verify(ProcessAsync, Times.Never)`
  - Grid-mode test: `_gridControllerMock.Verify(ProcessAsync, Times.Once)` and `_signalControllerMock.Verify(ProcessAsync, Times.Never)`
- **Dependencies**:
  - Task 2.3 (StrategyScheduler updated constructor)

#### Implementation Details

```csharp
// tests/TradePilot.Application.Tests/Scheduling/StrategySchedulerTests.cs — modification
// Add new mock field:
    private Mock<ISignalController> _signalControllerMock = default!;

// Initialize in Setup():
    _signalControllerMock = new Mock<ISignalController>();
    _signalControllerMock
        .Setup(controller => controller.ProcessAsync(
            It.IsAny<StrategyEvaluation>(),
            It.IsAny<MarketContext>(),
            It.IsAny<PositionState>(),
            It.IsAny<IStrategyConfig>(),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(Array.Empty<TradingSignal>());

    [TestMethod]
    public async Task GivenSignalModeConfig_WhenHandleCandleClosed_ThenSignalControllerCalledNotGridController()
    {
        // Arrange
        var sut = new StrategyScheduler(
            _contextBuilderMock.Object,
            _strategyEngineMock.Object,
            _gridControllerMock.Object,
            _riskEngineMock.Object,
            _positionManagerMock.Object,
            SignalTestConfig,
            signalController: _signalControllerMock.Object);

        SetupFourArgBuild();
        var evt = CreateEvent("15m");

        // Act
        await sut.HandleCandleClosedAsync(evt, null, null);

        // Assert
        _signalControllerMock.Verify(
            controller => controller.ProcessAsync(
                It.IsAny<StrategyEvaluation>(),
                It.IsAny<MarketContext>(),
                It.IsAny<PositionState>(),
                It.IsAny<IStrategyConfig>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _gridControllerMock.Verify(
            controller => controller.ProcessAsync(
                It.IsAny<StrategyEvaluation>(),
                It.IsAny<MarketContext>(),
                It.IsAny<GridState>(),
                It.IsAny<PositionState>(),
                It.IsAny<IStrategyConfig>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GivenGridModeConfig_WhenHandleCandleClosed_ThenGridControllerCalledNotSignalController()
    {
        // Arrange — default _sut uses grid-mode TestConfig without signal controller
        var evt = CreateEvent("15m");

        // Act
        await _sut.HandleCandleClosedAsync(evt, null, null);

        // Assert
        _gridControllerMock.Verify(
            controller => controller.ProcessAsync(
                It.IsAny<StrategyEvaluation>(),
                It.IsAny<MarketContext>(),
                It.IsAny<GridState>(),
                It.IsAny<PositionState>(),
                It.IsAny<IStrategyConfig>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

// Helper to set up 4-arg Build mock for signal-mode tests:
    private void SetupFourArgBuild()
    {
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
                    IndicatorContext = new IndicatorContext(),
                });
    }
```

##### Pattern References

- `tests/TradePilot.Application.Tests/Scheduling/StrategySchedulerTests.cs` — existing mock setup and verify patterns
- `src/TradePilot.Application/Abstractions/Services/ISignalController.cs` — `ProcessAsync` signature (from Task 2.1)

### Task 2.7: Run all tests to verify no regression {#task-27-run-all-tests}

Run the full `TradePilot.Application.Tests` project to verify no regressions.

- **Complexity**: Low
- **Risk Factors**: None — verification step
- **Files**:
  - All test files in `tests/TradePilot.Application.Tests/`
- **Success**:
  - All existing tests pass
  - All new Phase 2 tests pass
- **Dependencies**:
  - All Phase 2 tasks completed

## Phase Success Criteria

- `ISignalController` interface exists and mirrors `IGridController` pattern (without `GridState`)
- `SignalController` handles position entry (`OpenPosition`), stop-loss, and take-profit for signal-mode strategies
- `StrategyScheduler` routes signal-mode strategies through `ISignalController`, grid-mode through `IGridController`
- `ISignalController` is registered in DI
- All new and existing tests pass
