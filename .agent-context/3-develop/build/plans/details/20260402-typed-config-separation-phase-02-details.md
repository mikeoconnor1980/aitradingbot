<!-- markdownlint-disable-file -->

# Task Details: F0 — Typed Config & Execution Separation

## Phase 2: Core Pipeline Refactoring

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — sealed classes, async patterns, CancellationToken
- `.github/instructions/testing.instructions.md` — MSTest, Moq, FluentAssertions, Given_When_Then naming, Builder pattern
- `.agent-context/0-knowledge/14-strategy-runtime-model.md` — strategy execution pipeline
- `.agent-context/0-knowledge/15-grid-controller.md` — grid lifecycle state machine
- `.agent-context/0-knowledge/18-backtesting-architecture.md` — BacktestRunner, BacktestConfig, replay engine
- `.agent-context/0-knowledge/19-scheduling-architecture.md` — StrategyScheduler, per-candle fan-out

## Design References

- Interfaces change from `string strategyConfigJson` → `IStrategyConfig strategyConfig`
- Implementations cast `IStrategyConfig` → `GridStrategyConfig` (Domain) with a guard
- `BacktestConfig` replaces `FeeModel` + `StrategyConfigJson` with `IStrategyConfig Strategy` + `ExecutionConfig Execution`
- `BacktestProcessorService.BuildConfig` temporarily uses old `Application.GridStrategyConfig` for backward-compatible deserialization from single JSON column (cleaned up in Phase 3)
- `StrategyScheduler` stores typed `IStrategyConfig` field instead of raw JSON string

---

### Task 2.1: Refactor IStrategyEngine and GridStrategyEngine {#task-21-refactor-istrategyengine-and-gridstrategyengine}

Change `IStrategyEngine.EvaluateAsync` to accept `IStrategyConfig` instead of `string strategyConfigJson`. Update `GridStrategyEngine` to receive typed config, remove JSON deserialization.

- **Complexity**: Medium
- **Risk Factors**: All callers must be updated (StrategyScheduler — Task 2.3)
- **Files**:
  - `src/TradingApp.Application/Abstractions/Services/IStrategyEngine.cs` — modify signature
  - `src/TradingApp.Application/Trading/Services/GridStrategyEngine.cs` — remove JSON deserialization, cast to typed config
- **Success**:
  - `EvaluateAsync` accepts `IStrategyConfig strategyConfig` parameter
  - No `JsonSerializer.Deserialize` in `GridStrategyEngine`
  - `GridStrategyEngine` casts `IStrategyConfig` to `Domain.GridStrategyConfig`
- **Dependencies**: Phase 1 complete

#### Implementation Details

```csharp
// src/TradingApp.Application/Abstractions/Services/IStrategyEngine.cs — modification
using TradingApp.Domain.Trading;

namespace TradingApp.Application.Abstractions.Services;

public interface IStrategyEngine
{
    Task<StrategyEvaluation> EvaluateAsync(
        MarketContext context,
        IStrategyConfig strategyConfig,  // was: string strategyConfigJson
        CancellationToken cancellationToken = default);
}
```

```csharp
// src/TradingApp.Application/Trading/Services/GridStrategyEngine.cs — modification
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Trading;

namespace TradingApp.Application.Trading.Services;

public sealed class GridStrategyEngine : IStrategyEngine
{
    // Remove: private static readonly JsonSerializerOptions JsonOptions = ...;

    public Task<StrategyEvaluation> EvaluateAsync(
        MarketContext context,
        IStrategyConfig strategyConfig,  // was: string strategyConfigJson
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(strategyConfig);

        if (strategyConfig is not GridStrategyConfig config)
        {
            throw new ArgumentException(
                $"Expected {nameof(GridStrategyConfig)} but received {strategyConfig.GetType().Name}.",
                nameof(strategyConfig));
        }

        if (config.GridLevels <= 0 || config.GridSpacing <= 0m || config.PositionSize <= 0m)
        {
            return Task.FromResult(new StrategyEvaluation
            {
                SetupDetected = false,
                Reason = "Grid configuration is incomplete."
            });
        }

        if (context.LatestOneHourCandle is null || context.LatestFourHourCandle is null)
        {
            return Task.FromResult(new StrategyEvaluation
            {
                SetupDetected = false,
                Reason = "Higher timeframe context is not available yet."
            });
        }

        return Task.FromResult(new StrategyEvaluation
        {
            SetupDetected = true,
            Reason = "Grid setup available."
        });
    }
}
```

Remove `using System.Text.Json;` and the `JsonOptions` static field.

##### Pattern References

Based on existing `src/TradingApp.Application/Trading/Services/GridStrategyEngine.cs` — same logic, JSON deserialization replaced with typed cast.

---

### Task 2.2: Refactor IGridController and GridController {#task-22-refactor-igridcontroller-and-gridcontroller}

Change `IGridController.ProcessAsync` to accept `IStrategyConfig` instead of `string strategyConfigJson`. Update `GridController` to receive typed config, remove JSON deserialization.

- **Complexity**: Medium
- **Risk Factors**: GridController is the largest file — careful replacement needed
- **Files**:
  - `src/TradingApp.Application/Abstractions/Services/IGridController.cs` — modify signature
  - `src/TradingApp.Application/Trading/Services/GridController.cs` — remove JSON deserialization, cast to typed config
- **Success**:
  - `ProcessAsync` accepts `IStrategyConfig strategyConfig` parameter
  - No `JsonSerializer.Deserialize` in `GridController`
  - `GridController` casts `IStrategyConfig` to `Domain.GridStrategyConfig`
  - All signal generation logic unchanged
- **Dependencies**: Phase 1 complete

#### Implementation Details

```csharp
// src/TradingApp.Application/Abstractions/Services/IGridController.cs — modification
using TradingApp.Domain.Trading;

namespace TradingApp.Application.Abstractions.Services;

public interface IGridController
{
    Task<IReadOnlyList<TradingSignal>> ProcessAsync(
        StrategyEvaluation evaluation,
        MarketContext context,
        GridState gridState,
        PositionState positionState,
        IStrategyConfig strategyConfig,  // was: string strategyConfigJson
        CancellationToken cancellationToken = default);
}
```

```csharp
// src/TradingApp.Application/Trading/Services/GridController.cs — modification
// Replace the beginning of ProcessAsync:
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Trading;

namespace TradingApp.Application.Trading.Services;

public sealed class GridController : IGridController
{
    // Remove: private static readonly JsonSerializerOptions JsonOptions = ...;

    public Task<IReadOnlyList<TradingSignal>> ProcessAsync(
        StrategyEvaluation evaluation,
        MarketContext context,
        GridState gridState,
        PositionState positionState,
        IStrategyConfig strategyConfig,  // was: string strategyConfigJson
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(evaluation);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(gridState);
        ArgumentNullException.ThrowIfNull(positionState);
        ArgumentNullException.ThrowIfNull(strategyConfig);

        if (strategyConfig is not GridStrategyConfig config)
        {
            throw new ArgumentException(
                $"Expected {nameof(GridStrategyConfig)} but received {strategyConfig.GetType().Name}.",
                nameof(strategyConfig));
        }

        // ... rest of method unchanged — all config.XYZ references still work
        // because Domain.GridStrategyConfig has the same property names
    }
}
```

Remove `using System.Text.Json;` and the `JsonOptions` static field. Remove the old `JsonSerializer.Deserialize<GridStrategyConfig>` call and the `ArgumentException.ThrowIfNullOrWhiteSpace(strategyConfigJson)` guard.

**Important**: All `config.StopLossPercent`, `config.TakeProfitPercent`, `config.GridLevels`, `config.GridSpacing`, `config.PositionSize`, `config.EntryMode`, `config.ManualAnchorPrice` references remain unchanged — same property names on the new Domain record.

##### Pattern References

Based on existing `src/TradingApp.Application/Trading/Services/GridController.cs` — same logic, JSON deserialization replaced with typed cast.

---

### Task 2.3: Refactor StrategyScheduler {#task-23-refactor-strategyscheduler}

Change `StrategyScheduler` constructor to accept `IStrategyConfig` instead of `string strategyConfigJson`. Update field type and all call sites.

- **Complexity**: Medium
- **Risk Factors**: Constructor change affects BacktestRunner (Task 2.5)
- **Files**:
  - `src/TradingApp.Application/Scheduling/StrategyScheduler.cs` — modify constructor and field
- **Success**:
  - Constructor accepts `IStrategyConfig strategyConfig`
  - `_strategyConfig` field is `IStrategyConfig` instead of `string`
  - `HandleCandleClosedAsync` passes typed config to `EvaluateAsync` and `ProcessAsync`
- **Dependencies**: Tasks 2.1, 2.2

#### Implementation Details

```csharp
// src/TradingApp.Application/Scheduling/StrategyScheduler.cs — modification
using TradingApp.Domain.Trading;

// Change field:
private readonly IStrategyConfig _strategyConfig;  // was: string _strategyConfigJson

// Change constructor parameter:
public StrategyScheduler(
    IMarketContextBuilder contextBuilder,
    IStrategyEngine strategyEngine,
    IGridController gridController,
    IRiskEngine riskEngine,
    IPositionManager positionManager,
    IStrategyConfig strategyConfig,          // was: string strategyConfigJson
    string triggerTimeframe = "15m",
    IBacktestAuditCollector? auditCollector = null)
{
    // ... existing null checks ...
    ArgumentNullException.ThrowIfNull(strategyConfig);  // was: ArgumentException.ThrowIfNullOrWhiteSpace(strategyConfigJson)
    // ... existing assignments ...
    _strategyConfig = strategyConfig;  // was: _strategyConfigJson = strategyConfigJson
    // ...
}

// In HandleCandleClosedAsync:
var evaluation = await _strategyEngine.EvaluateAsync(
    context,
    _strategyConfig,         // was: _strategyConfigJson
    cancellationToken);

var signals = await _gridController.ProcessAsync(
    evaluation,
    context,
    _gridState,
    _positionState,
    _strategyConfig,         // was: _strategyConfigJson
    cancellationToken);
```

##### Pattern References

Based on existing `src/TradingApp.Application/Scheduling/StrategyScheduler.cs` — same structure, string field replaced with typed interface field.

---

### Task 2.4: Refactor BacktestConfig {#task-24-refactor-backtestconfig}

Replace `FeeModel FeeModel` + `string StrategyConfigJson` on `BacktestConfig` with `IStrategyConfig Strategy` + `ExecutionConfig Execution`.

- **Complexity**: Medium
- **Risk Factors**: All consumers of `BacktestConfig` must be updated (Tasks 2.5, 2.6)
- **Files**:
  - `src/TradingApp.Application/Backtesting/Models/BacktestConfig.cs` — modify properties
- **Success**:
  - `BacktestConfig` has `IStrategyConfig Strategy` and `ExecutionConfig Execution`
  - No `FeeModel FeeModel` property
  - No `string StrategyConfigJson` property
  - `InitialCapital` stays on `BacktestConfig` (backtest-specific)
- **Dependencies**: Phase 1 complete

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/Models/BacktestConfig.cs — modification
using TradingApp.Domain.Trading;

namespace TradingApp.Application.Backtesting.Models;

public sealed class BacktestConfig
{
    public required string Symbol { get; init; }
    public required IReadOnlyList<string> Intervals { get; init; }
    public required long StartDateUtc { get; init; }
    public required long EndDateUtc { get; init; }
    public required decimal InitialCapital { get; init; }
    public required IStrategyConfig Strategy { get; init; }       // was: string StrategyConfigJson
    public required ExecutionConfig Execution { get; init; }      // was: FeeModel FeeModel
    public int WarmupPeriod { get; init; } = 200;
    public bool EnableAuditLog { get; init; } = true;
}
```

##### Pattern References

Based on existing `src/TradingApp.Application/Backtesting/Models/BacktestConfig.cs`.

---

### Task 2.5: Refactor BacktestRunner {#task-25-refactor-backtestrunner}

Update `BacktestRunner` to pass typed config through the pipeline: `config.Strategy` to `StrategyScheduler`, `config.Execution.FeeModel` to `SimulatedExecutionEngine`. Update `ValidateConfig`.

- **Complexity**: Medium
- **Risk Factors**: Must correctly wire both `Strategy` and `Execution` to their consumers
- **Files**:
  - `src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs` — modify `RunAsync` and `ValidateConfig`
- **Success**:
  - `StrategyScheduler` constructed with `config.Strategy` (typed)
  - `SimulatedExecutionEngine` constructed with `config.Execution.FeeModel`
  - `ValidateConfig` checks `config.Strategy is not null` and `config.Execution is not null` instead of JSON string check
- **Dependencies**: Tasks 2.3, 2.4

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs — modification

// In RunAsync method, change the StrategyScheduler construction:
var executionEngine = new SimulatedExecutionEngine(config.Execution.FeeModel);  // was: config.FeeModel
// ... existing code ...
var scheduler = new StrategyScheduler(
    _marketContextBuilder,
    _strategyEngine,
    _gridController,
    _riskEngine,
    positionManager,
    config.Strategy,               // was: config.StrategyConfigJson
    auditCollector: collector);

// Update ValidateConfig:
private static void ValidateConfig(BacktestConfig config)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(config.Symbol, nameof(config.Symbol));
    ArgumentNullException.ThrowIfNull(config.Strategy);     // was: config.FeeModel
    ArgumentNullException.ThrowIfNull(config.Execution);    // new
    ArgumentNullException.ThrowIfNull(config.Execution.FeeModel);  // was at top level

    if (config.StartDateUtc >= config.EndDateUtc)
    {
        throw new ArgumentException("Start date must be before end date.");
    }

    if (config.InitialCapital <= 0)
    {
        throw new ArgumentException("Initial capital must be greater than zero.");
    }

    if (config.Intervals is null || config.Intervals.Count == 0)
    {
        throw new ArgumentException("At least one interval must be specified.");
    }

    EnsureRequiredInterval(config.Intervals, "15m");
    EnsureRequiredInterval(config.Intervals, "1h");
    EnsureRequiredInterval(config.Intervals, "4h");

    // Remove: ArgumentException.ThrowIfNullOrWhiteSpace(config.StrategyConfigJson, ...);
}
```

##### Pattern References

Based on existing `src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs` lines 50–82 and 259–285.

---

### Task 2.6: Refactor BacktestProcessorService.BuildConfig (temporary bridge) {#task-26-refactor-backtestprocessorservice-buildconfig}

Update `BuildConfig` to construct the new `BacktestConfig` shape. **Temporarily** deserializes from the old single JSON column using the old `Application.GridStrategyConfig` class to extract both strategy and execution params. This bridge pattern is replaced in Phase 3 when storage splits into two columns.

- **Complexity**: Medium
- **Risk Factors**: Must correctly split the old combined config into two typed objects
- **Files**:
  - `src/TradingApp.Api/Services/BacktestProcessorService.cs` — modify `BuildConfig`
- **Success**:
  - `BuildConfig` returns `BacktestConfig` with typed `Strategy` and `Execution` properties
  - Fee duplication eliminated — fees come only from `ExecutionConfig.FeeModel`
  - Backtest results identical to before
- **Dependencies**: Task 2.4

#### Implementation Details

```csharp
// src/TradingApp.Api/Services/BacktestProcessorService.cs — modification
// Temporary bridge: still reads from single JSON column using old Application.GridStrategyConfig
using AppGridConfig = TradingApp.Application.Backtesting.Models.GridStrategyConfig;  // old, temporary
using TradingApp.Domain.Trading;

private static BacktestConfig BuildConfig(BacktestRun run)
{
    var oldConfig = JsonSerializer.Deserialize<AppGridConfig>(
        run.StrategyConfigJson,
        JsonOptions)
        ?? throw new InvalidOperationException("Failed to deserialize strategy config.");

    return new BacktestConfig
    {
        Symbol = run.Symbol,
        Intervals = JsonSerializer.Deserialize<string[]>(
            run.IntervalsJson,
            JsonOptions) ?? [],
        StartDateUtc = run.StartDateUtc,
        EndDateUtc = run.EndDateUtc,
        InitialCapital = run.InitialCapital,
        Strategy = new Domain.Trading.GridStrategyConfig
        {
            GridLevels = oldConfig.GridLevels,
            GridSpacing = oldConfig.GridSpacing,
            TakeProfitPercent = oldConfig.TakeProfitPercent,
            StopLossPercent = oldConfig.StopLossPercent,
            BreakdownThreshold = oldConfig.BreakdownThreshold,
            EntryMode = oldConfig.EntryMode,
            ManualAnchorPrice = oldConfig.ManualAnchorPrice,
            PositionSize = oldConfig.PositionSize,
        },
        Execution = new ExecutionConfig
        {
            FeeModel = new FeeModel
            {
                MakerFeeRate = oldConfig.MakerFee,
                TakerFeeRate = oldConfig.TakerFee,
                SlippageRate = oldConfig.Slippage,
            },
            Leverage = oldConfig.Leverage,
        },
        EnableAuditLog = run.AuditLogEnabled,
    };
}
```

##### Pattern References

Based on existing `src/TradingApp.Api/Services/BacktestProcessorService.cs` lines 172–195 — same structure, output shape changed.

---

### Task 2.7: Update pipeline tests {#task-27-update-pipeline-tests}

Update all tests that reference changed signatures: `GridControllerTests`, `StrategySchedulerTests`, `BacktestRunnerTests`, `RealBacktestRunnerTests`, `CandleReplayEngineTests`.

- **Complexity**: High
- **Risk Factors**: Many test files with inline JSON strings and mock setups need updating; `RealBacktestRunnerTests` has 6 tests each with unique inline JSON
- **Files**:
  - `tests/TradingApp.Application.Tests/Trading/Services/GridControllerTests.cs` — replace `DefaultConfigJson` constant with typed `GridStrategyConfig` object
  - `tests/TradingApp.Application.Tests/Scheduling/StrategySchedulerTests.cs` — replace `"{}"` ctor arg with typed config; update mock setups/verifies
  - `tests/TradingApp.Application.Tests/Backtesting/Services/BacktestRunnerTests.cs` — update `CreateConfig` helper to use new `BacktestConfig` shape
  - `tests/TradingApp.Application.Tests/Backtesting/Services/RealBacktestRunnerTests.cs` — replace inline JSON with typed object construction
  - `tests/TradingApp.Application.Tests/Backtesting/Services/CandleReplayEngineTests.cs` — update `CreateConfig` helper
- **Success**:
  - All pipeline tests compile and pass
  - No raw JSON strings used for strategy config in tests
  - Mock setups/verifies use `IStrategyConfig` parameter type
- **Dependencies**: Tasks 2.1–2.6

#### Implementation Details

**GridControllerTests** — replace `DefaultConfigJson` constant:

```csharp
// tests/TradingApp.Application.Tests/Trading/Services/GridControllerTests.cs
using TradingApp.Domain.Trading;

// Replace: private const string DefaultConfigJson = """{"gridLevels":5,...}""";
// With:
private static readonly GridStrategyConfig DefaultConfig = new()
{
    GridLevels = 5,
    GridSpacing = 0.5m,
    TakeProfitPercent = 2.0m,
    StopLossPercent = 5.0m,
    BreakdownThreshold = 3.0m,
    PositionSize = 100m,
};

// Update all ProcessAsync calls:
// was: _sut.ProcessAsync(..., DefaultConfigJson)
// now: _sut.ProcessAsync(..., DefaultConfig)
```

**StrategySchedulerTests** — replace raw JSON constructor arg:

```csharp
// tests/TradingApp.Application.Tests/Scheduling/StrategySchedulerTests.cs
using TradingApp.Domain.Trading;

// Replace constructor arg "{}" with:
private static readonly GridStrategyConfig TestConfig = new()
{
    GridLevels = 5,
    GridSpacing = 0.5m,
    TakeProfitPercent = 2.0m,
    StopLossPercent = 5.0m,
    BreakdownThreshold = 3.0m,
    PositionSize = 100m,
};

_sut = new StrategyScheduler(
    _contextBuilderMock.Object,
    _strategyEngineMock.Object,
    _gridControllerMock.Object,
    _riskEngineMock.Object,
    _positionManagerMock.Object,
    TestConfig);   // was: "{}"

// Update mock Verify calls to match on IStrategyConfig:
// was: .Verify(e => e.EvaluateAsync(It.IsAny<MarketContext>(), "{}", ...))
// now: .Verify(e => e.EvaluateAsync(It.IsAny<MarketContext>(), TestConfig, ...))
```

**BacktestRunnerTests** — update `CreateConfig` helper:

```csharp
// tests/TradingApp.Application.Tests/Backtesting/Services/BacktestRunnerTests.cs
using TradingApp.Domain.Trading;

private static BacktestConfig CreateConfig(
    string symbol = "BTC",
    // ... other params ...
    IStrategyConfig? strategy = null,
    ExecutionConfig? execution = null)
{
    return new BacktestConfig
    {
        Symbol = symbol,
        // ... other properties ...
        Strategy = strategy ?? new GridStrategyConfig
        {
            GridLevels = 5,
            GridSpacing = 0.5m,
            TakeProfitPercent = 2.0m,
            StopLossPercent = 5.0m,
            BreakdownThreshold = 3.0m,
            PositionSize = 100m,
        },
        Execution = execution ?? new ExecutionConfig
        {
            FeeModel = new FeeModel(),
            Leverage = 1m,
        },
    };
}

// The test for invalid JSON (GivenInvalidStrategyConfigJson_WhenRunAsync_ThenThrowsArgumentException)
// becomes obsolete — JSON parsing no longer happens in the runner.
// Replace with a test for null strategy config:
// GivenNullStrategyConfig_WhenRunAsync_ThenThrowsArgumentNullException
```

**RealBacktestRunnerTests** — replace inline JSON with typed objects:

```csharp
// tests/TradingApp.Application.Tests/Backtesting/Services/RealBacktestRunnerTests.cs
// For each test, replace the inline JSON string:
// was: StrategyConfigJson = "{\"gridLevels\":1,\"gridSpacing\":0.5,...}"
// now:
Strategy = new GridStrategyConfig
{
    GridLevels = 1,
    GridSpacing = 0.5m,
    TakeProfitPercent = 2.0m,
    StopLossPercent = 5.0m,
    BreakdownThreshold = 3.0m,
    PositionSize = 100m,
},
Execution = new ExecutionConfig
{
    FeeModel = new FeeModel
    {
        MakerFeeRate = 0.0001m,
        TakerFeeRate = 0.00035m,
        SlippageRate = 0.0005m,
    },
    Leverage = 1m,
},
// Repeat for each test, preserving the unique config values from each inline JSON
```

**CandleReplayEngineTests** — update `CreateConfig`:

```csharp
// Same pattern as BacktestRunnerTests — replace StrategyConfigJson = "{}" with:
Strategy = new GridStrategyConfig { GridLevels = 5, GridSpacing = 0.5m, PositionSize = 100m },
Execution = new ExecutionConfig(),
```

##### Pattern References

Based on existing test files in `tests/TradingApp.Application.Tests/`.

---

### Task 2.8: Run build and all tests {#task-28-run-build-and-tests}

Verify the solution builds and all tests pass after the pipeline refactoring.

- **Complexity**: Low
- **Risk Factors**: Backtest results must be identical — if any test produces different numeric results, the config mapping is incorrect
- **Files**: None (verification step)
- **Success**:
  - `dotnet build TradingApp.sln` succeeds
  - `dotnet test TradingApp.sln` — all tests pass
  - `RealBacktestRunnerTests` produce identical results (same PnL, trades, etc.)
- **Dependencies**: Task 2.7

## Phase Success Criteria

- `IStrategyEngine.EvaluateAsync` accepts `IStrategyConfig` not `string`
- `IGridController.ProcessAsync` accepts `IStrategyConfig` not `string`
- No `JsonSerializer.Deserialize<GridStrategyConfig>` in `GridStrategyEngine` or `GridController`
- `BacktestConfig` has `IStrategyConfig Strategy` + `ExecutionConfig Execution`
- `StrategyScheduler` stores `IStrategyConfig` and passes it typed
- All pipeline tests pass with typed config objects
- `BacktestProcessorService.BuildConfig` correctly bridges old JSON → new typed objects
