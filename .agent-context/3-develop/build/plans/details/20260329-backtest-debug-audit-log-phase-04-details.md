<!-- markdownlint-disable-file -->

# Task Details: Backtest Debug/Audit Log

## Phase 4: API Endpoint & CQRS Query

## Standards and Knowledge References

- `.github/instructions/api-controllers.instructions.md` — sealed controllers, `[ProducesResponseType]`, kebab-case routes, MediatR dispatch
- `.github/instructions/csharp.instructions.md` — sealed classes, `NotFoundException`, `Given_When_Then` test naming
- `.github/instructions/testing.instructions.md` — controller tests via `BaseControllerTests`, no standalone handler tests
- `.github/instructions/dotnet-architecture.instructions.md` — CQRS `Query<T>` records, handler in same file

## Design References

- `GET /api/backtests/{id}/debug?cycleId={cycleId}` — new endpoint
- 404 = run not found (via `NotFoundException`), 204 = no debug data (inline `NoContent()`)
- Response: filtered candle evaluations, order events, and grid cycle summary for the specified cycle

### Task 4.1: Create GetBacktestDebugQuery and handler {#task-41-create-getbacktestdebugquery-and-handler}

Create the CQRS query and handler. The handler loads the `BacktestRun`, deserializes the 3 JSON blobs, filters by `cycleId`, and returns a response DTO. Returns null when no debug data is available (caller returns 204).

- **Complexity**: Medium
- **Risk Factors**: JSON deserialization of potentially large blobs; filtering by cycleId must be efficient
- **Files**:
  - `src/TradePilot.Application/Backtesting/GetBacktestDebugQuery.cs` — new file
- **Success**:
  - Query accepts `BacktestId` (Guid) and `CycleId` (string)
  - Handler returns `BacktestDebugResponse?` (nullable — null means "no debug data")
  - Handler throws `NotFoundException` when backtest run does not exist
  - Handler returns null when `CandleLogJson` is null (audit was disabled)
  - Deserialized data is filtered to the specified `cycleId`
- **Dependencies**: Phase 2 (entity + mapper), Task 4.2 (response DTO)

#### Implementation Details

```csharp
// src/TradePilot.Application/Backtesting/GetBacktestDebugQuery.cs — new file
using System.Text.Json;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Backtesting.Models;

namespace TradePilot.Application.Backtesting;

public sealed record GetBacktestDebugQuery(Guid BacktestId, string CycleId) : Query<BacktestDebugResponse?>;

public sealed class GetBacktestDebugQueryHandler : QueryHandler<GetBacktestDebugQuery, BacktestDebugResponse?>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IBacktestRunRepository _repository;

    public GetBacktestDebugQueryHandler(IBacktestRunRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public override async Task<BacktestDebugResponse?> Handle(
        GetBacktestDebugQuery request,
        CancellationToken cancellationToken)
    {
        var backtestRun = await _repository.GetByIdAsync(request.BacktestId, cancellationToken)
            ?? throw new NotFoundException($"Backtest run {request.BacktestId} not found.");

        if (backtestRun.CandleLogJson is null)
        {
            return null; // No debug data available — controller will return 204
        }

        var candleLog = JsonSerializer.Deserialize<List<CandleEvaluationEntry>>(
            backtestRun.CandleLogJson, JsonOptions) ?? [];
        var orderEventLog = JsonSerializer.Deserialize<List<OrderEventEntry>>(
            backtestRun.OrderEventLogJson ?? "[]", JsonOptions) ?? [];
        var gridCycleLog = JsonSerializer.Deserialize<List<GridCycleEntry>>(
            backtestRun.GridCycleLogJson ?? "[]", JsonOptions) ?? [];

        // TODO: v2 - consider per-cycle storage or pre-indexed JSON for large backtests
        // Current approach deserializes full blobs (~3.5 MB) and filters in-memory.
        // Filter to the requested cycle
        var cycleId = request.CycleId;
        var filteredCandles = candleLog.Where(c => c.GridCycleId == cycleId).ToList();
        var filteredOrders = orderEventLog.Where(o => o.GridCycleId == cycleId).ToList();
        var cycleSummary = gridCycleLog.FirstOrDefault(g => g.GridCycleId == cycleId);

        return new BacktestDebugResponse
        {
            CycleId = cycleId,
            CandleEvaluations = filteredCandles,
            OrderEvents = filteredOrders,
            GridCycleSummary = cycleSummary
        };
    }
}
```

##### Pattern References

- `src/TradePilot.Application/Backtesting/GetBacktestResultQuery.cs` — existing query + handler in same file, `NotFoundException` pattern
- `src/TradePilot.Application/Backtesting/BacktestRunResponseMapper.cs` — `JsonOptions` pattern for deserialization

---

### Task 4.2: Create BacktestDebugResponse DTOs {#task-42-create-backtestdebugresponse-dtos}

Create the response DTO for the debug endpoint. It wraps the three filtered log types for a specific grid cycle.

- **Complexity**: Low
- **Risk Factors**: None — simple DTO
- **Files**:
  - `src/TradePilot.Application/Backtesting/Models/BacktestDebugResponse.cs` — new file
- **Success**:
  - DTO contains `CycleId`, `CandleEvaluations`, `OrderEvents`, and `GridCycleSummary`
  - Used by the query handler and API controller
- **Dependencies**: Phase 1 (model types)

#### Implementation Details

```csharp
// src/TradePilot.Application/Backtesting/Models/BacktestDebugResponse.cs — new file
namespace TradePilot.Application.Backtesting.Models;

public sealed class BacktestDebugResponse
{
    public required string CycleId { get; init; }
    public required IReadOnlyList<CandleEvaluationEntry> CandleEvaluations { get; init; }
    public required IReadOnlyList<OrderEventEntry> OrderEvents { get; init; }
    public GridCycleEntry? GridCycleSummary { get; init; }
}
```

##### Pattern References

- `src/TradePilot.Application/Backtesting/Models/BacktestRunResponse.cs` — existing response DTO pattern with `required` init properties

---

### Task 4.3: Add debug endpoint to BacktestsController {#task-43-add-debug-endpoint-to-backtestscontroller}

Add `GET /api/backtests/{id:guid}/debug` with `[FromQuery] string cycleId` parameter. Returns 200 with data, 204 when no debug data, 404 when run not found.

- **Complexity**: Medium
- **Risk Factors**: 204 vs 404 distinction — 404 comes from `NotFoundException` via global filter; 204 returned inline when handler returns null
- **Files**:
  - `src/TradePilot.Api/Controllers/BacktestsController.cs` — modification
- **Success**:
  - New GET endpoint at `{id:guid}/debug` with `cycleId` query parameter
  - `[ProducesResponseType]` attributes for 200, 204, 404
  - Dispatches `GetBacktestDebugQuery` via Mediator
  - Returns `Ok(result)` when data available, `NoContent()` when null
- **Dependencies**: Task 4.1, Task 4.2

#### Implementation Details

```csharp
// src/TradePilot.Api/Controllers/BacktestsController.cs — modification
// Add new endpoint method after GetByIdAsync:

    [HttpGet("{id:guid}/debug")]
    [ProducesResponseType(typeof(BacktestDebugResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDebugDataAsync(
        Guid id,
        [FromQuery][Required] string cycleId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new GetBacktestDebugQuery(id, cycleId),
            cancellationToken);

        return result is not null ? Ok(result) : NoContent();
    }
```

Add required usings at top of controller file:

```csharp
using TradePilot.Application.Backtesting.Models;
using System.ComponentModel.DataAnnotations;
```

##### Pattern References

- `src/TradePilot.Api/Controllers/BacktestsController.cs` — existing `GetByIdAsync` pattern with `{id:guid}` route
- `src/TradePilot.Api/Infrastructure/Filters/HttpGlobalExceptionFilter.cs` — `NotFoundException` → 404 mapping (no inline 404 needed)

---

### Task 4.4: Add EnableAuditLog to RunBacktestRequest {#task-44-add-enableauditlog-to-runbacktestrequest}

Add `EnableAuditLog` boolean property to the API request DTO. Default to `true`. Update `RunBacktestCommand` and `RunBacktestCommandHandler` to pass the flag through to entity creation.

- **Complexity**: Low
- **Risk Factors**: None — additive property with default
- **Files**:
  - `src/TradePilot.Api/Models/RunBacktestRequest.cs` — modification
  - `src/TradePilot.Application/Backtesting/RunBacktestCommand.cs` — modification
- **Success**:
  - `RunBacktestRequest.EnableAuditLog` defaults to `true`
  - Flag is passed through command → handler → `BacktestRun.CreateQueued`
- **Dependencies**: Task 2.1

#### Implementation Details

```csharp
// src/TradePilot.Api/Models/RunBacktestRequest.cs — modification
// Add after existing StrategyConfig property:

    public bool EnableAuditLog { get; set; } = true;
```

```csharp
// src/TradePilot.Application/Backtesting/RunBacktestCommand.cs — modification
// Update the record to include EnableAuditLog:

// In the command record — add EnableAuditLog parameter
// In the handler — pass EnableAuditLog to BacktestRun.CreateQueued

// The exact change depends on the current command structure. Key pattern:
// var backtestRun = BacktestRun.CreateQueued(
//     ... existing params ...,
//     auditLogEnabled: command.EnableAuditLog);
```

##### Pattern References

- `src/TradePilot.Api/Models/RunBacktestRequest.cs` — existing request DTO with default values
- `src/TradePilot.Application/Backtesting/RunBacktestCommand.cs` — existing command → handler → entity creation flow

---

### Task 4.5: Controller tests for debug endpoint {#task-45-controller-tests-for-debug-endpoint}

Add controller tests covering: successful debug data retrieval, 204 for no debug data, 404 for missing backtest.

- **Complexity**: Medium
- **Risk Factors**: Need to persist a backtest run with debug data for the success case
- **Files**:
  - `tests/TradePilot.Api.Tests/Controllers/BacktestsControllerTests.cs` — modification
- **Success**:
  - Test: GET debug with valid ID and cycleId → 200 with debug response
  - Test: GET debug for backtest without audit data → 204
  - Test: GET debug for non-existent ID → 404
  - All tests pass: `dotnet test tests/TradePilot.Api.Tests --filter "FullyQualifiedName~BacktestsControllerTests"`
- **Dependencies**: Tasks 4.1–4.4

#### Implementation Details

```csharp
// tests/TradePilot.Api.Tests/Controllers/BacktestsControllerTests.cs — modification
// Add new test methods:

    [TestMethod]
    public async Task GivenBacktestWithAuditData_WhenGetDebug_ThenReturns200WithData()
    {
        var backtestRun = CreateBacktestRunWithAuditData();
        _backtestRunRepositoryMock
            .Setup(r => r.GetByIdAsync(backtestRun.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(backtestRun);

        using var client = GetTestClient();
        var response = await client.GetAsync($"/api/backtests/{backtestRun.Id}/debug?cycleId=cycle-1");

        var result = await response.ReadAndAssertSuccessAsync<BacktestDebugResponse>();
        result.CycleId.Should().Be("cycle-1");
    }

    [TestMethod]
    public async Task GivenBacktestWithoutAuditData_WhenGetDebug_ThenReturns204()
    {
        var backtestRun = CreateBacktestRunWithoutAuditData();
        _backtestRunRepositoryMock
            .Setup(r => r.GetByIdAsync(backtestRun.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(backtestRun);

        using var client = GetTestClient();
        var response = await client.GetAsync($"/api/backtests/{backtestRun.Id}/debug?cycleId=cycle-1");

        response.AssertStatusCode(HttpStatusCode.NoContent);
    }

    [TestMethod]
    public async Task GivenNonExistentBacktest_WhenGetDebug_ThenReturns404()
    {
        var id = Guid.NewGuid();
        _backtestRunRepositoryMock
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BacktestRun?)null);

        using var client = GetTestClient();
        var response = await client.GetAsync($"/api/backtests/{id}/debug?cycleId=cycle-1");

        response.AssertStatusCode(HttpStatusCode.NotFound);
    }

    // Helper method:
    private static BacktestRun CreateBacktestRunWithAuditData()
    {
        var run = BacktestRun.CreateQueued(
            "BTC", "[\"15m\",\"1h\",\"4h\"]", 1000, 2000,
            "{\"gridLevels\":5}", 10000m, auditLogEnabled: true);
        run.MarkRunning(100);
        run.MarkCompleted(
            100, 5000, 1, 1, 0, 1m, 10m, 5m, 10m, 60, 0, 1m,
            "[]", "[]",
            candleLogJson: "[{\"timestampUtc\":1000,\"gridCycleId\":\"cycle-1\"}]",
            orderEventLogJson: "[{\"timestampUtc\":1000,\"gridCycleId\":\"cycle-1\"}]",
            gridCycleLogJson: "[{\"gridCycleId\":\"cycle-1\"}]");
        return run;
    }

    private static BacktestRun CreateBacktestRunWithoutAuditData()
    {
        var run = BacktestRun.CreateQueued(
            "BTC", "[\"15m\",\"1h\",\"4h\"]", 1000, 2000,
            "{\"gridLevels\":5}", 10000m, auditLogEnabled: false);
        run.MarkRunning(100);
        run.MarkCompleted(
            100, 5000, 0, 0, 0, 0m, 0m, 0m, 0m, 0, 0, 0m,
            "[]", "[]");
        return run;
    }
```

Note: These tests follow the established `BacktestsControllerTests` pattern — inheriting from `BaseControllerTests`, using `WebApplicationFactory<Program>` with `ConfigureTestServices` for service mock injection, and calling HTTP endpoints. The mock setup shown above is pseudocode — register the mock via `ConfigureTestServices` as done for other repository mocks in the existing test class. The actual JSON deserialization in the handler will parse the stored JSON blobs, so the helper methods must provide valid JSON matching the model schemas.

##### Pattern References

- `tests/TradePilot.Api.Tests/Controllers/BacktestsControllerTests.cs` — existing test class with `ReadAndAssertSuccessAsync<T>()` and `AssertStatusCode()` patterns
- `tests/TradePilot.Api.Tests/Infrastructure/BaseControllerTests.cs` — `WebApplicationFactory` + service mock injection

## Phase Success Criteria

- `GET /api/backtests/{id}/debug?cycleId={cycleId}` returns 200 with filtered debug data
- Endpoint returns 204 when audit data is null
- Endpoint returns 404 when backtest run does not exist
- `EnableAuditLog` flows from API request → command → entity
- All controller tests pass: `dotnet test tests/TradePilot.Api.Tests`
- All existing tests still pass
