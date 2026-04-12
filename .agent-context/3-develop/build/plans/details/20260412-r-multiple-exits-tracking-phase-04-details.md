<!-- markdownlint-disable-file -->

# Task Details: R-Multiple Exit Types & Trade Tracking

## Phase 4: Aggregate R Metrics & API

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — sealed classes, PascalCase
- `.github/instructions/testing.instructions.md` — MSTest, FluentAssertions, Given_When_Then
- `.github/instructions/dotnet-architecture.instructions.md` — EF Core migrations, entity configuration
- `.github/instructions/api-controllers.instructions.md` — DTO patterns

## Design References

**Aggregate Metrics (computed from R-tracked trades only):**

| Metric | Formula |
|--------|---------|
| Expectancy | `mean(RMultipleResult)` |
| RWinRate | Trades with RMultiple > 0 / total R-tracked |
| AvgWinR | Mean R-multiple of winning trades |
| AvgLossR | Mean R-multiple of losing trades |
| ProfitFactor | Sum(positive R) / abs(Sum(negative R)) |
| SQN | `(Expectancy / StdDev(R-multiples)) × sqrt(N)` |

### Task 4.1: Add R-multiple aggregate fields to BacktestResult {#task-41-add-r-multiple-aggregate-fields-to-backtestresult}

Add nullable R-multiple aggregate metric properties to `BacktestResult`.

- **Complexity**: Low
- **Risk Factors**: None — additive
- **Files**:
  - `src/TradingApp.Application/Backtesting/Models/BacktestResult.cs` — add R metric properties
- **Success**:
  - `BacktestResult` has Expectancy, ProfitFactor, SQN, AvgWinR, AvgLossR, RDistribution fields
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/Models/BacktestResult.cs — modification
// Add after existing properties:

/// <summary>R-multiple aggregate metrics. Null when no R-tracked trades exist.</summary>
public decimal? Expectancy { get; init; }
public decimal? ProfitFactor { get; init; }
public decimal? Sqn { get; init; }
public decimal? AvgWinR { get; init; }
public decimal? AvgLossR { get; init; }
public decimal? RWinRate { get; init; }

/// <summary>Per-trade R-multiple values for histogram distribution. Null when no R-tracked trades.</summary>
public IReadOnlyList<decimal>? RDistribution { get; init; }
```

##### Pattern References

- `src/TradingApp.Application/Backtesting/Models/BacktestResult.cs` — existing property pattern

### Task 4.2: Extend BacktestMetricsCalculator {#task-42-extend-backtestmetricscalculator}

Add R-multiple aggregate metric calculations to `BacktestMetricsCalculator.Calculate`.

- **Complexity**: Medium
- **Risk Factors**: SQN requires standard deviation calculation — ensure N > 1 guard
- **Files**:
  - `src/TradingApp.Application/Backtesting/Services/BacktestMetricsCalculator.cs` — add R aggregate logic
- **Success**:
  - R metrics computed only from trades with non-null RMultipleResult
  - Expectancy, ProfitFactor, SQN match PBI formulas
  - Returns null R metrics when no R-tracked trades exist
- **Dependencies**: Task 4.1, Phase 3

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/Services/BacktestMetricsCalculator.cs — modification
// Add after existing metric calculations, before the return statement:

var rTrackedTrades = completedTrades
    .Where(t => t.RMultipleResult.HasValue)
    .ToList();

decimal? expectancy = null;
decimal? profitFactor = null;
decimal? sqn = null;
decimal? avgWinR = null;
decimal? avgLossR = null;
decimal? rWinRate = null;
IReadOnlyList<decimal>? rDistribution = null;

if (rTrackedTrades.Count > 0)
{
    var rValues = rTrackedTrades.Select(t => t.RMultipleResult!.Value).ToList();
    rDistribution = rValues;
    expectancy = Math.Round(rValues.Average(), 4);

    var rWinners = rValues.Where(r => r > 0m).ToList();
    var rLosers = rValues.Where(r => r < 0m).ToList();

    rWinRate = Math.Round((decimal)rWinners.Count / rTrackedTrades.Count * 100m, 2);
    avgWinR = rWinners.Count > 0 ? Math.Round(rWinners.Average(), 4) : null;
    avgLossR = rLosers.Count > 0 ? Math.Round(rLosers.Average(), 4) : null;

    var sumPositiveR = rWinners.Sum();
    var sumNegativeR = Math.Abs(rLosers.Sum());
    profitFactor = sumNegativeR > 0m ? Math.Round(sumPositiveR / sumNegativeR, 4) : null;

    if (rTrackedTrades.Count > 1)
    {
        var mean = (double)expectancy.Value;
        var variance = rValues.Sum(r => Math.Pow((double)r - mean, 2)) / (rValues.Count - 1);
        var stdDev = Math.Sqrt(variance);
        sqn = stdDev > 0
            ? Math.Round((decimal)(mean / stdDev * Math.Sqrt(rValues.Count)), 4)
            : null;
    }
}

// Add to the return new BacktestResult { ... }:
// Expectancy = expectancy,
// ProfitFactor = profitFactor,
// Sqn = sqn,
// AvgWinR = avgWinR,
// AvgLossR = avgLossR,
// RWinRate = rWinRate,
// RDistribution = rDistribution,
```

##### Pattern References

- `src/TradingApp.Application/Backtesting/Services/BacktestMetricsCalculator.cs` — existing `Calculate` method
- `src/TradingApp.Application/Backtesting/Models/BacktestSummaryForReview.cs` — existing `ProfitFactor` calculation (for reference)

### Task 4.3: Add R-multiple columns to BacktestRun entity and migration {#task-43-add-r-multiple-columns-to-backtestrun-entity}

Add nullable R-metric columns to `BacktestRun` entity and update `MarkCompleted`. Create EF Core migration. Update all callers of `MarkCompleted` to pass the new optional R-metric parameters.

- **Complexity**: Medium
- **Risk Factors**: Migration must be backward-compatible (nullable columns); `MarkCompleted` signature change requires updating callers
- **Files**:
  - `src/TradingApp.Domain/Entities/BacktestRun.cs` — add properties, update `MarkCompleted`
  - `src/TradingApp.Persistence/TradingAppDbContext.cs` — add entity configuration for new decimal columns
  - `src/TradingApp.Api/Services/BacktestProcessorService.cs` — update `MarkCompleted` call to pass R metrics from `BacktestResult`
  - `tests/TradingApp.Api.Tests/Controllers/StrategiesControllerTests.cs` — update `MarkCompleted` call (1 call site)
  - `tests/TradingApp.Persistence.Tests/Repositories/BacktestRunRepositoryTests.cs` — update `MarkCompleted` calls (3 call sites)
  - New migration file via `dotnet ef migrations add AddRMultipleMetrics`
- **Success**:
  - New nullable columns added to BacktestRuns table
  - Existing data unaffected (nulls)
  - MarkCompleted accepts new R metric parameters
- **Dependencies**: Task 4.1

#### Implementation Details

```csharp
// src/TradingApp.Domain/Entities/BacktestRun.cs — modification
// Add properties after TotalFeesPaid:

public decimal? Expectancy { get; private set; }
public decimal? ProfitFactor { get; private set; }
public decimal? Sqn { get; private set; }

// Update MarkCompleted signature to include new parameters:
public void MarkCompleted(
    // ... existing parameters ...
    decimal totalFeesPaid,
    string tradesJson,
    string equityTimeSeriesJson,
    string? candleLogJson = null,
    string? orderEventLogJson = null,
    string? gridCycleLogJson = null,
    decimal? expectancy = null,
    decimal? profitFactor = null,
    decimal? sqn = null)
{
    // ... existing assignments ...
    Expectancy = expectancy;
    ProfitFactor = profitFactor;
    Sqn = sqn;
}
```

```csharp
// src/TradingApp.Persistence/TradingAppDbContext.cs — modification
// In OnModelCreating, BacktestRun configuration section, add:

entity.Property(e => e.Expectancy).HasConversion<double?>();
entity.Property(e => e.ProfitFactor).HasConversion<double?>();
entity.Property(e => e.Sqn).HasConversion<double?>();
```

Then generate migration:
```bash
cd src/TradingApp.Persistence
dotnet ef migrations add AddRMultipleMetrics --startup-project ../TradingApp.Api
```

Also update the `Create` factory method and `BacktestService` caller that invokes `MarkCompleted` to pass the new parameters.

##### Pattern References

- `src/TradingApp.Domain/Entities/BacktestRun.cs` — existing `MarkCompleted` / `Create` signatures
- `src/TradingApp.Persistence/TradingAppDbContext.cs` — existing `HasConversion<double?>()` pattern for decimal nullable properties

### Task 4.4: Update Response DTOs {#task-44-update-response-dtos}

Add R-multiple fields to `BacktestRunResponse` and `BacktestTradeResponse`.

- **Complexity**: Low
- **Risk Factors**: None — additive nullable fields
- **Files**:
  - `src/TradingApp.Application/Backtesting/Models/BacktestRunResponse.cs` — add R aggregate fields
  - `src/TradingApp.Application/Backtesting/Models/BacktestTradeResponse.cs` — add per-trade R fields
- **Success**:
  - API response includes R metrics when available
  - Null for non-RiskBased backtests
- **Dependencies**: Task 4.1

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/Models/BacktestRunResponse.cs — modification
// Add after TotalFeesPaid:

public decimal? Expectancy { get; init; }
public decimal? ProfitFactor { get; init; }
public decimal? Sqn { get; init; }
public decimal? AvgWinR { get; init; }
public decimal? AvgLossR { get; init; }
public decimal? RWinRate { get; init; }
public IReadOnlyList<decimal>? RDistribution { get; init; }
```

```csharp
// src/TradingApp.Application/Backtesting/Models/BacktestTradeResponse.cs — modification
// Add after ExitReason:

public decimal? InitialRDollars { get; init; }
public decimal? RMultipleResult { get; init; }
public decimal? Mfe { get; init; }
public decimal? Mae { get; init; }
```

##### Pattern References

- `src/TradingApp.Application/Backtesting/Models/BacktestRunResponse.cs` — existing DTO pattern
- `src/TradingApp.Application/Backtesting/Models/BacktestTradeResponse.cs` — existing DTO pattern

### Task 4.5: Update BacktestRunResponseMapper {#task-45-update-backtestrunresponsemapper}

Update the mapper to include R-multiple fields in the response. Aggregate R metrics are computed from the deserialized trades (for backward-compatibility with runs that have R data in TradesJson but no entity columns).

- **Complexity**: Medium
- **Risk Factors**: Must handle both new runs (entity columns) and old runs (no R data)
- **Files**:
  - `src/TradingApp.Application/Backtesting/BacktestRunResponseMapper.cs` — update `ToResponse` and `MapTrades`
- **Success**:
  - R aggregate metrics flow from entity → response
  - Per-trade R fields flow from BacktestTrade → BacktestTradeResponse
  - RDistribution computed from trades on the fly (not persisted)
- **Dependencies**: Tasks 4.3, 4.4

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/BacktestRunResponseMapper.cs — modification

// In ToResponse, add to the return object:
Expectancy = entity.Expectancy,
ProfitFactor = entity.ProfitFactor,
Sqn = entity.Sqn,
// Compute remaining R metrics from trades on the fly:
AvgWinR = ComputeAvgWinR(trades),
AvgLossR = ComputeAvgLossR(trades),
RWinRate = ComputeRWinRate(trades),
RDistribution = trades
    .Where(t => t.RMultipleResult.HasValue)
    .Select(t => t.RMultipleResult!.Value)
    .ToList() is { Count: > 0 } dist ? dist : null,

// In MapTrades, add to BacktestTradeResponse:
InitialRDollars = trade.InitialRDollars,
RMultipleResult = trade.RMultipleResult,
Mfe = trade.MFE,
Mae = trade.MAE,

// Add helper methods:
private static decimal? ComputeAvgWinR(IReadOnlyList<BacktestTrade> trades)
{
    var winners = trades.Where(t => t.RMultipleResult is > 0m).Select(t => t.RMultipleResult!.Value).ToList();
    return winners.Count > 0 ? Math.Round(winners.Average(), 4) : null;
}

private static decimal? ComputeAvgLossR(IReadOnlyList<BacktestTrade> trades)
{
    var losers = trades.Where(t => t.RMultipleResult is < 0m).Select(t => t.RMultipleResult!.Value).ToList();
    return losers.Count > 0 ? Math.Round(losers.Average(), 4) : null;
}

private static decimal? ComputeRWinRate(IReadOnlyList<BacktestTrade> trades)
{
    var rTracked = trades.Where(t => t.RMultipleResult.HasValue).ToList();
    if (rTracked.Count == 0) return null;
    var winners = rTracked.Count(t => t.RMultipleResult > 0m);
    return Math.Round((decimal)winners / rTracked.Count * 100m, 2);
}
```

##### Pattern References

- `src/TradingApp.Application/Backtesting/BacktestRunResponseMapper.cs` — existing `ToResponse` and `MapTrades` methods

### Task 4.6: Unit tests for aggregate metrics {#task-46-unit-tests-for-aggregate-metrics}

Write tests for all R-multiple aggregate metric calculations.

- **Complexity**: Medium
- **Risk Factors**: Must verify exact PBI acceptance criteria values
- **Files**:
  - `tests/TradingApp.Application.Tests/Backtesting/Services/BacktestMetricsCalculatorTests.cs` — add R aggregate tests
- **Success**:
  - 10 trades with R-multiples [2.1, -1.0, 1.5, -1.0, 3.0, -0.8, 2.0, -1.0, 1.8, -1.0] → expectancy ≈ 0.56, win rate = 50%, profit factor ≈ 2.17
  - No R-tracked trades → all R metrics null
  - Single R-tracked trade → SQN null (needs N > 1)
- **Dependencies**: Tasks 4.1, 4.2

#### Implementation Details

```csharp
// tests/TradingApp.Application.Tests/Backtesting/Services/BacktestMetricsCalculatorTests.cs — modification

[TestMethod]
public void GivenRTrackedTrades_WhenCalculate_ThenExpectancyIsCorrect()
{
    // Arrange
    var trades = CreateRTrackedTrades(
        [2.1m, -1.0m, 1.5m, -1.0m, 3.0m, -0.8m, 2.0m, -1.0m, 1.8m, -1.0m]);
    var calculator = new BacktestMetricsCalculator();

    // Act
    var result = calculator.Calculate(trades, CreateEquitySeries(), 10_000m, 0);

    // Assert
    result.Expectancy.Should().BeApproximately(0.56m, 0.01m);
    result.RWinRate.Should().Be(50m);
    result.ProfitFactor.Should().BeApproximately(2.17m, 0.01m);
    result.Sqn.Should().NotBeNull();
    result.AvgWinR.Should().BeApproximately(2.08m, 0.01m);
    result.AvgLossR.Should().BeApproximately(-0.96m, 0.01m);
}

[TestMethod]
public void GivenNoRTrackedTrades_WhenCalculate_ThenRMetricsAreNull()
{
    // Arrange
    var trades = CreateTrades(count: 5); // trades without InitialRDollars
    var calculator = new BacktestMetricsCalculator();

    // Act
    var result = calculator.Calculate(trades, CreateEquitySeries(), 10_000m, 0);

    // Assert
    result.Expectancy.Should().BeNull();
    result.ProfitFactor.Should().BeNull();
    result.Sqn.Should().BeNull();
    result.RDistribution.Should().BeNull();
}

// Helper:
private static List<BacktestTrade> CreateRTrackedTrades(decimal[] rMultiples)
{
    return rMultiples.Select((r, i) => new BacktestTrade
    {
        TradeId = $"trade-{i}",
        GridCycleId = "cycle-1",
        EntryTimeUtc = 1000 + i * 100,
        EntryPrice = 50_000m,
        ExitTimeUtc = 1000 + i * 100 + 50,
        ExitPrice = 50_000m + r * 1000m,
        Side = OrderSide.Buy,
        Size = 0.1m,
        PnL = r * 100m,
        Fees = 1m,
        TradeType = TradeType.GridFill,
        ExitReason = "TakeProfitTriggered",
        InitialRDollars = 100m,
        RMultipleResult = r,
    }).ToList();
}
```

##### Pattern References

- `tests/TradingApp.Application.Tests/Backtesting/Services/BacktestMetricsCalculatorTests.cs` — existing `CreateTrade` factory helper pattern
- `tests/TradingApp.Application.Tests/Backtesting/Models/BacktestSummaryForReviewTests.cs` — ProfitFactor test (for reference)

### Task 4.7: Build and verify {#task-47-build-and-verify}

Build solution, run all tests, and verify migration generates correctly.

- **Complexity**: Low
- **Risk Factors**: Migration generation may fail if DbContext configuration is incorrect
- **Files**: None
- **Success**:
  - `dotnet build TradingApp.sln` succeeds
  - `dotnet test TradingApp.sln` — all tests pass
  - Migration file generated and compiles
- **Dependencies**: Task 4.6

## Phase Success Criteria

- BacktestMetricsCalculator computes Expectancy, ProfitFactor, SQN, AvgWinR, AvgLossR
- R metrics match PBI acceptance criteria for given inputs
- BacktestRun entity has new nullable columns with migration
- API response DTOs include R aggregate and per-trade fields
- Mapper correctly wires R data from entity/trades to response
- All existing tests continue to pass
