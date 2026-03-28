<!-- markdownlint-disable-file -->

# Task Details: F4 — Backtest API & Results

## Phase 2: Application Layer — DTOs & CQRS Commands/Queries

## Standards and Knowledge References

- **csharp.instructions.md**: Sealed classes, PascalCase naming, async/await with CancellationToken
- **dotnet-architecture.instructions.md**: CQRS command/query patterns — `Command<T>` / `Query<T>` base records, handlers in same file as command/query, DTOs in Models folder per bounded context
- **testing.instructions.md**: "Command and Query Handlers SHOULD NOT have their own test classes. They SHOULD ONLY be tested indirectly via the API Controller Tests" — handlers are tested in Phase 3
- **Knowledge 13 (Strategy Config Schema)**: Full GridStrategy JSON schema defining all config fields
- **Knowledge 18 (Backtesting Architecture)**: BacktestConfig fields, BacktestResult aggregate metrics, trade log structure

## Design References

- Existing `BacktestConfig` uses `StrategyConfigJson` (JSON string) — the API-layer `GridStrategyConfig` DTO will be serialized to this format
- Existing `BacktestResult` is an in-memory model with different field names (`MaxDrawdownAbsolute` vs PBI's `maxDrawdown`) — the response DTO maps from both the domain entity and the application model
- `BacktestConfig` timestamps are Unix ms (`long`) — the API request accepts ISO 8601 `DateTime` and converts

### Task 2.1: Create `GridStrategyConfig` and `BacktestRunResponse` DTOs {#task-21-create-gridstrategyconfig-and-backtestrunresponse-dtos}

Create the strongly-typed strategy configuration DTO and the backtest result response DTO in the Application Backtesting Models folder.

- **Complexity**: Medium
- **Risk Factors**: Must align `GridStrategyConfig` fields with PBI spec and existing `BacktestConfig.StrategyConfigJson` schema; Response DTO must include all PBI-required fields
- **Files**:
  - `src/TradingApp.Application/Backtesting/Models/GridStrategyConfig.cs` — new file
  - `src/TradingApp.Application/Backtesting/Models/BacktestRunResponse.cs` — new file
  - `src/TradingApp.Application/Backtesting/Models/BacktestTradeResponse.cs` — new file
- **Success**:
  - `GridStrategyConfig` has all fields from PBI spec (gridLevels, gridSpacing, takeProfitPercent, etc.)
  - `BacktestRunResponse` has all summary metrics, metadata, strategy config, and trades array
  - `BacktestTradeResponse` maps from existing `BacktestTrade` with ISO 8601 time strings
- **Dependencies**: Phase 1 complete

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/Models/GridStrategyConfig.cs — new file
namespace TradingApp.Application.Backtesting.Models;

public sealed class GridStrategyConfig
{
    public int GridLevels { get; set; }
    public decimal GridSpacing { get; set; }
    public decimal TakeProfitPercent { get; set; }
    public decimal BreakdownThreshold { get; set; }
    public decimal MakerFee { get; set; }
    public decimal TakerFee { get; set; }
    public decimal Slippage { get; set; }
    public decimal PositionSize { get; set; }
    public decimal Leverage { get; set; }
    public decimal StopLossPercent { get; set; }
}
```

```csharp
// src/TradingApp.Application/Backtesting/Models/BacktestRunResponse.cs — new file
namespace TradingApp.Application.Backtesting.Models;

public sealed class BacktestRunResponse
{
    public required Guid Id { get; init; }
    public required string Symbol { get; init; }
    public required string[] Intervals { get; init; }
    public required DateTime StartDate { get; init; }
    public required DateTime EndDate { get; init; }
    public required GridStrategyConfig StrategyConfig { get; init; }
    public required decimal InitialCapital { get; init; }
    public required int CandlesReplayed { get; init; }
    public required long ElapsedMs { get; init; }
    public required int TotalTrades { get; init; }
    public required int WinningTrades { get; init; }
    public required int LosingTrades { get; init; }
    public required decimal WinRate { get; init; }
    public required decimal TotalPnl { get; init; }
    public required decimal MaxDrawdown { get; init; }
    public required decimal AverageTradePnl { get; init; }
    public required double AverageHoldTimeMinutes { get; init; }
    public required int HedgesOpened { get; init; }
    public required decimal TotalFeesPaid { get; init; }
    public required IReadOnlyList<BacktestTradeResponse> Trades { get; init; }
    public required DateTime CreatedAt { get; init; }
}
```

```csharp
// src/TradingApp.Application/Backtesting/Models/BacktestTradeResponse.cs — new file
namespace TradingApp.Application.Backtesting.Models;

public sealed class BacktestTradeResponse
{
    public required DateTime EntryTime { get; init; }
    public required DateTime? ExitTime { get; init; }
    public required decimal EntryPrice { get; init; }
    public required decimal? ExitPrice { get; init; }
    public required string Side { get; init; }
    public required decimal Size { get; init; }
    public required decimal? Pnl { get; init; }
    public required decimal Fees { get; init; }
    public required string TradeType { get; init; }
}
```

##### Pattern References

- `src/TradingApp.Application/Backtesting/Models/BacktestResult.cs` — existing model with aggregate metrics, `required` init properties
- `src/TradingApp.Application/Backtesting/Models/BacktestTrade.cs` — existing trade model with `EntryTimeUtc` (long), `Side` (OrderSide enum)

### Task 2.2: Create `CandleCoverageResponse` DTO {#task-22-create-candlecoverageresponse-dto}

Create the DTO for the validate endpoint response showing candle data coverage per interval.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Application/Backtesting/Models/CandleCoverageResponse.cs` — new file
- **Success**:
  - DTO contains per-interval coverage information (from, to, candleCount)
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/Models/CandleCoverageResponse.cs — new file
namespace TradingApp.Application.Backtesting.Models;

public sealed class CandleCoverageResponse
{
    public required Dictionary<string, IntervalCoverage> Coverage { get; init; }
}

public sealed class IntervalCoverage
{
    public required DateTime? From { get; init; }
    public required DateTime? To { get; init; }
    public required int CandleCount { get; init; }
}
```

##### Pattern References

- `src/TradingApp.Application/MarketData/Models/MarketInfoDto.cs` — DTO pattern in Application layer

### Task 2.3: Create `RunBacktestCommand` and handler {#task-23-create-runbacktestcommand-and-handler}

Create the CQRS command that triggers a backtest run, persists the result, and returns the response. The handler maps the request to `BacktestConfig`, calls `IBacktestRunner`, creates a `BacktestRun` entity, persists it, and returns a `BacktestRunResponse`.

- **Complexity**: High
- **Risk Factors**: Complex mapping between API request → BacktestConfig → BacktestResult → BacktestRun entity → BacktestRunResponse; must handle cancellation/timeout correctly; must serialize GridStrategyConfig to JSON for BacktestConfig.StrategyConfigJson
- **Files**:
  - `src/TradingApp.Application/Backtesting/Models/BacktestResult.cs` — modification (add `CandlesReplayed` property)
  - `src/TradingApp.Application/Backtesting/RunBacktestCommand.cs` — new file
- **Success**:
  - Command carries all request fields
  - Handler calls `IBacktestRunner.RunAsync()` with correct `BacktestConfig`
  - Handler creates `BacktestRun` entity and persists via `IBacktestRunRepository`
  - Handler returns `BacktestRunResponse` with all fields populated
  - CancellationToken is propagated and combined with a server-side timeout
- **Dependencies**: Phase 1 complete, Task 2.1

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/RunBacktestCommand.cs — new file
using System.Diagnostics;
using System.Text.Json;
using TradingApp.Application.Abstractions.Commands;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Backtesting;

public sealed record RunBacktestCommand(
    string Symbol,
    string[] Intervals,
    DateTime StartDate,
    DateTime EndDate,
    GridStrategyConfig StrategyConfig,
    decimal InitialCapital) : Command<BacktestRunResponse>;

public sealed class RunBacktestCommandHandler : CommandHandler<RunBacktestCommand, BacktestRunResponse>
{
    private static readonly TimeSpan ServerTimeout = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IBacktestRunner _backtestRunner;
    private readonly IBacktestRunRepository _backtestRunRepository;

    public RunBacktestCommandHandler(
        IBacktestRunner backtestRunner,
        IBacktestRunRepository backtestRunRepository)
    {
        _backtestRunner = backtestRunner;
        _backtestRunRepository = backtestRunRepository;
    }

    public override async Task<BacktestRunResponse> Handle(RunBacktestCommand request, CancellationToken cancellationToken)
    {
        var strategyConfigJson = JsonSerializer.Serialize(request.StrategyConfig, JsonOptions);

        var config = new BacktestConfig
        {
            Symbol = request.Symbol,
            Intervals = request.Intervals,
            StartDateUtc = new DateTimeOffset(request.StartDate, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            EndDateUtc = new DateTimeOffset(request.EndDate, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            InitialCapital = request.InitialCapital,
            FeeModel = new FeeModel
            {
                MakerFeeRate = request.StrategyConfig.MakerFee,
                TakerFeeRate = request.StrategyConfig.TakerFee,
                SlippageRate = request.StrategyConfig.Slippage
            },
            StrategyConfigJson = strategyConfigJson
        };

        using var timeoutCts = new CancellationTokenSource(ServerTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var stopwatch = Stopwatch.StartNew();
        var result = await _backtestRunner.RunAsync(config, linkedCts.Token);
        stopwatch.Stop();

        var intervalsJson = JsonSerializer.Serialize(request.Intervals);
        var tradesJson = JsonSerializer.Serialize(result.TradeLog, JsonOptions);

        var backtestRun = BacktestRun.Create(
            symbol: request.Symbol,
            intervalsJson: intervalsJson,
            startDateUtc: config.StartDateUtc,
            endDateUtc: config.EndDateUtc,
            strategyConfigJson: strategyConfigJson,
            initialCapital: request.InitialCapital,
            candlesReplayed: result.CandlesReplayed,
            elapsedMs: stopwatch.ElapsedMilliseconds,
            totalTrades: result.TotalTrades,
            winningTrades: result.WinningTrades,
            losingTrades: result.LosingTrades,
            winRate: result.WinRate,
            totalPnl: result.TotalPnL,
            maxDrawdown: result.MaxDrawdownAbsolute,
            averageTradePnl: result.AverageTradePnL,
            averageHoldTimeMinutes: result.AverageHoldTime.TotalMinutes,
            hedgesOpened: result.HedgesOpened,
            totalFeesPaid: result.TotalFeesPaid,
            tradesJson: tradesJson);

        await _backtestRunRepository.AddAsync(backtestRun, cancellationToken);

        return MapToResponse(backtestRun, request.StrategyConfig);
    }

    private static BacktestRunResponse MapToResponse(BacktestRun entity, GridStrategyConfig config)
    {
        var trades = JsonSerializer.Deserialize<List<BacktestTradeResponse>>(entity.TradesJson, JsonOptions)
            ?? [];

        return new BacktestRunResponse
        {
            Id = entity.Id,
            Symbol = entity.Symbol,
            Intervals = JsonSerializer.Deserialize<string[]>(entity.IntervalsJson) ?? [],
            StartDate = DateTimeOffset.FromUnixTimeMilliseconds(entity.StartDateUtc).UtcDateTime,
            EndDate = DateTimeOffset.FromUnixTimeMilliseconds(entity.EndDateUtc).UtcDateTime,
            StrategyConfig = config,
            InitialCapital = entity.InitialCapital,
            CandlesReplayed = entity.CandlesReplayed,
            ElapsedMs = entity.ElapsedMs,
            TotalTrades = entity.TotalTrades,
            WinningTrades = entity.WinningTrades,
            LosingTrades = entity.LosingTrades,
            WinRate = entity.WinRate,
            TotalPnl = entity.TotalPnl,
            MaxDrawdown = entity.MaxDrawdown,
            AverageTradePnl = entity.AverageTradePnl,
            AverageHoldTimeMinutes = entity.AverageHoldTimeMinutes,
            HedgesOpened = entity.HedgesOpened,
            TotalFeesPaid = entity.TotalFeesPaid,
            Trades = trades,
            CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(entity.CreatedAtUtc).UtcDateTime
        };
    }
}
```

> **Action Required**: `BacktestResult` in `src/TradingApp.Application/Backtesting/Models/BacktestResult.cs` does **not** currently have a `CandlesReplayed` property. Before creating `RunBacktestCommand.cs`, add `public required int CandlesReplayed { get; init; }` to `BacktestResult` (after the existing `GridCycles` property). The `FeeModel` uses `init` properties with defaults — the handler code above matches this pattern correctly.

##### Pattern References

- `src/TradingApp.Application/Candles/IngestCandlesCommand.cs` — CQRS `Command<T>` record + `CommandHandler` in same file
- `src/TradingApp.Application/Abstractions/Commands/CommandHandler.cs` — `CommandHandler<TCommand, TResult>` abstract base
- `src/TradingApp.Application/Backtesting/Models/BacktestConfig.cs` — config input model
- `src/TradingApp.Application/Backtesting/Models/BacktestResult.cs` — result output model (must be modified to add `CandlesReplayed`)
- `src/TradingApp.Application/Backtesting/Models/FeeModel.cs` — fee model structure

### Task 2.4: Create `GetBacktestResultQuery` and handler {#task-24-create-getbacktestresultquery-and-handler}

Create the CQRS query that retrieves a persisted backtest result by ID.

- **Complexity**: Medium
- **Risk Factors**: Must deserialize JSON blobs (StrategyConfigJson, TradesJson) back into typed DTOs; must throw NotFoundException for missing IDs
- **Files**:
  - `src/TradingApp.Application/Backtesting/GetBacktestResultQuery.cs` — new file
- **Success**:
  - Query accepts a `Guid` ID
  - Handler returns `BacktestRunResponse` with all fields populated from the entity
  - Handler throws `NotFoundException` when ID doesn't exist
- **Dependencies**: Phase 1, Task 2.1

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/GetBacktestResultQuery.cs — new file
using System.Text.Json;
using TradingApp.Application.Abstractions.Queries;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Abstractions.Exceptions;

namespace TradingApp.Application.Backtesting;

public sealed record GetBacktestResultQuery(Guid Id) : Query<BacktestRunResponse>;

public sealed class GetBacktestResultQueryHandler : QueryHandler<GetBacktestResultQuery, BacktestRunResponse>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IBacktestRunRepository _repository;

    public GetBacktestResultQueryHandler(IBacktestRunRepository repository)
    {
        _repository = repository;
    }

    public override async Task<BacktestRunResponse> Handle(GetBacktestResultQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (entity is null)
            throw new NotFoundException("BacktestRun", request.Id.ToString());

        var strategyConfig = JsonSerializer.Deserialize<GridStrategyConfig>(entity.StrategyConfigJson, JsonOptions)
            ?? new GridStrategyConfig();

        var trades = JsonSerializer.Deserialize<List<BacktestTradeResponse>>(entity.TradesJson, JsonOptions)
            ?? [];

        return new BacktestRunResponse
        {
            Id = entity.Id,
            Symbol = entity.Symbol,
            Intervals = JsonSerializer.Deserialize<string[]>(entity.IntervalsJson) ?? [],
            StartDate = DateTimeOffset.FromUnixTimeMilliseconds(entity.StartDateUtc).UtcDateTime,
            EndDate = DateTimeOffset.FromUnixTimeMilliseconds(entity.EndDateUtc).UtcDateTime,
            StrategyConfig = strategyConfig,
            InitialCapital = entity.InitialCapital,
            CandlesReplayed = entity.CandlesReplayed,
            ElapsedMs = entity.ElapsedMs,
            TotalTrades = entity.TotalTrades,
            WinningTrades = entity.WinningTrades,
            LosingTrades = entity.LosingTrades,
            WinRate = entity.WinRate,
            TotalPnl = entity.TotalPnl,
            MaxDrawdown = entity.MaxDrawdown,
            AverageTradePnl = entity.AverageTradePnl,
            AverageHoldTimeMinutes = entity.AverageHoldTimeMinutes,
            HedgesOpened = entity.HedgesOpened,
            TotalFeesPaid = entity.TotalFeesPaid,
            Trades = trades,
            CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(entity.CreatedAtUtc).UtcDateTime
        };
    }
}
```

> **Note**: Verify the exact namespace and class name for `NotFoundException` in the codebase. Discovery shows it exists and is mapped to 404 by `HttpGlobalExceptionFilter`.

##### Pattern References

- `src/TradingApp.Application/MarketData/GetMarketInfoQuery.cs` — CQRS `Query<T>` + `QueryHandler` with `NotFoundException` pattern
- `src/TradingApp.Application/Abstractions/Queries/QueryHandler.cs` — `QueryHandler<TQuery, TResult>` abstract base

### Task 2.5: Create `GetCandleCoverageQuery` and handler {#task-25-create-getcandlecoveragequery-and-handler}

Create the CQRS query that checks candle data coverage for a symbol across multiple intervals.

- **Complexity**: Medium
- **Risk Factors**: Must query `ICandleRepository` per interval; must handle case where no data exists for an interval
- **Files**:
  - `src/TradingApp.Application/Backtesting/GetCandleCoverageQuery.cs` — new file
- **Success**:
  - Query accepts symbol and intervals
  - Handler returns coverage report with per-interval date ranges and candle counts
  - Intervals with no data show null dates and zero count
- **Dependencies**: Task 2.2

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/GetCandleCoverageQuery.cs — new file
using TradingApp.Application.Abstractions.Queries;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Backtesting.Models;

namespace TradingApp.Application.Backtesting;

public sealed record GetCandleCoverageQuery(string Symbol, string[] Intervals) : Query<CandleCoverageResponse>;

public sealed class GetCandleCoverageQueryHandler : QueryHandler<GetCandleCoverageQuery, CandleCoverageResponse>
{
    private readonly ICandleRepository _candleRepository;

    public GetCandleCoverageQueryHandler(ICandleRepository candleRepository)
    {
        _candleRepository = candleRepository;
    }

    public override async Task<CandleCoverageResponse> Handle(GetCandleCoverageQuery request, CancellationToken cancellationToken)
    {
        var coverage = new Dictionary<string, IntervalCoverage>();

        foreach (var interval in request.Intervals)
        {
            var key = $"{request.Symbol}/{interval}";

            // Query all candles for this symbol/interval to get coverage info
            var candles = await _candleRepository.GetCandlesAsync(
                request.Symbol, interval,
                startTime: 0, endTime: long.MaxValue,
                source: null,
                cancellationToken: cancellationToken);

            if (candles.Count == 0)
            {
                coverage[key] = new IntervalCoverage
                {
                    From = null,
                    To = null,
                    CandleCount = 0
                };
            }
            else
            {
                coverage[key] = new IntervalCoverage
                {
                    From = DateTimeOffset.FromUnixTimeMilliseconds(candles[0].Timestamp).UtcDateTime,
                    To = DateTimeOffset.FromUnixTimeMilliseconds(candles[^1].Timestamp).UtcDateTime,
                    CandleCount = candles.Count
                };
            }
        }

        return new CandleCoverageResponse { Coverage = coverage };
    }
}
```

> **Note**: Loading all candles just to get min/max timestamps and count is inefficient for large datasets (454k rows). The implementer should add `GetCandleCoverageAsync(symbol, interval, source?, ct)` (or separate `GetEarliestTimestampAsync` + `GetCandleCountAsync`) to `ICandleRepository` and `CandleRepository` to perform `MIN/MAX/COUNT` at the database level. Update the Files list for this task accordingly.

##### Pattern References

- `src/TradingApp.Application/Abstractions/Repositories/ICandleRepository.cs` — `GetCandlesAsync()`, `GetLatestTimestampAsync()` methods
- `src/TradingApp.Application/Abstractions/Queries/QueryHandler.cs` — query handler base

### Task 2.6: Add `OperationCanceledException` handling to `HttpGlobalExceptionFilter` {#task-26-add-operationcanceledexception-handling-to-httpglobalexceptionfilter}

Add handling for `OperationCanceledException` to return HTTP 408 Request Timeout when a backtest is cancelled due to timeout or client disconnect.

- **Complexity**: Low
- **Risk Factors**: Must not break existing exception handling; must correctly distinguish client disconnect from server timeout
- **Files**:
  - `src/TradingApp.Api/Infrastructure/Filters/HttpGlobalExceptionFilter.cs` — modification
- **Success**:
  - `OperationCanceledException` returns 408 with descriptive message
  - Existing exception handling is unchanged
- **Dependencies**: None

#### Implementation Details

The existing filter uses a `switch` expression pattern. Add a new arm for `OperationCanceledException` **before** the catch-all `_` arm:

```csharp
// src/TradingApp.Api/Infrastructure/Filters/HttpGlobalExceptionFilter.cs — modification
// Add this arm to the existing switch expression, before the _ catch-all:
OperationCanceledException => (
    StatusCodes.Status408RequestTimeout,
    new Envelope("Request was cancelled or exceeded maximum timeout", "request_timeout", correlationId)),
```

##### Pattern References

- `src/TradingApp.Api/Infrastructure/Filters/HttpGlobalExceptionFilter.cs` — existing `switch` expression maps exception types to `(statusCode, envelope)` tuples: `DomainException` → 400, `NotFoundException` → 404, etc.

### Task 2.7: Build solution successfully {#task-27-build-solution-successfully}

Verify the solution compiles with all new Application layer code. No separate handler tests are needed — per testing standards, command/query handlers are tested indirectly via controller integration tests in Phase 3.

- **Complexity**: Low
- **Risk Factors**: May need to adjust property names or types if existing models differ from expected
- **Files**: None (verification only)
- **Success**:
  - `dotnet build TradingApp.sln` succeeds with no errors
  - All existing tests still pass
- **Dependencies**: All prior tasks in Phase 2

## Phase Success Criteria

- `GridStrategyConfig`, `BacktestRunResponse`, `BacktestTradeResponse`, `CandleCoverageResponse` DTOs exist in `TradingApp.Application/Backtesting/Models/`
- `RunBacktestCommand` + handler, `GetBacktestResultQuery` + handler, `GetCandleCoverageQuery` + handler exist in `TradingApp.Application/Backtesting/`
- `HttpGlobalExceptionFilter` handles `OperationCanceledException` → 408
- Solution builds with zero errors
- All existing tests still pass
