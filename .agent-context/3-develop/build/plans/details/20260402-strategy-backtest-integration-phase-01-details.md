<!-- markdownlint-disable-file -->

# Task Details: F3.5 — Strategy–Backtest Integration

## Phase 1: Backend — Domain, Persistence & Tests

## Standards and Knowledge References

- **csharp.instructions.md**: `sealed` classes, private constructors + `Create`/`CreateQueued` factories, `ArgumentException` guards, `PascalCase` for public properties, `_camelCase` for private fields
- **dotnet-architecture.instructions.md**: Repository interfaces in `Application/Abstractions/Repositories/`, EF implementations in `Persistence/Repositories/`, migrations via `dotnet ef migrations add`
- **testing.instructions.md**: MSTest + Moq + FluentAssertions ≤ v6, `Given_When_Then` naming, builder pattern for entity creation, tests within phases
- **04-domain-model.md**: `BacktestRun` is not tenant-scoped, `Strategy` is tenant-scoped by `UserId`
- **18-backtesting-architecture.md**: `BacktestRun.StrategyConfigJson` is a snapshot — preserved alongside FK

### Task 1.1: Add Strategy Fields to BacktestRun Entity {#task-11-add-strategy-fields-to-backtest-run-entity}

Add two nullable properties to `BacktestRun` for linking to the source strategy and its revision at the time of the backtest.

- **Complexity**: Low
- **Risk Factors**: None — additive change to existing entity
- **Files**:
  - `src/TradePilot.Domain/Entities/BacktestRun.cs` — Add two new properties
- **Success**:
  - `BacktestRun` has `StrategyId` (Guid?) and `StrategyRevisionId` (int?) properties
  - Properties have private setters matching existing convention
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradePilot.Domain/Entities/BacktestRun.cs — modification
// Add after the existing CreatedAtUtc property (line ~37):

    public long CreatedAtUtc { get; private set; }
    public Guid? StrategyId { get; private set; }
    public int? StrategyRevisionId { get; private set; }
```

##### Pattern References

Based on `src/TradePilot.Domain/Entities/BacktestRun.cs` — follows existing private setter convention.

### Task 1.2: Update CreateQueued Factory {#task-12-update-createqueued-factory}

Add optional `strategyId` and `strategyRevisionId` parameters to the `CreateQueued` factory method.

- **Complexity**: Low
- **Risk Factors**: Default values preserve backward compatibility
- **Files**:
  - `src/TradePilot.Domain/Entities/BacktestRun.cs` — Update `CreateQueued` signature and body
- **Success**:
  - `CreateQueued` accepts optional `Guid? strategyId = null` and `int? strategyRevisionId = null`
  - New properties assigned inside factory
- **Dependencies**: Task 1.1

#### Implementation Details

```csharp
// src/TradePilot.Domain/Entities/BacktestRun.cs — modification
// Update CreateQueued signature to add optional params at end:

    public static BacktestRun CreateQueued(
        string symbol,
        string intervalsJson,
        long startDateUtc,
        long endDateUtc,
        string strategyConfigJson,
        string executionConfigJson,
        decimal initialCapital,
        bool auditLogEnabled = true,
        Guid? strategyId = null,
        int? strategyRevisionId = null)
    {
        // ... existing argument guards ...

        return new BacktestRun
        {
            // ... existing property assignments ...
            CreatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            StrategyId = strategyId,
            StrategyRevisionId = strategyRevisionId,
        };
    }
```

##### Pattern References

Based on `src/TradePilot.Domain/Entities/BacktestRun.cs` lines 44–83 — existing `CreateQueued` factory.

### Task 1.3: Update EF DbContext Configuration {#task-13-update-ef-dbcontext-configuration}

Add EF configuration for the new `StrategyId` and `StrategyRevisionId` columns in the `BacktestRun` entity block within `OnModelCreating`.

- **Complexity**: Low
- **Risk Factors**: Must match SQLite type conventions (Guid as TEXT, int as INTEGER)
- **Files**:
  - `src/TradePilot.Persistence/TradePilotDbContext.cs` — Add property config + index inside `BacktestRun` entity block
- **Success**:
  - `StrategyId` and `StrategyRevisionId` configured as nullable columns
  - Index on `StrategyId` for query performance
  - No FK constraint (backtest history survives strategy soft-deletion)
- **Dependencies**: Task 1.1

#### Implementation Details

```csharp
// src/TradePilot.Persistence/TradePilotDbContext.cs — modification
// Add at end of modelBuilder.Entity<BacktestRun>(entity => { ... }) block:

            entity.Property(b => b.StrategyId);
            entity.Property(b => b.StrategyRevisionId);
            entity.HasIndex(b => b.StrategyId)
                .HasDatabaseName("IX_BacktestRuns_StrategyId");
```

##### Pattern References

Based on `src/TradePilot.Persistence/TradePilotDbContext.cs` — inline `OnModelCreating` config convention, no separate `IEntityTypeConfiguration<T>` files.

### Task 1.4: Add EF Migration {#task-14-add-ef-migration}

Generate EF Core migration to add the two new columns to the `BacktestRuns` table.

- **Complexity**: Low
- **Risk Factors**: Migration naming must follow `yyyyMMddHHmmss_PascalCase` convention
- **Files**:
  - `src/TradePilot.Persistence/Migrations/` — New migration file auto-generated
- **Success**:
  - Migration adds nullable `StrategyId` (TEXT) and `StrategyRevisionId` (INTEGER) columns to `BacktestRuns`
  - Migration adds index `IX_BacktestRuns_StrategyId`
  - `dotnet ef database update` succeeds
- **Dependencies**: Tasks 1.1, 1.3

#### Implementation Details

Run from the `src/TradePilot.Persistence` directory:

```bash
dotnet ef migrations add AddStrategyLinkToBacktestRuns --startup-project ../TradePilot.Api
```

Verify the generated migration contains:
- `migrationBuilder.AddColumn<Guid>("StrategyId", "BacktestRuns", nullable: true)`
- `migrationBuilder.AddColumn<int>("StrategyRevisionId", "BacktestRuns", nullable: true)`
- `migrationBuilder.CreateIndex("IX_BacktestRuns_StrategyId", "BacktestRuns", "StrategyId")`

### Task 1.5: Add Strategy-Scoped Repository Method {#task-15-add-strategy-scoped-repository-method}

Add `GetPagedSummariesByStrategyAsync` to `IBacktestRunRepository` interface and its EF implementation.

- **Complexity**: Medium
- **Risk Factors**: Must include `StrategyId`/`StrategyRevisionId` in projection; must match existing projection pattern
- **Files**:
  - `src/TradePilot.Application/Abstractions/Repositories/IBacktestRunRepository.cs` — Add new method signature
  - `src/TradePilot.Persistence/Repositories/BacktestRunRepository.cs` — Add implementation with strategy filter
- **Success**:
  - New method filters by `strategyId` and returns paged summaries with strategy fields
  - Projection includes `StrategyId` and `StrategyRevisionId`
- **Dependencies**: Tasks 1.1, 1.6

#### Implementation Details

```csharp
// src/TradePilot.Application/Abstractions/Repositories/IBacktestRunRepository.cs — modification
// Add new method to interface:

    Task<PagedResult<BacktestRunSummary>> GetPagedSummariesByStrategyAsync(
        Guid strategyId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
```

```csharp
// src/TradePilot.Persistence/Repositories/BacktestRunRepository.cs — modification
// Add new method implementation after GetPagedSummariesAsync:

    public async Task<PagedResult<BacktestRunSummary>> GetPagedSummariesByStrategyAsync(
        Guid strategyId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        var query = _context.BacktestRuns
            .AsNoTracking()
            .Where(backtestRun => backtestRun.StrategyId == strategyId);

        var totalCount = await query.CountAsync(cancellationToken);

        var projections = await query
            .OrderByDescending(backtestRun => backtestRun.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(backtestRun => new
            {
                backtestRun.Id,
                backtestRun.Symbol,
                backtestRun.IntervalsJson,
                backtestRun.StartDateUtc,
                backtestRun.EndDateUtc,
                backtestRun.TotalTrades,
                backtestRun.WinRate,
                backtestRun.TotalPnl,
                backtestRun.MaxDrawdown,
                backtestRun.CreatedAtUtc,
                backtestRun.StrategyId,
                backtestRun.StrategyRevisionId,
            })
            .ToListAsync(cancellationToken);

        var items = projections.Select(backtestRun => new BacktestRunSummary
        {
            Id = backtestRun.Id,
            Symbol = backtestRun.Symbol,
            Intervals = JsonSerializer.Deserialize<string[]>(backtestRun.IntervalsJson, JsonOptions) ?? [],
            StartDate = DateTimeOffset.FromUnixTimeMilliseconds(backtestRun.StartDateUtc).UtcDateTime,
            EndDate = DateTimeOffset.FromUnixTimeMilliseconds(backtestRun.EndDateUtc).UtcDateTime,
            TotalTrades = backtestRun.TotalTrades,
            WinRate = backtestRun.WinRate,
            TotalPnl = backtestRun.TotalPnl,
            MaxDrawdown = backtestRun.MaxDrawdown,
            CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(backtestRun.CreatedAtUtc).UtcDateTime,
            StrategyId = backtestRun.StrategyId,
            StrategyRevisionId = backtestRun.StrategyRevisionId,
        }).ToList();

        return new PagedResult<BacktestRunSummary>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }
```

##### Pattern References

Based on `src/TradePilot.Persistence/Repositories/BacktestRunRepository.cs` — existing `GetPagedSummariesAsync` method.

### Task 1.6: Update BacktestRunSummary {#task-16-update-backtest-run-summary}

Add strategy metadata fields to `BacktestRunSummary` so they can be projected from the repository.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradePilot.Application/Backtesting/Models/BacktestRunSummary.cs` — Add 3 new properties
- **Success**:
  - `BacktestRunSummary` includes `StrategyId`, `StrategyRevisionId`, and `StrategyName`
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradePilot.Application/Backtesting/Models/BacktestRunSummary.cs — modification
// Add after existing CreatedAt property:

    public Guid? StrategyId { get; init; }
    public int? StrategyRevisionId { get; init; }
    public string? StrategyName { get; init; }
```

> **Note**: `StrategyName` is **not** stored on `BacktestRun` and cannot be projected from the repository. It will always be `null` from persistence-layer queries. The API layer (Tasks 2.7/2.8) is responsible for enriching `StrategyName` via a strategy lookup.

##### Pattern References

Based on `src/TradePilot.Application/Backtesting/Models/BacktestRunSummary.cs` — existing `required` init property pattern. New fields are nullable (not `required`) since they are optional.

### Task 1.7: Add Domain and Persistence Tests {#task-17-add-domain-and-persistence-tests}

Add unit tests for the updated `BacktestRun` entity and persistence tests for the new repository method.

- **Complexity**: Medium
- **Risk Factors**: Must follow MSTest + FluentAssertions conventions
- **Files**:
  - `tests/TradePilot.Domain.Tests/Entities/BacktestRunTests.cs` — New test class for `BacktestRun` entity (new file)
  - `tests/TradePilot.Persistence.Tests/Repositories/BacktestRunRepositoryTests.cs` — Add tests for `GetPagedSummariesByStrategyAsync` (new file or append to existing)
- **Success**:
  - Domain tests verify `CreateQueued` sets `StrategyId`/`StrategyRevisionId` when provided and null when not
  - Persistence tests verify `GetPagedSummariesByStrategyAsync` filters correctly by `StrategyId`
  - All tests pass
- **Dependencies**: Tasks 1.1–1.6

#### Implementation Details

```csharp
// tests/TradePilot.Domain.Tests/Entities/BacktestRunTests.cs — new file

using FluentAssertions;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;

namespace TradePilot.Domain.Tests.Entities;

[TestClass]
public sealed class BacktestRunTests
{
    [TestMethod]
    public void GivenNoStrategyId_WhenCreateQueued_ThenStrategyIdIsNull()
    {
        var run = BacktestRun.CreateQueued(
            symbol: "BTC-USD",
            intervalsJson: "[\"15m\"]",
            startDateUtc: 1000,
            endDateUtc: 2000,
            strategyConfigJson: "{}",
            executionConfigJson: "{}",
            initialCapital: 10000m);

        run.StrategyId.Should().BeNull();
        run.StrategyRevisionId.Should().BeNull();
    }

    [TestMethod]
    public void GivenStrategyId_WhenCreateQueued_ThenStrategyFieldsAreSet()
    {
        var strategyId = Guid.NewGuid();

        var run = BacktestRun.CreateQueued(
            symbol: "BTC-USD",
            intervalsJson: "[\"15m\"]",
            startDateUtc: 1000,
            endDateUtc: 2000,
            strategyConfigJson: "{}",
            executionConfigJson: "{}",
            initialCapital: 10000m,
            strategyId: strategyId,
            strategyRevisionId: 3);

        run.StrategyId.Should().Be(strategyId);
        run.StrategyRevisionId.Should().Be(3);
    }

    [TestMethod]
    public void GivenValidParams_WhenCreateQueued_ThenStatusIsQueued()
    {
        var run = BacktestRun.CreateQueued(
            symbol: "ETH-USD",
            intervalsJson: "[\"1h\"]",
            startDateUtc: 1000,
            endDateUtc: 2000,
            strategyConfigJson: "{}",
            executionConfigJson: "{}",
            initialCapital: 5000m);

        run.Status.Should().Be(BacktestStatus.Queued);
        run.Id.Should().NotBeEmpty();
    }
}
```

```csharp
// tests/TradePilot.Persistence.Tests/Repositories/BacktestRunRepositoryTests.cs — modification
// Add new test methods for GetPagedSummariesByStrategyAsync:

    [TestMethod]
    public async Task GivenRunsWithStrategyId_WhenGetPagedSummariesByStrategy_ThenReturnsOnlyMatchingRuns()
    {
        var strategyId = Guid.NewGuid();
        var otherStrategyId = Guid.NewGuid();

        var run1 = BacktestRun.CreateQueued("BTC-USD", "[\"15m\"]", 1000, 2000, "{}", "{}", 10000m,
            strategyId: strategyId, strategyRevisionId: 1);
        var run2 = BacktestRun.CreateQueued("BTC-USD", "[\"15m\"]", 1000, 2000, "{}", "{}", 10000m,
            strategyId: strategyId, strategyRevisionId: 2);
        var run3 = BacktestRun.CreateQueued("ETH-USD", "[\"1h\"]", 1000, 2000, "{}", "{}", 5000m,
            strategyId: otherStrategyId, strategyRevisionId: 1);

        await using (var writeContext = CreateContext())
        {
            writeContext.BacktestRuns.AddRange(run1, run2, run3);
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = CreateContext();
        var repository = new BacktestRunRepository(readContext);
        var result = await repository.GetPagedSummariesByStrategyAsync(strategyId, 1, 10);

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.Items.Should().AllSatisfy(s => s.StrategyId.Should().Be(strategyId));
    }
```

##### Pattern References

Based on `tests/TradePilot.Domain.Tests/Entities/StrategyTests.cs` (domain test pattern) and `tests/TradePilot.Persistence.Tests/Repositories/BacktestRunRepositoryTests.cs` (persistence test pattern with SQLite in-memory).

### Task 1.8: Run Architecture Tests {#task-18-run-architecture-tests}

Verify all domain and persistence tests pass, and ensure the solution builds cleanly.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None — verification step only
- **Success**:
  - `dotnet build` succeeds
  - `dotnet test tests/TradePilot.Domain.Tests` passes
  - `dotnet test tests/TradePilot.Persistence.Tests` passes
- **Dependencies**: Tasks 1.1–1.7

## Phase Success Criteria

- `BacktestRun` entity has `StrategyId` and `StrategyRevisionId` properties
- `CreateQueued` accepts optional strategy parameters
- EF migration adds nullable columns with index
- `IBacktestRunRepository` has `GetPagedSummariesByStrategyAsync` method
- `BacktestRunSummary` includes strategy metadata fields
- All domain and persistence tests pass
