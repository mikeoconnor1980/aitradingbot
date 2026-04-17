<!-- markdownlint-disable-file -->

# Task Details: Candle Data Persistence

## Phase 1: Domain Entity, Persistence Layer & Tests

## Standards and Knowledge References

- **csharp.instructions.md** — `sealed` classes, private parameterless constructor for EF Core entities, `async/await` with `CancellationToken`, `Async` suffix on all I/O methods
- **testing.instructions.md** — MSTest + FluentAssertions 6.12.2 + Moq, `Given_When_Then` naming, `[TestClass] public sealed class`, global usings in `Usings.cs`
- **dotnet-architecture.instructions.md** — Repository interfaces in `TradePilot.Application/Abstractions/`, implementations in `TradePilot.Persistence`, dependency inversion
- **04-domain-model.md** — Candle entity definition (F1 PBI spec overrides ERD field naming)
- **18-backtesting-architecture.md** — Candle data consumed by `HistoricalDataProvider`
- **ADR 3** — SQLite (POC) → Azure SQL; EF Core abstracts both
- **ADR 6** — Candle entity explicitly exempt from multi-tenancy (shared public market data)

## Design References

- **EF Core SQLite provider** — `Microsoft.EntityFrameworkCore.Sqlite` v8.x; `UseSqlite()` configuration
- **INSERT OR IGNORE** — EF Core has no high-level abstraction; raw SQL via `ExecuteSqlRawAsync` required
- **SQLite decimal storage** — No native DECIMAL type; use `HasConversion<double>()` for OHLCV prices to enable server-side range queries
- **In-memory SQLite testing** — Use `SqliteConnection("Data Source=:memory:")` opened before passing to EF, with `EnsureCreated()` (not `MigrateAsync()`)
- **SQLite parameter limit** — 32,766 on modern versions (bundled via Microsoft.Data.Sqlite); 500 rows × 9 columns = 4,500 is safe

### Task 1.1: Create the `Candle` domain entity {#task-11-create-the-candle-domain-entity}

Create the first domain entity in the `TradePilot.Domain` project. The `Candle` represents a single OHLCV candle bar for a given symbol and interval.

- **Complexity**: Medium
- **Risk Factors**: First entity in codebase — establishes the pattern for all future entities
- **Files**:
  - `src/TradePilot.Domain/Entities/Candle.cs` — New file: Candle domain entity
- **Success**:
  - `Candle` class exists with all properties: `Id` (long), `Symbol` (string), `Interval` (string), `Timestamp` (long), `Open` (decimal), `High` (decimal), `Low` (decimal), `Close` (decimal), `Volume` (decimal), `NumTrades` (int)
  - All price/volume properties use `decimal` type
  - Class is `sealed` with a private parameterless constructor (for EF Core) and a `static Create()` factory method
  - Factory method validates required inputs
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradePilot.Domain/Entities/Candle.cs — new file
namespace TradePilot.Domain.Entities;

public sealed class Candle
{
    public long Id { get; private set; }
    public string Symbol { get; private set; } = string.Empty;
    public string Interval { get; private set; } = string.Empty;
    public long Timestamp { get; private set; }
    public decimal Open { get; private set; }
    public decimal High { get; private set; }
    public decimal Low { get; private set; }
    public decimal Close { get; private set; }
    public decimal Volume { get; private set; }
    public int NumTrades { get; private set; }

    private Candle() { } // Required for EF Core

    public static Candle Create(
        string symbol,
        string interval,
        long timestamp,
        decimal open,
        decimal high,
        decimal low,
        decimal close,
        decimal volume,
        int numTrades)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(interval);

        return new Candle
        {
            Symbol = symbol,
            Interval = interval,
            Timestamp = timestamp,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Volume = volume,
            NumTrades = numTrades
        };
    }
}
```

##### Pattern References

- `src/TradePilot.Application/MarketData/Models/CandleDto.cs` — Property naming pattern (Timestamp, Open, High, Low, Close, Volume)
- `src/TradePilot.Application/Abstractions/Exceptions/DomainException.cs` — Sealed class pattern
- Validation uses `ArgumentException.ThrowIfNullOrWhiteSpace` (existing codebase pattern, not Ardalis.GuardClauses)

### Task 1.2: Add EF Core NuGet packages to Persistence project {#task-12-add-ef-core-nuget-packages-to-persistence-project}

Add the required EF Core packages to the Persistence project.

- **Complexity**: Low
- **Risk Factors**: None — clean slate project
- **Files**:
  - `src/TradePilot.Persistence/TradePilot.Persistence.csproj` — Modify: add NuGet package references
- **Success**:
  - `Microsoft.EntityFrameworkCore.Sqlite` v8.x is referenced
  - `Microsoft.EntityFrameworkCore.Design` v8.x is referenced with `PrivateAssets="all"`
  - Solution builds successfully
- **Dependencies**: None

#### Implementation Details

```xml
<!-- src/TradePilot.Persistence/TradePilot.Persistence.csproj — modification -->
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\TradePilot.Application\TradePilot.Application.csproj" />
    <ProjectReference Include="..\TradePilot.Domain\TradePilot.Domain.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.12" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.12" PrivateAssets="all" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

> **Note**: Use the latest 8.0.x version available via `dotnet add package`. The exact version (8.0.12) is illustrative — the implementer should use `dotnet add package Microsoft.EntityFrameworkCore.Sqlite` to get the latest 8.0.x.

##### Pattern References

- `src/TradePilot.Persistence/TradePilot.Persistence.csproj` — Current file (empty package references)
- `src/TradePilot.Infrastructure/TradePilot.Infrastructure.csproj` — Existing NuGet reference pattern

### Task 1.3: Create `TradePilotDbContext` with Candle entity configuration {#task-13-create-TradePilotdbcontext-with-candle-entity-configuration}

Create the EF Core DbContext configured for SQLite with the Candle entity mapping, composite unique index, and decimal-to-double conversions.

- **Complexity**: Medium
- **Risk Factors**: Decimal-to-double conversion for SQLite; composite unique index configuration
- **Files**:
  - `src/TradePilot.Persistence/TradePilotDbContext.cs` — New file: EF Core DbContext
- **Success**:
  - `TradePilotDbContext` inherits `DbContext` and exposes `DbSet<Candle> Candles`
  - Composite unique index on (`Symbol`, `Interval`, `Timestamp`) is configured with name `IX_Candles_Symbol_Interval_Timestamp`
  - `Symbol` has max length 20, `Interval` has max length 10
  - All decimal properties have `HasConversion<double>()` for SQLite server-side query support
  - `Id` is configured as auto-increment primary key
- **Dependencies**: Task 1.1 (Candle entity), Task 1.2 (EF Core packages)

#### Implementation Details

```csharp
// src/TradePilot.Persistence/TradePilotDbContext.cs — new file
using Microsoft.EntityFrameworkCore;
using TradePilot.Domain.Entities;

namespace TradePilot.Persistence;

public sealed class TradePilotDbContext : DbContext
{
    public TradePilotDbContext(DbContextOptions<TradePilotDbContext> options)
        : base(options) { }

    public DbSet<Candle> Candles => Set<Candle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Candle>(entity =>
        {
            entity.ToTable("Candles");

            entity.HasKey(c => c.Id);

            entity.Property(c => c.Symbol)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(c => c.Interval)
                .HasMaxLength(10)
                .IsRequired();

            entity.HasIndex(c => new { c.Symbol, c.Interval, c.Timestamp })
                .IsUnique()
                .HasDatabaseName("IX_Candles_Symbol_Interval_Timestamp");

            // SQLite has no native DECIMAL type.
            // Store as REAL (double) for server-side range query support.
            // C# entity model retains decimal for precision in business logic.
            entity.Property(c => c.Open).HasConversion<double>();
            entity.Property(c => c.High).HasConversion<double>();
            entity.Property(c => c.Low).HasConversion<double>();
            entity.Property(c => c.Close).HasConversion<double>();
            entity.Property(c => c.Volume).HasConversion<double>();
        });
    }
}
```

##### Pattern References

- Official EF Core documentation — `HasConversion<double>()` for SQLite decimal workaround
- Official EF Core documentation — `HasIndex().IsUnique()` for composite unique index

### Task 1.4: Create `ICandleRepository` interface {#task-14-create-icandlerepository-interface}

Define the repository interface in the Application layer's Abstractions folder, following the existing service interface placement pattern.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradePilot.Application/Abstractions/Repositories/ICandleRepository.cs` — New file: repository interface
- **Success**:
  - Interface defines `GetCandlesAsync`, `BulkInsertAsync`, and `GetLatestTimestampAsync` methods
  - All methods accept `CancellationToken`
  - Return types match PBI spec
- **Dependencies**: Task 1.1 (Candle entity)

#### Implementation Details

```csharp
// src/TradePilot.Application/Abstractions/Repositories/ICandleRepository.cs — new file
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Abstractions.Repositories;

public interface ICandleRepository
{
    Task<List<Candle>> GetCandlesAsync(
        string symbol,
        string interval,
        long startTime,
        long endTime,
        CancellationToken cancellationToken = default);

    Task BulkInsertAsync(
        IEnumerable<Candle> candles,
        CancellationToken cancellationToken = default);

    Task<long?> GetLatestTimestampAsync(
        string symbol,
        string interval,
        CancellationToken cancellationToken = default);
}
```

##### Pattern References

- `src/TradePilot.Application/Abstractions/Services/IHyperliquidRestClient.cs` — Interface placement pattern in `Application/Abstractions/`
- New `Repositories/` subfolder distinguishes data-access interfaces from external service interfaces

### Task 1.5: Create `CandleRepository` implementation with INSERT OR IGNORE bulk insert {#task-15-create-candlerepository-implementation-with-insert-or-ignore-bulk-insert}

Implement the repository using EF Core for queries and raw SQL for INSERT OR IGNORE bulk inserts with 500-row batching.

- **Complexity**: High
- **Risk Factors**: Raw SQL with parameterized INSERT OR IGNORE; batch chunking; SQLite parameter limits
- **Files**:
  - `src/TradePilot.Persistence/Repositories/CandleRepository.cs` — New file: repository implementation
- **Success**:
  - `GetCandlesAsync` returns candles filtered by symbol, interval, and timestamp range, ordered by Timestamp ascending
  - `GetCandlesAsync` returns empty list when no candles match
  - `BulkInsertAsync` uses `INSERT OR IGNORE` raw SQL via `ExecuteSqlRawAsync`
  - `BulkInsertAsync` processes in batches of 500 rows per transaction
  - `GetLatestTimestampAsync` returns the max Timestamp for a given symbol/interval, or `null` if none exist
  - Class is `sealed`
- **Dependencies**: Task 1.3 (DbContext), Task 1.4 (ICandleRepository)

#### Implementation Details

```csharp
// src/TradePilot.Persistence/Repositories/CandleRepository.cs — new file
using System.Text;
using Microsoft.EntityFrameworkCore;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Domain.Entities;

namespace TradePilot.Persistence.Repositories;

public sealed class CandleRepository : ICandleRepository
{
    private const int BatchSize = 500;
    private readonly TradePilotDbContext _context;

    public CandleRepository(TradePilotDbContext context)
    {
        _context = context;
    }

    public async Task<List<Candle>> GetCandlesAsync(
        string symbol,
        string interval,
        long startTime,
        long endTime,
        CancellationToken cancellationToken = default)
    {
        return await _context.Candles
            .Where(c => c.Symbol == symbol
                && c.Interval == interval
                && c.Timestamp >= startTime
                && c.Timestamp <= endTime)
            .OrderBy(c => c.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task BulkInsertAsync(
        IEnumerable<Candle> candles,
        CancellationToken cancellationToken = default)
    {
        foreach (var batch in candles.Chunk(BatchSize))
        {
            await using var transaction = await _context.Database
                .BeginTransactionAsync(cancellationToken);

            var sql = new StringBuilder();
            sql.Append("INSERT OR IGNORE INTO Candles (Symbol, Interval, Timestamp, Open, High, Low, Close, Volume, NumTrades) VALUES ");

            var parameters = new List<object>();
            for (var i = 0; i < batch.Length; i++)
            {
                if (i > 0) sql.Append(',');

                var offset = i * 9;
                sql.Append($"({{{offset}}},{{{offset + 1}}},{{{offset + 2}}},{{{offset + 3}}},{{{offset + 4}}},{{{offset + 5}}},{{{offset + 6}}},{{{offset + 7}}},{{{offset + 8}}})");

                var candle = batch[i];
                parameters.Add(candle.Symbol);
                parameters.Add(candle.Interval);
                parameters.Add(candle.Timestamp);
                parameters.Add((double)candle.Open);
                parameters.Add((double)candle.High);
                parameters.Add((double)candle.Low);
                parameters.Add((double)candle.Close);
                parameters.Add((double)candle.Volume);
                parameters.Add(candle.NumTrades);
            }

            await _context.Database.ExecuteSqlRawAsync(
                sql.ToString(), parameters, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
    }

    public async Task<long?> GetLatestTimestampAsync(
        string symbol,
        string interval,
        CancellationToken cancellationToken = default)
    {
        return await _context.Candles
            .Where(c => c.Symbol == symbol && c.Interval == interval)
            .MaxAsync(
                c => (long?)c.Timestamp,
                cancellationToken);
    }
}
```

##### Pattern References

- EF Core documentation — `ExecuteSqlRawAsync` with positional parameters for dynamic multi-row SQL
- EF Core documentation — `BeginTransactionAsync`/`CommitAsync` for explicit transaction control
- `LINQ.Chunk()` (.NET 6+) for batch splitting

### Task 1.6: Create `PersistenceServiceExtensions` for DI registration {#task-16-create-persistenceserviceextensions-for-di-registration}

Create the DI registration extension method that both API and Worker will call.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradePilot.Persistence/PersistenceServiceExtensions.cs` — New file: DI registration
- **Success**:
  - `AddPersistence(IServiceCollection, IConfiguration)` registers `TradePilotDbContext` and `ICandleRepository`
  - DbContext is configured with SQLite provider using `DefaultConnection` connection string
  - `CandleRepository` is registered as scoped
- **Dependencies**: Tasks 1.3 and 1.5 (DbContext and Repository)

#### Implementation Details

```csharp
// src/TradePilot.Persistence/PersistenceServiceExtensions.cs — new file
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Persistence.Repositories;

namespace TradePilot.Persistence;

public static class PersistenceServiceExtensions
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<TradePilotDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<ICandleRepository, CandleRepository>();

        return services;
    }
}
```

##### Pattern References

- `src/TradePilot.Api/Program.cs` — Existing inline DI registration pattern; this introduces the first extension method
- Standard .NET `AddDbContext` + `UseSqlite` pattern from EF Core documentation

### Task 1.7: Generate the initial EF Core migration {#task-17-generate-the-initial-ef-core-migration}

Generate the EF Core migration that creates the `Candles` table with the composite unique index.

- **Complexity**: Low
- **Risk Factors**: Requires a temporary startup project reference or design-time factory for `dotnet ef` tooling
- **Files**:
  - `src/TradePilot.Persistence/Migrations/*.cs` — Auto-generated migration files
  - `src/TradePilot.Persistence/DesignTimeDbContextFactory.cs` — New file: design-time factory for `dotnet ef` CLI
- **Success**:
  - Migration files are generated under `src/TradePilot.Persistence/Migrations/`
  - Migration creates `Candles` table with correct columns, types, and composite unique index
  - `dotnet ef migrations list` shows the migration
- **Dependencies**: Tasks 1.2, 1.3 (EF Core packages and DbContext)

#### Implementation Details

A design-time factory is needed because the Persistence project has no `Program.cs` for `dotnet ef` to discover the DbContext configuration.

```csharp
// src/TradePilot.Persistence/DesignTimeDbContextFactory.cs — new file
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TradePilot.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TradePilotDbContext>
{
    public TradePilotDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TradePilotDbContext>()
            .UseSqlite("Data Source=Data/TradePilot.db")
            .Options;

        return new TradePilotDbContext(options);
    }
}
```

Then run:

```powershell
# Install EF Core tools globally if not already installed
dotnet tool install --global dotnet-ef

# Generate the initial migration
dotnet ef migrations add InitialCreate --project src/TradePilot.Persistence --output-dir Migrations
```

##### Pattern References

- EF Core documentation — `IDesignTimeDbContextFactory<T>` for CLI migration generation in class library projects

### Task 1.8: Create `TradePilot.Persistence.Tests` project and `CandleRepository` integration tests {#task-18-create-persistence-tests-project-and-candlerepository-integration-tests}

Create a new test project for persistence integration tests. Use in-memory SQLite (`Data Source=:memory:`) with the real `TradePilotDbContext` to validate EF Core mappings, index behavior, and bulk insert semantics.

- **Complexity**: High
- **Risk Factors**: First persistence test project; in-memory SQLite connection lifecycle management; INSERT OR IGNORE validation
- **Files**:
  - `tests/TradePilot.Persistence.Tests/TradePilot.Persistence.Tests.csproj` — New file: test project
  - `tests/TradePilot.Persistence.Tests/Usings.cs` — New file: global usings
  - `tests/TradePilot.Persistence.Tests/Repositories/CandleRepositoryTests.cs` — New file: integration tests
- **Success**:
  - Test project compiles and is included in the solution
  - Tests cover: bulk insert, duplicate skip, range query, empty range, latest timestamp, null latest timestamp, batch processing
  - All tests pass
- **Dependencies**: Tasks 1.1–1.5 (all persistence infrastructure)

#### Implementation Details

```xml
<!-- tests/TradePilot.Persistence.Tests/TradePilot.Persistence.Tests.csproj — new file -->
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="FluentAssertions" Version="6.12.2" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.6.0" />
    <PackageReference Include="Moq" Version="4.20.72" />
    <PackageReference Include="MSTest.TestAdapter" Version="3.0.4" />
    <PackageReference Include="MSTest.TestFramework" Version="3.0.4" />
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.12" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\TradePilot.Persistence\TradePilot.Persistence.csproj" />
  </ItemGroup>

</Project>
```

> **Note**: Use the same `Microsoft.EntityFrameworkCore.Sqlite` version as the Persistence project. The implementer should match the exact version from Task 1.2.

```csharp
// tests/TradePilot.Persistence.Tests/Usings.cs — new file
global using FluentAssertions;
global using Microsoft.VisualStudio.TestTools.UnitTesting;
global using Moq;
```

```csharp
// tests/TradePilot.Persistence.Tests/Repositories/CandleRepositoryTests.cs — new file
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TradePilot.Domain.Entities;
using TradePilot.Persistence.Repositories;

namespace TradePilot.Persistence.Tests.Repositories;

[TestClass]
public sealed class CandleRepositoryTests
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
    public async Task GivenCandles_WhenBulkInsertAsync_ThenAllCandlesArePersisted()
    {
        // Arrange
        var candles = CreateCandles("BTC", "15m", 1000, 3);
        await using var context = CreateContext();
        var sut = new CandleRepository(context);

        // Act
        await sut.BulkInsertAsync(candles);

        // Assert
        await using var verifyContext = CreateContext();
        var stored = await verifyContext.Candles.ToListAsync();
        stored.Should().HaveCount(3);
    }

    [TestMethod]
    public async Task GivenDuplicateCandles_WhenBulkInsertAsync_ThenDuplicatesAreSkipped()
    {
        // Arrange
        var candles = CreateCandles("BTC", "15m", 1000, 3);
        await using var context1 = CreateContext();
        var sut1 = new CandleRepository(context1);
        await sut1.BulkInsertAsync(candles);

        // Act — insert same candles again plus one new one
        var duplicatesWithNew = candles.Concat(CreateCandles("BTC", "15m", 4000, 1)).ToList();
        await using var context2 = CreateContext();
        var sut2 = new CandleRepository(context2);
        await sut2.BulkInsertAsync(duplicatesWithNew);

        // Assert
        await using var verifyContext = CreateContext();
        var stored = await verifyContext.Candles.ToListAsync();
        stored.Should().HaveCount(4);
    }

    [TestMethod]
    public async Task GivenCandlesInRange_WhenGetCandlesAsync_ThenReturnsFilteredOrderedByTimestamp()
    {
        // Arrange
        var candles = new[]
        {
            Candle.Create("BTC", "15m", 3000, 100m, 105m, 95m, 102m, 50m, 10),
            Candle.Create("BTC", "15m", 1000, 100m, 105m, 95m, 102m, 50m, 10),
            Candle.Create("BTC", "15m", 2000, 100m, 105m, 95m, 102m, 50m, 10),
            Candle.Create("BTC", "15m", 4000, 100m, 105m, 95m, 102m, 50m, 10),
            Candle.Create("ETH", "15m", 1000, 100m, 105m, 95m, 102m, 50m, 10),
        };
        await using var context = CreateContext();
        var sut = new CandleRepository(context);
        await sut.BulkInsertAsync(candles);

        // Act
        await using var queryContext = CreateContext();
        var querySut = new CandleRepository(queryContext);
        var result = await querySut.GetCandlesAsync("BTC", "15m", 1000, 3000);

        // Assert
        result.Should().HaveCount(3);
        result.Select(c => c.Timestamp).Should().BeInAscendingOrder();
        result.Should().OnlyContain(c => c.Symbol == "BTC");
    }

    [TestMethod]
    public async Task GivenNoCandlesInRange_WhenGetCandlesAsync_ThenReturnsEmptyList()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = new CandleRepository(context);

        // Act
        var result = await sut.GetCandlesAsync("BTC", "15m", 1000, 2000);

        // Assert
        result.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenCandlesExist_WhenGetLatestTimestampAsync_ThenReturnsMaxTimestamp()
    {
        // Arrange
        var candles = CreateCandles("BTC", "1h", 1000, 5);
        await using var context = CreateContext();
        var sut = new CandleRepository(context);
        await sut.BulkInsertAsync(candles);

        // Act
        await using var queryContext = CreateContext();
        var querySut = new CandleRepository(queryContext);
        var result = await querySut.GetLatestTimestampAsync("BTC", "1h");

        // Assert
        result.Should().Be(5000);
    }

    [TestMethod]
    public async Task GivenNoCandlesExist_WhenGetLatestTimestampAsync_ThenReturnsNull()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = new CandleRepository(context);

        // Act
        var result = await sut.GetLatestTimestampAsync("BTC", "15m");

        // Assert
        result.Should().BeNull();
    }

    [TestMethod]
    public async Task GivenLargeBatch_WhenBulkInsertAsync_ThenProcessesInBatches()
    {
        // Arrange — 1200 candles should produce 3 batches (500 + 500 + 200)
        var candles = CreateCandles("BTC", "15m", 1000, 1200);
        await using var context = CreateContext();
        var sut = new CandleRepository(context);

        // Act
        await sut.BulkInsertAsync(candles);

        // Assert
        await using var verifyContext = CreateContext();
        var stored = await verifyContext.Candles.ToListAsync();
        stored.Should().HaveCount(1200);
    }

    [TestMethod]
    public async Task GivenCandlesWithDecimalPrices_WhenBulkInsertAndQuery_ThenPrecisionIsPreserved()
    {
        // Arrange
        var candle = Candle.Create("BTC", "15m", 1000, 67234.56m, 67500.12m, 67100.99m, 67300.45m, 1234.5678m, 42);
        await using var context = CreateContext();
        var sut = new CandleRepository(context);
        await sut.BulkInsertAsync(new[] { candle });

        // Act
        await using var queryContext = CreateContext();
        var querySut = new CandleRepository(queryContext);
        var result = await querySut.GetCandlesAsync("BTC", "15m", 1000, 1000);

        // Assert
        var stored = result.Single();
        stored.Open.Should().BeApproximately(67234.56m, 0.01m);
        stored.High.Should().BeApproximately(67500.12m, 0.01m);
        stored.Low.Should().BeApproximately(67100.99m, 0.01m);
        stored.Close.Should().BeApproximately(67300.45m, 0.01m);
        stored.Volume.Should().BeApproximately(1234.5678m, 0.001m);
    }

    private static List<Candle> CreateCandles(string symbol, string interval, long startTimestamp, int count)
    {
        return Enumerable.Range(0, count)
            .Select(i => Candle.Create(
                symbol, interval,
                startTimestamp + (i * 1000),
                100m + i, 105m + i, 95m + i, 102m + i, 50m + i, 10 + i))
            .ToList();
    }
}
```

##### Pattern References

- `tests/TradePilot.Domain.Tests/TradePilot.Domain.Tests.csproj` — Test project structure (MSTest packages, FluentAssertions 6.12.2, Moq)
- `tests/TradePilot.Domain.Tests/Usings.cs` — Global usings pattern
- `tests/TradePilot.Infrastructure.Tests/Services/NonceProviderTests.cs` — `[TestClass] public sealed class`, `Given_When_Then` naming
- EF Core documentation — In-memory SQLite testing with `SqliteConnection("Data Source=:memory:")` + `EnsureCreated()`

### Task 1.9: Create `Candle` domain entity tests {#task-19-create-candle-domain-entity-tests}

Add unit tests for the `Candle.Create()` factory method to validate input guards and property assignment.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `tests/TradePilot.Domain.Tests/Entities/CandleTests.cs` — New file: domain entity tests
- **Success**:
  - Tests verify factory method creates entity with correct property values
  - Tests verify ArgumentException thrown for null/empty Symbol and Interval
  - All tests pass
- **Dependencies**: Task 1.1 (Candle entity)

#### Implementation Details

```csharp
// tests/TradePilot.Domain.Tests/Entities/CandleTests.cs — new file
using TradePilot.Domain.Entities;

namespace TradePilot.Domain.Tests.Entities;

[TestClass]
public sealed class CandleTests
{
    [TestMethod]
    public void GivenValidInputs_WhenCreate_ThenReturnsCandle()
    {
        var candle = Candle.Create("BTC", "15m", 1710000000000, 67000m, 67500m, 66800m, 67200m, 1234.56m, 42);

        candle.Symbol.Should().Be("BTC");
        candle.Interval.Should().Be("15m");
        candle.Timestamp.Should().Be(1710000000000);
        candle.Open.Should().Be(67000m);
        candle.High.Should().Be(67500m);
        candle.Low.Should().Be(66800m);
        candle.Close.Should().Be(67200m);
        candle.Volume.Should().Be(1234.56m);
        candle.NumTrades.Should().Be(42);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    public void GivenInvalidSymbol_WhenCreate_ThenThrowsArgumentException(string? symbol)
    {
        var act = () => Candle.Create(symbol!, "15m", 1000, 100m, 105m, 95m, 102m, 50m, 10);

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    public void GivenInvalidInterval_WhenCreate_ThenThrowsArgumentException(string? interval)
    {
        var act = () => Candle.Create("BTC", interval!, 1000, 100m, 105m, 95m, 102m, 50m, 10);

        act.Should().Throw<ArgumentException>();
    }
}
```

##### Pattern References

- `tests/TradePilot.Infrastructure.Tests/Services/NonceProviderTests.cs` — Sealed test class, Given_When_Then naming convention
- `tests/TradePilot.Domain.Tests/Usings.cs` — Existing global usings

### Task 1.10: Build solution and run all tests {#task-110-build-solution-and-run-all-tests}

Build the entire solution to verify no compilation errors and run all tests to confirm both new and existing tests pass.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification step)
- **Success**:
  - `dotnet build TradePilot.sln` succeeds with 0 errors
  - `dotnet test TradePilot.sln` passes all tests (existing + new domain + new persistence integration)
- **Dependencies**: All previous tasks in Phase 1

#### Implementation Details

```powershell
# Add new test project to solution
dotnet sln TradePilot.sln add tests/TradePilot.Persistence.Tests/TradePilot.Persistence.Tests.csproj

# Build entire solution
dotnet build TradePilot.sln

# Run all tests
dotnet test TradePilot.sln
```

## Phase Success Criteria

- `Candle` entity exists in `TradePilot.Domain/Entities/` with all required properties using `decimal` types
- `TradePilotDbContext` exists in `TradePilot.Persistence/` with Candle configuration and composite unique index
- `ICandleRepository` exists in `TradePilot.Application/Abstractions/Repositories/`
- `CandleRepository` exists in `TradePilot.Persistence/Repositories/` with INSERT OR IGNORE bulk insert
- `PersistenceServiceExtensions` exists in `TradePilot.Persistence/` with `AddPersistence()` method
- Initial EF Core migration exists under `src/TradePilot.Persistence/Migrations/`
- `TradePilot.Persistence.Tests` project exists with integration tests for all repository methods
- `TradePilot.Domain.Tests` has `Candle` entity tests
- `dotnet build TradePilot.sln` succeeds
- `dotnet test TradePilot.sln` — all tests pass
