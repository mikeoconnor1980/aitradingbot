<!-- markdownlint-disable-file -->

# Task Details: F4 — Backtest API & Results

## Phase 1: Domain Entity & Persistence

## Standards and Knowledge References

- **csharp.instructions.md**: Sealed classes, private constructors, static `Create()` factory methods, `ArgumentException` guards, `_camelCase` private fields
- **dotnet-architecture.instructions.md**: Entity pattern, repository interface in Application layer, implementation in Persistence layer, EF Core decimal→double conversion for SQLite
- **testing.instructions.md**: MSTest, Moq, FluentAssertions v6, Given_When_Then naming, SQLite in-memory for repository tests
- **Knowledge 04 (Domain Model)**: BacktestRun entity stores both run parameters and results in a single table
- **Knowledge 18 (Backtesting Architecture)**: Backtest results include trade log, equity curve, and aggregate metrics

### Task 1.1: Create `BacktestRun` domain entity {#task-11-create-backtestrun-domain-entity}

Create the domain entity that represents a persisted backtest run with both input parameters and output results.

- **Complexity**: Medium
- **Risk Factors**: Must correctly map all PBI fields; JSON blob columns for trade log and strategy config
- **Files**:
  - `src/TradePilot.Domain/Entities/BacktestRun.cs` — new file
- **Success**:
  - Entity compiles with all required properties
  - Static `Create()` factory validates required fields
  - Private constructor and private setters enforced
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradePilot.Domain/Entities/BacktestRun.cs — new file
namespace TradePilot.Domain.Entities;

public sealed class BacktestRun
{
    public Guid Id { get; private set; }
    public string Symbol { get; private set; } = string.Empty;
    public string IntervalsJson { get; private set; } = string.Empty;
    public long StartDateUtc { get; private set; }
    public long EndDateUtc { get; private set; }
    public string StrategyConfigJson { get; private set; } = string.Empty;
    public decimal InitialCapital { get; private set; }
    public int CandlesReplayed { get; private set; }
    public long ElapsedMs { get; private set; }
    public int TotalTrades { get; private set; }
    public int WinningTrades { get; private set; }
    public int LosingTrades { get; private set; }
    public decimal WinRate { get; private set; }
    public decimal TotalPnl { get; private set; }
    public decimal MaxDrawdown { get; private set; }
    public decimal AverageTradePnl { get; private set; }
    public double AverageHoldTimeMinutes { get; private set; }
    public int HedgesOpened { get; private set; }
    public decimal TotalFeesPaid { get; private set; }
    public string TradesJson { get; private set; } = string.Empty;
    public long CreatedAtUtc { get; private set; }

    private BacktestRun() { }

    public static BacktestRun Create(
        string symbol,
        string intervalsJson,
        long startDateUtc,
        long endDateUtc,
        string strategyConfigJson,
        decimal initialCapital,
        int candlesReplayed,
        long elapsedMs,
        int totalTrades,
        int winningTrades,
        int losingTrades,
        decimal winRate,
        decimal totalPnl,
        decimal maxDrawdown,
        decimal averageTradePnl,
        double averageHoldTimeMinutes,
        int hedgesOpened,
        decimal totalFeesPaid,
        string tradesJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(intervalsJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(strategyConfigJson);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialCapital);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startDateUtc, endDateUtc);

        return new BacktestRun
        {
            Id = Guid.NewGuid(),
            Symbol = symbol,
            IntervalsJson = intervalsJson,
            StartDateUtc = startDateUtc,
            EndDateUtc = endDateUtc,
            StrategyConfigJson = strategyConfigJson,
            InitialCapital = initialCapital,
            CandlesReplayed = candlesReplayed,
            ElapsedMs = elapsedMs,
            TotalTrades = totalTrades,
            WinningTrades = winningTrades,
            LosingTrades = losingTrades,
            WinRate = winRate,
            TotalPnl = totalPnl,
            MaxDrawdown = maxDrawdown,
            AverageTradePnl = averageTradePnl,
            AverageHoldTimeMinutes = averageHoldTimeMinutes,
            HedgesOpened = hedgesOpened,
            TotalFeesPaid = totalFeesPaid,
            TradesJson = tradesJson ?? "[]",
            CreatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }
}
```

##### Pattern References

- `src/TradePilot.Domain/Entities/Candle.cs` — sealed class, private constructor, static `Create()`, `ArgumentException` guards, private setters
- `src/TradePilot.Domain/Entities/FundingRate.cs` — same entity pattern

### Task 1.2: Create `IBacktestRunRepository` interface {#task-12-create-ibacktestrunrepository-interface}

Create the repository interface in the Application layer abstractions.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradePilot.Application/Abstractions/Repositories/IBacktestRunRepository.cs` — new file
- **Success**:
  - Interface declares `AddAsync`, `GetByIdAsync` methods
  - Interface follows existing repository pattern
- **Dependencies**: Task 1.1

#### Implementation Details

```csharp
// src/TradePilot.Application/Abstractions/Repositories/IBacktestRunRepository.cs — new file
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Abstractions.Repositories;

public interface IBacktestRunRepository
{
    Task<BacktestRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(BacktestRun backtestRun, CancellationToken cancellationToken = default);
}
```

##### Pattern References

- `src/TradePilot.Application/Abstractions/Repositories/ICandleRepository.cs` — repository interface pattern with CancellationToken

### Task 1.3: Create `BacktestRunRepository` implementation {#task-13-create-backtestrunrepository-implementation}

Create the EF Core repository implementation in the Persistence layer.

- **Complexity**: Low
- **Risk Factors**: None — simple LINQ reads and EF add/save
- **Files**:
  - `src/TradePilot.Persistence/Repositories/BacktestRunRepository.cs` — new file
- **Success**:
  - Repository reads with `FirstOrDefaultAsync`
  - Repository adds with `AddAsync` + `SaveChangesAsync`
- **Dependencies**: Task 1.1, Task 1.2

#### Implementation Details

```csharp
// src/TradePilot.Persistence/Repositories/BacktestRunRepository.cs — new file
using Microsoft.EntityFrameworkCore;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Domain.Entities;

namespace TradePilot.Persistence.Repositories;

public sealed class BacktestRunRepository : IBacktestRunRepository
{
    private readonly TradePilotDbContext _context;

    public BacktestRunRepository(TradePilotDbContext context)
    {
        _context = context;
    }

    public async Task<BacktestRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.BacktestRuns
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task AddAsync(BacktestRun backtestRun, CancellationToken cancellationToken = default)
    {
        await _context.BacktestRuns.AddAsync(backtestRun, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
```

##### Pattern References

- `src/TradePilot.Persistence/Repositories/CandleRepository.cs` — constructor injection of `TradePilotDbContext`, LINQ queries

### Task 1.4: Update `TradePilotDbContext` with `BacktestRuns` DbSet {#task-14-update-TradePilotdbcontext-with-backtestruns-dbset}

Add the new DbSet and configure the entity mapping in the existing DbContext.

- **Complexity**: Medium
- **Risk Factors**: Must correctly configure all decimal→double conversions for SQLite; must set up JSON blob columns as TEXT
- **Files**:
  - `src/TradePilot.Persistence/TradePilotDbContext.cs` — modification
- **Success**:
  - `DbSet<BacktestRun> BacktestRuns` property exists
  - All decimal properties have `HasConversion<double>()`
  - JSON blob columns configured as TEXT with no max length constraint
  - Primary key is `Id` (GUID)
- **Dependencies**: Task 1.1

#### Implementation Details

Add the DbSet property:
```csharp
// src/TradePilot.Persistence/TradePilotDbContext.cs — modification
// Add alongside existing DbSets:
public DbSet<BacktestRun> BacktestRuns => Set<BacktestRun>();
```

Add entity configuration inside the existing `OnModelCreating` method (after existing `FundingRate` configuration):
```csharp
// Inside OnModelCreating, after the FundingRate configuration block:
modelBuilder.Entity<BacktestRun>(entity =>
{
    entity.HasKey(e => e.Id);

    entity.Property(e => e.Symbol).IsRequired().HasMaxLength(20);
    entity.Property(e => e.IntervalsJson).IsRequired();
    entity.Property(e => e.StrategyConfigJson).IsRequired();
    entity.Property(e => e.TradesJson).IsRequired();

    entity.Property(e => e.InitialCapital).HasConversion<double>();
    entity.Property(e => e.WinRate).HasConversion<double>();
    entity.Property(e => e.TotalPnl).HasConversion<double>();
    entity.Property(e => e.MaxDrawdown).HasConversion<double>();
    entity.Property(e => e.AverageTradePnl).HasConversion<double>();
    entity.Property(e => e.TotalFeesPaid).HasConversion<double>();
});
```

##### Pattern References

- `src/TradePilot.Persistence/TradePilotDbContext.cs` — existing DbSet declarations, `OnModelCreating` with `HasConversion<double>()` for decimal columns, `HasKey()`, `IsRequired()`, `HasMaxLength()`

### Task 1.5: Create EF Core migration {#task-15-create-ef-core-migration}

Generate the EF Core migration for the new `BacktestRuns` table.

- **Complexity**: Low
- **Risk Factors**: Must run from correct directory; must verify migration SQL is correct
- **Files**:
  - `src/TradePilot.Persistence/Migrations/` — new migration files (auto-generated)
- **Success**:
  - Migration creates `BacktestRuns` table with all columns
  - Migration compiles and can be applied
- **Dependencies**: Task 1.4

#### Implementation Details

Run from the `src/TradePilot.Persistence/` directory:
```bash
dotnet ef migrations add AddBacktestRuns
```

Verify the generated migration creates a table with:
- `Id` TEXT PRIMARY KEY
- `Symbol` TEXT NOT NULL (max 20)
- `IntervalsJson` TEXT NOT NULL
- `StartDateUtc` INTEGER NOT NULL
- `EndDateUtc` INTEGER NOT NULL
- `StrategyConfigJson` TEXT NOT NULL
- `InitialCapital` REAL NOT NULL
- `CandlesReplayed` INTEGER NOT NULL
- `ElapsedMs` INTEGER NOT NULL
- All metric columns (TotalTrades, WinRate, TotalPnl, etc.)
- `TradesJson` TEXT NOT NULL
- `CreatedAtUtc` INTEGER NOT NULL

### Task 1.6: Register repository in DI {#task-16-register-repository-in-di}

Register the new repository in the persistence service extensions.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradePilot.Persistence/PersistenceServiceExtensions.cs` — modification
- **Success**:
  - `IBacktestRunRepository` → `BacktestRunRepository` registered as scoped
- **Dependencies**: Task 1.2, Task 1.3

#### Implementation Details

```csharp
// src/TradePilot.Persistence/PersistenceServiceExtensions.cs — modification
// Add after existing repository registrations:
services.AddScoped<IBacktestRunRepository, BacktestRunRepository>();
```

##### Pattern References

- `src/TradePilot.Persistence/PersistenceServiceExtensions.cs` — existing `AddScoped<ICandleRepository, CandleRepository>()` registration

### Task 1.7: Write `BacktestRunRepositoryTests` {#task-17-write-backtestrunrepositorytests}

Write repository tests using the SQLite in-memory pattern.

- **Complexity**: Medium
- **Risk Factors**: Must follow two-context verify pattern for writes
- **Files**:
  - `tests/TradePilot.Persistence.Tests/Repositories/BacktestRunRepositoryTests.cs` — new file
- **Success**:
  - Tests verify round-trip persistence (add → get by ID)
  - Tests verify null return for non-existent ID
  - Tests verify all property values are correctly persisted and retrieved
  - All tests pass
- **Dependencies**: Tasks 1.1–1.4

#### Implementation Details

```csharp
// tests/TradePilot.Persistence.Tests/Repositories/BacktestRunRepositoryTests.cs — new file
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TradePilot.Domain.Entities;
using TradePilot.Persistence.Repositories;

namespace TradePilot.Persistence.Tests.Repositories;

[TestClass]
public sealed class BacktestRunRepositoryTests
{
    private SqliteConnection _connection = null!;
    private DbContextOptions<TradePilotDbContext> _contextOptions = null!;

    [TestInitialize]
    public void Setup()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _contextOptions = new DbContextOptionsBuilder<TradePilotDbContext>()
            .UseSqlite(_connection)
            .Options;
        using var context = new TradePilotDbContext(_contextOptions);
        context.Database.EnsureCreated();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _connection.Dispose();
    }

    private TradePilotDbContext CreateContext() => new(_contextOptions);

    [TestMethod]
    public async Task GivenBacktestRun_WhenAddAsync_ThenCanBeRetrievedById()
    {
        // Arrange
        var backtestRun = BacktestRun.Create(
            symbol: "BTC",
            intervalsJson: "[\"15m\",\"1h\",\"4h\"]",
            startDateUtc: 1704067200000,
            endDateUtc: 1735689599000,
            strategyConfigJson: "{\"gridLevels\":10}",
            initialCapital: 10000m,
            candlesReplayed: 35040,
            elapsedMs: 12500,
            totalTrades: 847,
            winningTrades: 612,
            losingTrades: 235,
            winRate: 72.3m,
            totalPnl: 4521.87m,
            maxDrawdown: -1234.56m,
            averageTradePnl: 5.34m,
            averageHoldTimeMinutes: 245.0,
            hedgesOpened: 12,
            totalFeesPaid: 89.23m,
            tradesJson: "[]");

        // Act
        await using (var writeContext = CreateContext())
        {
            var sut = new BacktestRunRepository(writeContext);
            await sut.AddAsync(backtestRun);
        }

        // Assert
        await using var readContext = CreateContext();
        var readSut = new BacktestRunRepository(readContext);
        var result = await readSut.GetByIdAsync(backtestRun.Id);

        result.Should().NotBeNull();
        result!.Symbol.Should().Be("BTC");
        result.TotalTrades.Should().Be(847);
        result.WinRate.Should().BeApproximately(72.3m, 0.01m);
        result.TotalPnl.Should().BeApproximately(4521.87m, 0.01m);
        result.MaxDrawdown.Should().BeApproximately(-1234.56m, 0.01m);
        result.TradesJson.Should().Be("[]");
    }

    [TestMethod]
    public async Task GivenNonExistentId_WhenGetByIdAsync_ThenReturnsNull()
    {
        await using var context = CreateContext();
        var sut = new BacktestRunRepository(context);

        var result = await sut.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }
}
```

##### Pattern References

- `tests/TradePilot.Persistence.Tests/Repositories/CandleRepositoryTests.cs` — SQLite in-memory setup, two-context verify, `[TestInitialize]`/`[TestCleanup]`

### Task 1.8: Build solution and run all tests {#task-18-build-solution-and-run-all-tests}

Verify the entire solution builds and all tests pass.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification only)
- **Success**:
  - `dotnet build TradePilot.sln` succeeds with no errors
  - `dotnet test` on all test projects passes
- **Dependencies**: All prior tasks in Phase 1

## Phase Success Criteria

- `BacktestRun` entity exists in `TradePilot.Domain/Entities/` with static `Create()` factory
- `IBacktestRunRepository` interface exists in `TradePilot.Application/Abstractions/Repositories/`
- `BacktestRunRepository` implementation exists in `TradePilot.Persistence/Repositories/`
- `TradePilotDbContext` has `BacktestRuns` DbSet with correct column configuration
- EF Core migration for `BacktestRuns` table exists and applies cleanly
- Repository is registered in DI via `PersistenceServiceExtensions`
- Repository tests pass (round-trip persistence, null for non-existent ID)
- Solution builds with zero errors
