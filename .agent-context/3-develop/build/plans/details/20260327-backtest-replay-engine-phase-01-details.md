<!-- markdownlint-disable-file -->

# Task Details: F3 — Backtest Replay Engine

## Phase 1: Foundation — Models, Interfaces, and Scheduling

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — `sealed` classes, one class per file (except handler co-location), PascalCase naming, `_camelCase` private fields, `async/await` + `CancellationToken`, `ArgumentException.ThrowIfNullOrWhiteSpace` for validation
- `.github/instructions/testing.instructions.md` — MSTest, Moq, FluentAssertions v6, `Given_When_Then` naming, `[TestClass]`/`[TestMethod]`, `[TestInitialize]` for setup
- `.github/instructions/dotnet-architecture.instructions.md` — Clean architecture layers, bounded-context folders, interfaces in `Application/Abstractions/Services/`
- `.agent-context/0-knowledge/18-backtesting-architecture.md` — `IExecutionEngine` interface segregation pattern, backtest component names
- `.agent-context/0-knowledge/19-scheduling-architecture.md` — `CandleClock` implementation with code samples, `CandleClosedEvent` model

## Design References

- `src/TradingApp.Application/Abstractions/Services/IHyperliquidRestClient.cs` — Interface pattern for `Application/Abstractions/Services/`
- `src/TradingApp.Application/MarketData/Models/CandleDto.cs` — DTO pattern with `sealed class` + `init` properties
- `src/TradingApp.Application/Abstractions/Configuration/HyperliquidOptions.cs` — Configuration model pattern

---

### Task 1.1: Create backtest models and DTOs {#task-11-create-backtest-models-and-dtos}

Create all backtest-specific models and DTOs in `src/TradingApp.Application/Backtesting/Models/`.

- **Complexity**: Medium
- **Risk Factors**: Many new files; must establish folder structure and namespace conventions for the Backtesting feature area
- **Files**:
  - `src/TradingApp.Application/Backtesting/Models/BacktestConfig.cs` — new file
  - `src/TradingApp.Application/Backtesting/Models/BacktestResult.cs` — new file
  - `src/TradingApp.Application/Backtesting/Models/BacktestTrade.cs` — new file
  - `src/TradingApp.Application/Backtesting/Models/EquitySnapshot.cs` — new file
  - `src/TradingApp.Application/Backtesting/Models/FeeModel.cs` — new file
  - `src/TradingApp.Application/Backtesting/Models/SimulatedFill.cs` — new file
  - `src/TradingApp.Application/Backtesting/Models/SimulatedOrder.cs` — new file
  - `src/TradingApp.Application/Backtesting/Models/SimulatedPosition.cs` — new file
- **Success**:
  - All model files compile
  - Namespace convention: `TradingApp.Application.Backtesting.Models`
  - All classes are `sealed`
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/Models/BacktestConfig.cs — new file
namespace TradingApp.Application.Backtesting.Models;

public sealed class BacktestConfig
{
    public required string Symbol { get; init; }
    public required IReadOnlyList<string> Intervals { get; init; } // e.g., ["15m", "1h", "4h"]
    public required long StartDateUtc { get; init; }   // unix ms
    public required long EndDateUtc { get; init; }     // unix ms
    public required decimal InitialCapital { get; init; }
    public required FeeModel FeeModel { get; init; }
    public int WarmupPeriod { get; init; } = 200;      // candles
    public required string StrategyConfigJson { get; init; }  // serialised strategy config
}
```

```csharp
// src/TradingApp.Application/Backtesting/Models/BacktestResult.cs — new file
namespace TradingApp.Application.Backtesting.Models;

public sealed class BacktestResult
{
    public required int TotalTrades { get; init; }
    public required int WinningTrades { get; init; }
    public required int LosingTrades { get; init; }
    public required decimal WinRate { get; init; }           // percentage
    public required decimal TotalPnL { get; init; }
    public required decimal MaxDrawdownAbsolute { get; init; }
    public required decimal MaxDrawdownPercent { get; init; } // percentage of peak equity
    public required decimal AverageTradePnL { get; init; }
    public required TimeSpan AverageHoldTime { get; init; }
    public required int HedgesOpened { get; init; }
    public required decimal TotalFeesPaid { get; init; }
    public required int GridCycles { get; init; }
    public required decimal FinalEquity { get; init; }
    public required IReadOnlyList<EquitySnapshot> EquityTimeSeries { get; init; }
    public required IReadOnlyList<BacktestTrade> TradeLog { get; init; }
}
```

```csharp
// src/TradingApp.Application/Backtesting/Models/BacktestTrade.cs — new file
namespace TradingApp.Application.Backtesting.Models;

public sealed class BacktestTrade
{
    public required string TradeId { get; init; }
    public required string GridCycleId { get; init; }
    public required long EntryTimeUtc { get; init; }   // unix ms
    public required decimal EntryPrice { get; init; }
    public long? ExitTimeUtc { get; init; }            // unix ms, null if still open
    public decimal? ExitPrice { get; init; }
    public required OrderSide Side { get; init; }
    public required decimal Size { get; init; }
    public decimal? PnL { get; init; }
    public required decimal Fees { get; init; }
    public required TradeType TradeType { get; init; }
}
```

```csharp
// src/TradingApp.Application/Backtesting/Models/EquitySnapshot.cs — new file
namespace TradingApp.Application.Backtesting.Models;

public sealed record EquitySnapshot(long TimestampUtc, decimal Equity);
```

```csharp
// src/TradingApp.Application/Backtesting/Models/FeeModel.cs — new file
namespace TradingApp.Application.Backtesting.Models;

public sealed class FeeModel
{
    public decimal MakerFeeRate { get; init; } = 0.0001m;  // 0.01%
    public decimal TakerFeeRate { get; init; } = 0.00035m; // 0.035%
    public decimal SlippageRate { get; init; } = 0m;       // 0%

    public static FeeModel Default => new();

    public decimal CalculateFee(decimal fillSize, decimal fillPrice, bool isMaker)
    {
        var rate = isMaker ? MakerFeeRate : TakerFeeRate;
        return fillSize * fillPrice * rate;
    }

    public decimal ApplySlippage(decimal price, OrderSide side)
    {
        // Slippage always moves price against the trader
        return side == OrderSide.Buy
            ? price * (1 + SlippageRate)
            : price * (1 - SlippageRate);
    }
}
```

```csharp
// NOTE: TradeType enum has been moved to Task 1.2 (Trading/Models/TradeType.cs)
// to avoid cross-namespace dependency from shared pipeline types to backtest-specific types.
```

```csharp
// src/TradingApp.Application/Backtesting/Models/SimulatedFill.cs — new file
namespace TradingApp.Application.Backtesting.Models;

public sealed class SimulatedFill
{
    public required string OrderId { get; init; }
    public required long FillTimeUtc { get; init; }    // unix ms — candle timestamp
    public required decimal FillPrice { get; init; }
    public required OrderSide Side { get; init; }
    public required decimal Size { get; init; }
    public required decimal Fee { get; init; }
    public required string Symbol { get; init; }
    public required TradeType TradeType { get; init; }
    public bool IsMaker { get; init; }
}
```

```csharp
// src/TradingApp.Application/Backtesting/Models/SimulatedOrder.cs — new file
namespace TradingApp.Application.Backtesting.Models;

public sealed class SimulatedOrder
{
    public required string OrderId { get; init; }
    public required string Symbol { get; init; }
    public required OrderSide Side { get; init; }
    public required OrderType OrderType { get; init; }
    public required decimal Price { get; init; }
    public required decimal Size { get; init; }
    public required TradeType TradeType { get; init; }
    public long PlacedAtUtc { get; init; }
}
```

```csharp
// src/TradingApp.Application/Backtesting/Models/SimulatedPosition.cs — new file
namespace TradingApp.Application.Backtesting.Models;

public sealed class SimulatedPosition
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Size { get; set; }
    public decimal AverageEntryPrice { get; set; }
    public decimal UnrealisedPnL { get; set; }
    public decimal RealisedPnL { get; set; }

    public bool IsOpen => Size != 0;
}
```

##### Pattern References

- `src/TradingApp.Application/MarketData/Models/CandleDto.cs` — DTO pattern: `sealed class`, `init` properties, same namespace convention
- `.agent-context/3-develop/backlog/draft/backtesting/F3-backtest-replay-engine.md` — All model fields derived from PBI specification

---

### Task 1.2: Create trading pipeline models {#task-12-create-trading-pipeline-models}

Create shared trading models used by pipeline interfaces. These are intentionally minimal and will be expanded by future pipeline PBIs.

- **Complexity**: Medium
- **Risk Factors**: These models define the interface boundaries for the entire trading pipeline; design choices propagate to all future PBIs
- **Files**:
  - `src/TradingApp.Application/Trading/Models/OrderRequest.cs` — new file
  - `src/TradingApp.Application/Trading/Models/OrderSide.cs` — new file
  - `src/TradingApp.Application/Trading/Models/OrderType.cs` — new file
  - `src/TradingApp.Application/Trading/Models/TradeType.cs` — new file (moved from Backtesting/Models to avoid cross-namespace dependency)
  - `src/TradingApp.Application/Trading/Models/MarketContext.cs` — new file
  - `src/TradingApp.Application/Trading/Models/IndicatorSnapshot.cs` — new file
  - `src/TradingApp.Application/Trading/Models/GridState.cs` — new file
  - `src/TradingApp.Application/Trading/Models/PositionState.cs` — new file
- **Success**:
  - All model files compile
  - Namespace: `TradingApp.Application.Trading.Models`
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Application/Trading/Models/OrderSide.cs — new file
namespace TradingApp.Application.Trading.Models;

public enum OrderSide
{
    Buy,
    Sell
}
```

```csharp
// src/TradingApp.Application/Trading/Models/OrderType.cs — new file
namespace TradingApp.Application.Trading.Models;

public enum OrderType
{
    Limit,
    Market
}
```

```csharp
// src/TradingApp.Application/Trading/Models/TradeType.cs — new file
namespace TradingApp.Application.Trading.Models;

public enum TradeType
{
    GridFill,
    TakeProfit,
    HedgeOpen,
    HedgeClose
}
```

```csharp
// src/TradingApp.Application/Trading/Models/OrderRequest.cs — new file
namespace TradingApp.Application.Trading.Models;

public sealed class OrderRequest
{
    public required string Symbol { get; init; }
    public required OrderSide Side { get; init; }
    public required OrderType OrderType { get; init; }
    public required decimal Price { get; init; }
    public required decimal Size { get; init; }
    public required TradeType TradeType { get; init; }
    public string? ClientOrderId { get; init; }
}
```

Note: `TradeType` is defined in `Trading/Models/` since it's used by shared pipeline interfaces (`OrderRequest`, `SimulatedOrder`, `SimulatedFill`). Backtest models reference it via `using TradingApp.Application.Trading.Models;`.

```csharp
// src/TradingApp.Application/Trading/Models/MarketContext.cs — new file
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Trading.Models;

/// <summary>
/// Market context provided to the strategy engine at each candle close.
/// Contains the trigger candle, higher-timeframe context, and computed indicators.
/// </summary>
public sealed class MarketContext
{
    public required string Symbol { get; init; }
    public required long TimestampUtc { get; init; }     // current tick timestamp (unix ms)
    public required Candle CurrentCandle { get; init; }   // 15m trigger candle
    public Candle? LatestOneHourCandle { get; init; }
    public Candle? LatestFourHourCandle { get; init; }
    public required IndicatorSnapshot Indicators { get; init; }
}
```

```csharp
// src/TradingApp.Application/Trading/Models/IndicatorSnapshot.cs — new file
namespace TradingApp.Application.Trading.Models;

/// <summary>
/// Computed technical indicator values at a point in time.
/// Will be expanded as more indicators are added.
/// </summary>
public sealed class IndicatorSnapshot
{
    public decimal EmaFast { get; init; }
    public decimal EmaSlow { get; init; }
    public decimal EmaTrend { get; init; }
    public decimal Rsi { get; init; }
    public decimal Atr { get; init; }
}
```

```csharp
// src/TradingApp.Application/Trading/Models/GridState.cs — new file
namespace TradingApp.Application.Trading.Models;

/// <summary>
/// Represents the current state of a grid deployment.
/// Minimal definition — will be expanded by GridController PBI.
/// </summary>
public sealed class GridState
{
    public GridLifecycle Lifecycle { get; set; } = GridLifecycle.Inactive;
    public string? GridCycleId { get; set; }
    public int FilledLevels { get; set; }
    public int TotalLevels { get; set; }
}

public enum GridLifecycle
{
    Inactive,
    Planning,
    Deploying,
    Active,
    PartiallyFilled,
    FullyFilled,
    Closing,
    Closed
}
```

```csharp
// src/TradingApp.Application/Trading/Models/PositionState.cs — new file
namespace TradingApp.Application.Trading.Models;

/// <summary>
/// Represents the current position state for a symbol.
/// Minimal definition — will be expanded by PositionManager PBI.
/// </summary>
public sealed class PositionState
{
    public string Symbol { get; init; } = string.Empty;
    public decimal Size { get; init; }
    public decimal AverageEntryPrice { get; init; }
    public decimal UnrealisedPnL { get; init; }
    public bool IsOpen => Size != 0;
}
```

##### Pattern References

- `.agent-context/0-knowledge/15-grid-controller.md` — Grid lifecycle states: Inactive → Planning → Deploying → Active → PartiallyFilled → FullyFilled → Closing → Closed
- `.agent-context/0-knowledge/14-strategy-runtime-model.md` — Strategy evaluation inputs
- `src/TradingApp.Application/MarketData/Models/CandleDto.cs` — DTO naming and structure pattern

---

### Task 1.3: Create pipeline interfaces {#task-13-create-pipeline-interfaces}

Define thin pipeline interfaces in `Application/Abstractions/Services/`. These establish the contract boundary between strategy logic and execution. Implementations will come with future pipeline PBIs.

- **Complexity**: Medium
- **Risk Factors**: Interface design propagates to all future pipeline implementations; keep intentionally minimal
- **Files**:
  - `src/TradingApp.Application/Abstractions/Services/IExecutionEngine.cs` — new file
  - `src/TradingApp.Application/Abstractions/Services/IBacktestRunner.cs` — new file
  - `src/TradingApp.Application/Abstractions/Services/IStrategyEngine.cs` — new file
  - `src/TradingApp.Application/Abstractions/Services/IGridController.cs` — new file
  - `src/TradingApp.Application/Abstractions/Services/IRiskEngine.cs` — new file
  - `src/TradingApp.Application/Abstractions/Services/IPositionManager.cs` — new file
  - `src/TradingApp.Application/Abstractions/Services/IMarketContextBuilder.cs` — new file
- **Success**:
  - All interfaces compile
  - Follow existing `Application/Abstractions/Services/` pattern
  - Namespace: `TradingApp.Application.Abstractions.Services`
- **Dependencies**: Task 1.1 (backtest models), Task 1.2 (trading models)

#### Implementation Details

```csharp
// src/TradingApp.Application/Abstractions/Services/IExecutionEngine.cs — new file
using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.Abstractions.Services;

/// <summary>
/// Execution boundary interface. Live mode uses HyperliquidExecutionEngine;
/// backtest mode uses SimulatedExecutionEngine.
/// </summary>
public interface IExecutionEngine
{
    Task<string> PlaceOrderAsync(OrderRequest order, CancellationToken cancellationToken = default);
    Task CancelOrderAsync(string orderId, CancellationToken cancellationToken = default);
    Task CancelAllOrdersAsync(string symbol, CancellationToken cancellationToken = default);
}
```

```csharp
// src/TradingApp.Application/Abstractions/Services/IBacktestRunner.cs — new file
using TradingApp.Application.Backtesting.Models;

namespace TradingApp.Application.Abstractions.Services;

public interface IBacktestRunner
{
    Task<BacktestResult> RunAsync(BacktestConfig config, CancellationToken cancellationToken = default);
}
```

```csharp
// src/TradingApp.Application/Abstractions/Services/IMarketContextBuilder.cs — new file
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Abstractions.Services;

/// <summary>
/// Builds MarketContext from candle data and indicator state.
/// Shared between live and backtest modes.
/// </summary>
public interface IMarketContextBuilder
{
    /// <summary>
    /// Feed a candle into the indicator buffers (used during warmup and evaluation).
    /// </summary>
    void UpdateIndicators(Candle candle);

    /// <summary>
    /// Build the full market context for strategy evaluation.
    /// </summary>
    MarketContext Build(Candle triggerCandle, Candle? latestOneHourCandle, Candle? latestFourHourCandle);
}
```

```csharp
// src/TradingApp.Application/Abstractions/Services/IStrategyEngine.cs — new file
using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.Abstractions.Services;

/// <summary>
/// Evaluates market context and determines if a grid setup exists.
/// </summary>
public interface IStrategyEngine
{
    Task<StrategyEvaluation> EvaluateAsync(MarketContext context, string strategyConfigJson, CancellationToken cancellationToken = default);
}
```

```csharp
// src/TradingApp.Application/Trading/Models/StrategyEvaluation.cs — new file
namespace TradingApp.Application.Trading.Models;

/// <summary>
/// Result of strategy evaluation — indicates whether a setup was detected.
/// </summary>
public sealed class StrategyEvaluation
{
    public bool SetupDetected { get; init; }
    public string? Reason { get; init; }
}
```

```csharp
// src/TradingApp.Application/Abstractions/Services/IGridController.cs — new file
using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.Abstractions.Services;

/// <summary>
/// Manages grid lifecycle and emits trading signals.
/// </summary>
public interface IGridController
{
    Task<IReadOnlyList<TradingSignal>> ProcessAsync(
        StrategyEvaluation evaluation,
        MarketContext context,
        GridState gridState,
        PositionState positionState,
        string strategyConfigJson,
        CancellationToken cancellationToken = default);
}
```

```csharp
// src/TradingApp.Application/Trading/Models/TradingSignal.cs — new file
namespace TradingApp.Application.Trading.Models;

/// <summary>
/// A trading signal emitted by the grid controller.
/// Will be expanded to typed signal hierarchy (DeployGrid, TakeProfit, etc.) by pipeline PBIs.
/// </summary>
public sealed class TradingSignal
{
    public required string SignalType { get; init; }  // e.g., "DeployGrid", "TakeProfit", "OpenHedge"
    public required string Symbol { get; init; }
    public string? Reason { get; init; }
    public IReadOnlyDictionary<string, object>? Parameters { get; init; }
}
```

```csharp
// src/TradingApp.Application/Abstractions/Services/IRiskEngine.cs — new file
namespace TradingApp.Application.Abstractions.Services;

/// <summary>
/// Validates trading signals against risk limits.
/// </summary>
public interface IRiskEngine
{
    Task<IReadOnlyList<TradingSignal>> ValidateAsync(
        IReadOnlyList<TradingSignal> signals,
        CancellationToken cancellationToken = default);
}
```

```csharp
// src/TradingApp.Application/Abstractions/Services/IPositionManager.cs — new file
namespace TradingApp.Application.Abstractions.Services;

/// <summary>
/// Translates approved trading signals into order actions and executes them via IExecutionEngine.
/// </summary>
public interface IPositionManager
{
    Task ExecuteSignalsAsync(
        IReadOnlyList<TradingSignal> approvedSignals,
        CancellationToken cancellationToken = default);
}
```

##### Pattern References

- `src/TradingApp.Application/Abstractions/Services/IHyperliquidRestClient.cs` — Interface in `Application/Abstractions/Services/` namespace pattern
- `.agent-context/0-knowledge/18-backtesting-architecture.md` — `IExecutionEngine` segregation: `LiveExecutionEngine` and `SimulatedExecutionEngine`
- `.agent-context/0-knowledge/16-signal-contracts.md` — Signal types: DeployGrid, CancelGrid, TakeProfit, OpenHedge, etc.
- `.agent-context/0-knowledge/15-grid-controller.md` — GridController inputs: StrategyConfig, MarketSnapshot, IndicatorSnapshot, GridState, PositionState

---

### Task 1.4: Implement CandleClock and CandleClosedEvent {#task-14-implement-candleclock-and-candleclosedevent}

Implement the `CandleClock` scheduling component and `CandleClosedEvent` model from the knowledge docs. The CandleClock detects candle close transitions and emits events exactly once per candle, preventing duplicates.

- **Complexity**: Medium
- **Risk Factors**: Must prevent duplicate event emission; used by both live and backtest modes
- **Files**:
  - `src/TradingApp.Application/Scheduling/Models/CandleClosedEvent.cs` — new file
  - `src/TradingApp.Application/Scheduling/CandleClock.cs` — new file
- **Success**:
  - CandleClock emits exactly one event per unique candle close
  - Duplicate candle submissions are ignored
  - Events fire in submission order
- **Dependencies**: F1's `Candle` entity (assumed complete)

#### Implementation Details

```csharp
// src/TradingApp.Application/Scheduling/Models/CandleClosedEvent.cs — new file
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Scheduling.Models;

public sealed class CandleClosedEvent
{
    public required string Symbol { get; init; }
    public required string Timeframe { get; init; }
    public required long OpenTimeUtc { get; init; }
    public required long CloseTimeUtc { get; init; }
    public required Candle Candle { get; init; }
}
```

```csharp
// src/TradingApp.Application/Scheduling/CandleClock.cs — new file
using TradingApp.Application.Scheduling.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Scheduling;

/// <summary>
/// Detects candle close transitions and emits CandleClosedEvent exactly once per candle.
/// Shared between live trading (WebSocket feed) and backtesting (replay feed).
/// </summary>
public sealed class CandleClock
{
    private readonly Dictionary<string, long> _lastClosed = new();

    public event Func<CandleClosedEvent, Task>? CandleClosed;

    public async Task ProcessCandleAsync(Candle candle)
    {
        var key = $"{candle.Symbol}:{candle.Interval}";
        var closeTimeUtc = candle.Timestamp + GetIntervalMs(candle.Interval);

        if (_lastClosed.TryGetValue(key, out var lastCloseTime) &&
            lastCloseTime >= closeTimeUtc)
        {
            return; // duplicate or older candle — ignore
        }

        _lastClosed[key] = closeTimeUtc;

        if (CandleClosed is not null)
        {
            await CandleClosed.Invoke(new CandleClosedEvent
            {
                Symbol = candle.Symbol,
                Timeframe = candle.Interval,
                OpenTimeUtc = candle.Timestamp,
                CloseTimeUtc = closeTimeUtc,
                Candle = candle
            });
        }
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

> **Note**: F1's `Candle` entity has `Timestamp` (open time) only — no `CloseTimeUtc` property. The `CandleClock` computes close time via `Timestamp + GetIntervalMs(Interval)`. Test helpers should construct candles using `Timestamp` and derive close time the same way.

##### Pattern References

- `.agent-context/0-knowledge/19-scheduling-architecture.md` — Full `CandleClock` code sample: duplicate detection via `Dictionary<string, long>`, event-based `CandleClosed` delegate
- `src/TradingApp.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs` — Timeframe-to-interval-ms mapping: `["15m"] = 15L * 60L * 1000L`

---

### Task 1.5: Write CandleClock unit tests {#task-15-write-candleclock-unit-tests}

Write unit tests for `CandleClock` covering event emission, duplicate prevention, and multi-timeframe support.

- **Complexity**: Low
- **Risk Factors**: None significant
- **Files**:
  - `tests/TradingApp.Application.Tests/Scheduling/CandleClockTests.cs` — new file
- **Success**:
  - Tests cover: single event emission, duplicate prevention, multiple timeframes
  - All tests pass
- **Dependencies**: Task 1.4 (CandleClock implementation)

#### Implementation Details

```csharp
// tests/TradingApp.Application.Tests/Scheduling/CandleClockTests.cs — new file
using TradingApp.Application.Scheduling;
using TradingApp.Application.Scheduling.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Tests.Scheduling;

[TestClass]
public sealed class CandleClockTests
{
    private CandleClock _sut = default!;
    private List<CandleClosedEvent> _emittedEvents = default!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new CandleClock();
        _emittedEvents = new List<CandleClosedEvent>();
        _sut.CandleClosed += evt =>
        {
            _emittedEvents.Add(evt);
            return Task.CompletedTask;
        };
    }

    [TestMethod]
    public async Task GivenNewCandle_WhenProcessCandleAsync_ThenEmitsCandleClosedEvent()
    {
        // Arrange
        var candle = CreateCandle("BTC", "15m", timestampUtc: 1000);

        // Act
        await _sut.ProcessCandleAsync(candle);

        // Assert
        _emittedEvents.Should().HaveCount(1);
        _emittedEvents[0].Symbol.Should().Be("BTC");
        _emittedEvents[0].Timeframe.Should().Be("15m");
    }

    [TestMethod]
    public async Task GivenDuplicateCandle_WhenProcessCandleAsync_ThenDoesNotEmitDuplicateEvent()
    {
        // Arrange
        var candle = CreateCandle("BTC", "15m", timestampUtc: 1000);

        // Act
        await _sut.ProcessCandleAsync(candle);
        await _sut.ProcessCandleAsync(candle); // duplicate

        // Assert
        _emittedEvents.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task GivenOlderCandle_WhenProcessCandleAsync_ThenIgnoresOlderCandle()
    {
        // Arrange
        var newerCandle = CreateCandle("BTC", "15m", timestampUtc: 2000);
        var olderCandle = CreateCandle("BTC", "15m", timestampUtc: 1000);

        // Act
        await _sut.ProcessCandleAsync(newerCandle);
        await _sut.ProcessCandleAsync(olderCandle); // older — ignored

        // Assert
        _emittedEvents.Should().HaveCount(1);
        _emittedEvents[0].OpenTimeUtc.Should().Be(2000);
    }

    [TestMethod]
    public async Task GivenDifferentTimeframes_WhenProcessCandleAsync_ThenTracksIndependently()
    {
        // Arrange
        var candle15m = CreateCandle("BTC", "15m", timestampUtc: 1000);
        var candle1h = CreateCandle("BTC", "1h", timestampUtc: 1000);

        // Act
        await _sut.ProcessCandleAsync(candle15m);
        await _sut.ProcessCandleAsync(candle1h);

        // Assert
        _emittedEvents.Should().HaveCount(2);
    }

    // Helper — F1's Candle entity has Timestamp (open time) only.
    // CandleClock computes close time from Timestamp + IntervalDurationMs.
    private static Candle CreateCandle(string symbol, string interval, long timestampUtc)
    {
        return new Candle
        {
            Symbol = symbol,
            Interval = interval,
            Timestamp = timestampUtc,
            Open = 100m,
            High = 105m,
            Low = 95m,
            Close = 102m,
            Volume = 1000m
        };
    }
}
```

##### Pattern References

- `tests/TradingApp.Infrastructure.Tests/Services/HyperliquidSignerTests.cs` — Sealed test class, `Given_When_Then` naming, FluentAssertions
- `tests/TradingApp.Application.Tests/Usings.cs` — Global usings: FluentAssertions, MSTest, Moq

---

### Task 1.6: Verify solution builds and tests pass {#task-16-verify-solution-builds-and-tests-pass}

Build the full solution and run all tests to confirm Phase 1 compiles and tests pass.

- **Complexity**: Low
- **Risk Factors**: Possible compilation issues from cross-namespace references or missing F1 types
- **Files**: None (verification only)
- **Success**:
  - `dotnet build` succeeds with zero errors
  - `dotnet test` passes all CandleClock tests
  - No regressions in existing tests
- **Dependencies**: All Phase 1 tasks

---

## Phase Success Criteria

- All backtest models, trading models, and pipeline interfaces compile
- CandleClock implementation matches the scheduling architecture knowledge doc
- CandleClock unit tests pass (event emission, duplicate prevention, multi-timeframe)
- Full solution builds with zero errors
- No regressions in existing tests
