<!-- markdownlint-disable-file -->

# Task Details: F2 — Candle Ingestion Service

## Phase 3: API Endpoint & Exception Handling

## Standards and Knowledge References

- **api-controllers.instructions.md** — `ApiController` base class, `[Route("api/...")]` kebab-case, `[ProducesResponseType]` on every action, POST returns result via `Ok(result)`
- **csharp.instructions.md** — `sealed` classes, async/await with `CancellationToken`
- **testing.instructions.md** — MSTest + Moq + FluentAssertions 6.x, controller tests via `BaseControllerTests`, `Given_When_Then` naming
- **dotnet-architecture.instructions.md** — MediatR commands for POST operations, `Command<T>` base record, handler co-located with command

## Design References

- The `CandlesController` inherits `ApiController` (MediatR pattern) and dispatches via `Mediator.Send(new IngestCandlesCommand(...))`
- Validation of symbol/intervals is done at the API layer via a request model with data annotations + manual validation against `HyperliquidAssetMapper`
- The concurrency guard is in `CandleIngestionService` (Phase 2); the controller simply catches `IngestionAlreadyRunningException` which maps to 409 in `HttpGlobalExceptionFilter`
- The exception filter already uses a pattern-matching switch expression — adding a new arm for `IngestionAlreadyRunningException` follows the established convention

### Task 3.1: Create exception and 409 mapping {#task-31-create-exception-and-409-mapping}

Create `IngestionAlreadyRunningException` in the Application layer and add a 409 Conflict mapping arm to `HttpGlobalExceptionFilter`.

- **Complexity**: Low
- **Risk Factors**: Must insert the new arm in the correct position in the switch expression (before the catch-all `_` arm)
- **Files**:
  - `src/TradingApp.Application/Abstractions/Exceptions/IngestionAlreadyRunningException.cs` — New exception class
  - `src/TradingApp.Api/Infrastructure/Filters/HttpGlobalExceptionFilter.cs` — Add 409 mapping arm
- **Success**:
  - `IngestionAlreadyRunningException` extends `Exception` with a default message
  - `HttpGlobalExceptionFilter` maps it to 409 Conflict with error code `"ingestion_conflict"`
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Application/Abstractions/Exceptions/IngestionAlreadyRunningException.cs — new file
namespace TradingApp.Application.Abstractions.Exceptions;

public sealed class IngestionAlreadyRunningException : Exception
{
    public IngestionAlreadyRunningException()
        : base("A candle ingestion job is already running. Please wait for it to complete.")
    {
    }
}
```

```csharp
// src/TradingApp.Api/Infrastructure/Filters/HttpGlobalExceptionFilter.cs — modification
// Add new arm in the switch expression, before the HttpRequestException arm:

            // ... existing code ...
            SigningException ex => (
                StatusCodes.Status422UnprocessableEntity,
                new Envelope(ex.Message, "signing_error", correlationId)),

            IngestionAlreadyRunningException ex => (
                StatusCodes.Status409Conflict,
                new Envelope(ex.Message, "ingestion_conflict", correlationId)),

            HyperliquidApiException ex when ex.ExchangeStatusCode >= 400 && ex.ExchangeStatusCode < 500 => (
            // ... existing code ...
```

Add the required using at the top of the file:
```csharp
using TradingApp.Application.Abstractions.Exceptions;
```
(This using likely already exists since `DomainException` is already referenced.)

##### Pattern References

- `src/TradingApp.Api/Infrastructure/Filters/HttpGlobalExceptionFilter.cs` — existing switch expression pattern (DomainException→400, NotFoundException→404, RateLimitException→429)
- `src/TradingApp.Application/Abstractions/Exceptions/RateLimitException.cs` — existing custom exception pattern

---

### Task 3.2: Create `IngestCandlesCommand` {#task-32-create-ingestcandlescommand}

Create the MediatR command and handler that delegates to `ICandleIngestionService`. Validation of symbol and intervals is handled by the controller (which has access to `HyperliquidAssetMapper` via Api → Infrastructure reference chain).

- **Complexity**: Low
- **Risk Factors**: None — thin handler with no business logic
- **Files**:
  - `src/TradingApp.Application/Candles/Commands/IngestCandlesCommand.cs` — New command + handler (co-located)
- **Success**:
  - Command record carries `Symbol`, `Intervals`, optional `StartTime`/`EndTime`
  - Handler delegates to `ICandleIngestionService.IngestAsync()`
  - Returns `IngestionResult`
- **Dependencies**: Phase 2 (ICandleIngestionService), Phase 1 (CandleIngestionOptions)

#### Implementation Details

```csharp
// src/TradingApp.Application/Candles/Commands/IngestCandlesCommand.cs — new file
using TradingApp.Application.Abstractions.Commands;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Candles.Models;

namespace TradingApp.Application.Candles.Commands;

public sealed record IngestCandlesCommand(
    string Symbol,
    string[] Intervals,
    long? StartTime,
    long? EndTime) : Command<IngestionResult>;

public sealed class IngestCandlesCommandHandler : CommandHandler<IngestCandlesCommand, IngestionResult>
{
    private readonly ICandleIngestionService _ingestionService;

    public IngestCandlesCommandHandler(ICandleIngestionService ingestionService)
    {
        _ingestionService = ingestionService;
    }

    public override async Task<IngestionResult> Handle(IngestCandlesCommand command, CancellationToken cancellationToken)
    {
        var request = new IngestionRequest
        {
            Symbol = command.Symbol,
            Intervals = command.Intervals,
            StartTime = command.StartTime,
            EndTime = command.EndTime,
        };

        return await _ingestionService.IngestAsync(request, cancellationToken);
    }
}
```

##### Pattern References

- `src/TradingApp.Application/Abstractions/Commands/Command.cs` — `Command<T>` base record, `CommandHandler<TCommand, TResult>`
- `src/TradingApp.Application/MarketData/Queries/GetCandlesQuery.cs` — query + handler co-located in same file

---

### Task 3.3: Create `CandlesController` {#task-33-create-candlescontroller}

Create the API controller with `POST /api/candles/ingest` endpoint. Includes request model with data annotations, custom validation for symbol/intervals, and proper `[ProducesResponseType]` declarations.

**Prerequisite**: Add `IsValidCoin(string coin)` to `HyperliquidAssetMapper` to validate that a coin symbol exists in the known assets dictionary. This is needed because `ToCoin()` strips the `-PERP` suffix but does **not** validate against known symbols.

- **Complexity**: Medium
- **Risk Factors**: Must validate symbol against `HyperliquidAssetMapper` before MediatR dispatch; must return all possible status codes in `[ProducesResponseType]`
- **Files**:
  - `src/TradingApp.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs` — Add `IsValidCoin()` method
  - `src/TradingApp.Api/Models/IngestCandlesRequest.cs` — New request model
  - `src/TradingApp.Api/Controllers/CandlesController.cs` — New controller
- **Success**:
  - `POST /api/candles/ingest` accepts valid request and returns 200 OK with `IngestionResult`
  - Invalid/missing `Symbol` returns 400 (data annotations)
  - Invalid/missing `Intervals` returns 400 (data annotations)
  - Unknown symbol returns 400 (custom validation via `DomainException`)
  - Unsupported interval returns 400 (custom validation via `DomainException`)
  - Concurrent request returns 409 (via `IngestionAlreadyRunningException`)
- **Dependencies**: Task 3.1 (exception), Task 3.2 (command)

#### Implementation Details

```csharp
// src/TradingApp.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs — modification
// Add after the existing IsValidTimeframe method:

    public static bool IsValidCoin(string coin)
    {
        return CoinToDisplay.ContainsKey(coin);
    }
```

```csharp
// src/TradingApp.Api/Models/IngestCandlesRequest.cs — new file
using System.ComponentModel.DataAnnotations;

namespace TradingApp.Api.Models;

public sealed class IngestCandlesRequest
{
    [Required]
    public string Symbol { get; set; } = default!;

    [Required]
    [MinLength(1)]
    public string[] Intervals { get; set; } = default!;

    public long? StartTime { get; set; }

    public long? EndTime { get; set; }
}
```

```csharp
// src/TradingApp.Api/Controllers/CandlesController.cs — new file
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TradingApp.Api.Infrastructure;
using TradingApp.Api.Models;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Candles.Commands;
using TradingApp.Application.Candles.Models;
using TradingApp.Infrastructure.Hyperliquid;

namespace TradingApp.Api.Controllers;

[Route("api/candles")]
public sealed class CandlesController : ApiController
{
    public CandlesController(IMediator mediator, IdentityService identityService)
        : base(mediator, identityService)
    {
    }

    [HttpPost("ingest")]
    [ProducesResponseType(typeof(IngestionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> IngestAsync(
        [FromBody] IngestCandlesRequest request,
        CancellationToken cancellationToken)
    {
        // Validate symbol — ToCoin strips -PERP suffix, then IsValidCoin checks against known assets
        var coin = HyperliquidAssetMapper.ToCoin(request.Symbol);
        if (!HyperliquidAssetMapper.IsValidCoin(coin))
        {
            throw new DomainException(
                $"Unknown symbol '{request.Symbol}'. Supported: BTC, ETH, SOL, DOGE, AVAX, ARB, LINK, OP");
        }

        // Validate intervals
        foreach (var interval in request.Intervals)
        {
            if (!HyperliquidAssetMapper.IsValidTimeframe(interval))
            {
                throw new DomainException(
                    $"Invalid interval '{interval}'. Supported: 5m, 15m, 1h, 4h");
            }
        }

        var result = await Mediator.Send(
            new IngestCandlesCommand(
                request.Symbol,
                request.Intervals,
                request.StartTime,
                request.EndTime),
            cancellationToken);

        return Ok(result);
    }
}
```

##### Pattern References

- `src/TradingApp.Api/Controllers/MarketDataController.cs` — MediatR controller pattern inheriting `ApiController`
- `src/TradingApp.Api/Models/PlaceOrderRequest.cs` — request model with data annotations
- `src/TradingApp.Api/Infrastructure/ApiController.cs` — base class with `Mediator` and `IdentityService`

---

### Task 3.4: Write integration tests {#task-34-write-integration-tests}

Write integration tests for `CandlesController` following the `BaseControllerTests` pattern. Mock `ICandleIngestionService` to test endpoint behavior without real data.

- **Complexity**: Medium
- **Risk Factors**: Must properly replace `ICandleIngestionService` in test DI; must handle concurrent request testing
- **Files**:
  - `tests/TradingApp.Api.Tests/Controllers/CandlesControllerTests.cs` — New test class
- **Success**:
  - Valid request returns 200 OK with `IngestionResult`
  - Missing symbol returns 400
  - Empty intervals array returns 400
  - Invalid interval returns 400 with error message listing valid intervals
  - Concurrent request returns 409 Conflict
  - Response body matches `IngestionResult` shape
- **Dependencies**: Tasks 3.1, 3.2, 3.3

#### Implementation Details

```csharp
// tests/TradingApp.Api.Tests/Controllers/CandlesControllerTests.cs — new file
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TradingApp.Api.Tests.Infrastructure;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Candles.Models;

namespace TradingApp.Api.Tests.Controllers;

[TestClass]
public sealed class CandlesControllerTests : BaseControllerTests
{
    private const string BaseUrl = "api/candles";
    private const string TestPrivateKey = "0x4c0883a69102937d6231471b5dbb6204fe512961708279f2a4c5890a0c1f9b2e";

    private readonly Mock<ICandleIngestionService> _ingestionServiceMock = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Hyperliquid:PrivateKey", TestPrivateKey);
        builder.UseSetting("Hyperliquid:BaseUrl", "https://api.hyperliquid-testnet.xyz");
        builder.UseSetting("Hyperliquid:Network", "testnet");
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<ICandleIngestionService>();
        services.AddSingleton(_ingestionServiceMock.Object);
    }

    [TestMethod]
    public async Task GivenValidRequest_WhenPostIngest_ThenReturnsOkWithResult()
    {
        // Arrange
        var expectedResult = new IngestionResult
        {
            TotalFetched = 1000,
            TotalInserted = 1000,
            TotalSkipped = 0,
            ElapsedMs = 5000,
            Intervals = new List<IntervalResult>
            {
                new() { Interval = "1h", Fetched = 1000, Inserted = 1000, Skipped = 0 },
            },
        };

        _ingestionServiceMock
            .Setup(s => s.IngestAsync(It.IsAny<IngestionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var client = GetTestClient();

        // Act
        var response = await client.PostAsync(
            $"{BaseUrl}/ingest",
            GetStringContent(new { symbol = "BTC", intervals = new[] { "1h" } }));

        // Assert
        var result = await response.ReadAndAssertSuccessAsync<IngestionResult>();
        result.TotalFetched.Should().Be(1000);
        result.TotalInserted.Should().Be(1000);
    }

    [TestMethod]
    public async Task GivenMissingSymbol_WhenPostIngest_ThenReturnsBadRequest()
    {
        // Arrange
        var client = GetTestClient();

        // Act
        var response = await client.PostAsync(
            $"{BaseUrl}/ingest",
            GetStringContent(new { intervals = new[] { "1h" } }));

        // Assert
        response.AssertStatusCode(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task GivenEmptyIntervals_WhenPostIngest_ThenReturnsBadRequest()
    {
        // Arrange
        var client = GetTestClient();

        // Act
        var response = await client.PostAsync(
            $"{BaseUrl}/ingest",
            GetStringContent(new { symbol = "BTC", intervals = Array.Empty<string>() }));

        // Assert
        response.AssertStatusCode(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task GivenInvalidInterval_WhenPostIngest_ThenReturnsBadRequest()
    {
        // Arrange
        var client = GetTestClient();

        // Act
        var response = await client.PostAsync(
            $"{BaseUrl}/ingest",
            GetStringContent(new { symbol = "BTC", intervals = new[] { "invalid" } }));

        // Assert
        response.AssertStatusCode(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task GivenConcurrentIngestion_WhenPostIngest_ThenReturns409Conflict()
    {
        // Arrange
        _ingestionServiceMock
            .Setup(s => s.IngestAsync(It.IsAny<IngestionRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IngestionAlreadyRunningException());

        var client = GetTestClient();

        // Act
        var response = await client.PostAsync(
            $"{BaseUrl}/ingest",
            GetStringContent(new { symbol = "BTC", intervals = new[] { "1h" } }));

        // Assert
        response.AssertStatusCode(HttpStatusCode.Conflict);
    }
}
```

##### Pattern References

- `tests/TradingApp.Api.Tests/Controllers/MarketDataControllerTests.cs` — controller integration test pattern with `BaseControllerTests`, `ConfigureWebHost`, `ConfigureTestServices`, mock replacement
- `tests/TradingApp.Api.Tests/Infrastructure/BaseControllerTests.cs` — `GetTestClient()`, `ReadAndAssertSuccessAsync<T>()`, `AssertStatusCode()`

---

### Task 3.5: Build and run all tests {#task-35-build-and-run-all-tests}

Build the entire solution and run all tests to verify Phase 3 and the complete F2 implementation.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification step)
- **Success**:
  - `dotnet build` succeeds with no errors
  - All existing tests continue to pass
  - `CandlesControllerTests` pass (valid request → 200, missing symbol → 400, invalid interval → 400, concurrent → 409)
  - `CandleIngestionServiceTests` pass
  - `HyperliquidRestClientCandleSnapshotTests` pass
- **Dependencies**: Tasks 3.1–3.4

## Phase Success Criteria

- `POST /api/candles/ingest` returns 200 OK with `IngestionResult` for valid requests
- Invalid symbol/interval returns 400 Bad Request via `DomainException` mapping
- Concurrent ingestion returns 409 Conflict via `IngestionAlreadyRunningException` mapping
- `HttpGlobalExceptionFilter` includes 409 mapping arm
- `IngestCandlesCommand` MediatR command delegates to `ICandleIngestionService`
- All integration tests pass: valid request, missing symbol, empty intervals, invalid interval, concurrent request
- Full solution builds and all tests pass
