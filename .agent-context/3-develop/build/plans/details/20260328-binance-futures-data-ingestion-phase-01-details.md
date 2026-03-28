<!-- markdownlint-disable-file -->

# Task Details: Binance USDⓈ-M Futures Data Ingestion

## Phase 1: Domain & Persistence Foundation (Source Column)

## Standards and Knowledge References

- **csharp.instructions.md**: Sealed classes, static `Create` factory with BCL argument guards, private setters
- **testing.instructions.md**: MSTest, Moq, FluentAssertions ≤ v6, `Given_When_Then` naming, tests included per phase
- **dotnet-architecture.instructions.md**: Repository interfaces in Application, implementations in Persistence, EF migrations from Persistence project
- **04-domain-model.md**: Core entities use factory pattern with validation

### Task 1.1: Add `Source` property to `Candle` entity and update `Create` factory {#task-11-add-source-property-to-candle-entity}

Add a `Source` string property to the `Candle` entity. Update the `Create` factory method to accept a `source` parameter with a default value of `"Hyperliquid"` for backwards compatibility.

- **Complexity**: Medium
- **Risk Factors**: Changing an entity factory signature impacts all callers — default parameter mitigates this
- **Files**:
  - `src/TradingApp.Domain/Entities/Candle.cs` — Add Source property, update Create factory
- **Success**:
  - `Candle.Create(...)` with `source: "Binance"` sets `Source = "Binance"`
  - `Candle.Create(...)` without `source` parameter defaults to `Source = "Hyperliquid"`
  - `null` or empty `source` throws `ArgumentException`
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Domain/Entities/Candle.cs — modification
// Add new property alongside existing properties:
public string Source { get; private set; } = string.Empty;

// Update Create factory — add source parameter with default:
public static Candle Create(
    string symbol,
    string interval,
    long timestamp,
    decimal open,
    decimal high,
    decimal low,
    decimal close,
    decimal volume,
    int numTrades,
    string source = "Hyperliquid")
{
    // ... existing validation ...
    ArgumentException.ThrowIfNullOrWhiteSpace(source);

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
        NumTrades = numTrades,
        Source = source
    };
}
```

##### Pattern References

- `src/TradingApp.Domain/Entities/Candle.cs` — existing Create factory with BCL argument guards

---

### Task 1.2: Update `TradingAppDbContext` with Source column configuration and new unique index {#task-12-update-dbcontext-configuration}

Configure the `Source` column in EF Core model builder. Update the unique index from `(Symbol, Interval, Timestamp)` to `(Source, Symbol, Interval, Timestamp)`.

- **Complexity**: Medium
- **Risk Factors**: Index change affects deduplication behaviour — `INSERT OR IGNORE` now considers Source
- **Files**:
  - `src/TradingApp.Persistence/TradingAppDbContext.cs` — Add Source column config, update unique index
- **Success**:
  - Source column configured as `string(20)`, required, with default value `"Hyperliquid"`
  - Unique index renamed to `IX_Candles_Source_Symbol_Interval_Timestamp`
  - Existing unique index dropped
- **Dependencies**: Task 1.1

#### Implementation Details

```csharp
// src/TradingApp.Persistence/TradingAppDbContext.cs — modification
// Inside OnModelCreating, in the Candle entity configuration block:

// Add Source column config (after existing property configs):
entity.Property(c => c.Source)
    .HasMaxLength(20)
    .IsRequired()
    .HasDefaultValue("Hyperliquid");

// Replace existing unique index:
entity.HasIndex(c => new { c.Source, c.Symbol, c.Interval, c.Timestamp })
    .IsUnique()
    .HasDatabaseName("IX_Candles_Source_Symbol_Interval_Timestamp");
```

##### Pattern References

- `src/TradingApp.Persistence/TradingAppDbContext.cs` — existing `HasIndex` and `HasMaxLength` patterns

---

### Task 1.3: Create EF migration `AddSourceToCandles` {#task-13-create-ef-migration}

Generate and verify the EF Core migration that adds the `Source` column with default value and updates the unique index.

- **Complexity**: Low
- **Risk Factors**: Existing data must get default value `"Hyperliquid"` — EF `HasDefaultValue` handles this
- **Files**:
  - `src/TradingApp.Persistence/Migrations/{timestamp}_AddSourceToCandles.cs` — Generated migration
- **Success**:
  - Migration adds `Source` column (TEXT, max 20, not null, default `"Hyperliquid"`)
  - Migration drops old index `IX_Candles_Symbol_Interval_Timestamp`
  - Migration creates new index `IX_Candles_Source_Symbol_Interval_Timestamp`
  - Migration applies cleanly via `dotnet ef database update`
- **Dependencies**: Task 1.2

#### Implementation Details

Run the following commands:

```bash
# Generate migration
cd src/TradingApp.Persistence
dotnet ef migrations add AddSourceToCandles --startup-project ../TradingApp.Api

# Verify migration applies
cd ../TradingApp.Api
dotnet ef database update --project ../TradingApp.Persistence
```

The generated migration should contain operations similar to:
```csharp
migrationBuilder.AddColumn<string>(
    name: "Source",
    table: "Candles",
    type: "TEXT",
    maxLength: 20,
    nullable: false,
    defaultValue: "Hyperliquid");

migrationBuilder.DropIndex(
    name: "IX_Candles_Symbol_Interval_Timestamp",
    table: "Candles");

migrationBuilder.CreateIndex(
    name: "IX_Candles_Source_Symbol_Interval_Timestamp",
    table: "Candles",
    columns: new[] { "Source", "Symbol", "Interval", "Timestamp" },
    unique: true);
```

##### Pattern References

- `src/TradingApp.Persistence/Migrations/20260327214340_InitialCreate.cs` — existing migration pattern

---

### Task 1.4: Update `CandleRepository` for Source column {#task-14-update-candlerepository-bulkinsertasync}

Update the raw SQL `INSERT OR IGNORE` statement to include the `Source` column. Update `GetLatestTimestampAsync` and `GetCandlesAsync` to accept an optional `source` parameter for source-specific filtering. Update the `ICandleRepository` interface accordingly.

- **Complexity**: Medium
- **Risk Factors**: Hardcoded column count in SQL and parameter offset math must change from 9 to 10; query methods gain optional Source filter
- **Files**:
  - `src/TradingApp.Application/Abstractions/Repositories/ICandleRepository.cs` — Add `source` parameter to `GetLatestTimestampAsync` and `GetCandlesAsync`
  - `src/TradingApp.Persistence/Repositories/CandleRepository.cs` — Update BulkInsertAsync SQL, update query methods for Source filter
- **Success**:
  - SQL includes `Source` as the first column in INSERT statement
  - Parameter offset uses `i * 10` (was `i * 9`)
  - 10 parameters per row (was 9)
  - Existing `INSERT OR IGNORE` deduplication still works with new 4-column unique index
  - `GetLatestTimestampAsync(symbol, interval, source)` filters by Source when provided
  - `GetCandlesAsync(symbol, interval, startTime, endTime, source)` filters by Source when provided
  - Passing `source: null` returns data across all sources (backward-compatible)
- **Dependencies**: Task 1.3

#### Implementation Details

```csharp
// src/TradingApp.Persistence/Repositories/CandleRepository.cs — modification
// In BulkInsertAsync method:

sql.Append("INSERT OR IGNORE INTO Candles (Source, Symbol, Interval, Timestamp, Open, High, Low, Close, Volume, NumTrades) VALUES ");

// Update parameter offset calculation:
var offset = i * 10;
sql.Append($"(@p{offset}, @p{offset + 1}, @p{offset + 2}, @p{offset + 3}, @p{offset + 4}, @p{offset + 5}, @p{offset + 6}, @p{offset + 7}, @p{offset + 8}, @p{offset + 9})");

// Update parameter list to include Source first:
parameters.Add(new SqliteParameter($"@p{offset}", candle.Source));
parameters.Add(new SqliteParameter($"@p{offset + 1}", candle.Symbol));
parameters.Add(new SqliteParameter($"@p{offset + 2}", candle.Interval));
parameters.Add(new SqliteParameter($"@p{offset + 3}", candle.Timestamp));
parameters.Add(new SqliteParameter($"@p{offset + 4}", (double)candle.Open));
parameters.Add(new SqliteParameter($"@p{offset + 5}", (double)candle.High));
parameters.Add(new SqliteParameter($"@p{offset + 6}", (double)candle.Low));
parameters.Add(new SqliteParameter($"@p{offset + 7}", (double)candle.Close));
parameters.Add(new SqliteParameter($"@p{offset + 8}", (double)candle.Volume));
parameters.Add(new SqliteParameter($"@p{offset + 9}", candle.NumTrades));
```

##### Pattern References

- `src/TradingApp.Persistence/Repositories/CandleRepository.cs` — existing `BulkInsertAsync` raw SQL pattern

#### Additional Implementation: Query Method Updates

```csharp
// src/TradingApp.Application/Abstractions/Repositories/ICandleRepository.cs — modification
// Add optional source parameter to both query methods:

Task<IReadOnlyList<Candle>> GetCandlesAsync(
    string symbol,
    string interval,
    long startTime,
    long endTime,
    string? source = null,
    CancellationToken cancellationToken = default);

Task<long?> GetLatestTimestampAsync(
    string symbol,
    string interval,
    string? source = null,
    CancellationToken cancellationToken = default);
```

```csharp
// src/TradingApp.Persistence/Repositories/CandleRepository.cs — modification
// Update GetCandlesAsync to filter by Source when provided:
public async Task<IReadOnlyList<Candle>> GetCandlesAsync(
    string symbol, string interval, long startTime, long endTime,
    string? source = null, CancellationToken cancellationToken = default)
{
    var query = _context.Candles
        .Where(c => c.Symbol == symbol && c.Interval == interval
            && c.Timestamp >= startTime && c.Timestamp <= endTime);

    if (source is not null)
        query = query.Where(c => c.Source == source);

    return await query.OrderBy(c => c.Timestamp).ToListAsync(cancellationToken);
}

// Update GetLatestTimestampAsync to filter by Source when provided:
public async Task<long?> GetLatestTimestampAsync(
    string symbol, string interval, string? source = null,
    CancellationToken cancellationToken = default)
{
    var query = _context.Candles
        .Where(c => c.Symbol == symbol && c.Interval == interval);

    if (source is not null)
        query = query.Where(c => c.Source == source);

    return await query.MaxAsync(c => (long?)c.Timestamp, cancellationToken);
}
```

---

### Task 1.5: Update `CandleIngestionService` to pass `Source = "Hyperliquid"` {#task-15-update-candleingestionservice-for-source}

Update the existing Hyperliquid `CandleIngestionService` to pass `source: "Hyperliquid"` when creating `Candle` entities from ingested data. Also update its `GetLatestTimestampAsync` call to pass `source: "Hyperliquid"` for source-specific resume. Additionally, update `IngestionAlreadyRunningException` to accept an optional message parameter (required by Phase 2 Binance and Phase 4 FundingRate services).

- **Complexity**: Low
- **Risk Factors**: None — the default parameter value already handles Candle.Create, but being explicit is better
- **Files**:
  - `src/TradingApp.Infrastructure/Services/CandleIngestionService.cs` — Pass source explicitly to Candle.Create and GetLatestTimestampAsync
  - `src/TradingApp.Application/Abstractions/Exceptions/IngestionAlreadyRunningException.cs` — Add `(string message)` constructor
- **Success**:
  - All `Candle.Create(...)` calls in `CandleIngestionService` include `source: "Hyperliquid"`
  - `GetLatestTimestampAsync` calls pass `source: "Hyperliquid"`
  - Existing ingestion behaviour unchanged
  - `IngestionAlreadyRunningException` has both parameterless and `(string message)` constructors
- **Dependencies**: Task 1.1, Task 1.4

#### Implementation Details

```csharp
// src/TradingApp.Infrastructure/Services/CandleIngestionService.cs — modification
// In the mapping from CandleSnapshotDto to Candle entity, add source parameter:
var candle = Candle.Create(
    request.Symbol,
    interval,
    snapshot.Timestamp,
    snapshot.Open,
    snapshot.High,
    snapshot.Low,
    snapshot.Close,
    snapshot.Volume,
    snapshot.NumTrades,
    source: "Hyperliquid");

// Also update GetLatestTimestampAsync call to pass source:
var latestTimestamp = await _candleRepository.GetLatestTimestampAsync(
    coin, interval, source: "Hyperliquid", token);
```

```csharp
// src/TradingApp.Application/Abstractions/Exceptions/IngestionAlreadyRunningException.cs — modification
// Add message constructor alongside existing parameterless constructor:
public sealed class IngestionAlreadyRunningException : Exception
{
    public IngestionAlreadyRunningException()
        : base("Candle ingestion is already running.")
    {
    }

    public IngestionAlreadyRunningException(string message)
        : base(message)
    {
    }
}
```

##### Pattern References

- `src/TradingApp.Infrastructure/Services/CandleIngestionService.cs` — existing Candle.Create call site

---

### Task 1.6: Update existing tests for Source column changes {#task-16-update-existing-tests}

Update all existing tests that create `Candle` entities or assert on candle data to account for the new `Source` property.

- **Complexity**: Medium
- **Risk Factors**: Multiple test files need updates — risk of missing a call site
- **Files**:
  - `tests/TradingApp.Domain.Tests/Entities/CandleTests.cs` — Add Source assertions + validation tests
  - `tests/TradingApp.Persistence.Tests/Repositories/CandleRepositoryTests.cs` — Update Candle.Create calls, verify Source in SQL
  - `tests/TradingApp.Api.Tests/Services/CandleIngestionServiceTests.cs` — Update Candle.Create calls and assertions
  - `tests/TradingApp.Api.Tests/Controllers/CandlesControllerTests.cs` — Update assertions if checking response data
- **Success**:
  - All existing tests pass with updated Source parameter
  - New test: `GivenNullSource_WhenCreate_ThenThrowsArgumentException`
  - New test: `GivenEmptySource_WhenCreate_ThenThrowsArgumentException`
  - Candle entity tests assert `Source` property is set correctly
  - Repository tests verify `Source` column is included in inserts
- **Dependencies**: Tasks 1.1–1.5

#### Implementation Details

```csharp
// tests/TradingApp.Domain.Tests/Entities/CandleTests.cs — modification
// Add new test methods:

[TestMethod]
public void GivenNullSource_WhenCreate_ThenThrowsArgumentException()
{
    var act = () => Candle.Create("BTC", "15m", 1700000000000, 50000m, 51000m, 49000m, 50500m, 100m, 10, source: null!);
    act.Should().Throw<ArgumentException>();
}

[TestMethod]
public void GivenEmptySource_WhenCreate_ThenThrowsArgumentException()
{
    var act = () => Candle.Create("BTC", "15m", 1700000000000, 50000m, 51000m, 49000m, 50500m, 100m, 10, source: "");
    act.Should().Throw<ArgumentException>();
}

[TestMethod]
public void GivenValidParameters_WhenCreate_ThenSourceIsSet()
{
    var candle = Candle.Create("BTC", "15m", 1700000000000, 50000m, 51000m, 49000m, 50500m, 100m, 10, source: "Binance");
    candle.Source.Should().Be("Binance");
}

[TestMethod]
public void GivenNoSourceParameter_WhenCreate_ThenSourceDefaultsToHyperliquid()
{
    var candle = Candle.Create("BTC", "15m", 1700000000000, 50000m, 51000m, 49000m, 50500m, 100m, 10);
    candle.Source.Should().Be("Hyperliquid");
}
```

```csharp
// tests/TradingApp.Persistence.Tests/Repositories/CandleRepositoryTests.cs — modification
// Update all CreateCandle helper methods to include source parameter:
private static Candle CreateCandle(string symbol = "BTC", string interval = "15m",
    long timestamp = 1700000000000, string source = "Hyperliquid")
    => Candle.Create(symbol, interval, timestamp, 50000m, 51000m, 49000m, 50500m, 100m, 10, source);
```

##### Pattern References

- `tests/TradingApp.Domain.Tests/Entities/CandleTests.cs` — existing entity validation test pattern
- `tests/TradingApp.Persistence.Tests/Repositories/CandleRepositoryTests.cs` — existing CreateCandle helper

---

### Task 1.7: Build and run all test projects {#task-17-build-and-run-tests}

Build and run all affected test projects to verify the Source column changes.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification step)
- **Success**:
  - `dotnet build tests/TradingApp.Domain.Tests` — succeeds
  - `dotnet test tests/TradingApp.Domain.Tests` — all tests pass
  - `dotnet build tests/TradingApp.Persistence.Tests` — succeeds
  - `dotnet test tests/TradingApp.Persistence.Tests` — all tests pass
  - `dotnet build tests/TradingApp.Api.Tests` — succeeds
  - `dotnet test tests/TradingApp.Api.Tests` — all tests pass
- **Dependencies**: Task 1.6

## Phase Success Criteria

- `Candle` entity has `Source` property with string type and BCL guard validation
- `TradingAppDbContext` configures `Source` column (max 20, required, default `"Hyperliquid"`)
- Unique index is `IX_Candles_Source_Symbol_Interval_Timestamp`
- `CandleRepository.BulkInsertAsync` includes `Source` in SQL (10 columns)
- `CandleIngestionService` explicitly passes `source: "Hyperliquid"`
- EF migration applies cleanly
- All existing and new tests pass
