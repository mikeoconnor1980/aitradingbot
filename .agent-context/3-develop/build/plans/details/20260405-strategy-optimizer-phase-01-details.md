<!-- markdownlint-disable-file -->

# Task Details: Strategy Optimizer — Phase 1: Domain Model & Sweep Engine

## Phase 1: Backend — Domain Model & Sweep Engine

## Standards and Knowledge References

- **csharp.instructions.md**: `sealed` classes, private constructors with factory methods, `_camelCase` private fields
- **testing.instructions.md**: MSTest, FluentAssertions ≤ v6, Moq, `Given_When_Then` naming
- **dotnet-architecture.instructions.md**: Domain entities with private setters, Application layer services
- **18-backtesting-architecture.md**: `BacktestRunner.RunAsync` in-memory execution
- **01-trading-strategy.md**: Grid strategy parameters, indicator settings
- **StrategyConfig**: `StrategyMode.Signal`, `EntryConditions`, `EntryLogic`, `TrendFilter`, `Exit`, `Risk`
- **EntryConditionConfig**: `RsiParams`, `MacdParams`, `PriceVsEmaParams` with typed operators
- **BacktestResult**: `TotalPnL`, `WinRate`, `MaxDrawdownAbsolute`, `MaxDrawdownPercent`, `TotalTrades`, `TotalFeesPaid`, etc.

---

### Task 1.1: Create `OptimizationRun` domain entity {#task-11-create-optimizationrun-domain-entity}

Create the domain entity that persists an optimization run (one sweep execution).

- **Complexity**: Medium
- **Risk Factors**: Must follow BacktestRun pattern — factory method, private constructor, private setters
- **Files**:
  - `src/TradingApp.Domain/Entities/OptimizationRun.cs` — new file
  - `src/TradingApp.Domain/Enums/OptimizationStatus.cs` — new file
- **Success**:
  - Entity compiles with all required properties
  - Factory method `CreateQueued(...)` validates required inputs
  - Mutation methods: `MarkRunning()`, `MarkCompleted(...)`, `MarkFailed(string errorMessage)`, `UpdateProgress(int completed, int total)`

#### Implementation Details

```csharp
// src/TradingApp.Domain/Enums/OptimizationStatus.cs
namespace TradingApp.Domain.Enums;

public enum OptimizationStatus
{
    Queued,
    Running,
    Completed,
    Failed
}
```

```csharp
// src/TradingApp.Domain/Entities/OptimizationRun.cs
using TradingApp.Domain.Enums;

namespace TradingApp.Domain.Entities;

public sealed class OptimizationRun
{
    public Guid Id { get; private set; }
    public string Symbol { get; private set; } = string.Empty;
    public long StartDateUtc { get; private set; }
    public long EndDateUtc { get; private set; }
    public decimal InitialCapital { get; private set; }
    public string SweepConfigJson { get; private set; } = string.Empty;
    public string ThresholdsJson { get; private set; } = string.Empty;
    public int TotalCombinations { get; private set; }
    public int CompletedCount { get; private set; }
    public int QualifiedCount { get; private set; }
    public OptimizationStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public long ElapsedMs { get; private set; }
    public long CreatedAtUtc { get; private set; }

    private OptimizationRun() { }

    public static OptimizationRun CreateQueued(
        string symbol,
        long startDateUtc,
        long endDateUtc,
        decimal initialCapital,
        string sweepConfigJson,
        string thresholdsJson,
        int totalCombinations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(sweepConfigJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(thresholdsJson);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialCapital);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(startDateUtc, endDateUtc);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(totalCombinations);

        return new OptimizationRun
        {
            Id = Guid.NewGuid(),
            Symbol = symbol,
            StartDateUtc = startDateUtc,
            EndDateUtc = endDateUtc,
            InitialCapital = initialCapital,
            SweepConfigJson = sweepConfigJson,
            ThresholdsJson = thresholdsJson,
            TotalCombinations = totalCombinations,
            CompletedCount = 0,
            QualifiedCount = 0,
            Status = OptimizationStatus.Queued,
            CreatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }

    public void MarkRunning()
    {
        Status = OptimizationStatus.Running;
    }

    public void UpdateProgress(int completed, int total)
    {
        CompletedCount = completed;
        TotalCombinations = total;
    }

    public void MarkCompleted(int qualifiedCount, long elapsedMs)
    {
        Status = OptimizationStatus.Completed;
        QualifiedCount = qualifiedCount;
        ElapsedMs = elapsedMs;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = OptimizationStatus.Failed;
        ErrorMessage = errorMessage;
    }
}
```

---

### Task 1.2: Create `OptimizationResult` domain entity {#task-12-create-optimizationresult-domain-entity}

Create the entity for storing individual ranked results within an optimization run (top 10).

- **Complexity**: Low
- **Risk Factors**: None — straightforward data entity
- **Files**:
  - `src/TradingApp.Domain/Entities/OptimizationResult.cs` — new file
- **Success**:
  - Entity compiles with all required properties
  - Factory method `Create(...)` validates inputs
  - Stores `StrategyConfigJson` for round-trip strategy promotion

#### Implementation Details

```csharp
// src/TradingApp.Domain/Entities/OptimizationResult.cs
namespace TradingApp.Domain.Entities;

public sealed class OptimizationResult
{
    public Guid Id { get; private set; }
    public Guid OptimizationRunId { get; private set; }
    public int Rank { get; private set; }
    public decimal FitnessScore { get; private set; }
    public string StrategyConfigJson { get; private set; } = string.Empty;
    public string SignalDescription { get; private set; } = string.Empty;

    // Metrics snapshot
    public decimal TotalPnl { get; private set; }
    public decimal WinRate { get; private set; }
    public decimal MaxDrawdown { get; private set; }
    public int TotalTrades { get; private set; }
    public int WinningTrades { get; private set; }
    public int LosingTrades { get; private set; }
    public decimal TotalFeesPaid { get; private set; }
    public decimal AverageTradePnl { get; private set; }
    public double AverageHoldTimeMinutes { get; private set; }

    private OptimizationResult() { }

    public static OptimizationResult Create(
        Guid optimizationRunId,
        int rank,
        decimal fitnessScore,
        string strategyConfigJson,
        string signalDescription,
        decimal totalPnl,
        decimal winRate,
        decimal maxDrawdown,
        int totalTrades,
        int winningTrades,
        int losingTrades,
        decimal totalFeesPaid,
        decimal averageTradePnl,
        double averageHoldTimeMinutes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(strategyConfigJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(signalDescription);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rank);

        return new OptimizationResult
        {
            Id = Guid.NewGuid(),
            OptimizationRunId = optimizationRunId,
            Rank = rank,
            FitnessScore = fitnessScore,
            StrategyConfigJson = strategyConfigJson,
            SignalDescription = signalDescription,
            TotalPnl = totalPnl,
            WinRate = winRate,
            MaxDrawdown = maxDrawdown,
            TotalTrades = totalTrades,
            WinningTrades = winningTrades,
            LosingTrades = losingTrades,
            TotalFeesPaid = totalFeesPaid,
            AverageTradePnl = averageTradePnl,
            AverageHoldTimeMinutes = averageHoldTimeMinutes
        };
    }
}
```

---

### Task 1.3: Create `SweepConfig` and `ParameterBounds` models {#task-13-create-sweepconfig-and-parameterbounds-models}

Create the configuration models that define what parameter ranges to sweep.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Application/Optimization/Models/SweepConfig.cs` — new file
  - `src/TradingApp.Application/Optimization/Models/ParameterBounds.cs` — new file
- **Success**:
  - Models compile with all range properties
  - Sensible defaults provided

#### Implementation Details

```csharp
// src/TradingApp.Application/Optimization/Models/ParameterBounds.cs
namespace TradingApp.Application.Optimization.Models;

public sealed record ParameterBounds
{
    // Exit — Stop Loss (fixed percent)
    public decimal StopLossMin { get; init; } = 1m;
    public decimal StopLossMax { get; init; } = 5m;
    public decimal StopLossStep { get; init; } = 0.5m;

    // Exit — Take Profit (fixed percent)
    public decimal TakeProfitMin { get; init; } = 2m;
    public decimal TakeProfitMax { get; init; } = 10m;
    public decimal TakeProfitStep { get; init; } = 1m;

    // Risk — Leverage
    public decimal LeverageMin { get; init; } = 3m;
    public decimal LeverageMax { get; init; } = 10m;
    public decimal LeverageStep { get; init; } = 1m;

    // Risk — Position Size (percent wallet)
    public decimal[] PositionSizeOptions { get; init; } = [10m, 15m, 20m];

    // Indicator — RSI
    public int[] RsiPeriods { get; init; } = [7, 14, 21];
    public decimal[] RsiThresholds { get; init; } = [30m, 35m, 40m, 45m];

    // Indicator — MACD
    public int[] MacdFastPeriods { get; init; } = [8, 12, 16];
    public int[] MacdSlowPeriods { get; init; } = [21, 26, 30];
    public int[] MacdSignalPeriods { get; init; } = [9];

    // Indicator — PriceVsEma
    public int[] EmaPeriods { get; init; } = [20, 50, 100];
    public decimal[] EmaProximityPercents { get; init; } = [0.15m, 0.25m, 0.5m];

    // Trend Filter (optional)
    public bool IncludeTrendFilter { get; init; } = true;
    public int[][] TrendFilterPairs { get; init; } = [[20, 50], [50, 200]];
}
```

```csharp
// src/TradingApp.Application/Optimization/Models/SweepConfig.cs
namespace TradingApp.Application.Optimization.Models;

public sealed record SweepConfig
{
    public required string Symbol { get; init; }
    public required long StartDateUtc { get; init; }
    public required long EndDateUtc { get; init; }
    public required decimal InitialCapital { get; init; }
    public int SampleSize { get; init; } = 500;
    public int MaxDegreeOfParallelism { get; init; } = 0; // 0 = Environment.ProcessorCount
    public ParameterBounds Bounds { get; init; } = new();
    public FitnessThresholds Thresholds { get; init; } = new();
}
```

---

### Task 1.4: Create `FitnessThresholds` model {#task-14-create-fitnessthresholds-model}

Create the configurable fitness threshold model used for qualifying/disqualifying results.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Application/Optimization/Models/FitnessThresholds.cs` — new file
- **Success**:
  - Defaults match agreed thresholds: WinRate ≥40%, TotalTrades ≥10, MaxDrawdown <30%

#### Implementation Details

```csharp
// src/TradingApp.Application/Optimization/Models/FitnessThresholds.cs
namespace TradingApp.Application.Optimization.Models;

public sealed record FitnessThresholds
{
    public decimal MinWinRate { get; init; } = 40m;
    public int MinTotalTrades { get; init; } = 10;
    public decimal MaxDrawdownPercent { get; init; } = 30m;
}
```

---

### Task 1.5: Create `StrategyConfigGenerator` — random combo generation {#task-15-create-strategyconfiggenerator}

Create the service that generates random `StrategyConfig` instances from the parameter bounds. It builds all 11 signal templates (single, pair, triple entry conditions × EntryLogic) and samples from each uniformly, randomly assigning exit/risk params from the bounds.

- **Complexity**: High
- **Risk Factors**: Must produce valid `StrategyConfig` instances that the `CompositeStrategyEngine` can evaluate. All entry condition param types must be correctly constructed.
- **Files**:
  - `src/TradingApp.Application/Optimization/Services/StrategyConfigGenerator.cs` — new file
- **Success**:
  - Generates N random `StrategyConfig` instances from given `ParameterBounds`
  - Each config is `StrategyMode.Signal`, `Direction.Long`
  - Entry conditions are valid combinations of RSI, MACD, PriceVsEma (1-3 conditions)
  - Multi-signal combos use both `EntryLogic.All` and `EntryLogic.Any`
  - Risk params (SL, TP, leverage, position size) are randomly selected within bounds
  - Optional trend filter is randomly included/excluded
  - Deterministic when seeded (accepts `Random` instance for testability)
- **Dependencies**: `StrategyConfig`, `EntryConditionConfig`, `RsiParams`, `MacdParams`, `PriceVsEmaParams`, `TrendFilterConfig`, `ExitConfig`, `RiskConfig`

#### Implementation Details

The generator should:

1. Define the 11 signal templates:
   - Singles: RSI, MACD, PriceVsEma (3)
   - Pairs: RSI+MACD, RSI+PriceVsEma, MACD+PriceVsEma (3) × All/Any (6)
   - Triple: RSI+MACD+PriceVsEma × All/Any (2)

2. For each generated config:
   a. Pick a random signal template
   b. For each entry condition in the template, pick random params from the bounds
   c. Pick random SL, TP, leverage, position size from bounds
   d. Optionally include trend filter with random fast/slow pair
   e. Assemble into a valid `StrategyConfig`

3. Generate a human-readable signal description string, e.g.: `"RSI(14) < 40 + MACD(12,26,9) cross_above_signal | All | SL:2% TP:5% Lev:5x"`

Key method signatures:

```csharp
public interface IStrategyConfigGenerator
{
    IReadOnlyList<GeneratedStrategy> Generate(ParameterBounds bounds, int sampleSize, int? seed = null);
}

public sealed record GeneratedStrategy(StrategyConfig Config, string Description);
```

```csharp
// src/TradingApp.Application/Optimization/Services/StrategyConfigGenerator.cs
public sealed class StrategyConfigGenerator : IStrategyConfigGenerator
{
    public IReadOnlyList<GeneratedStrategy> Generate(ParameterBounds bounds, int sampleSize, int? seed = null)
    {
        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        var results = new List<GeneratedStrategy>(sampleSize);

        for (var i = 0; i < sampleSize; i++)
        {
            var (conditions, entryLogic) = GenerateEntryConditions(bounds, rng);
            var exit = GenerateExitConfig(bounds, rng);
            var risk = GenerateRiskConfig(bounds, rng);
            var trendFilter = GenerateTrendFilter(bounds, rng);
            var description = BuildDescription(conditions, entryLogic, exit, risk, trendFilter);

            var config = new StrategyConfig
            {
                SchemaVersion = 1,
                StrategyMode = StrategyMode.Signal,
                StrategyName = $"Optimizer-{i + 1}",
                Exchange = "Hyperliquid",
                Market = "", // resolved from symbol at runtime
                Timeframe = "15m",
                Direction = Direction.Long,
                Enabled = true,
                EntryLogic = entryLogic,
                EntryConditions = conditions,
                TrendFilter = trendFilter,
                Exit = exit,
                Risk = risk
            };

            results.Add(new GeneratedStrategy(config, description));
        }

        return results;
    }

    // Private methods for generating each section...
    // RSI condition: operator "lt" (for long — buy when oversold)
    // MACD condition: operator "cross_above_signal"
    // PriceVsEma condition: operator "near" with proximity distance
}
```

---

### Task 1.6: Create `FitnessScorer` — scoring and threshold filtering {#task-16-create-fitnessscorer}

Create the service that scores a `BacktestResult` and checks it against `FitnessThresholds`.

- **Complexity**: Low
- **Risk Factors**: Fitness formula must handle edge cases (zero drawdown, zero trades)
- **Files**:
  - `src/TradingApp.Application/Optimization/Services/FitnessScorer.cs` — new file
- **Success**:
  - `IsQualified(result, thresholds, initialCapital)` returns false if any threshold fails
  - `Score(result)` returns decimal fitness score
  - Formula: `(TotalPnL / MaxDrawdownAbsolute) × sqrt(TotalTrades)`
  - Zero MaxDrawdown with positive PnL → use small epsilon (0.01) to avoid divide-by-zero
  - Negative PnL → score is negative (still ranked but below positive scorers)

#### Implementation Details

```csharp
// src/TradingApp.Application/Optimization/Services/FitnessScorer.cs
namespace TradingApp.Application.Optimization.Services;

public interface IFitnessScorer
{
    bool IsQualified(BacktestResult result, FitnessThresholds thresholds, decimal initialCapital);
    decimal Score(BacktestResult result);
}

public sealed class FitnessScorer : IFitnessScorer
{
    public bool IsQualified(BacktestResult result, FitnessThresholds thresholds, decimal initialCapital)
    {
        if (result.TotalTrades < thresholds.MinTotalTrades) return false;
        if (result.WinRate < thresholds.MinWinRate) return false;

        var drawdownPercent = initialCapital > 0
            ? (result.MaxDrawdownAbsolute / initialCapital) * 100m
            : 100m;
        if (drawdownPercent >= thresholds.MaxDrawdownPercent) return false;

        return true;
    }

    public decimal Score(BacktestResult result)
    {
        if (result.TotalTrades == 0) return decimal.MinValue;

        var drawdown = result.MaxDrawdownAbsolute > 0
            ? result.MaxDrawdownAbsolute
            : 0.01m; // epsilon to avoid div-by-zero

        var calmarLike = result.TotalPnL / drawdown;
        var tradeFactor = (decimal)Math.Sqrt(result.TotalTrades);

        return calmarLike * tradeFactor;
    }
}
```

---

### Task 1.7: Create `SweepRunner` — parallel backtest orchestration {#task-17-create-sweeprunner}

Create the core orchestrator that runs the full sweep: generates configs, runs backtests in parallel, filters/ranks results, returns top 10.

- **Complexity**: High
- **Risk Factors**: Parallel execution must be safe — `IBacktestRunner` must support concurrent calls (it does — each RunAsync creates its own scoped services). Progress reporting via callback must be thread-safe.
- **Files**:
  - `src/TradingApp.Application/Optimization/Services/SweepRunner.cs` — new file
- **Success**:
  - Runs N backtests in parallel using `Parallel.ForEachAsync`
  - Reports progress via `Action<int, int>` callback (completed, total)
  - Filters results through `FitnessScorer.IsQualified`
  - Ranks qualified results by fitness score descending
  - Returns top 10 as `SweepResult`
  - Thread-safe progress counting
  - Supports cancellation
- **Dependencies**: `IBacktestRunner`, `IStrategyConfigGenerator`, `IFitnessScorer`

#### Implementation Details

```csharp
// src/TradingApp.Application/Optimization/Services/SweepRunner.cs
namespace TradingApp.Application.Optimization.Services;

public sealed record SweepResult(
    IReadOnlyList<RankedResult> TopResults,
    int TotalRun,
    int TotalQualified,
    long ElapsedMs);

public sealed record RankedResult(
    int Rank,
    decimal FitnessScore,
    GeneratedStrategy Strategy,
    BacktestResult BacktestResult);

public interface ISweepRunner
{
    Task<SweepResult> RunAsync(
        SweepConfig config,
        Action<int, int>? onProgress = null,
        CancellationToken ct = default);
}

public sealed class SweepRunner : ISweepRunner
{
    private readonly IBacktestRunner _backtestRunner;
    private readonly IStrategyConfigGenerator _configGenerator;
    private readonly IFitnessScorer _fitnessScorer;

    public SweepRunner(
        IBacktestRunner backtestRunner,
        IStrategyConfigGenerator configGenerator,
        IFitnessScorer fitnessScorer)
    {
        _backtestRunner = backtestRunner;
        _configGenerator = configGenerator;
        _fitnessScorer = fitnessScorer;
    }

    public async Task<SweepResult> RunAsync(
        SweepConfig config,
        Action<int, int>? onProgress = null,
        CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var strategies = _configGenerator.Generate(config.Bounds, config.SampleSize);
        var total = strategies.Count;
        var completed = 0;
        var qualifiedResults = new System.Collections.Concurrent.ConcurrentBag<(GeneratedStrategy Strategy, BacktestResult Result, decimal Score)>();

        var parallelism = config.MaxDegreeOfParallelism > 0
            ? config.MaxDegreeOfParallelism
            : Environment.ProcessorCount;

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = parallelism,
            CancellationToken = ct
        };

        await Parallel.ForEachAsync(strategies, parallelOptions, async (strategy, token) =>
        {
            var backtestConfig = new BacktestConfig
            {
                Symbol = config.Symbol,
                Intervals = new[] { "15m", "1h", "4h" },
                StartDateUtc = config.StartDateUtc,
                EndDateUtc = config.EndDateUtc,
                InitialCapital = config.InitialCapital,
                Strategy = strategy.Config,
                Execution = new ExecutionConfig(),
                EnableAuditLog = false // no audit logs for optimization — save memory
            };

            try
            {
                var result = await _backtestRunner.RunAsync(backtestConfig, token);

                if (_fitnessScorer.IsQualified(result, config.Thresholds, config.InitialCapital))
                {
                    var score = _fitnessScorer.Score(result);
                    qualifiedResults.Add((strategy, result, score));
                }
            }
            catch (OperationCanceledException)
            {
                throw; // propagate cancellation
            }
            catch
            {
                // Individual backtest failures are silently skipped — don't stop the sweep
            }

            var current = Interlocked.Increment(ref completed);
            onProgress?.Invoke(current, total);
        });

        sw.Stop();

        var ranked = qualifiedResults
            .OrderByDescending(r => r.Score)
            .Take(10)
            .Select((r, i) => new RankedResult(i + 1, r.Score, r.Strategy, r.Result))
            .ToList();

        return new SweepResult(ranked, total, qualifiedResults.Count, sw.ElapsedMilliseconds);
    }
}
```

---

### Task 1.8: Write unit tests for `StrategyConfigGenerator` {#task-18-write-unit-tests-for-strategyconfiggenerator}

- **Complexity**: Medium
- **Risk Factors**: Need to verify all 11 template types are reachable
- **Files**:
  - `tests/TradingApp.Application.Tests/Optimization/StrategyConfigGeneratorTests.cs` — new file
- **Success**: All tests pass

#### Test Cases

```csharp
[TestMethod]
public void GivenDefaultBounds_WhenGenerate_ThenReturnsRequestedSampleSize()

[TestMethod]
public void GivenSeed_WhenGenerateTwice_ThenReturnsSameConfigs()

[TestMethod]
public void GivenDefaultBounds_WhenGenerate_ThenAllConfigsAreSignalModeLong()

[TestMethod]
public void GivenDefaultBounds_WhenGenerate_ThenEntryConditionsArePopulated()

[TestMethod]
public void GivenDefaultBounds_WhenGenerate_ThenStopLossWithinBounds()

[TestMethod]
public void GivenDefaultBounds_WhenGenerate_ThenTakeProfitWithinBounds()

[TestMethod]
public void GivenDefaultBounds_WhenGenerate_ThenLeverageWithinBounds()

[TestMethod]
public void GivenDefaultBounds_WhenGenerate_ThenDescriptionsNotEmpty()

[TestMethod]
public void GivenLargeSample_WhenGenerate_ThenMultipleConditionTypesPresent()
```

---

### Task 1.9: Write unit tests for `FitnessScorer` {#task-19-write-unit-tests-for-fitnessscorer}

- **Complexity**: Low
- **Risk Factors**: Edge cases — zero drawdown, zero trades, negative PnL
- **Files**:
  - `tests/TradingApp.Application.Tests/Optimization/FitnessScorerTests.cs` — new file
- **Success**: All tests pass

#### Test Cases

```csharp
[TestMethod]
public void GivenResultBelowMinWinRate_WhenIsQualified_ThenReturnsFalse()

[TestMethod]
public void GivenResultBelowMinTrades_WhenIsQualified_ThenReturnsFalse()

[TestMethod]
public void GivenResultExceedsMaxDrawdown_WhenIsQualified_ThenReturnsFalse()

[TestMethod]
public void GivenResultMeetsAllThresholds_WhenIsQualified_ThenReturnsTrue()

[TestMethod]
public void GivenZeroTrades_WhenScore_ThenReturnsMinValue()

[TestMethod]
public void GivenPositivePnlLowDrawdown_WhenScore_ThenReturnsPositive()

[TestMethod]
public void GivenNegativePnl_WhenScore_ThenReturnsNegative()

[TestMethod]
public void GivenZeroDrawdown_WhenScore_ThenUsesEpsilon()

[TestMethod]
public void GivenHigherPnlSameDrawdown_WhenScore_ThenHigherScore()

[TestMethod]
public void GivenMoreTradesSamePnl_WhenScore_ThenHigherScore()
```

---

### Task 1.10: Write unit tests for `SweepRunner` {#task-110-write-unit-tests-for-sweeprunner}

- **Complexity**: Medium
- **Risk Factors**: Must mock `IBacktestRunner` for many concurrent calls; verify parallel execution and ranking
- **Files**:
  - `tests/TradingApp.Application.Tests/Optimization/SweepRunnerTests.cs` — new file
- **Success**: All tests pass
- **Dependencies**: Moq mocks for `IBacktestRunner`, `IStrategyConfigGenerator`, `IFitnessScorer`

#### Test Cases

```csharp
[TestMethod]
public async Task GivenValidConfig_WhenRunAsync_ThenCallsBacktestRunnerForEachStrategy()

[TestMethod]
public async Task GivenQualifiedResults_WhenRunAsync_ThenReturnsTop10RankedByFitness()

[TestMethod]
public async Task GivenNoQualifiedResults_WhenRunAsync_ThenReturnsEmptyTopResults()

[TestMethod]
public async Task GivenProgressCallback_WhenRunAsync_ThenReportsProgressIncrementally()

[TestMethod]
public async Task GivenCancellationRequested_WhenRunAsync_ThenThrowsOperationCanceled()

[TestMethod]
public async Task GivenBacktestFailsForSome_WhenRunAsync_ThenContinuesWithRemainingStrategies()

[TestMethod]
public async Task GivenMoreThan10Qualified_WhenRunAsync_ThenReturnsOnlyTop10()
```
