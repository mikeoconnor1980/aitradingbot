<!-- markdownlint-disable-file -->

# Task Details: Binance USDⓈ-M Futures Data Ingestion

## Phase 4: FundingRate Entity & Ingestion

## Standards and Knowledge References

- **csharp.instructions.md**: Sealed entity, static `Create` factory, BCL argument guards, private setters
- **dotnet-architecture.instructions.md**: Entity in Domain, repository interface in Application, implementation in Persistence, service interface in Application, implementation in Infrastructure
- **api-controllers.instructions.md**: New controller for new resource (`/api/funding`), `ApiController` base
- **testing.instructions.md**: MSTest, Moq strict, FluentAssertions ≤ v6, entity tests, repository tests (in-memory SQLite), controller integration tests
- **04-domain-model.md**: Factory pattern with validation for domain entities

## Design References

- Binance `GET /fapi/v1/fundingRate`: returns JSON array of `{ symbol, fundingTime, fundingRate, markPrice }`
- Max 1000 records per request, weight = 1
- Funding rate recorded every 8 hours (00:00, 08:00, 16:00 UTC)

---

### Task 4.1: Create `FundingRate` domain entity {#task-41-create-fundingrate-entity}

Create a new domain entity for storing funding rate snapshots.

- **Complexity**: Medium
- **Risk Factors**: New entity — must follow existing `Candle` patterns exactly
- **Files**:
  - `src/TradingApp.Domain/Entities/FundingRate.cs` — New file
- **Success**:
  - Sealed class with private ctor + static `Create` factory
  - Properties: `Id`, `Symbol`, `Timestamp`, `Rate`, `MarkPrice`
  - BCL argument guards on `symbol`
  - `Timestamp` is Unix ms
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Domain/Entities/FundingRate.cs — new file
namespace TradingApp.Domain.Entities;

public sealed class FundingRate
{
    public long Id { get; private set; }
    public string Symbol { get; private set; } = string.Empty;
    public long Timestamp { get; private set; }
    public decimal Rate { get; private set; }
    public decimal MarkPrice { get; private set; }

    private FundingRate() { }

    public static FundingRate Create(string symbol, long timestamp, decimal rate, decimal markPrice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        return new FundingRate
        {
            Symbol = symbol,
            Timestamp = timestamp,
            Rate = rate,
            MarkPrice = markPrice
        };
    }
}
```

##### Pattern References

- `src/TradingApp.Domain/Entities/Candle.cs` — existing entity pattern with factory + guards

---

### Task 4.2: Update `TradingAppDbContext` and create EF migration for `FundingRates` table {#task-42-update-dbcontext-and-create-migration}

Add `FundingRates` DbSet, configure entity properties with decimal-to-double conversions (SQLite), and create the EF migration.

- **Complexity**: Medium
- **Risk Factors**: SQLite decimal→double conversions required for `Rate` and `MarkPrice`
- **Files**:
  - `src/TradingApp.Persistence/TradingAppDbContext.cs` — Add DbSet + entity configuration
  - `src/TradingApp.Persistence/Migrations/{timestamp}_AddFundingRates.cs` — Generated migration
- **Success**:
  - `FundingRates` table created with `Id`, `Symbol`, `Timestamp`, `Rate`, `MarkPrice`
  - Unique index `IX_FundingRates_Symbol_Timestamp` on `(Symbol, Timestamp)`
  - Decimal properties converted to double for SQLite compatibility
  - Migration applies cleanly
- **Dependencies**: Task 4.1

#### Implementation Details

```csharp
// src/TradingApp.Persistence/TradingAppDbContext.cs — modification
// Add DbSet:
public DbSet<FundingRate> FundingRates => Set<FundingRate>();

// Add entity configuration in OnModelCreating:
modelBuilder.Entity<FundingRate>(entity =>
{
    entity.HasKey(f => f.Id);

    entity.Property(f => f.Symbol)
        .HasMaxLength(20)
        .IsRequired();

    entity.Property(f => f.Timestamp)
        .IsRequired();

    entity.Property(f => f.Rate)
        .HasConversion<double>()
        .IsRequired();

    entity.Property(f => f.MarkPrice)
        .HasConversion<double>()
        .IsRequired();

    entity.HasIndex(f => new { f.Symbol, f.Timestamp })
        .IsUnique()
        .HasDatabaseName("IX_FundingRates_Symbol_Timestamp");
});
```

```bash
# Generate migration
cd src/TradingApp.Persistence
dotnet ef migrations add AddFundingRates --startup-project ../TradingApp.Api
```

##### Pattern References

- `src/TradingApp.Persistence/TradingAppDbContext.cs` — existing Candle entity configuration with decimal→double conversion

---

### Task 4.3: Create `IFundingRateRepository` and `FundingRateRepository` {#task-43-create-funding-rate-repository}

Create the repository interface and implementation for funding rate persistence with bulk insert support.

- **Complexity**: Medium
- **Risk Factors**: Raw SQL `INSERT OR IGNORE` pattern for idempotent bulk inserts
- **Files**:
  - `src/TradingApp.Application/Abstractions/Repositories/IFundingRateRepository.cs` — New file
  - `src/TradingApp.Persistence/Repositories/FundingRateRepository.cs` — New file
  - `src/TradingApp.Persistence/PersistenceServiceExtensions.cs` — Register in DI
- **Success**:
  - `BulkInsertAsync` uses `INSERT OR IGNORE` for idempotent inserts
  - `GetLatestTimestampAsync` returns most recent funding rate timestamp for a symbol
  - Repository registered as scoped in `PersistenceServiceExtensions`
- **Dependencies**: Task 4.2

#### Implementation Details

```csharp
// src/TradingApp.Application/Abstractions/Repositories/IFundingRateRepository.cs — new file
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Abstractions.Repositories;

public interface IFundingRateRepository
{
    Task BulkInsertAsync(IEnumerable<FundingRate> fundingRates, CancellationToken cancellationToken = default);
    Task<long?> GetLatestTimestampAsync(string symbol, CancellationToken cancellationToken = default);
}
```

```csharp
// src/TradingApp.Persistence/Repositories/FundingRateRepository.cs — new file
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Domain.Entities;

namespace TradingApp.Persistence.Repositories;

public sealed class FundingRateRepository : IFundingRateRepository
{
    private readonly TradingAppDbContext _context;

    public FundingRateRepository(TradingAppDbContext context)
    {
        _context = context;
    }

    public async Task BulkInsertAsync(IEnumerable<FundingRate> fundingRates, CancellationToken cancellationToken = default)
    {
        var rates = fundingRates.ToList();
        if (rates.Count == 0) return;

        const int batchSize = 500;
        for (var batch = 0; batch < rates.Count; batch += batchSize)
        {
            var chunk = rates.Skip(batch).Take(batchSize).ToList();
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var sql = new StringBuilder();
            var parameters = new List<SqliteParameter>();

            sql.Append("INSERT OR IGNORE INTO FundingRates (Symbol, Timestamp, Rate, MarkPrice) VALUES ");

            for (var i = 0; i < chunk.Count; i++)
            {
                if (i > 0) sql.Append(", ");
                var offset = i * 4;
                sql.Append($"(@p{offset}, @p{offset + 1}, @p{offset + 2}, @p{offset + 3})");

                var rate = chunk[i];
                parameters.Add(new SqliteParameter($"@p{offset}", rate.Symbol));
                parameters.Add(new SqliteParameter($"@p{offset + 1}", rate.Timestamp));
                parameters.Add(new SqliteParameter($"@p{offset + 2}", (double)rate.Rate));
                parameters.Add(new SqliteParameter($"@p{offset + 3}", (double)rate.MarkPrice));
            }

            await _context.Database.ExecuteSqlRawAsync(sql.ToString(), parameters, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
    }

    public async Task<long?> GetLatestTimestampAsync(string symbol, CancellationToken cancellationToken = default)
    {
        return await _context.FundingRates
            .Where(f => f.Symbol == symbol)
            .OrderByDescending(f => f.Timestamp)
            .Select(f => (long?)f.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
```

```csharp
// src/TradingApp.Persistence/PersistenceServiceExtensions.cs — modification
// Add repository registration in AddPersistence method:
services.AddScoped<IFundingRateRepository, FundingRateRepository>();
```

##### Pattern References

- `src/TradingApp.Persistence/Repositories/CandleRepository.cs` — existing `BulkInsertAsync` with raw SQL `INSERT OR IGNORE`
- `src/TradingApp.Application/Abstractions/Repositories/ICandleRepository.cs` — existing repository interface placement

---

### Task 4.4: Add `GetFundingRatesAsync` to `IBinanceFuturesRestClient` and implementation {#task-44-add-getfundingratesasync-to-rest-client}

Extend the Binance REST client with funding rate endpoint support. Create the wire model for the funding rate API response.

- **Complexity**: Medium
- **Risk Factors**: Different response format from klines (JSON objects, not arrays)
- **Files**:
  - `src/TradingApp.Application/Abstractions/Services/IBinanceFuturesRestClient.cs` — Add method
  - `src/TradingApp.Infrastructure/Services/BinanceFuturesRestClient.cs` — Add implementation
  - `src/TradingApp.Infrastructure/Binance/Models/BinanceFundingRate.cs` — New wire model
  - `src/TradingApp.Application/FundingRates/Models/FundingRateDto.cs` — New DTO
- **Success**:
  - `GetFundingRatesAsync` calls `GET /fapi/v1/fundingRate?symbol=X&startTime=Y&limit=1000`
  - Response deserialized from JSON objects to `FundingRateDto`
  - Error mapping consistent with existing patterns
- **Dependencies**: Phase 2 (existing REST client)

#### Implementation Details

```csharp
// src/TradingApp.Application/FundingRates/Models/FundingRateDto.cs — new file
namespace TradingApp.Application.FundingRates.Models;

public sealed class FundingRateDto
{
    public long FundingTime { get; init; }
    public decimal FundingRate { get; init; }
    public decimal MarkPrice { get; init; }
}
```

```csharp
// src/TradingApp.Infrastructure/Binance/Models/BinanceFundingRate.cs — new file
using System.Text.Json.Serialization;
using System.Globalization;
using TradingApp.Application.FundingRates.Models;

namespace TradingApp.Infrastructure.Binance.Models;

public sealed class BinanceFundingRate
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("fundingTime")]
    public long FundingTime { get; init; }

    [JsonPropertyName("fundingRate")]
    public string FundingRateValue { get; init; } = string.Empty;

    [JsonPropertyName("markPrice")]
    public string MarkPriceValue { get; init; } = string.Empty;

    public FundingRateDto ToDto() => new()
    {
        FundingTime = FundingTime,
        FundingRate = decimal.Parse(FundingRateValue, CultureInfo.InvariantCulture),
        MarkPrice = decimal.Parse(MarkPriceValue, CultureInfo.InvariantCulture)
    };
}
```

```csharp
// src/TradingApp.Application/Abstractions/Services/IBinanceFuturesRestClient.cs — modification
// Add new method to interface:
Task<IReadOnlyList<FundingRateDto>> GetFundingRatesAsync(
    string futuresSymbol,
    long startTime,
    long? endTime = null,
    int limit = 1000,
    CancellationToken cancellationToken = default);
```

```csharp
// src/TradingApp.Infrastructure/Services/BinanceFuturesRestClient.cs — modification
// Add implementation:
public async Task<IReadOnlyList<FundingRateDto>> GetFundingRatesAsync(
    string futuresSymbol,
    long startTime,
    long? endTime = null,
    int limit = 1000,
    CancellationToken cancellationToken = default)
{
    var url = $"/fapi/v1/fundingRate?symbol={futuresSymbol}&startTime={startTime}&limit={limit}";
    if (endTime.HasValue)
        url += $"&endTime={endTime.Value}";

    using var response = await _httpClient.GetAsync(url, cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        MapErrorResponse(response.StatusCode, body);
    }

    await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
    var rates = await JsonSerializer.DeserializeAsync<List<BinanceFundingRate>>(stream, cancellationToken: cancellationToken);

    return rates?.Select(r => r.ToDto()).ToList() ?? [];
}
```

##### Pattern References

- `src/TradingApp.Infrastructure/Services/BinanceFuturesRestClient.cs` — existing `GetKlinesAsync` pattern
- `src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidCandle.cs` — wire model with `JsonPropertyName`

---

### Task 4.5: Create `IFundingRateIngestionService` and `FundingRateIngestionService` {#task-45-create-funding-rate-ingestion-service}

Create the funding rate ingestion service with forward pagination and concurrency guard.

- **Complexity**: High
- **Risk Factors**: Simpler than candle ingestion (no binary search needed for funding rates — data is continuous at 8h intervals), but still requires pagination logic
- **Files**:
  - `src/TradingApp.Application/Abstractions/Services/IFundingRateIngestionService.cs` — New file
  - `src/TradingApp.Infrastructure/Services/FundingRateIngestionService.cs` — New file
  - `src/TradingApp.Application/FundingRates/Models/FundingRateIngestionResult.cs` — New file
  - `src/TradingApp.Application/FundingRates/Models/FundingRateIngestionRequest.cs` — New file
- **Success**:
  - Paginates forward from default start (2019-09-01) or last stored timestamp
  - Concurrent ingestion guard throws `IngestionAlreadyRunningException`
  - Creates `FundingRate` entities with display symbol (BTC)
  - Bulk inserts via `IFundingRateRepository`
  - Returns result with total counts
- **Dependencies**: Tasks 4.3, 4.4

#### Implementation Details

```csharp
// src/TradingApp.Application/FundingRates/Models/FundingRateIngestionRequest.cs — new file
namespace TradingApp.Application.FundingRates.Models;

public sealed class FundingRateIngestionRequest
{
    public required string Symbol { get; init; }
    public long? StartTime { get; init; }
    public long? EndTime { get; init; }
}
```

```csharp
// src/TradingApp.Application/FundingRates/Models/FundingRateIngestionResult.cs — new file
namespace TradingApp.Application.FundingRates.Models;

public sealed class FundingRateIngestionResult
{
    public string Symbol { get; init; } = string.Empty;
    public int TotalFetched { get; init; }
    public int TotalInserted { get; init; }
    public int TotalSkipped { get; init; }
    public long ElapsedMs { get; init; }
    public string? EarliestTimestamp { get; init; }
    public string? LatestTimestamp { get; init; }
    public string? Error { get; init; }
}
```

```csharp
// src/TradingApp.Application/Abstractions/Services/IFundingRateIngestionService.cs — new file
using TradingApp.Application.FundingRates.Models;

namespace TradingApp.Application.Abstractions.Services;

public interface IFundingRateIngestionService
{
    Task<FundingRateIngestionResult> IngestAsync(
        FundingRateIngestionRequest request, CancellationToken cancellationToken = default);
}
```

```csharp
// src/TradingApp.Infrastructure/Services/FundingRateIngestionService.cs — new file
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.FundingRates.Models;
using TradingApp.Domain.Entities;
using TradingApp.Infrastructure.Binance;

namespace TradingApp.Infrastructure.Services;

public sealed class FundingRateIngestionService : IFundingRateIngestionService
{
    private static readonly SemaphoreSlim Guard = new(1, 1);

    private readonly IBinanceFuturesRestClient _restClient;
    private readonly IFundingRateRepository _repository;
    private readonly BinanceIngestionOptions _options;
    private readonly ILogger<FundingRateIngestionService> _logger;

    public FundingRateIngestionService(
        IBinanceFuturesRestClient restClient,
        IFundingRateRepository repository,
        IOptions<BinanceIngestionOptions> options,
        ILogger<FundingRateIngestionService> logger)
    {
        _restClient = restClient;
        _repository = repository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FundingRateIngestionResult> IngestAsync(
        FundingRateIngestionRequest request, CancellationToken cancellationToken = default)
    {
        if (!Guard.Wait(0))
            throw new IngestionAlreadyRunningException("Funding rate ingestion is already running.");

        try
        {
            var sw = Stopwatch.StartNew();
            var futuresSymbol = BinanceAssetMapper.ToFuturesSymbol(request.Symbol);
            var totalFetched = 0;
            var totalInserted = 0;

            // Determine start time
            var latestStored = await _repository.GetLatestTimestampAsync(request.Symbol, cancellationToken);
            var cursor = request.StartTime
                ?? latestStored + 1
                ?? new DateTimeOffset(_options.DefaultStartDate).ToUnixTimeMilliseconds();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.MaxIngestionTimeoutMs);

            long? earliestTimestamp = null;
            long? latestTimestamp = null;

            // Forward pagination loop
            while (!timeoutCts.Token.IsCancellationRequested)
            {
                var batch = await _restClient.GetFundingRatesAsync(
                    futuresSymbol, cursor, request.EndTime, 1000, timeoutCts.Token);

                if (batch.Count == 0)
                    break;

                totalFetched += batch.Count;

                var entities = batch.Select(dto => FundingRate.Create(
                    request.Symbol, dto.FundingTime, dto.FundingRate, dto.MarkPrice)).ToList();

                await _repository.BulkInsertAsync(entities, timeoutCts.Token);
                totalInserted += entities.Count;

                earliestTimestamp ??= batch[0].FundingTime;
                latestTimestamp = batch[^1].FundingTime;

                cursor = batch[^1].FundingTime + 1;

                if (batch.Count < 1000)
                    break;

                await Task.Delay(_options.BatchDelayMs, timeoutCts.Token);
            }

            sw.Stop();
            return new FundingRateIngestionResult
            {
                Symbol = request.Symbol,
                TotalFetched = totalFetched,
                TotalInserted = totalInserted,
                TotalSkipped = totalFetched - totalInserted,
                ElapsedMs = sw.ElapsedMilliseconds,
                EarliestTimestamp = earliestTimestamp.HasValue
                    ? DateTimeOffset.FromUnixTimeMilliseconds(earliestTimestamp.Value).ToString("o")
                    : null,
                LatestTimestamp = latestTimestamp.HasValue
                    ? DateTimeOffset.FromUnixTimeMilliseconds(latestTimestamp.Value).ToString("o")
                    : null
            };
        }
        finally
        {
            Guard.Release();
        }
    }
}
```

##### Pattern References

- `src/TradingApp.Infrastructure/Services/CandleIngestionService.cs` — pagination loop, concurrency guard, timeout pattern

---

### Task 4.6: Create `IngestFundingRatesCommand`, handler, and `FundingRatesController` {#task-46-create-api-layer}

Create the MediatR command, handler, API controller, and request model for funding rate ingestion.

- **Complexity**: Medium
- **Risk Factors**: New controller for new resource path `/api/funding`
- **Files**:
  - `src/TradingApp.Application/FundingRates/Commands/IngestFundingRatesCommand.cs` — New file
  - `src/TradingApp.Api/Controllers/FundingRatesController.cs` — New file
  - `src/TradingApp.Api/Models/IngestFundingRatesRequest.cs` — New file
- **Success**:
  - `POST /api/funding/ingest` accepts `{ "symbol": "BTC" }`
  - Invalid symbol → 400 with valid Binance symbol list
  - Concurrent ingestion → 409 Conflict
  - Returns `FundingRateIngestionResult`
- **Dependencies**: Task 4.5

#### Implementation Details

```csharp
// src/TradingApp.Application/FundingRates/Commands/IngestFundingRatesCommand.cs — new file
using TradingApp.Application.Abstractions.Commands;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.FundingRates.Models;

namespace TradingApp.Application.FundingRates.Commands;

public sealed record IngestFundingRatesCommand(FundingRateIngestionRequest Request)
    : Command<FundingRateIngestionResult>;

public sealed class IngestFundingRatesCommandHandler
    : CommandHandler<IngestFundingRatesCommand, FundingRateIngestionResult>
{
    private readonly IFundingRateIngestionService _ingestionService;

    public IngestFundingRatesCommandHandler(IFundingRateIngestionService ingestionService)
    {
        _ingestionService = ingestionService;
    }

    public override async Task<FundingRateIngestionResult> Handle(
        IngestFundingRatesCommand request, CancellationToken cancellationToken)
    {
        return await _ingestionService.IngestAsync(request.Request, cancellationToken);
    }
}
```

```csharp
// src/TradingApp.Api/Models/IngestFundingRatesRequest.cs — new file
using System.ComponentModel.DataAnnotations;

namespace TradingApp.Api.Models;

public sealed class IngestFundingRatesRequest
{
    [Required]
    public string Symbol { get; set; } = string.Empty;

    public long? StartTime { get; set; }
    public long? EndTime { get; set; }
}
```

```csharp
// src/TradingApp.Api/Controllers/FundingRatesController.cs — new file
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TradingApp.Api.Infrastructure;
using TradingApp.Api.Models;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.FundingRates.Commands;
using TradingApp.Application.FundingRates.Models;
using TradingApp.Infrastructure.Binance;

namespace TradingApp.Api.Controllers;

[Route("api/funding")]
public sealed class FundingRatesController : ApiController
{
    public FundingRatesController(IMediator mediator, IdentityService identityService)
        : base(mediator, identityService) { }

    [HttpPost("ingest")]
    [ProducesResponseType(typeof(FundingRateIngestionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> IngestAsync(
        [FromBody] IngestFundingRatesRequest request, CancellationToken cancellationToken)
    {
        if (!BinanceAssetMapper.IsValidSymbol(request.Symbol))
            throw new DomainException(
                $"Invalid symbol: '{request.Symbol}'. Valid symbols: {string.Join(", ", BinanceAssetMapper.ValidSymbols)}");

        var ingestionRequest = new FundingRateIngestionRequest
        {
            Symbol = request.Symbol,
            StartTime = request.StartTime,
            EndTime = request.EndTime
        };

        var result = await Mediator.Send(
            new IngestFundingRatesCommand(ingestionRequest), cancellationToken);

        return Ok(result);
    }
}
```

##### Pattern References

- `src/TradingApp.Api/Controllers/CandlesController.cs` — controller pattern with ApiController base, validation, MediatR dispatch
- `src/TradingApp.Application/Candles/Commands/IngestCandlesCommand.cs` — command + handler co-location

---

### Task 4.7: Wire up DI and configuration {#task-47-wire-up-di-and-configuration}

Register `FundingRateIngestionService` in Program.cs DI container.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Api/Program.cs` — Register `IFundingRateIngestionService`
- **Success**:
  - `IFundingRateIngestionService` → `FundingRateIngestionService` as scoped
- **Dependencies**: Task 4.6

#### Implementation Details

```csharp
// src/TradingApp.Api/Program.cs — modification
// Add after Binance ingestion service registration:
builder.Services.AddScoped<IFundingRateIngestionService, FundingRateIngestionService>();
```

##### Pattern References

- `src/TradingApp.Api/Program.cs` — existing `AddScoped` registrations

---

### Task 4.8: Write tests for all FundingRate components {#task-48-write-tests}

Write comprehensive tests for the FundingRate entity, repository, ingestion service, and API controller.

- **Complexity**: High
- **Risk Factors**: Multiple test files across different test projects
- **Files**:
  - `tests/TradingApp.Domain.Tests/Entities/FundingRateTests.cs` — New file
  - `tests/TradingApp.Persistence.Tests/Repositories/FundingRateRepositoryTests.cs` — New file
  - `tests/TradingApp.Api.Tests/Services/FundingRateIngestionServiceTests.cs` — New file
  - `tests/TradingApp.Api.Tests/Controllers/FundingRatesControllerTests.cs` — New file
- **Success**:
  - Entity tests: valid creation, null symbol throws, property assertions
  - Repository tests: bulk insert, duplicate skip, latest timestamp query
  - Ingestion service tests: single batch, multi-batch, concurrent guard, timeout
  - Controller tests: valid request → 200, invalid symbol → 400, concurrent → 409
- **Dependencies**: Tasks 4.1–4.7

#### Implementation Details

```csharp
// tests/TradingApp.Domain.Tests/Entities/FundingRateTests.cs — new file
[TestClass]
public sealed class FundingRateTests
{
    [TestMethod]
    public void GivenValidParameters_WhenCreate_ThenPropertiesAreSet()
    {
        var rate = FundingRate.Create("BTC", 1700000000000, 0.0001m, 50000m);

        rate.Symbol.Should().Be("BTC");
        rate.Timestamp.Should().Be(1700000000000);
        rate.Rate.Should().Be(0.0001m);
        rate.MarkPrice.Should().Be(50000m);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    public void GivenInvalidSymbol_WhenCreate_ThenThrowsArgumentException(string? symbol)
    {
        var act = () => FundingRate.Create(symbol!, 1700000000000, 0.0001m, 50000m);
        act.Should().Throw<ArgumentException>();
    }
}
```

```csharp
// tests/TradingApp.Persistence.Tests/Repositories/FundingRateRepositoryTests.cs — new file
// Pattern: In-memory SQLite with EnsureCreated(), separate contexts for write/verify
[TestClass]
public sealed class FundingRateRepositoryTests
{
    // Setup: SqliteConnection("Data Source=:memory:"), EnsureCreated
    // Test: BulkInsert stores records correctly
    // Test: BulkInsert duplicates are ignored (INSERT OR IGNORE)
    // Test: GetLatestTimestampAsync returns correct value
    // Test: GetLatestTimestampAsync returns null for unknown symbol
}
```

```csharp
// tests/TradingApp.Api.Tests/Controllers/FundingRatesControllerTests.cs — new file
[TestClass]
public sealed class FundingRatesControllerTests : BaseControllerTests
{
    [TestMethod]
    public async Task GivenValidRequest_WhenPostIngestFunding_ThenReturnsOkWithResult()
    {
        // Mock IFundingRateIngestionService, configure test client, assert 200 OK
    }

    [TestMethod]
    public async Task GivenInvalidSymbol_WhenPostIngestFunding_ThenReturnsBadRequest()
    {
        // Assert 400 for invalid symbol
    }

    [TestMethod]
    public async Task GivenConcurrentIngestion_WhenPostIngestFunding_ThenReturnsConflict()
    {
        // Mock throws IngestionAlreadyRunningException, assert 409
    }
}
```

##### Pattern References

- `tests/TradingApp.Domain.Tests/Entities/CandleTests.cs` — entity test pattern
- `tests/TradingApp.Persistence.Tests/Repositories/CandleRepositoryTests.cs` — in-memory SQLite repository test pattern
- `tests/TradingApp.Api.Tests/Controllers/CandlesControllerTests.cs` — controller integration test pattern

---

### Task 4.9: Build and run tests {#task-49-build-and-run-tests}

Build and run all affected test projects.

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
- **Dependencies**: Task 4.8

## Phase Success Criteria

- `FundingRate` entity created with factory pattern and BCL guards
- `FundingRates` table created with unique index `(Symbol, Timestamp)`
- `FundingRateRepository` supports `BulkInsertAsync` with `INSERT OR IGNORE`
- `FundingRateIngestionService` paginates forward with concurrency guard and timeout
- `POST /api/funding/ingest` endpoint validates symbols, returns `FundingRateIngestionResult`
- All entity, repository, service, and controller tests pass
