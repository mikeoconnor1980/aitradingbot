<!-- markdownlint-disable-file -->

# Task Details: Backtest Debug/Audit Log

## Phase 1: Audit Log Models & Collector Infrastructure

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — sealed classes, factory methods, `Given_When_Then` test naming
- `.github/instructions/testing.instructions.md` — MSTest, FluentAssertions, Moq; handlers tested via controller tests only
- `.github/instructions/dotnet-architecture.instructions.md` — models in bounded context folders, CQRS patterns
- `.agent-context/0-knowledge/18-backtesting-architecture.md` — backtest execution phases, result model
- `.agent-context/0-knowledge/15-grid-controller.md` — grid lifecycle state machine
- `.agent-context/0-knowledge/16-signal-contracts.md` — signal types and payloads

### Task 1.1: Create audit log entry models {#task-11-create-audit-log-entry-models}

Create three record types representing the per-candle evaluation, order event, and grid cycle log entries. These are simple data carriers serialized to JSON blobs.

- **Complexity**: Medium
- **Risk Factors**: Model must capture all fields specified in PBI requirements (candle OHLCV, indicator snapshot, grid state, etc.)
- **Files**:
  - `src/TradingApp.Application/Backtesting/Models/CandleEvaluationEntry.cs` — new file
  - `src/TradingApp.Application/Backtesting/Models/OrderEventEntry.cs` — new file
  - `src/TradingApp.Application/Backtesting/Models/GridCycleEntry.cs` — new file
- **Success**:
  - All three record types exist with complete required fields
  - Records compile and are referenced by the collector interface (Task 1.3)
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/Models/CandleEvaluationEntry.cs — new file
namespace TradingApp.Application.Backtesting.Models;

/// <summary>
/// Per-candle audit entry capturing the full evaluation state at each 15m candle.
/// </summary>
public sealed record CandleEvaluationEntry
{
    public required long TimestampUtc { get; init; }
    public required decimal Open { get; init; }
    public required decimal High { get; init; }
    public required decimal Low { get; init; }
    public required decimal Close { get; init; }
    public required decimal Volume { get; init; }
    public required bool IsWarmup { get; init; }
    public required decimal EmaFast { get; init; }
    public required decimal EmaSlow { get; init; }
    public required decimal EmaTrend { get; init; }
    public required decimal Rsi { get; init; }
    public required decimal Atr { get; init; }
    public required bool SetupDetected { get; init; }
    public required string GridLifecycleState { get; init; }
    public required decimal PositionSize { get; init; }
    public required decimal PositionAvgEntry { get; init; }
    public required IReadOnlyList<string> SignalsEmitted { get; init; }
    public string? GridCycleId { get; init; }
}
```

```csharp
// src/TradingApp.Application/Backtesting/Models/OrderEventEntry.cs — new file
namespace TradingApp.Application.Backtesting.Models;

/// <summary>
/// Order lifecycle event captured during backtest execution.
/// </summary>
public sealed record OrderEventEntry
{
    public required long TimestampUtc { get; init; }
    public required OrderEventType EventType { get; init; }
    public required string OrderId { get; init; }
    public required string Side { get; init; }
    public required string OrderType { get; init; }
    public required decimal Price { get; init; }
    public required decimal Size { get; init; }
    public decimal? FillPrice { get; init; }
    public decimal? Fee { get; init; }
    public bool? IsMaker { get; init; }
    public CancellationReason? CancellationReason { get; init; }
    public required string GridCycleId { get; init; }
}
```

```csharp
// src/TradingApp.Application/Backtesting/Models/GridCycleEntry.cs — new file
namespace TradingApp.Application.Backtesting.Models;

/// <summary>
/// Summary of a completed grid cycle for audit purposes.
/// </summary>
public sealed record GridCycleEntry
{
    public required string GridCycleId { get; init; }
    public required long DeployTimestampUtc { get; init; }
    public required decimal AnchorPrice { get; init; }
    public required int LevelsPlaced { get; init; }
    public required IReadOnlyList<decimal> LevelPrices { get; init; }
    public required int LevelsFilled { get; init; }
    public required decimal TakeProfitPrice { get; init; }
    public required decimal StopLossPrice { get; init; }
    public required string ExitReason { get; init; }
    public required decimal CyclePnl { get; init; }
    public required long CycleDurationMs { get; init; }
    public required long CloseTimestampUtc { get; init; }
}
```

##### Pattern References

- `src/TradingApp.Application/Backtesting/Models/BacktestTrade.cs` — sealed class with `required` init properties (same pattern)
- `src/TradingApp.Application/Backtesting/Models/EquitySnapshot.cs` — record pattern for small data carriers
- `src/TradingApp.Application/Trading/Models/IndicatorSnapshot.cs` — indicator fields to mirror

---

### Task 1.2: Create OrderEventType and CancellationReason enums {#task-12-create-ordereventtype-and-cancellationreason-enums}

Create two enums for the order event type classification and cancellation reason codes.

- **Complexity**: Low
- **Risk Factors**: None — straightforward enum definitions
- **Files**:
  - `src/TradingApp.Application/Backtesting/Models/OrderEventType.cs` — new file
  - `src/TradingApp.Application/Backtesting/Models/CancellationReason.cs` — new file
- **Success**:
  - Enums compile and are referenced by `OrderEventEntry`
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/Models/OrderEventType.cs — new file
namespace TradingApp.Application.Backtesting.Models;

public enum OrderEventType
{
    Placed,
    Filled,
    Cancelled,
    Replaced
}
```

```csharp
// src/TradingApp.Application/Backtesting/Models/CancellationReason.cs — new file
namespace TradingApp.Application.Backtesting.Models;

public enum CancellationReason
{
    GridRedeployed,
    PositionOpened,
    StopLossTriggered,
    ManualCancel
}
```

##### Pattern References

- `src/TradingApp.Application/Trading/Models/TradeType.cs` — existing enum pattern in same layer
- `src/TradingApp.Application/Trading/Models/OrderSide.cs` — existing enum pattern

---

### Task 1.3: Create IBacktestAuditCollector interface and implementations {#task-13-create-ibacktestauditcollector-interface-and-implementations}

Create the `IBacktestAuditCollector` interface and two implementations: `BacktestAuditCollector` (collects data in-memory) and `NullBacktestAuditCollector` (no-op, zero overhead). This is the core abstraction that allows the shared pipeline to log audit data without knowing whether audit is enabled.

- **Complexity**: Medium
- **Risk Factors**: Interface design must capture all three log types without coupling to specific pipeline internals
- **Files**:
  - `src/TradingApp.Application/Backtesting/Services/IBacktestAuditCollector.cs` — new file
  - `src/TradingApp.Application/Backtesting/Services/BacktestAuditCollector.cs` — new file
  - `src/TradingApp.Application/Backtesting/Services/NullBacktestAuditCollector.cs` — new file
- **Success**:
  - Interface defines methods for all three log types
  - `BacktestAuditCollector` stores entries in thread-safe lists and exposes results
  - `NullBacktestAuditCollector` is a true no-op (no allocations, no side effects)
- **Dependencies**: Task 1.1, Task 1.2

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/Services/IBacktestAuditCollector.cs — new file
using TradingApp.Application.Backtesting.Models;

namespace TradingApp.Application.Backtesting.Services;

/// <summary>
/// Collects audit/debug data during a backtest run.
/// Implementations: BacktestAuditCollector (active) and NullBacktestAuditCollector (disabled/live).
/// </summary>
public interface IBacktestAuditCollector
{
    void LogCandleEvaluation(CandleEvaluationEntry entry);
    void LogOrderEvent(OrderEventEntry entry);
    void LogGridCycleCompleted(GridCycleEntry entry);
}
```

```csharp
// src/TradingApp.Application/Backtesting/Services/BacktestAuditCollector.cs — new file
using TradingApp.Application.Backtesting.Models;

namespace TradingApp.Application.Backtesting.Services;

/// <summary>
/// Collects all audit log entries in-memory during a backtest run.
/// </summary>
public sealed class BacktestAuditCollector : IBacktestAuditCollector
{
    private readonly List<CandleEvaluationEntry> _candleEvaluations = [];
    private readonly List<OrderEventEntry> _orderEvents = [];
    private readonly List<GridCycleEntry> _gridCycles = [];

    public void LogCandleEvaluation(CandleEvaluationEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _candleEvaluations.Add(entry);
    }

    public void LogOrderEvent(OrderEventEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _orderEvents.Add(entry);
    }

    public void LogGridCycleCompleted(GridCycleEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _gridCycles.Add(entry);
    }

    public IReadOnlyList<CandleEvaluationEntry> CandleEvaluations => _candleEvaluations;
    public IReadOnlyList<OrderEventEntry> OrderEvents => _orderEvents;
    public IReadOnlyList<GridCycleEntry> GridCycles => _gridCycles;
}
```

```csharp
// src/TradingApp.Application/Backtesting/Services/NullBacktestAuditCollector.cs — new file
using TradingApp.Application.Backtesting.Models;

namespace TradingApp.Application.Backtesting.Services;

/// <summary>
/// No-op audit collector used when audit logging is disabled or in live trading.
/// Zero overhead — no allocations, no side effects.
/// </summary>
public sealed class NullBacktestAuditCollector : IBacktestAuditCollector
{
    public static readonly NullBacktestAuditCollector Instance = new();

    public void LogCandleEvaluation(CandleEvaluationEntry entry) { }
    public void LogOrderEvent(OrderEventEntry entry) { }
    public void LogGridCycleCompleted(GridCycleEntry entry) { }
}
```

##### Pattern References

- Null object pattern following `NullLogger<T>` from Microsoft.Extensions.Logging
- `src/TradingApp.Application/Backtesting/BacktestExecutionContextAccessor.cs` — similar accessor pattern for sharing state across the pipeline

---

### Task 1.4: Add EnableAuditLog to BacktestConfig {#task-14-add-enableauditlog-to-backtestconfig}

Add the `EnableAuditLog` boolean property to `BacktestConfig` with a default of `true`.

- **Complexity**: Low
- **Risk Factors**: None — additive change with default preserving existing behavior
- **Files**:
  - `src/TradingApp.Application/Backtesting/Models/BacktestConfig.cs` — modification
- **Success**:
  - `BacktestConfig.EnableAuditLog` exists and defaults to `true`
  - Existing code that creates `BacktestConfig` instances continues to work (default value)
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/Models/BacktestConfig.cs — modification
// Add after the existing StrategyConfigJson property:

    public required string StrategyConfigJson { get; init; }
    public bool EnableAuditLog { get; init; } = true;
}
```

##### Pattern References

- `src/TradingApp.Application/Backtesting/Models/BacktestConfig.cs` — existing property patterns (non-required with default for `WarmupPeriod`)

---

### Task 1.5: Unit tests for BacktestAuditCollector {#task-15-unit-tests-for-backtestauditcollector}

Write unit tests verifying `BacktestAuditCollector` correctly accumulates entries and `NullBacktestAuditCollector` is a safe no-op.

- **Complexity**: Medium
- **Risk Factors**: None — pure unit tests
- **Files**:
  - `tests/TradingApp.Application.Tests/Backtesting/Services/BacktestAuditCollectorTests.cs` — new file
- **Success**:
  - Tests cover: adding entries to each log type, entries accessible via read-only properties, null entry rejection
  - Tests cover: `NullBacktestAuditCollector` does not throw
  - All tests pass: `dotnet test tests/TradingApp.Application.Tests --filter "FullyQualifiedName~BacktestAuditCollectorTests"`
- **Dependencies**: Task 1.1, Task 1.2, Task 1.3

#### Implementation Details

```csharp
// tests/TradingApp.Application.Tests/Backtesting/Services/BacktestAuditCollectorTests.cs — new file
using FluentAssertions;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Backtesting.Services;

namespace TradingApp.Application.Tests.Backtesting.Services;

[TestClass]
public sealed class BacktestAuditCollectorTests
{
    private BacktestAuditCollector _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new BacktestAuditCollector();
    }

    [TestMethod]
    public void GivenCandleEntry_WhenLogCandleEvaluation_ThenEntryIsStored()
    {
        var entry = CreateCandleEntry();

        _sut.LogCandleEvaluation(entry);

        _sut.CandleEvaluations.Should().ContainSingle().Which.Should().Be(entry);
    }

    [TestMethod]
    public void GivenOrderEventEntry_WhenLogOrderEvent_ThenEntryIsStored()
    {
        var entry = CreateOrderEventEntry();

        _sut.LogOrderEvent(entry);

        _sut.OrderEvents.Should().ContainSingle().Which.Should().Be(entry);
    }

    [TestMethod]
    public void GivenGridCycleEntry_WhenLogGridCycleCompleted_ThenEntryIsStored()
    {
        var entry = CreateGridCycleEntry();

        _sut.LogGridCycleCompleted(entry);

        _sut.GridCycles.Should().ContainSingle().Which.Should().Be(entry);
    }

    [TestMethod]
    public void GivenMultipleEntries_WhenLogged_ThenAllEntriesArePreservedInOrder()
    {
        var entry1 = CreateCandleEntry(timestampUtc: 1000);
        var entry2 = CreateCandleEntry(timestampUtc: 2000);

        _sut.LogCandleEvaluation(entry1);
        _sut.LogCandleEvaluation(entry2);

        _sut.CandleEvaluations.Should().HaveCount(2);
        _sut.CandleEvaluations[0].TimestampUtc.Should().Be(1000);
        _sut.CandleEvaluations[1].TimestampUtc.Should().Be(2000);
    }

    [TestMethod]
    public void GivenNullEntry_WhenLogCandleEvaluation_ThenThrowsArgumentNullException()
    {
        var act = () => _sut.LogCandleEvaluation(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void GivenNullEntry_WhenLogOrderEvent_ThenThrowsArgumentNullException()
    {
        var act = () => _sut.LogOrderEvent(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void GivenNullEntry_WhenLogGridCycleCompleted_ThenThrowsArgumentNullException()
    {
        var act = () => _sut.LogGridCycleCompleted(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void GivenNullCollector_WhenLogCandleEvaluation_ThenDoesNotThrow()
    {
        var act = () => NullBacktestAuditCollector.Instance.LogCandleEvaluation(CreateCandleEntry());

        act.Should().NotThrow();
    }

    [TestMethod]
    public void GivenNullCollector_WhenLogOrderEvent_ThenDoesNotThrow()
    {
        var act = () => NullBacktestAuditCollector.Instance.LogOrderEvent(CreateOrderEventEntry());

        act.Should().NotThrow();
    }

    [TestMethod]
    public void GivenNullCollector_WhenLogGridCycleCompleted_ThenDoesNotThrow()
    {
        var act = () => NullBacktestAuditCollector.Instance.LogGridCycleCompleted(CreateGridCycleEntry());

        act.Should().NotThrow();
    }

    private static CandleEvaluationEntry CreateCandleEntry(long timestampUtc = 1000) => new()
    {
        TimestampUtc = timestampUtc,
        Open = 100m, High = 105m, Low = 95m, Close = 102m, Volume = 500m,
        IsWarmup = false,
        EmaFast = 101m, EmaSlow = 100m, EmaTrend = 99m, Rsi = 55m, Atr = 2.5m,
        SetupDetected = true,
        GridLifecycleState = "Active",
        PositionSize = 0.5m, PositionAvgEntry = 100m,
        SignalsEmitted = [],
        GridCycleId = "abc123"
    };

    private static OrderEventEntry CreateOrderEventEntry() => new()
    {
        TimestampUtc = 1000,
        EventType = OrderEventType.Placed,
        OrderId = "order-1",
        Side = "Buy", OrderType = "Limit",
        Price = 100m, Size = 0.1m,
        GridCycleId = "abc123"
    };

    private static GridCycleEntry CreateGridCycleEntry() => new()
    {
        GridCycleId = "abc123",
        DeployTimestampUtc = 1000,
        AnchorPrice = 100m,
        LevelsPlaced = 5, LevelPrices = [99m, 98m, 97m, 96m, 95m],
        LevelsFilled = 2,
        TakeProfitPrice = 102m, StopLossPrice = 94m,
        ExitReason = "TakeProfit",
        CyclePnl = 5.5m, CycleDurationMs = 3600000,
        CloseTimestampUtc = 4600000
    };
}
```

##### Pattern References

- `tests/TradingApp.Application.Tests/Backtesting/Services/BacktestMetricsCalculatorTests.cs` — pure unit tests with no mocks, private factory helpers

## Phase Success Criteria

- All 5 new model files compile without errors
- `IBacktestAuditCollector`, `BacktestAuditCollector`, and `NullBacktestAuditCollector` exist with correct interface implementations
- `BacktestConfig.EnableAuditLog` defaults to `true`
- All unit tests pass: `dotnet test tests/TradingApp.Application.Tests --filter "FullyQualifiedName~BacktestAuditCollectorTests"`
