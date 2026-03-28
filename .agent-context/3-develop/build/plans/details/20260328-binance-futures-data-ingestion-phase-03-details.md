<!-- markdownlint-disable-file -->

# Task Details: Binance USDⓈ-M Futures Data Ingestion

## Phase 3: Binance Kline API Endpoint

## Standards and Knowledge References

- **api-controllers.instructions.md**: `ApiController` base, kebab-case routes, `[ProducesResponseType]` on all endpoints, POST action → 200 OK
- **dotnet-architecture.instructions.md**: `Command<T>` base record, handler in same file, handler tested only via controller integration tests
- **testing.instructions.md**: Controller tests extend `BaseControllerTests`, use `WebApplicationFactory`, swap real services with mocks via `ConfigureTestServices`
- **csharp.instructions.md**: Options bound via `AddOptions<T>().Bind().ValidateDataAnnotations().ValidateOnStart()`

---

### Task 3.1: Create `IngestBinanceCandlesCommand` and handler {#task-31-create-ingestbinancecandlescommand}

Create a MediatR command record and handler that delegates to `IBinanceCandleIngestionService`. Follows the existing `IngestCandlesCommand` pattern.

- **Complexity**: Low
- **Risk Factors**: None — direct delegation pattern
- **Files**:
  - `src/TradingApp.Application/Candles/Commands/IngestBinanceCandlesCommand.cs` — New file
- **Success**:
  - Command record wraps `IngestionRequest`, returns `IngestionResult`
  - Handler delegates to `IBinanceCandleIngestionService.IngestAsync()`
  - Handler and command in same file
- **Dependencies**: Phase 2 complete

#### Implementation Details

```csharp
// src/TradingApp.Application/Candles/Commands/IngestBinanceCandlesCommand.cs — new file
using TradingApp.Application.Abstractions.Commands;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Candles.Models;

namespace TradingApp.Application.Candles.Commands;

public sealed record IngestBinanceCandlesCommand(IngestionRequest Request) : Command<IngestionResult>;

public sealed class IngestBinanceCandlesCommandHandler
    : CommandHandler<IngestBinanceCandlesCommand, IngestionResult>
{
    private readonly IBinanceCandleIngestionService _ingestionService;

    public IngestBinanceCandlesCommandHandler(IBinanceCandleIngestionService ingestionService)
    {
        _ingestionService = ingestionService;
    }

    public override async Task<IngestionResult> Handle(
        IngestBinanceCandlesCommand request, CancellationToken cancellationToken)
    {
        return await _ingestionService.IngestAsync(request.Request, cancellationToken);
    }
}
```

##### Pattern References

- `src/TradingApp.Application/Candles/Commands/IngestCandlesCommand.cs` — existing command + handler co-location

---

### Task 3.2: Add `POST /api/candles/ingest/binance` endpoint to `CandlesController` {#task-32-add-binance-ingest-endpoint}

Add a new endpoint for Binance candle ingestion. Validate symbols and intervals using `BinanceAssetMapper` (not `HyperliquidAssetMapper`).

- **Complexity**: Medium
- **Risk Factors**: Symbol/interval validation must use Binance-specific mapper; must not break existing Hyperliquid endpoint
- **Files**:
  - `src/TradingApp.Api/Controllers/CandlesController.cs` — Add new endpoint method
- **Success**:
  - `POST /api/candles/ingest/binance` accepts `IngestCandlesRequest` body
  - Invalid symbols → 400 with Binance-specific valid symbol list
  - Invalid intervals → 400 with Binance-specific valid interval list
  - Successful ingestion → 200 with `IngestionResult`
  - Concurrent ingestion → 409 Conflict
  - `[ProducesResponseType]` attributes on the new endpoint
- **Dependencies**: Task 3.1

#### Implementation Details

```csharp
// src/TradingApp.Api/Controllers/CandlesController.cs — modification
// Add new endpoint method alongside existing IngestAsync:

[HttpPost("ingest/binance")]
[ProducesResponseType(typeof(IngestionResult), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(Envelope), StatusCodes.Status409Conflict)]
public async Task<IActionResult> IngestBinanceAsync(
    [FromBody] IngestCandlesRequest request, CancellationToken cancellationToken)
{
    if (!BinanceAssetMapper.IsValidSymbol(request.Symbol))
        throw new DomainException(
            $"Invalid symbol: '{request.Symbol}'. Valid Binance symbols: {string.Join(", ", BinanceAssetMapper.ValidSymbols)}");

    foreach (var interval in request.Intervals)
    {
        if (!BinanceAssetMapper.IsValidInterval(interval))
            throw new DomainException(
                $"Invalid interval: '{interval}'. Valid Binance intervals: {string.Join(", ", BinanceAssetMapper.ValidIntervals)}");
    }

    var ingestionRequest = new IngestionRequest
    {
        Symbol = request.Symbol,
        Intervals = request.Intervals,
        StartTime = request.StartTime,
        EndTime = request.EndTime
    };

    var result = await Mediator.Send(
        new IngestBinanceCandlesCommand(ingestionRequest), cancellationToken);

    return Ok(result);
}
```

Add the using at the top of the file:
```csharp
using TradingApp.Infrastructure.Binance;
using TradingApp.Application.Candles.Commands;
```

##### Pattern References

- `src/TradingApp.Api/Controllers/CandlesController.cs` — existing `IngestAsync` endpoint pattern

---

### Task 3.3: Wire up Binance DI registrations in `Program.cs` {#task-33-wire-up-binance-di}

Register all Binance services in the DI container: options, typed HttpClient with Polly resilience, and ingestion service.

- **Complexity**: Medium
- **Risk Factors**: Polly pipeline configuration must match Binance rate limits; typed HttpClient must match interface
- **Files**:
  - `src/TradingApp.Api/Program.cs` — Add DI registrations
- **Success**:
  - `BinanceIngestionOptions` bound and validated on start
  - `IBinanceFuturesRestClient` → `BinanceFuturesRestClient` with typed HttpClient and Polly retry
  - `IBinanceCandleIngestionService` → `BinanceCandleIngestionService` as scoped
  - BaseUrl configured from options
- **Dependencies**: Tasks 3.1, 3.2

#### Implementation Details

```csharp
// src/TradingApp.Api/Program.cs — modification
// Add after existing Hyperliquid DI registrations:

// Binance configuration
builder.Services.AddOptions<BinanceIngestionOptions>()
    .Bind(builder.Configuration.GetSection(BinanceIngestionOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Binance REST client with Polly resilience
builder.Services.AddHttpClient<IBinanceFuturesRestClient, BinanceFuturesRestClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<BinanceIngestionOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddResilienceHandler("binance-retry", pipeline =>
{
    pipeline.AddRetry(new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = 5,
        BackoffType = DelayBackoffType.Exponential,
        Delay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromSeconds(60),
        UseJitter = true,
        ShouldHandle = args => ValueTask.FromResult(
            args.Outcome.Result?.StatusCode == HttpStatusCode.TooManyRequests ||
            (args.Outcome.Result is not null && (int)args.Outcome.Result.StatusCode >= 500))
    });
    pipeline.AddTimeout(TimeSpan.FromSeconds(5));
});

// Binance ingestion service
builder.Services.AddScoped<IBinanceCandleIngestionService, BinanceCandleIngestionService>();
```

Add the using statements:
```csharp
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Infrastructure.Services;
```

##### Pattern References

- `src/TradingApp.Api/Program.cs` — existing Hyperliquid `AddHttpClient` + Polly pipeline registration

---

### Task 3.4: Add `BinanceIngestion` configuration to `appsettings.json` {#task-34-add-appsettings-configuration}

Add the Binance ingestion configuration section to the API appsettings file.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Api/appsettings.json` — Add BinanceIngestion section
- **Success**:
  - `BinanceIngestion` section present with all configurable properties
  - Values match PBI specification
- **Dependencies**: Task 2.1

#### Implementation Details

```json
// src/TradingApp.Api/appsettings.json — modification
// Add new section alongside existing CandleIngestion:
{
  "BinanceIngestion": {
    "BatchDelayMs": 250,
    "MaxRetries": 3,
    "MaxIngestionTimeoutMs": 7200000,
    "DefaultStartDate": "2019-09-01T00:00:00Z",
    "PageSize": 1500,
    "BaseUrl": "https://fapi.binance.com"
  }
}
```

##### Pattern References

- `src/TradingApp.Api/appsettings.json` — existing `CandleIngestion` section

---

### Task 3.5: Create controller tests for Binance ingestion endpoint {#task-35-create-controller-tests}

Add integration tests for the `POST /api/candles/ingest/binance` endpoint. Tests exercise the full MediatR pipeline with mocked `IBinanceCandleIngestionService`.

- **Complexity**: Medium
- **Risk Factors**: Must mock `IBinanceCandleIngestionService` (not `ICandleIngestionService`) in test DI
- **Files**:
  - `tests/TradingApp.Api.Tests/Controllers/CandlesControllerTests.cs` — Add Binance endpoint tests (or create new `BinanceCandlesControllerTests.cs` if cleaner)
- **Success**:
  - Test: Valid request → 200 OK with `IngestionResult`
  - Test: Invalid symbol → 400 Bad Request with valid Binance symbols
  - Test: Invalid interval → 400 Bad Request with valid Binance intervals
  - Test: Concurrent ingestion → 409 Conflict
  - Test: Missing required fields → 400 Bad Request
- **Dependencies**: Tasks 3.2, 3.3

#### Implementation Details

```csharp
// tests/TradingApp.Api.Tests/Controllers/CandlesControllerTests.cs — modification
// Add new test methods for the Binance endpoint:

[TestMethod]
public async Task GivenValidRequest_WhenPostIngestBinance_ThenReturnsOkWithResult()
{
    var expectedResult = new IngestionResult { TotalFetched = 1000, TotalInserted = 1000 };
    var mockService = new Mock<IBinanceCandleIngestionService>(MockBehavior.Strict);
    mockService
        .Setup(s => s.IngestAsync(It.IsAny<IngestionRequest>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(expectedResult);

    var client = GetTestClient(services =>
    {
        services.RemoveAll<IBinanceCandleIngestionService>();
        services.AddSingleton(mockService.Object);
    });

    var request = new { symbol = "BTC", intervals = new[] { "15m", "1h" } };
    var response = await client.PostAsync("/api/candles/ingest/binance", GetStringContent(request));

    var result = await response.ReadAndAssertSuccessAsync<IngestionResult>();
    result.TotalFetched.Should().Be(1000);
}

[TestMethod]
public async Task GivenInvalidSymbol_WhenPostIngestBinance_ThenReturnsBadRequest()
{
    var client = GetTestClient();
    var request = new { symbol = "INVALID", intervals = new[] { "15m" } };

    var response = await client.PostAsync("/api/candles/ingest/binance", GetStringContent(request));
    response.AssertStatusCode(HttpStatusCode.BadRequest);
}

[TestMethod]
public async Task GivenInvalidInterval_WhenPostIngestBinance_ThenReturnsBadRequest()
{
    var client = GetTestClient();
    var request = new { symbol = "BTC", intervals = new[] { "3m" } };

    var response = await client.PostAsync("/api/candles/ingest/binance", GetStringContent(request));
    response.AssertStatusCode(HttpStatusCode.BadRequest);
}

[TestMethod]
public async Task GivenConcurrentIngestion_WhenPostIngestBinance_ThenReturnsConflict()
{
    var mockService = new Mock<IBinanceCandleIngestionService>(MockBehavior.Strict);
    mockService
        .Setup(s => s.IngestAsync(It.IsAny<IngestionRequest>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new IngestionAlreadyRunningException("Already running"));

    var client = GetTestClient(services =>
    {
        services.RemoveAll<IBinanceCandleIngestionService>();
        services.AddSingleton(mockService.Object);
    });

    var request = new { symbol = "BTC", intervals = new[] { "15m" } };
    var response = await client.PostAsync("/api/candles/ingest/binance", GetStringContent(request));
    response.AssertStatusCode(HttpStatusCode.Conflict);
}
```

##### Pattern References

- `tests/TradingApp.Api.Tests/Controllers/CandlesControllerTests.cs` — existing controller test pattern with `GetTestClient`, `ConfigureTestServices`

---

### Task 3.6: Build and run tests {#task-36-build-and-run-tests}

Build and run all affected test projects.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification step)
- **Success**:
  - `dotnet build tests/TradingApp.Api.Tests` — succeeds
  - `dotnet test tests/TradingApp.Api.Tests` — all tests pass (existing + new)
- **Dependencies**: Task 3.5

## Phase Success Criteria

- `IngestBinanceCandlesCommand` and handler created following MediatR pattern
- `POST /api/candles/ingest/binance` endpoint validates Binance symbols/intervals and returns `IngestionResult`
- Binance DI registrations in `Program.cs` (options, HttpClient with Polly, ingestion service)
- `BinanceIngestion` config section in `appsettings.json`
- All controller integration tests pass (existing Hyperliquid + new Binance)
- Existing `POST /api/candles/ingest` endpoint unchanged and tests still pass
