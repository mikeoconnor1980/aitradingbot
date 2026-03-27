<!-- markdownlint-disable-file -->

# Task Details: F3 — Backtest Replay Engine

## Phase 4: BacktestRunner Orchestrator

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — `sealed` classes, `async/await`, `CancellationToken`, `ArgumentException.ThrowIfNullOrWhiteSpace`
- `.github/instructions/testing.instructions.md` — MSTest, Moq, FluentAssertions v6, `Given_When_Then` naming, `[TestInitialize]`
- `.agent-context/0-knowledge/19-scheduling-architecture.md` — `StrategyScheduler` pattern: subscribe to CandleClock, filter for trigger timeframe, run pipeline
- `.agent-context/0-knowledge/18-backtesting-architecture.md` — BacktestRunner orchestration: replay → context → strategy → signals → risk → position → execution
- `.agent-context/3-develop/backlog/draft/backtesting/F3-backtest-replay-engine.md` — Runner creates fresh instances per run; wires full pipeline; equity tracking per tick; multi-cycle grid redeployment

## Design References

Backtest main loop (from PBI):
1. Load candle data via CandleReplayEngine
2. Warmup: feed candles to MarketContextBuilder (no signals)
3. For each 15m candle after warmup:
   a. SimulatedExecutionEngine.ProcessCandle → fills from previous orders
   b. Record fills, update trade log
   c. Feed candle to CandleClock → fires CandleClosedEvent
   d. StrategyScheduler handles event → pipeline runs → new orders placed
   e. Track equity (initial capital ± realised PnL ± unrealised PnL - fees)
4. Compute metrics via BacktestMetricsCalculator

---

### Task 4.1: Create StrategyScheduler {#task-41-create-strategyscheduler}

Create the `StrategyScheduler` that subscribes to CandleClock events and orchestrates the strategy evaluation pipeline. Filters for 15m trigger timeframe. Calls pipeline interfaces in sequence: MarketContextBuilder → StrategyEngine → GridController → RiskEngine → PositionManager.

- **Complexity**: Medium
- **Risk Factors**: Must correctly filter for trigger timeframe; pipeline call sequence must match architecture docs; must propagate CancellationToken
- **Files**:
  - `src/TradingApp.Application/Scheduling/StrategyScheduler.cs` — new file
- **Success**:
  - Handles CandleClosedEvent and filters for 15m timeframe
  - Calls pipeline interfaces in correct sequence
  - Non-15m events are silently ignored
  - CancellationToken propagated through all pipeline calls
- **Dependencies**: Phase 1 (interfaces, CandleClock, CandleClosedEvent, MarketContext, GridState, PositionState)

#### Implementation Details

```csharp
// src/TradingApp.Application/Scheduling/StrategyScheduler.cs — new file
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Scheduling.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Scheduling;

/// <summary>
/// Subscribes to CandleClock events and orchestrates strategy evaluation.
/// Filters for the 15m trigger timeframe and runs the trading pipeline:
/// MarketContextBuilder → StrategyEngine → GridController → RiskEngine → PositionManager.
/// Shared between live and backtest modes.
/// </summary>
public sealed class StrategyScheduler
{
    private readonly IMarketContextBuilder _contextBuilder;
    private readonly IStrategyEngine _strategyEngine;
    private readonly IGridController _gridController;
    private readonly IRiskEngine _riskEngine;
    private readonly IPositionManager _positionManager;
    private readonly string _strategyConfigJson;
    private readonly string _triggerTimeframe;

    private GridState _gridState = new();
    private PositionState _positionState = new();

    public StrategyScheduler(
        IMarketContextBuilder contextBuilder,
        IStrategyEngine strategyEngine,
        IGridController gridController,
        IRiskEngine riskEngine,
        IPositionManager positionManager,
        string strategyConfigJson,
        string triggerTimeframe = "15m")
    {
        _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
        _strategyEngine = strategyEngine ?? throw new ArgumentNullException(nameof(strategyEngine));
        _gridController = gridController ?? throw new ArgumentNullException(nameof(gridController));
        _riskEngine = riskEngine ?? throw new ArgumentNullException(nameof(riskEngine));
        _positionManager = positionManager ?? throw new ArgumentNullException(nameof(positionManager));
        _strategyConfigJson = strategyConfigJson;
        _triggerTimeframe = triggerTimeframe;
    }

    /// <summary>
    /// Handle a candle close event. Only processes the trigger timeframe.
    /// </summary>
    public async Task HandleCandleClosedAsync(
        CandleClosedEvent evt,
        Candle? latestOneHourCandle,
        Candle? latestFourHourCandle,
        CancellationToken cancellationToken = default)
    {
        if (evt.Timeframe != _triggerTimeframe)
            return;

        // 1. Build market context
        var context = _contextBuilder.Build(
            evt.Candle,
            latestOneHourCandle,
            latestFourHourCandle);

        // 2. Strategy evaluation
        var evaluation = await _strategyEngine.EvaluateAsync(
            context, _strategyConfigJson, cancellationToken);

        // 3. Grid controller — manage lifecycle and emit signals
        var signals = await _gridController.ProcessAsync(
            evaluation, context, _gridState, _positionState,
            _strategyConfigJson, cancellationToken);

        if (signals.Count == 0)
            return;

        // 4. Risk engine — validate signals
        var approvedSignals = await _riskEngine.ValidateAsync(signals, cancellationToken);

        if (approvedSignals.Count == 0)
            return;

        // 5. Position manager — execute approved signals (calls IExecutionEngine internally)
        await _positionManager.ExecuteSignalsAsync(approvedSignals, cancellationToken);
    }

    /// <summary>
    /// Update the scheduler's view of grid and position state.
    /// Called by BacktestRunner after processing fills.
    /// </summary>
    public void UpdateState(GridState gridState, PositionState positionState)
    {
        _gridState = gridState;
        _positionState = positionState;
    }

    /// <summary>
    /// Get the current grid state (for tracking grid cycles).
    /// </summary>
    public GridState GetGridState() => _gridState;
}
```

##### Pattern References

- `.agent-context/0-knowledge/19-scheduling-architecture.md` — StrategyScheduler sample: filter for `15m`, build context, run pipeline
- `.agent-context/0-knowledge/15-grid-controller.md` — Pipeline sequence: Strategy → GridController → Signals → RiskEngine → PositionManager → ExecutionEngine

---

### Task 4.2: Create BacktestRunner implementing IBacktestRunner {#task-42-create-backtestrunner-implementing-ibacktestrunner}

Create the `BacktestRunner` that orchestrates a complete backtest run. Creates fresh instances of all components per run. Drives the replay loop through CandleReplayEngine → CandleClock → StrategyScheduler pipeline with equity tracking and trade log collection.

- **Complexity**: High
- **Risk Factors**: Must create fresh instances per run (stateless between runs); equity calculation must account for unrealised + realised PnL - fees; multi-cycle grid redeployment must work; pipeline wiring between all components
- **Files**:
  - `src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs` — new file
- **Success**:
  - Implements `IBacktestRunner.RunAsync(BacktestConfig, CancellationToken)`
  - Creates fresh component instances per run (no shared state)
  - Drives replay loop: load data → warmup → evaluation with fills + signals
  - Pairs grid fill entries with TP/hedge exits to compute per-trade PnL, ExitTimeUtc, and ExitPrice
  - Tracks equity at each 15m tick (including mark-to-market unrealised PnL)
  - Collects ordered trade log
  - Supports multi-cycle grid redeployment
  - Returns complete `BacktestResult`
  - Deterministic: same inputs → same outputs
- **Dependencies**: All previous phases, all pipeline interfaces

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs — new file
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Scheduling;
using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.Backtesting.Services;

/// <summary>
/// Orchestrates a complete backtest run. Creates fresh instances of all components
/// per run. Replays historical candles through the trading pipeline with a
/// simulated execution engine.
/// </summary>
public sealed class BacktestRunner : IBacktestRunner
{
    private readonly ICandleRepository _candleRepository;
    private readonly IMarketContextBuilder _marketContextBuilder;
    private readonly IStrategyEngine _strategyEngine;
    private readonly IGridController _gridController;
    private readonly IRiskEngine _riskEngine;
    private readonly IPositionManager _positionManager;

    public BacktestRunner(
        ICandleRepository candleRepository,
        IMarketContextBuilder marketContextBuilder,
        IStrategyEngine strategyEngine,
        IGridController gridController,
        IRiskEngine riskEngine,
        IPositionManager positionManager)
    {
        _candleRepository = candleRepository ?? throw new ArgumentNullException(nameof(candleRepository));
        _marketContextBuilder = marketContextBuilder ?? throw new ArgumentNullException(nameof(marketContextBuilder));
        _strategyEngine = strategyEngine ?? throw new ArgumentNullException(nameof(strategyEngine));
        _gridController = gridController ?? throw new ArgumentNullException(nameof(gridController));
        _riskEngine = riskEngine ?? throw new ArgumentNullException(nameof(riskEngine));
        _positionManager = positionManager ?? throw new ArgumentNullException(nameof(positionManager));
    }

    public async Task<BacktestResult> RunAsync(BacktestConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(config.Symbol);

        // Create fresh instances per run
        var executionEngine = new SimulatedExecutionEngine(config.FeeModel);
        var replayEngine = new CandleReplayEngine(_candleRepository);
        var clock = new CandleClock();
        var metricsCalculator = new BacktestMetricsCalculator();

        // Load and validate data
        var replayData = await replayEngine.LoadAsync(config, cancellationToken);

        // Create scheduler wired to pipeline interfaces
        var scheduler = new StrategyScheduler(
            _marketContextBuilder,
            _strategyEngine,
            _gridController,
            _riskEngine,
            _positionManager,
            config.StrategyConfigJson);

        // Track state
        var tradeLog = new List<BacktestTrade>();
        var equityTimeSeries = new List<EquitySnapshot>();
        var gridCycles = 0;
        var currentEquity = config.InitialCapital;
        var totalFeesPaid = 0m;

        // --- Warmup Phase ---
        for (var i = 0; i < replayData.WarmupEndIndex; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var candle = replayData.Candles15m[i];
            _marketContextBuilder.UpdateIndicators(candle);

            // Also feed higher-timeframe candles to CandleClock during warmup
            // (so it tracks latest closed for each timeframe)
            await clock.ProcessCandleAsync(candle);
        }

        // --- Evaluation Phase ---
        for (var i = replayData.WarmupEndIndex; i < replayData.Candles15m.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var candle = replayData.Candles15m[i];

            // 1. Process fills from open orders against this candle
            var fills = executionEngine.ProcessCandle(candle);

            // 2. Record fills and pair trades
            foreach (var fill in fills)
            {
                if (fill.TradeType == TradeType.GridFill)
                {
                    // Entry fill — add as open trade
                    tradeLog.Add(new BacktestTrade
                    {
                        TradeId = fill.OrderId,
                        GridCycleId = scheduler.GetGridState().GridCycleId ?? "default",
                        EntryTimeUtc = fill.FillTimeUtc,
                        EntryPrice = fill.FillPrice,
                        ExitTimeUtc = null,
                        ExitPrice = null,
                        Side = fill.Side,
                        Size = fill.Size,
                        PnL = null,
                        Fees = fill.Fee,
                        TradeType = fill.TradeType
                    });
                }
                else if (fill.TradeType is TradeType.TakeProfit or TradeType.HedgeClose)
                {
                    // Exit fill — find the earliest unpaired entry and pair it
                    var openTrade = tradeLog.FirstOrDefault(t =>
                        t.ExitTimeUtc is null &&
                        t.TradeType == TradeType.GridFill &&
                        t.Side == OrderSide.Buy);

                    if (openTrade is not null)
                    {
                        // Pair: compute PnL = (exit - entry) × size, then update trade
                        var tradePnL = (fill.FillPrice - openTrade.EntryPrice) * openTrade.Size;
                        // BacktestTrade is immutable (init properties), so replace with paired version
                        var pairedTrade = new BacktestTrade
                        {
                            TradeId = openTrade.TradeId,
                            GridCycleId = openTrade.GridCycleId,
                            EntryTimeUtc = openTrade.EntryTimeUtc,
                            EntryPrice = openTrade.EntryPrice,
                            ExitTimeUtc = fill.FillTimeUtc,
                            ExitPrice = fill.FillPrice,
                            Side = openTrade.Side,
                            Size = openTrade.Size,
                            PnL = tradePnL,
                            Fees = openTrade.Fees + fill.Fee,
                            TradeType = openTrade.TradeType
                        };
                        var idx = tradeLog.IndexOf(openTrade);
                        tradeLog[idx] = pairedTrade;
                    }
                    else
                    {
                        // Standalone exit (e.g., hedge open) — record as its own trade
                        tradeLog.Add(new BacktestTrade
                        {
                            TradeId = fill.OrderId,
                            GridCycleId = scheduler.GetGridState().GridCycleId ?? "default",
                            EntryTimeUtc = fill.FillTimeUtc,
                            EntryPrice = fill.FillPrice,
                            ExitTimeUtc = null,
                            ExitPrice = null,
                            Side = fill.Side,
                            Size = fill.Size,
                            PnL = null,
                            Fees = fill.Fee,
                            TradeType = fill.TradeType
                        });
                    }
                }
                else
                {
                    // HedgeOpen or other — record as standalone
                    tradeLog.Add(new BacktestTrade
                    {
                        TradeId = fill.OrderId,
                        GridCycleId = scheduler.GetGridState().GridCycleId ?? "default",
                        EntryTimeUtc = fill.FillTimeUtc,
                        EntryPrice = fill.FillPrice,
                        ExitTimeUtc = null,
                        ExitPrice = null,
                        Side = fill.Side,
                        Size = fill.Size,
                        PnL = null,
                        Fees = fill.Fee,
                        TradeType = fill.TradeType
                    });
                }
                totalFeesPaid += fill.Fee;
            }

            // 3. Update indicators with current candle
            _marketContextBuilder.UpdateIndicators(candle);

            // 4. Get latest closed higher-timeframe candles
            var latest1h = CandleReplayEngine.GetLatestClosedCandle(replayData.Candles1h, candle.Timestamp);
            var latest4h = CandleReplayEngine.GetLatestClosedCandle(replayData.Candles4h, candle.Timestamp);

            // 5. Update position state for scheduler
            var position = executionEngine.GetPosition();
            var positionState = new PositionState
            {
                Symbol = config.Symbol,
                Size = position.Size,
                AverageEntryPrice = position.AverageEntryPrice,
                UnrealisedPnL = position.UnrealisedPnL
            };
            scheduler.UpdateState(scheduler.GetGridState(), positionState);

            // 6. Feed candle to CandleClock → StrategyScheduler handles the event
            // Wire the scheduler to handle the clock event for this tick
            var evt = new Scheduling.Models.CandleClosedEvent
            {
                Symbol = candle.Symbol,
                Timeframe = candle.Interval,
                OpenTimeUtc = candle.Timestamp,
                CloseTimeUtc = candle.Timestamp + GetIntervalMs(candle.Interval),
                Candle = candle
            };
            await scheduler.HandleCandleClosedAsync(evt, latest1h, latest4h, cancellationToken);

            // 7. Track grid cycles
            var gridState = scheduler.GetGridState();
            if (gridState.Lifecycle == GridLifecycle.Closed)
            {
                gridCycles++;
            }

            // 8. Mark position to market and track equity
            var simPosition = executionEngine.GetPosition();
            // Compute unrealised PnL against current candle close
            var unrealisedPnL = simPosition.Size != 0
                ? (candle.Close - simPosition.AverageEntryPrice) * simPosition.Size
                : 0m;
            currentEquity = config.InitialCapital + simPosition.RealisedPnL + unrealisedPnL - totalFeesPaid;
            equityTimeSeries.Add(new EquitySnapshot(candle.Timestamp, currentEquity));
        }

        // Compute final metrics
        return metricsCalculator.Calculate(tradeLog, equityTimeSeries, config.InitialCapital, gridCycles);
    }

    private static long GetIntervalMs(string interval) => interval switch
    {
        "5m" => 5L * 60L * 1000L,
        "15m" => 15L * 60L * 1000L,
        "1h" => 60L * 60L * 1000L,
        "4h" => 4L * 60L * 60L * 1000L,
        _ => throw new ArgumentException($"Unsupported interval: {interval}")
    };
}
```

> **Note**: The trade pairing logic above uses a FIFO (first-in-first-out) approach: the earliest unpaired grid fill entry is matched to each TP/hedge exit. For v1 this is sufficient. A more sophisticated pairer could handle partial fills or multi-level grid matching in future iterations.

> **Note**: Unrealised PnL is now computed inline in the equity tracking loop as `(candle.Close - avgEntryPrice) × positionSize`. This gives accurate equity curves while positions are open.

##### Pattern References

- `.agent-context/0-knowledge/18-backtesting-architecture.md` — Backtest pipeline: replay → context → strategy → signals → risk → position → execution
- `.agent-context/0-knowledge/19-scheduling-architecture.md` — StrategyScheduler handles CandleClosedEvent
- `.agent-context/3-develop/backlog/draft/backtesting/F3-backtest-replay-engine.md` — Runner creates fresh instances per run; equity tracking per tick; multi-cycle support

---

### Task 4.3: Implement input validation with fail-fast error handling {#task-43-implement-input-validation-with-fail-fast-error-handling}

Add comprehensive input validation to `BacktestRunner.RunAsync` that validates config before starting replay. Validation is already partially handled by `CandleReplayEngine.LoadAsync` (data availability), but additional config validation is needed.

- **Complexity**: Low
- **Risk Factors**: Must catch all invalid inputs before starting potentially long replay
- **Files**:
  - `src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs` — modification (add validation at start of `RunAsync`)
- **Success**:
  - Invalid symbol (null/empty) throws descriptive error
  - Invalid date range (start ≥ end) throws descriptive error
  - Invalid initial capital (≤ 0) throws descriptive error
  - Missing intervals throws descriptive error
  - Invalid strategy config JSON throws descriptive error
- **Dependencies**: Task 4.2

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs — modification
// Add this validation block at the start of RunAsync, after the null checks:

private static void ValidateConfig(BacktestConfig config)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(config.Symbol, nameof(config.Symbol));

    if (config.StartDateUtc >= config.EndDateUtc)
        throw new ArgumentException("Start date must be before end date.");

    if (config.InitialCapital <= 0)
        throw new ArgumentException("Initial capital must be greater than zero.");

    if (config.Intervals is null || config.Intervals.Count == 0)
        throw new ArgumentException("At least one interval must be specified.");

    if (!config.Intervals.Contains("15m"))
        throw new ArgumentException("15m interval is required for strategy evaluation.");

    ArgumentException.ThrowIfNullOrWhiteSpace(config.StrategyConfigJson, nameof(config.StrategyConfigJson));

    if (config.WarmupPeriod < 0)
        throw new ArgumentException("Warmup period cannot be negative.");
}
```

##### Pattern References

- `src/TradingApp.Application/MarketData/Queries/GetCandlesQuery.cs` — `ArgumentException.ThrowIfNullOrWhiteSpace` validation pattern in handler

---

### Task 4.4: Write BacktestRunner unit tests {#task-44-write-backtestrunner-unit-tests}

Write unit tests for `BacktestRunner` covering the orchestration flow, input validation, and error handling. Pipeline interfaces are mocked.

- **Complexity**: Medium
- **Risk Factors**: Complex mocking setup with 6+ interface dependencies; must verify correct call sequence
- **Files**:
  - `tests/TradingApp.Application.Tests/Backtesting/Services/BacktestRunnerTests.cs` — new file
- **Success**:
  - Tests cover: successful run with mocked pipeline, config validation errors, missing data errors, deterministic results, equity tracking
  - All tests pass
- **Dependencies**: Tasks 4.1, 4.2, 4.3

#### Implementation Details

```csharp
// tests/TradingApp.Application.Tests/Backtesting/Services/BacktestRunnerTests.cs — new file
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Backtesting.Services;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Tests.Backtesting.Services;

[TestClass]
public sealed class BacktestRunnerTests
{
    private Mock<ICandleRepository> _candleRepoMock = default!;
    private Mock<IMarketContextBuilder> _contextBuilderMock = default!;
    private Mock<IStrategyEngine> _strategyEngineMock = default!;
    private Mock<IGridController> _gridControllerMock = default!;
    private Mock<IRiskEngine> _riskEngineMock = default!;
    private Mock<IPositionManager> _positionManagerMock = default!;
    private BacktestRunner _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _candleRepoMock = new Mock<ICandleRepository>();
        _contextBuilderMock = new Mock<IMarketContextBuilder>();
        _strategyEngineMock = new Mock<IStrategyEngine>();
        _gridControllerMock = new Mock<IGridController>();
        _riskEngineMock = new Mock<IRiskEngine>();
        _positionManagerMock = new Mock<IPositionManager>();

        _sut = new BacktestRunner(
            _candleRepoMock.Object,
            _contextBuilderMock.Object,
            _strategyEngineMock.Object,
            _gridControllerMock.Object,
            _riskEngineMock.Object,
            _positionManagerMock.Object);

        // Default: strategy sees no setup → no signals
        _strategyEngineMock
            .Setup(s => s.EvaluateAsync(It.IsAny<MarketContext>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StrategyEvaluation { SetupDetected = false });

        _gridControllerMock
            .Setup(g => g.ProcessAsync(
                It.IsAny<StrategyEvaluation>(), It.IsAny<MarketContext>(),
                It.IsAny<GridState>(), It.IsAny<PositionState>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TradingSignal>());

        // Default: context builder returns a basic context
        _contextBuilderMock
            .Setup(b => b.Build(It.IsAny<Candle>(), It.IsAny<Candle?>(), It.IsAny<Candle?>()))
            .Returns((Candle trigger, Candle? h1, Candle? h4) => new MarketContext
            {
                Symbol = trigger.Symbol,
                TimestampUtc = trigger.Timestamp,
                CurrentCandle = trigger,
                LatestOneHourCandle = h1,
                LatestFourHourCandle = h4,
                Indicators = new IndicatorSnapshot()
            });
    }

    [TestMethod]
    public async Task GivenValidConfig_WhenRunAsync_ThenReturnsBacktestResult()
    {
        // Arrange
        var config = CreateConfig(warmup: 2);
        SetupCandles("15m", count: 10, startTime: 0);
        SetupCandles("1h", count: 5, startTime: 0);
        SetupCandles("4h", count: 3, startTime: 0);

        // Act
        var result = await _sut.RunAsync(config);

        // Assert
        result.Should().NotBeNull();
        result.FinalEquity.Should().Be(config.InitialCapital); // no trades → equity unchanged
        result.EquityTimeSeries.Should().NotBeEmpty();
        result.TradeLog.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenInvalidDateRange_WhenRunAsync_ThenThrowsArgumentException()
    {
        // Arrange: start ≥ end
        var config = new BacktestConfig
        {
            Symbol = "BTC",
            Intervals = new[] { "15m", "1h", "4h" },
            StartDateUtc = 2000000,
            EndDateUtc = 1000000, // before start
            InitialCapital = 10000m,
            FeeModel = FeeModel.Default,
            StrategyConfigJson = "{}"
        };

        // Act & Assert
        var act = () => _sut.RunAsync(config);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Start date must be before end date*");
    }

    [TestMethod]
    public async Task GivenZeroInitialCapital_WhenRunAsync_ThenThrowsArgumentException()
    {
        // Arrange
        var config = new BacktestConfig
        {
            Symbol = "BTC",
            Intervals = new[] { "15m", "1h", "4h" },
            StartDateUtc = 1000000,
            EndDateUtc = 2000000,
            InitialCapital = 0m,
            FeeModel = FeeModel.Default,
            StrategyConfigJson = "{}"
        };

        // Act & Assert
        var act = () => _sut.RunAsync(config);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Initial capital must be greater than zero*");
    }

    [TestMethod]
    public async Task GivenSameInputs_WhenRunTwice_ThenResultsAreIdentical()
    {
        // Arrange
        var config = CreateConfig(warmup: 2);
        SetupCandles("15m", count: 10, startTime: 0);
        SetupCandles("1h", count: 5, startTime: 0);
        SetupCandles("4h", count: 3, startTime: 0);

        // Act
        var result1 = await _sut.RunAsync(config);
        var result2 = await _sut.RunAsync(config);

        // Assert — deterministic
        result1.TotalPnL.Should().Be(result2.TotalPnL);
        result1.FinalEquity.Should().Be(result2.FinalEquity);
        result1.TotalTrades.Should().Be(result2.TotalTrades);
        result1.EquityTimeSeries.Should().HaveCount(result2.EquityTimeSeries.Count);
    }

    [TestMethod]
    public async Task GivenValidConfig_WhenRunAsync_ThenEquityTrackingStartsAtInitialCapital()
    {
        // Arrange
        var config = CreateConfig(warmup: 2);
        SetupCandles("15m", count: 10, startTime: 0);
        SetupCandles("1h", count: 5, startTime: 0);
        SetupCandles("4h", count: 3, startTime: 0);

        // Act
        var result = await _sut.RunAsync(config);

        // Assert
        result.EquityTimeSeries.Should().NotBeEmpty();
        result.EquityTimeSeries[0].Equity.Should().Be(10000m);
    }

    // --- Helpers ---

    private static BacktestConfig CreateConfig(int warmup = 2)
    {
        return new BacktestConfig
        {
            Symbol = "BTC",
            Intervals = new[] { "15m", "1h", "4h" },
            StartDateUtc = 1000000,
            EndDateUtc = 2000000,
            InitialCapital = 10000m,
            FeeModel = FeeModel.Default,
            WarmupPeriod = warmup,
            StrategyConfigJson = "{}"
        };
    }

    private void SetupCandles(string interval, int count, long startTime)
    {
        var intervalMs = interval switch
        {
            "15m" => 15L * 60 * 1000,
            "1h" => 60L * 60 * 1000,
            "4h" => 4L * 60 * 60 * 1000,
            _ => throw new ArgumentException($"Unknown interval: {interval}")
        };

        var candles = Enumerable.Range(0, count)
            .Select(i => new Candle
            {
                Symbol = "BTC",
                Interval = interval,
                Timestamp = startTime + i * intervalMs,
                Open = 100m, High = 105m, Low = 95m, Close = 102m, Volume = 1000m
            })
            .ToList();

        _candleRepoMock
            .Setup(r => r.GetCandlesAsync("BTC", interval, It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(candles);
    }
}
```

##### Pattern References

- `tests/TradingApp.Api.Tests/Services/HyperliquidOrderServiceTests.cs` — Multi-dependency SUT with Moq, `[TestInitialize]`
- `tests/TradingApp.Infrastructure.Tests/Services/HyperliquidSignerTests.cs` — Given_When_Then naming, FluentAssertions

---

### Task 4.5: Write StrategyScheduler unit tests {#task-45-write-strategyscheduler-unit-tests}

Write unit tests for `StrategyScheduler` covering trigger timeframe filtering and pipeline call sequence.

- **Complexity**: Medium
- **Risk Factors**: Must verify that pipeline interfaces are called in correct order; must verify non-trigger events are ignored
- **Files**:
  - `tests/TradingApp.Application.Tests/Scheduling/StrategySchedulerTests.cs` — new file
- **Success**:
  - Tests cover: 15m event triggers pipeline, non-15m event ignored, pipeline called in correct order, no signals → position manager not called
  - All tests pass
- **Dependencies**: Task 4.1

#### Implementation Details

```csharp
// tests/TradingApp.Application.Tests/Scheduling/StrategySchedulerTests.cs — new file
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Scheduling;
using TradingApp.Application.Scheduling.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Tests.Scheduling;

[TestClass]
public sealed class StrategySchedulerTests
{
    private Mock<IMarketContextBuilder> _contextBuilderMock = default!;
    private Mock<IStrategyEngine> _strategyEngineMock = default!;
    private Mock<IGridController> _gridControllerMock = default!;
    private Mock<IRiskEngine> _riskEngineMock = default!;
    private Mock<IPositionManager> _positionManagerMock = default!;
    private StrategyScheduler _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _contextBuilderMock = new Mock<IMarketContextBuilder>();
        _strategyEngineMock = new Mock<IStrategyEngine>();
        _gridControllerMock = new Mock<IGridController>();
        _riskEngineMock = new Mock<IRiskEngine>();
        _positionManagerMock = new Mock<IPositionManager>();

        _sut = new StrategyScheduler(
            _contextBuilderMock.Object,
            _strategyEngineMock.Object,
            _gridControllerMock.Object,
            _riskEngineMock.Object,
            _positionManagerMock.Object,
            "{}");

        _contextBuilderMock
            .Setup(b => b.Build(It.IsAny<Candle>(), It.IsAny<Candle?>(), It.IsAny<Candle?>()))
            .Returns(new MarketContext
            {
                Symbol = "BTC", TimestampUtc = 1000,
                CurrentCandle = CreateCandle("15m"), Indicators = new IndicatorSnapshot()
            });

        _strategyEngineMock
            .Setup(s => s.EvaluateAsync(It.IsAny<MarketContext>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StrategyEvaluation { SetupDetected = false });

        _gridControllerMock
            .Setup(g => g.ProcessAsync(
                It.IsAny<StrategyEvaluation>(), It.IsAny<MarketContext>(),
                It.IsAny<GridState>(), It.IsAny<PositionState>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TradingSignal>());
    }

    [TestMethod]
    public async Task GivenNon15mEvent_WhenHandleCandleClosedAsync_ThenPipelineNotCalled()
    {
        // Arrange
        var evt = CreateEvent("1h");

        // Act
        await _sut.HandleCandleClosedAsync(evt, null, null);

        // Assert
        _strategyEngineMock.Verify(
            s => s.EvaluateAsync(It.IsAny<MarketContext>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task Given15mEvent_WhenHandleCandleClosedAsync_ThenStrategyEngineIsCalled()
    {
        // Arrange
        var evt = CreateEvent("15m");

        // Act
        await _sut.HandleCandleClosedAsync(evt, null, null);

        // Assert
        _strategyEngineMock.Verify(
            s => s.EvaluateAsync(It.IsAny<MarketContext>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenNoSignals_WhenHandleCandleClosedAsync_ThenPositionManagerNotCalled()
    {
        // Arrange
        var evt = CreateEvent("15m");

        // Act
        await _sut.HandleCandleClosedAsync(evt, null, null);

        // Assert
        _positionManagerMock.Verify(
            p => p.ExecuteSignalsAsync(It.IsAny<IReadOnlyList<TradingSignal>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static CandleClosedEvent CreateEvent(string timeframe)
    {
        return new CandleClosedEvent
        {
            Symbol = "BTC",
            Timeframe = timeframe,
            OpenTimeUtc = 1000,
            CloseTimeUtc = 1900,
            Candle = CreateCandle(timeframe)
        };
    }

    private static Candle CreateCandle(string interval)
    {
        return new Candle
        {
            Symbol = "BTC", Interval = interval, Timestamp = 1000,
            Open = 100m, High = 105m, Low = 95m, Close = 102m, Volume = 1000m
        };
    }
}
```

##### Pattern References

- `tests/TradingApp.Api.Tests/Services/HyperliquidOrderServiceTests.cs` — Mock verification pattern using `Verify(..., Times.Once)`

---

### Task 4.6: Verify full solution builds and all tests pass {#task-46-verify-full-solution-builds-and-all-tests-pass}

Build the full solution and run ALL tests across all test projects.

- **Complexity**: Low
- **Risk Factors**: Potential compilation issues from complex wiring; ensure no regressions
- **Files**: None (verification only)
- **Success**:
  - `dotnet build` succeeds across entire solution
  - `dotnet test` passes all tests in all test projects
  - No regressions in existing tests
  - All new tests pass (CandleClock, SimulatedExecutionEngine, CandleReplayEngine, BacktestMetricsCalculator, BacktestRunner, StrategyScheduler)
- **Dependencies**: All Phase 4 tasks

---

## Phase Success Criteria

- StrategyScheduler correctly filters for 15m trigger timeframe and ignores other timeframes
- StrategyScheduler calls pipeline interfaces in correct sequence (contextBuilder → strategyEngine → gridController → riskEngine → positionManager)
- BacktestRunner creates fresh component instances per run (stateless between runs)
- BacktestRunner drives replay loop: warmup → evaluation with fill processing + signal generation
- Equity tracked at each 15m tick starting from initial capital
- BacktestRunner returns complete BacktestResult with metrics, equity time-series, and trade log
- Input validation catches invalid config before starting replay
- Same inputs produce identical results (deterministic)
- All unit tests pass across entire solution with zero regressions
