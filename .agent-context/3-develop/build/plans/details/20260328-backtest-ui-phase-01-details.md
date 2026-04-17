<!-- markdownlint-disable-file -->

# Task Details: Backtest UI Dashboard (F5)

## Phase 1: Backend — Paginated List Endpoint

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — sealed classes, required init, CancellationToken, argument guards
- `.github/instructions/api-controllers.instructions.md` — ApiController base, MediatR Send(), ProducesResponseType, Envelope errors
- `.github/instructions/dotnet-architecture.instructions.md` — CQRS query/handler co-location, repository interface in Application.Abstractions
- `.github/instructions/testing.instructions.md` — MSTest, Moq, FluentAssertions, GivenWhenThen naming, BaseControllerTests
- `.agent-context/0-knowledge/18-backtesting-architecture.md` — BacktestResult model, API endpoints

## Design References

- F4 is assumed complete: `BacktestsController`, `BacktestRun` entity, `IBacktestRunRepository` with `AddAsync`/`GetByIdAsync`, and POST/GET/{id}/validate endpoints all exist
- The new `GET /api/backtests` list endpoint is the only backend addition in F5

### Task 1.1: Create PagedResult<T> generic model {#task-11-create-pagedresult-generic-model}

Create a reusable generic pagination response model that can be used across the application.

- **Complexity**: Low
- **Risk Factors**: None — simple data class
- **Files**:
  - `src/TradePilot.Application/Abstractions/Models/PagedResult.cs` — new file
- **Success**:
  - `PagedResult<T>` class exists with Items, Page, PageSize, TotalCount, TotalPages properties
  - Compiles without errors

#### Implementation Details

```csharp
// src/TradePilot.Application/Abstractions/Models/PagedResult.cs — new file
namespace TradePilot.Application.Abstractions.Models;

public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required int TotalCount { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
```

##### Pattern References

- `src/TradePilot.Application/Backtesting/Models/BacktestResult.cs` — `required init` property pattern, `IReadOnlyList<T>` usage

---

### Task 1.2: Create BacktestSummaryDto {#task-12-create-backtestsummarydto}

Create a summary DTO for the list endpoint response that excludes the full trade log and equity time series.

- **Complexity**: Low
- **Risk Factors**: None — simple DTO
- **Files**:
  - `src/TradePilot.Api/Models/BacktestSummaryDto.cs` — new file
- **Success**:
  - DTO contains id, symbol, intervals, startDate, endDate, totalTrades, winRate, totalPnl, maxDrawdown, createdAt
  - Compiles without errors

#### Implementation Details

```csharp
// src/TradePilot.Api/Models/BacktestSummaryDto.cs — new file
namespace TradePilot.Api.Models;

public sealed class BacktestSummaryDto
{
    public required Guid Id { get; init; }
    public required string Symbol { get; init; }
    public required IReadOnlyList<string> Intervals { get; init; }
    public required DateTime StartDate { get; init; }
    public required DateTime EndDate { get; init; }
    public required int TotalTrades { get; init; }
    public required decimal WinRate { get; init; }
    public required decimal TotalPnl { get; init; }
    public required decimal MaxDrawdown { get; init; }
    public required DateTime CreatedAt { get; init; }
}
```

##### Pattern References

- `src/TradePilot.Api/Models/PlaceOrderRequest.cs` — API model pattern with `required` properties
- `src/TradePilot.Application/Backtesting/Models/BacktestResult.cs` — field naming

---

### Task 1.3: Create GetBacktestListQuery and handler {#task-13-create-getbacktestlistquery-and-handler}

Create a MediatR query and handler that retrieves paginated backtest summaries from the repository.

- **Complexity**: Medium
- **Risk Factors**: Must ensure the repository method for paginated retrieval exists (assumed from F4) or add it
- **Files**:
  - `src/TradePilot.Application/Backtesting/Queries/GetBacktestListQuery.cs` — new file
  - `src/TradePilot.Application/Backtesting/Models/BacktestRunSummary.cs` — new file (model class should NOT be co-located in query file per C# standards)
  - `src/TradePilot.Application/Abstractions/Repositories/IBacktestRunRepository.cs` — modification, assumed to exist from F4 (add GetPagedAsync method if not present)
  - `src/TradePilot.Persistence/Repositories/BacktestRunRepository.cs` — modification, assumed to exist from F4 (add GetPagedAsync implementation if not present)
- **Success**:
  - `GetBacktestListQuery` record with Page and PageSize parameters
  - Handler queries repository and returns `PagedResult<BacktestSummaryDto>`
  - Compiles without errors
- **Dependencies**:
  - Task 1.1 (PagedResult<T>)
  - Task 1.2 (BacktestSummaryDto)

#### Implementation Details

```csharp
// src/TradePilot.Application/Backtesting/Queries/GetBacktestListQuery.cs — new file
using TradePilot.Application.Abstractions.Models;
using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Repositories;

namespace TradePilot.Application.Backtesting.Queries;

public sealed record GetBacktestListQuery(int Page, int PageSize) : Query<PagedResult<BacktestRunSummary>>;

// NOTE: BacktestRunSummary is in a separate file:
// src/TradePilot.Application/Backtesting/Models/BacktestRunSummary.cs

```

```csharp
// src/TradePilot.Application/Backtesting/Models/BacktestRunSummary.cs — new file
namespace TradePilot.Application.Backtesting.Models;

public sealed class BacktestRunSummary
{
    public required Guid Id { get; init; }
    public required string Symbol { get; init; }
    public required IReadOnlyList<string> Intervals { get; init; }
    public required DateTime StartDate { get; init; }
    public required DateTime EndDate { get; init; }
    public required int TotalTrades { get; init; }
    public required decimal WinRate { get; init; }
    public required decimal TotalPnl { get; init; }
    public required decimal MaxDrawdown { get; init; }
    public required DateTime CreatedAt { get; init; }
}
```

```csharp
public sealed class GetBacktestListQueryHandler : QueryHandler<GetBacktestListQuery, PagedResult<BacktestRunSummary>>
{
    private readonly IBacktestRunRepository _repository;

    public GetBacktestListQueryHandler(IBacktestRunRepository repository)
    {
        _repository = repository;
    }

    public override async Task<PagedResult<BacktestRunSummary>> Handle(
        GetBacktestListQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(request.Page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.PageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(request.PageSize, 100);

        return await _repository.GetPagedSummariesAsync(request.Page, request.PageSize, cancellationToken);
    }
}
```

If `GetPagedSummariesAsync` doesn't exist on `IBacktestRunRepository`, add it:

```csharp
// src/TradePilot.Application/Abstractions/Repositories/IBacktestRunRepository.cs — modification
// Add to interface:
Task<PagedResult<BacktestRunSummary>> GetPagedSummariesAsync(int page, int pageSize, CancellationToken cancellationToken = default);
```

```csharp
// src/TradePilot.Persistence/Repositories/BacktestRunRepository.cs — modification
// Add implementation:
public async Task<PagedResult<BacktestRunSummary>> GetPagedSummariesAsync(
    int page, int pageSize, CancellationToken cancellationToken = default)
{
    var totalCount = await _context.BacktestRuns.CountAsync(cancellationToken);

    var items = await _context.BacktestRuns
        .OrderByDescending(r => r.CreatedAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(r => new BacktestRunSummary
        {
            Id = r.Id,
            Symbol = r.Symbol,
            Intervals = r.Intervals,
            StartDate = r.StartDate,
            EndDate = r.EndDate,
            TotalTrades = r.TotalTrades,
            WinRate = r.WinRate,
            TotalPnl = r.TotalPnl,
            MaxDrawdown = r.MaxDrawdown,
            CreatedAt = r.CreatedAt
        })
        .ToListAsync(cancellationToken);

    return new PagedResult<BacktestRunSummary>
    {
        Items = items,
        Page = page,
        PageSize = pageSize,
        TotalCount = totalCount
    };
}
```

##### Pattern References

- `src/TradePilot.Application/MarketData/Queries/GetCandlesQuery.cs` — query + handler co-location pattern, argument guards
- `src/TradePilot.Application/Abstractions/Queries/Query.cs` — `Query<T>` / `QueryHandler<TQuery, TResult>` base classes
- `src/TradePilot.Persistence/Repositories/CandleRepository.cs` — EF Core repository implementation pattern

---

### Task 1.4: Add list endpoint to BacktestsController {#task-14-add-list-endpoint-to-backtestscontroller}

Add the `GET /api/backtests` action to the existing BacktestsController (created as part of F4).

- **Complexity**: Low
- **Risk Factors**: Route ordering — `GET /api/backtests` must not conflict with `GET /api/backtests/{id}` or `GET /api/backtests/validate`. ASP.NET Core routes `GET` without parameters first, then specific string segments, then parameterised — no conflict expected.
- **Files**:
  - `src/TradePilot.Api/Controllers/BacktestsController.cs` — modification, assumed to exist from F4
- **Success**:
  - `GET /api/backtests?page=1&pageSize=20` endpoint returns 200 with `PagedResult<BacktestSummaryDto>`
  - ProducesResponseType attributes present
  - Compiles without errors
- **Dependencies**:
  - Task 1.2 (BacktestSummaryDto)
  - Task 1.3 (GetBacktestListQuery)

#### Implementation Details

```csharp
// src/TradePilot.Api/Controllers/BacktestsController.cs — modification
// Add this action method to the existing controller class:

[HttpGet]
[ProducesResponseType(typeof(PagedResult<BacktestSummaryDto>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetBacktestsAsync(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken cancellationToken = default)
{
    var result = await Mediator.Send(new GetBacktestListQuery(page, pageSize), cancellationToken);

    var dto = new PagedResult<BacktestSummaryDto>
    {
        Items = result.Items.Select(r => new BacktestSummaryDto
        {
            Id = r.Id,
            Symbol = r.Symbol,
            Intervals = r.Intervals,
            StartDate = r.StartDate,
            EndDate = r.EndDate,
            TotalTrades = r.TotalTrades,
            WinRate = r.WinRate,
            TotalPnl = r.TotalPnl,
            MaxDrawdown = r.MaxDrawdown,
            CreatedAt = r.CreatedAt
        }).ToList(),
        Page = result.Page,
        PageSize = result.PageSize,
        TotalCount = result.TotalCount
    };

    return Ok(dto);
}
```

##### Pattern References

- `src/TradePilot.Api/Controllers/CandlesController.cs` — controller action pattern with MediatR.Send(), ProducesResponseType

---

### Task 1.5: Add controller integration tests {#task-15-add-controller-integration-tests}

Add integration tests for the new GET /api/backtests endpoint using the existing BaseControllerTests pattern.

- **Complexity**: Medium
- **Risk Factors**: Must mock IBacktestRunRepository to return test data
- **Files**:
  - `tests/TradePilot.Api.Tests/Controllers/BacktestsControllerTests.cs` — new file (or add to existing if F4 created it)
- **Success**:
  - Tests cover: empty list, paginated results, page & pageSize parameters, invalid page values
  - All tests pass
- **Dependencies**:
  - Task 1.4 (controller endpoint)

#### Implementation Details

```csharp
// tests/TradePilot.Api.Tests/Controllers/BacktestsControllerTests.cs — new file or modification
using TradePilot.Application.Abstractions.Models;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Backtesting.Queries;

namespace TradePilot.Api.Tests.Controllers;

[TestClass]
public sealed class BacktestsControllerListTests : BaseControllerTests
{
    private const string BaseUrl = "api/backtests";
    private readonly Mock<IBacktestRunRepository> _repositoryMock = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<IBacktestRunRepository>();
        services.AddSingleton(_repositoryMock.Object);
    }

    [TestMethod]
    public async Task GivenNoBacktests_WhenGetBacktests_ThenReturnsEmptyPage()
    {
        _repositoryMock
            .Setup(r => r.GetPagedSummariesAsync(1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<BacktestRunSummary>
            {
                Items = [],
                Page = 1,
                PageSize = 20,
                TotalCount = 0
            });

        var client = GetTestClient();
        var response = await client.GetAsync($"{BaseUrl}?page=1&pageSize=20");

        await response.ReadAndAssertSuccessAsync<PagedResult<BacktestRunSummary>>();
    }

    [TestMethod]
    public async Task GivenBacktestsExist_WhenGetBacktests_ThenReturnsPagedSummaries()
    {
        var summary = new BacktestRunSummary
        {
            Id = Guid.NewGuid(),
            Symbol = "BTC",
            Intervals = ["15m", "1h"],
            StartDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            TotalTrades = 100,
            WinRate = 65.5m,
            TotalPnl = 1500.50m,
            MaxDrawdown = -500.25m,
            CreatedAt = DateTime.UtcNow
        };

        _repositoryMock
            .Setup(r => r.GetPagedSummariesAsync(1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<BacktestRunSummary>
            {
                Items = [summary],
                Page = 1,
                PageSize = 20,
                TotalCount = 1
            });

        var client = GetTestClient();
        var response = await client.GetAsync($"{BaseUrl}?page=1&pageSize=20");

        var result = await response.ReadAndAssertSuccessAsync<PagedResult<BacktestRunSummary>>();
        result.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
    }

    [TestMethod]
    public async Task GivenDefaultParams_WhenGetBacktests_ThenUsesPage1Size20()
    {
        _repositoryMock
            .Setup(r => r.GetPagedSummariesAsync(1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<BacktestRunSummary>
            {
                Items = [],
                Page = 1,
                PageSize = 20,
                TotalCount = 0
            });

        var client = GetTestClient();
        var response = await client.GetAsync(BaseUrl);

        await response.ReadAndAssertSuccessAsync<PagedResult<BacktestRunSummary>>();
        _repositoryMock.Verify(r => r.GetPagedSummariesAsync(1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

##### Pattern References

- `tests/TradePilot.Api.Tests/Controllers/CandlesControllerTests.cs` — BaseControllerTests inheritance, mock setup, ReadAndAssertSuccessAsync
- `tests/TradePilot.Api.Tests/Infrastructure/BaseControllerTests.cs` — test infrastructure pattern

---

### Task 1.6: Build solution and run all tests {#task-16-build-solution-and-run-all-tests}

Build the full solution and run all backend tests to verify no regressions.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None — verification step
- **Success**:
  - `dotnet build TradePilot.sln` succeeds with no errors
  - `dotnet test` passes all existing + new tests
- **Dependencies**:
  - All previous tasks in Phase 1

## Phase Success Criteria

- `GET /api/backtests?page=1&pageSize=20` returns paginated backtest summaries
- `PagedResult<T>` is reusable across the application
- All backend tests pass including new integration tests
- Solution builds without errors
