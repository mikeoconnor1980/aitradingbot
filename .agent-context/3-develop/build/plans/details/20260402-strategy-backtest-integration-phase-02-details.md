<!-- markdownlint-disable-file -->

# Task Details: F3.5 — Strategy–Backtest Integration

## Phase 2: Backend — Application, API & Tests

## Standards and Knowledge References

- **csharp.instructions.md**: Sealed records for commands/queries, async methods suffixed `Async`, `CancellationToken` on all async methods
- **dotnet-architecture.instructions.md**: `Command<T>` / `Query<T>` base types, co-locate handler with command/query, `NotFoundException` for 404, `DomainException` for 400
- **api-controllers.instructions.md**: `[ProducesResponseType]` on every action, 202 for async backtest creation, 200 for GET, `Envelope` for errors, kebab-case routes
- **testing.instructions.md**: Command/query handlers tested only via controller tests, MSTest + Moq + FluentAssertions ≤ v6

### Task 2.1: Update RunBacktestCommand {#task-21-update-runbacktestcommand}

Add optional `StrategyId` property to the `RunBacktestCommand` record.

- **Complexity**: Low
- **Risk Factors**: None — optional parameter
- **Files**:
  - `src/TradingApp.Application/Backtesting/RunBacktestCommand.cs` — Add `Guid? StrategyId` to record
- **Success**:
  - `RunBacktestCommand` has `Guid? StrategyId` property
  - Existing callers compile without changes (default null)
- **Dependencies**: Phase 1

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/RunBacktestCommand.cs — modification
// Update the record definition:

public sealed record RunBacktestCommand(
    string Symbol,
    string[] Intervals,
    DateTime StartDate,
    DateTime EndDate,
    StrategyConfig StrategyConfig,
    ExecutionConfig ExecutionConfig,
    decimal InitialCapital,
    bool EnableAuditLog,
    Guid? StrategyId = null) : Command<BacktestRunResponse>;
```

##### Pattern References

Based on `src/TradingApp.Application/Backtesting/RunBacktestCommand.cs` — existing record definition.

### Task 2.2: Update Handler to Resolve Strategy {#task-22-update-handler-to-resolve-strategy}

Update `RunBacktestCommandHandler` to resolve the strategy when `StrategyId` is provided, capture the current `StrategyRevision` number, and pass both to `BacktestRun.CreateQueued`.

- **Complexity**: Medium
- **Risk Factors**: Must handle non-existent strategy (NotFoundException), must resolve latest revision number from `IStrategyRevisionRepository`
- **Files**:
  - `src/TradingApp.Application/Backtesting/RunBacktestCommand.cs` — Update handler to inject `IStrategyRepository` and `IStrategyRevisionRepository`, resolve strategy, capture revision
- **Success**:
  - When `StrategyId` is null, behavior is unchanged
  - When `StrategyId` is provided: strategy is fetched, validated to exist, latest revision number is resolved, both are passed to `CreateQueued`
  - `NotFoundException` thrown if strategy doesn't exist
- **Dependencies**: Task 2.1, Phase 1

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/RunBacktestCommand.cs — modification
// Update handler class:

public sealed class RunBacktestCommandHandler : CommandHandler<RunBacktestCommand, BacktestRunResponse>
{
    private readonly IBacktestRunRepository _backtestRunRepository;
    private readonly IStrategyRepository _strategyRepository;
    private readonly IStrategyRevisionRepository _strategyRevisionRepository;
    private readonly BacktestJobQueue _backtestJobQueue;

    public RunBacktestCommandHandler(
        IBacktestRunRepository backtestRunRepository,
        IStrategyRepository strategyRepository,
        IStrategyRevisionRepository strategyRevisionRepository,
        BacktestJobQueue backtestJobQueue)
    {
        _backtestRunRepository = backtestRunRepository;
        _strategyRepository = strategyRepository;
        _strategyRevisionRepository = strategyRevisionRepository;
        _backtestJobQueue = backtestJobQueue;
    }

    public override async Task<BacktestRunResponse> Handle(RunBacktestCommand request, CancellationToken cancellationToken)
    {
        // ... existing validation ...

        int? strategyRevisionId = null;

        if (request.StrategyId.HasValue)
        {
            var strategy = await _strategyRepository.GetByIdAsync(request.StrategyId.Value, cancellationToken)
                ?? throw new NotFoundException(nameof(Strategy), request.StrategyId.Value);

            var latestRevisionNumber = await _strategyRevisionRepository
                .GetLatestRevisionNumberAsync(strategy.Id, cancellationToken);
            strategyRevisionId = latestRevisionNumber > 0 ? latestRevisionNumber : null;
        }

        // ... existing date/config serialization ...

        var backtestRun = BacktestRun.CreateQueued(
            symbol: request.Symbol,
            intervalsJson: JsonSerializer.Serialize(request.Intervals),
            startDateUtc: new DateTimeOffset(startDateUtc).ToUnixTimeMilliseconds(),
            endDateUtc: new DateTimeOffset(endDateUtc).ToUnixTimeMilliseconds(),
            strategyConfigJson: strategyConfigJson,
            executionConfigJson: executionConfigJson,
            initialCapital: request.InitialCapital,
            auditLogEnabled: request.EnableAuditLog,
            strategyId: request.StrategyId,
            strategyRevisionId: strategyRevisionId);

        // ... existing add + enqueue ...
    }
}
```

> **Note**: `IStrategyRevisionRepository.GetLatestRevisionNumberAsync(Guid strategyId)` returns `Task<int>` — i.e. the revision number directly (not a `StrategyRevision` entity). A return value of `0` means no revisions exist yet.

##### Pattern References

Based on `src/TradingApp.Application/Backtesting/RunBacktestCommand.cs` lines 22–68 — existing handler implementation.

### Task 2.3: Add GetBacktestsByStrategyQuery {#task-23-add-get-backtests-by-strategy-query}

Create a new query + handler for fetching paged backtest summaries filtered by strategy ID.

- **Complexity**: Medium
- **Risk Factors**: Must validate strategy ownership (strategy belongs to calling user) before returning results
- **Files**:
  - `src/TradingApp.Application/Backtesting/GetBacktestsByStrategyQuery.cs` — New file with query + handler
- **Success**:
  - Query accepts `StrategyId`, `Page`, `PageSize`, and `AppIdentity`
  - Handler validates strategy exists and belongs to the user, then delegates to `GetPagedSummariesByStrategyAsync`
  - `NotFoundException` if strategy not found or belongs to different user
- **Dependencies**: Phase 1 Task 1.5

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/GetBacktestsByStrategyQuery.cs — new file

using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Models;
using TradingApp.Application.Abstractions.Queries;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Domain.Entities;
using TradingApp.Application.Abstractions.Identity;

namespace TradingApp.Application.Backtesting;

public sealed record GetBacktestsByStrategyQuery(
    Guid StrategyId,
    int Page,
    int PageSize,
    AppIdentity Identity) : Query<PagedResult<BacktestRunSummary>>;

public sealed class GetBacktestsByStrategyQueryHandler
    : QueryHandler<GetBacktestsByStrategyQuery, PagedResult<BacktestRunSummary>>
{
    private readonly IStrategyRepository _strategyRepository;
    private readonly IBacktestRunRepository _backtestRunRepository;

    public GetBacktestsByStrategyQueryHandler(
        IStrategyRepository strategyRepository,
        IBacktestRunRepository backtestRunRepository)
    {
        _strategyRepository = strategyRepository;
        _backtestRunRepository = backtestRunRepository;
    }

    public override async Task<PagedResult<BacktestRunSummary>> Handle(
        GetBacktestsByStrategyQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(request.Page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.PageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(request.PageSize, 100);

        var strategy = await _strategyRepository.GetByIdAsync(request.StrategyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Strategy), request.StrategyId);

        if (strategy.UserId != request.Identity.UserId)
        {
            throw new NotFoundException(nameof(Strategy), request.StrategyId);
        }

        return await _backtestRunRepository.GetPagedSummariesByStrategyAsync(
            request.StrategyId,
            request.Page,
            request.PageSize,
            cancellationToken);
    }
}
```

> **Note**: `AppIdentity` lives in `TradingApp.Application.Abstractions.Identity`. `IdentityService.Identity` (injected in `ApiController` base) returns the current `AppIdentity` with `UserId` and `Email` properties.

##### Pattern References

Based on `src/TradingApp.Application/Backtesting/GetBacktestListQuery.cs` — existing query/handler pattern with `Query<T>` base type.

### Task 2.4: Update Response and Mapper {#task-24-update-response-and-mapper}

Add strategy metadata fields to `BacktestRunResponse` and update `BacktestRunResponseMapper.ToResponse` to map them.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Application/Backtesting/Models/BacktestRunResponse.cs` — Add 3 nullable properties
  - `src/TradingApp.Application/Backtesting/BacktestRunResponseMapper.cs` — Map new properties in `ToResponse`
- **Success**:
  - `BacktestRunResponse` includes `StrategyId`, `StrategyRevisionId`, `StrategyName`
  - `ToResponse` maps from entity; `StrategyName` is null in the mapper (resolved at API layer via strategy lookup or left null)
- **Dependencies**: Phase 1

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/Models/BacktestRunResponse.cs — modification
// Add after existing HasAuditLog property:

    public Guid? StrategyId { get; init; }
    public int? StrategyRevisionId { get; init; }
    public string? StrategyName { get; init; }
```

```csharp
// src/TradingApp.Application/Backtesting/BacktestRunResponseMapper.cs — modification
// Add to the return block in ToResponse, after HasAuditLog:

            StrategyId = entity.StrategyId,
            StrategyRevisionId = entity.StrategyRevisionId,
            // StrategyName resolved at API layer when needed
```

##### Pattern References

Based on `src/TradingApp.Application/Backtesting/BacktestRunResponseMapper.cs` — existing `ToResponse` mapping.

### Task 2.5: Update API Models {#task-25-update-api-models}

Add `StrategyId` to `RunBacktestRequest` and strategy fields to `BacktestSummaryDto`.

- **Complexity**: Low
- **Risk Factors**: `StrategyConfig` must remain `[Required]` when `StrategyId` is null — cross-field validation needed in controller
- **Files**:
  - `src/TradingApp.Api/Models/RunBacktestRequest.cs` — Add nullable `StrategyId` property
  - `src/TradingApp.Api/Models/BacktestSummaryDto.cs` — Add strategy metadata fields
- **Success**:
  - `RunBacktestRequest.StrategyId` is nullable Guid
  - `BacktestSummaryDto` includes `StrategyId`, `StrategyRevisionId`, `StrategyName`
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Api/Models/RunBacktestRequest.cs — modification
// Add after EnableAuditLog property:

    public Guid? StrategyId { get; set; }
```

```csharp
// src/TradingApp.Api/Models/BacktestSummaryDto.cs — modification
// Add after existing CreatedAt property:

    public Guid? StrategyId { get; init; }
    public int? StrategyRevisionId { get; init; }
    public string? StrategyName { get; init; }
```

##### Pattern References

Based on `src/TradingApp.Api/Models/RunBacktestRequest.cs` and `src/TradingApp.Api/Models/BacktestSummaryDto.cs`.

### Task 2.6: Update BacktestsController {#task-26-update-backtests-controller}

Update `RunAsync` to handle strategy-based backtests: when `StrategyId` is provided, resolve the strategy's config from the backend instead of requiring the client to send it inline. When `StrategyId` is null, require `StrategyConfig` as before.

- **Complexity**: High
- **Risk Factors**: Cross-field validation (StrategyId XOR StrategyConfig), config deserialization from strategy's ConfigJson
- **Files**:
  - `src/TradingApp.Api/Controllers/BacktestsController.cs` — Update `RunAsync` with strategy resolution logic
- **Success**:
  - When `StrategyId` is provided: strategy is fetched, `StrategyConfig` is deserialized from `Strategy.ConfigJson`, symbol/intervals/direction are derived
  - When `StrategyId` is null: `StrategyConfig` is required (existing `[Required]` validation enforced)
  - `StrategyId` passed to `RunBacktestCommand`
- **Dependencies**: Tasks 2.1, 2.5

#### Implementation Details

```csharp
// src/TradingApp.Api/Controllers/BacktestsController.cs — modification
// In RunBacktestRequest, remove [Required] from StrategyConfig and add cross-field validation
// In RunAsync, update to handle both paths:

    [HttpPost]
    [ProducesResponseType(typeof(BacktestRunResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RunAsync([FromBody] RunBacktestRequest request, CancellationToken cancellationToken)
    {
        StrategyConfig strategyConfig;
        string symbol;
        string[] intervals;

        if (request.StrategyId.HasValue)
        {
            // Strategy-based backtest: resolve config from saved strategy
            // Use IStrategyRepository directly (not MediatR query) to avoid circular controller→mediator calls
            var strategy = await _strategyRepository.GetByIdAsync(request.StrategyId.Value, cancellationToken)
                ?? throw new NotFoundException(nameof(Strategy), request.StrategyId.Value);

            strategyConfig = JsonSerializer.Deserialize<StrategyConfig>(
                strategy.ConfigJson, StrategyJsonOptions.Default)
                ?? throw new DomainException("Failed to deserialize strategy configuration.");
            symbol = strategyConfig.Market;
            intervals = [strategyConfig.Timeframe];
        }
        else
        {
            // Manual backtest: use inline config from request
            if (request.StrategyConfig is null)
            {
                throw new DomainException("Either strategyId or strategyConfig must be provided.");
            }

            ValidateRequest(request);
            strategyConfig = MapStrategyConfig(request.StrategyConfig);
            symbol = request.Symbol;
            intervals = request.Intervals;
        }

        // ... build ExecutionConfig from request.ExecutionConfig ...
        // ... create and send RunBacktestCommand with request.StrategyId ...
    }
```

> **Note**: Extract the existing StrategyConfig mapping logic from `RunAsync` into a private `MapStrategyConfig(StrategyConfigRequest)` method for clarity. The strategy-based path deserializes `StrategyConfig` from `Strategy.ConfigJson` using `StrategyJsonOptions.Default`. Inject `IStrategyRepository` into the controller constructor.

##### Pattern References

Based on `src/TradingApp.Api/Controllers/BacktestsController.cs` lines 33–114 — existing `RunAsync` implementation.

### Task 2.7: Add Strategy Backtests Endpoint {#task-27-add-strategy-backtests-endpoint}

Add `GET /api/strategies/{id}/backtests` endpoint to `StrategiesController` for strategy-scoped backtest history.

- **Complexity**: Medium
- **Risk Factors**: First nested cross-resource route in the codebase; must validate strategy ownership
- **Files**:
  - `src/TradingApp.Api/Controllers/StrategiesController.cs` — Add new action method
- **Success**:
  - `GET /api/strategies/{id:guid}/backtests` returns `PagedResult<BacktestSummaryDto>`
  - Supports `page` and `pageSize` query parameters
  - Strategy ownership validated via `GetBacktestsByStrategyQuery`
- **Dependencies**: Task 2.3

#### Implementation Details

```csharp
// src/TradingApp.Api/Controllers/StrategiesController.cs — modification
// Add new endpoint method:

    [HttpGet("{id:guid}/backtests")]
    [ProducesResponseType(typeof(PagedResult<BacktestSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBacktestsByStrategy(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(
            new GetBacktestsByStrategyQuery(id, page, pageSize, IdentityService.Identity),
            cancellationToken);

        return Ok(new PagedResult<BacktestSummaryDto>
        {
            Items = result.Items
                .Select(summary => new BacktestSummaryDto
                {
                    Id = summary.Id,
                    Symbol = summary.Symbol,
                    Intervals = summary.Intervals,
                    StartDate = summary.StartDate,
                    EndDate = summary.EndDate,
                    TotalTrades = summary.TotalTrades,
                    WinRate = summary.WinRate,
                    TotalPnl = summary.TotalPnl,
                    MaxDrawdown = summary.MaxDrawdown,
                    CreatedAt = summary.CreatedAt,
                    StrategyId = summary.StrategyId,
                    StrategyRevisionId = summary.StrategyRevisionId,
                    StrategyName = summary.StrategyName,
                })
                .ToList(),
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
        });
    }
```

> **Note**: `StrategyName` enrichment — after fetching the paged results, batch-fetch all referenced strategies by their IDs via `IStrategyRepository`. Map `StrategyName` from the strategy's `Name` property. For strategies where `IsActive = false`, append " (deleted)" to the name. For strategies not found (hard-deleted, if ever), use `null`.

##### Pattern References

Based on `src/TradingApp.Api/Controllers/BacktestsController.cs` `GetBacktestsAsync` — existing paged list endpoint.

### Task 2.8: Update Backtest List Mapping {#task-28-update-backtest-list-mapping}

Update `BacktestsController.GetBacktestsAsync` to map the new strategy fields from `BacktestRunSummary` to `BacktestSummaryDto`.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Api/Controllers/BacktestsController.cs` — Update projection in `GetBacktestsAsync`
- **Success**:
  - `BacktestSummaryDto` includes strategy fields in the global backtest list
- **Dependencies**: Task 2.5

#### Implementation Details

```csharp
// src/TradingApp.Api/Controllers/BacktestsController.cs — modification
// Update the Select projection in GetBacktestsAsync to include:

                    StrategyId = summary.StrategyId,
                    StrategyRevisionId = summary.StrategyRevisionId,
                    StrategyName = summary.StrategyName,
```

Also update `GetPagedSummariesAsync` in the repository to include `StrategyId` and `StrategyRevisionId` in its projection (same as Task 1.5 pattern — add the two fields to the anonymous projection and the `BacktestRunSummary` mapping in the existing method).

> **Note**: `StrategyName` enrichment in `GetBacktestsAsync` follows the same pattern as Task 2.7 — batch-fetch strategies by IDs from the paged results and map `StrategyName` (with " (deleted)" suffix for soft-deleted strategies).

##### Pattern References

Based on `src/TradingApp.Api/Controllers/BacktestsController.cs` lines 119–145 — existing `GetBacktestsAsync` list mapping.

### Task 2.9: Add API Controller Tests {#task-29-add-api-controller-tests}

Add tests to `BacktestsControllerTests` and `StrategiesControllerTests` for the new functionality.

- **Complexity**: High
- **Risk Factors**: Must mock `IStrategyRepository`, `IStrategyRevisionRepository` in `BacktestsControllerTests`; `StrategiesControllerTests` uses real SQLite so the new endpoint needs seed data
- **Files**:
  - `tests/TradingApp.Api.Tests/Controllers/BacktestsControllerTests.cs` — Add tests for strategy-based backtest submission
  - `tests/TradingApp.Api.Tests/Controllers/StrategiesControllerTests.cs` — Add tests for `GET /api/strategies/{id}/backtests`
- **Success**:
  - Test: POST backtest with `strategyId` returns 202 with strategy fields populated
  - Test: POST backtest with neither `strategyId` nor `strategyConfig` returns 400
  - Test: POST backtest with non-existent `strategyId` returns 404
  - Test: GET strategy backtests returns 200 with filtered results
  - Test: GET strategy backtests for non-existent strategy returns 404
  - All existing tests continue to pass
- **Dependencies**: Tasks 2.1–2.8

#### Implementation Details

```csharp
// tests/TradingApp.Api.Tests/Controllers/BacktestsControllerTests.cs — modification
// Add new test methods:

    [TestMethod]
    public async Task GivenValidStrategyId_WhenPostBacktest_ThenReturnsAcceptedWithStrategyFields()
    {
        // Arrange: mock IStrategyRepository to return a strategy
        // Arrange: mock IStrategyRevisionRepository to return latest revision
        // Arrange: create RunBacktestRequest with StrategyId, no StrategyConfig
        // Act: POST /api/backtests
        // Assert: 202 Accepted, response contains StrategyId and StrategyRevisionId
    }

    [TestMethod]
    public async Task GivenNoStrategyIdAndNoConfig_WhenPostBacktest_ThenReturnsBadRequest()
    {
        // Arrange: create RunBacktestRequest with null StrategyId and null StrategyConfig
        // Act: POST /api/backtests
        // Assert: 400 Bad Request with "Either strategyId or strategyConfig must be provided"
    }

    [TestMethod]
    public async Task GivenNonExistentStrategyId_WhenPostBacktest_ThenReturnsNotFound()
    {
        // Arrange: mock IStrategyRepository to return null
        // Act: POST /api/backtests with non-existent StrategyId
        // Assert: 404 Not Found
    }
```

```csharp
// tests/TradingApp.Api.Tests/Controllers/StrategiesControllerTests.cs — modification
// Add new test methods:

    [TestMethod]
    public async Task GivenStrategyWithBacktests_WhenGetBacktestsByStrategy_ThenReturnsPagedResults()
    {
        // Arrange: create strategy, create backtest run with that strategyId
        // Act: GET /api/strategies/{id}/backtests
        // Assert: 200 OK with paged results containing the backtest
    }

    [TestMethod]
    public async Task GivenNonExistentStrategy_WhenGetBacktestsByStrategy_ThenReturnsNotFound()
    {
        // Act: GET /api/strategies/{nonExistentId}/backtests
        // Assert: 404 Not Found
    }
```

##### Pattern References

Based on `tests/TradingApp.Api.Tests/Controllers/BacktestsControllerTests.cs` (mocked pipeline) and `tests/TradingApp.Api.Tests/Controllers/StrategiesControllerTests.cs` (real SQLite DB).

### Task 2.10: Run Architecture Tests {#task-210-run-architecture-tests}

Verify all tests pass and solution builds cleanly.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None — verification step only
- **Success**:
  - `dotnet build` succeeds
  - `dotnet test tests/TradingApp.Api.Tests` passes
  - `dotnet test tests/TradingApp.Application.Tests` passes (if any new tests added)
  - All existing tests continue to pass
- **Dependencies**: Tasks 2.1–2.9

## Phase Success Criteria

- `RunBacktestCommand` accepts optional `StrategyId`, handler resolves strategy and captures revision
- `GetBacktestsByStrategyQuery` validates ownership and returns filtered results
- `GET /api/strategies/{id}/backtests` endpoint returns paged backtest history
- `POST /api/backtests` handles both strategy-based and manual backtests
- `BacktestRunResponse` and `BacktestSummaryDto` include strategy metadata
- All API controller tests pass
