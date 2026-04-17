<!-- markdownlint-disable-file -->

# Task Details: F4 — Backtest API & Results

## Phase 3: API Controller & Integration Tests

## Standards and Knowledge References

- **api-controllers.instructions.md**: Controller extends `ApiController`, `[Route("api/backtests")]`, MediatR dispatch, `[ProducesResponseType]` attributes, error paths throw exceptions caught by global filter
- **csharp.instructions.md**: Sealed classes, PascalCase naming, data annotations for request validation
- **testing.instructions.md**: MSTest, Moq, FluentAssertions v6, `BaseControllerTests` for controller integration tests, Given_When_Then naming, mock injection via `ConfigureTestServices`
- **dotnet-architecture.instructions.md**: Request models in `TradePilot.Api/Models/`, validation via data annotations + domain exception guards

## Design References

- `GET /api/backtests/validate` must be routed before `GET /api/backtests/{id}` to prevent ASP.NET Core from treating "validate" as a GUID parameter
- `POST /api/backtests` returns HTTP 200 with the full result (per PBI: "the endpoint returns 200 with the BacktestResult"), not 201 Created
- Controller tests mock `IBacktestRunner` and `IBacktestRunRepository` to isolate the API layer from persistence and engine logic

### Task 3.1: Create `RunBacktestRequest` API model {#task-31-create-runbacktestrequest-api-model}

Create the API request model with data annotation validation attributes.

- **Complexity**: Medium
- **Risk Factors**: Must include all PBI validation rules as data annotations; `GridStrategyConfig` nested object validation
- **Files**:
  - `src/TradePilot.Api/Models/RunBacktestRequest.cs` — new file
- **Success**:
  - Request model has all fields from PBI spec including `initialCapital`
  - Data annotations enforce basic validation (Required, Range)
  - Model binds correctly from JSON
- **Dependencies**: Phase 2 complete (GridStrategyConfig DTO exists)

#### Implementation Details

```csharp
// src/TradePilot.Api/Models/RunBacktestRequest.cs — new file
using System.ComponentModel.DataAnnotations;
using TradePilot.Application.Backtesting.Models;

namespace TradePilot.Api.Models;

public sealed class RunBacktestRequest
{
    [Required]
    public string Symbol { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public string[] Intervals { get; set; } = [];

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "initialCapital must be > 0")]
    public decimal InitialCapital { get; set; }

    [Required]
    public GridStrategyConfigRequest StrategyConfig { get; set; } = null!;
}

public sealed class GridStrategyConfigRequest
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "gridLevels must be > 0")]
    public int GridLevels { get; set; }

    [Required]
    [Range(0.001, double.MaxValue, ErrorMessage = "gridSpacing must be > 0")]
    public decimal GridSpacing { get; set; }

    [Required]
    [Range(0.001, double.MaxValue, ErrorMessage = "takeProfitPercent must be > 0")]
    public decimal TakeProfitPercent { get; set; }

    [Required]
    public decimal BreakdownThreshold { get; set; }

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "makerFee must be >= 0")]
    public decimal MakerFee { get; set; }

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "takerFee must be >= 0")]
    public decimal TakerFee { get; set; }

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "slippage must be >= 0")]
    public decimal Slippage { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "positionSize must be > 0")]
    public decimal PositionSize { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "leverage must be > 0")]
    public decimal Leverage { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "stopLossPercent must be > 0")]
    public decimal StopLossPercent { get; set; }
}
```

##### Pattern References

- `src/TradePilot.Api/Models/IngestCandlesRequest.cs` — request model with `[Required]` data annotations
- `src/TradePilot.Api/Models/PlaceOrderRequest.cs` — request model with `[Required]`, `[Range]` annotations

### Task 3.2: Create `BacktestsController` {#task-32-create-backtestscontroller}

Create the API controller with all three endpoints. The controller extends `ApiController`, validates inputs, and dispatches MediatR commands/queries.

- **Complexity**: Medium
- **Risk Factors**: Route ordering for `/validate` vs `/{id}`; correct mapping from `RunBacktestRequest` to `RunBacktestCommand`; symbol/interval validation using `BinanceAssetMapper`
- **Files**:
  - `src/TradePilot.Api/Controllers/BacktestsController.cs` — new file
- **Success**:
  - `POST /api/backtests` validates inputs, dispatches `RunBacktestCommand`, returns 200
  - `GET /api/backtests/{id}` dispatches `GetBacktestResultQuery`, returns 200 or 404
  - `GET /api/backtests/validate` dispatches `GetCandleCoverageQuery`, returns 200
  - All `[ProducesResponseType]` attributes are present
  - Symbol and interval validation throw `DomainException` (→ 400)
- **Dependencies**: Phase 2 complete, Task 3.1

#### Implementation Details

```csharp
// src/TradePilot.Api/Controllers/BacktestsController.cs — new file
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TradePilot.Api.Infrastructure;
using TradePilot.Api.Models;
using TradePilot.Application.Backtesting;
using TradePilot.Application.Backtesting.Models;
using TradePilot.Domain.Exceptions;
using TradePilot.Infrastructure.Binance;

namespace TradePilot.Api.Controllers;

[Route("api/backtests")]
public sealed class BacktestsController : ApiController
{
    public BacktestsController(IMediator mediator, IdentityService identityService)
        : base(mediator, identityService) { }

    [HttpPost]
    [ProducesResponseType(typeof(BacktestRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status408RequestTimeout)]
    public async Task<IActionResult> RunAsync(
        [FromBody] RunBacktestRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        var strategyConfig = new GridStrategyConfig
        {
            GridLevels = request.StrategyConfig.GridLevels,
            GridSpacing = request.StrategyConfig.GridSpacing,
            TakeProfitPercent = request.StrategyConfig.TakeProfitPercent,
            BreakdownThreshold = request.StrategyConfig.BreakdownThreshold,
            MakerFee = request.StrategyConfig.MakerFee,
            TakerFee = request.StrategyConfig.TakerFee,
            Slippage = request.StrategyConfig.Slippage,
            PositionSize = request.StrategyConfig.PositionSize,
            Leverage = request.StrategyConfig.Leverage,
            StopLossPercent = request.StrategyConfig.StopLossPercent
        };

        var command = new RunBacktestCommand(
            Symbol: request.Symbol,
            Intervals: request.Intervals,
            StartDate: request.StartDate,
            EndDate: request.EndDate,
            StrategyConfig: strategyConfig,
            InitialCapital: request.InitialCapital);

        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpGet("validate")]
    [ProducesResponseType(typeof(CandleCoverageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ValidateAsync(
        [FromQuery] string symbol,
        [FromQuery] string intervals,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new DomainException("symbol is required");

        if (string.IsNullOrWhiteSpace(intervals))
            throw new DomainException("intervals is required");

        if (!BinanceAssetMapper.IsValidSymbol(symbol))
            throw new DomainException($"Unknown symbol '{symbol}'. Supported: {string.Join(", ", BinanceAssetMapper.ValidSymbols)}");

        var intervalArray = intervals.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var interval in intervalArray)
        {
            if (!BinanceAssetMapper.IsValidInterval(interval))
                throw new DomainException($"Invalid interval '{interval}'. Valid: {string.Join(", ", BinanceAssetMapper.ValidIntervals)}");
        }

        var query = new GetCandleCoverageQuery(symbol, intervalArray);
        var result = await Mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BacktestRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetBacktestResultQuery(id), cancellationToken);
        return Ok(result);
    }

    private static void ValidateRequest(RunBacktestRequest request)
    {
        if (!BinanceAssetMapper.IsValidSymbol(request.Symbol))
            throw new DomainException($"Unknown symbol '{request.Symbol}'. Supported: {string.Join(", ", BinanceAssetMapper.ValidSymbols)}");

        foreach (var interval in request.Intervals)
        {
            if (!BinanceAssetMapper.IsValidInterval(interval))
                throw new DomainException($"Invalid interval '{interval}'. Valid: {string.Join(", ", BinanceAssetMapper.ValidIntervals)}");
        }

        if (request.EndDate <= request.StartDate)
            throw new DomainException("endDate must be after startDate");
    }
}
```

> **Note**: The exact namespace and static members of `BinanceAssetMapper` are confirmed: use `BinanceAssetMapper.IsValidSymbol()`, `BinanceAssetMapper.IsValidInterval()`, `BinanceAssetMapper.ValidSymbols` (IReadOnlyCollection<string>), and `BinanceAssetMapper.ValidIntervals` (IReadOnlyCollection<string>). Namespace: `TradePilot.Infrastructure.Binance`.

> **Note**: The `validate` action is declared before `{id:guid}` and uses the route template `"validate"` — ASP.NET Core's `{id:guid}` constraint will not match the literal string "validate", but explicit ordering is a safety measure.

##### Pattern References

- `src/TradePilot.Api/Controllers/CandlesController.cs` — controller extending ApiController, MediatR dispatch, DomainException for validation, `[ProducesResponseType]` attributes
- `src/TradePilot.Api/Controllers/FundingRatesController.cs` — same MediatR + validation pattern
- `src/TradePilot.Api/Infrastructure/ApiController.cs` — base controller with `Mediator` and `IdentityService`

### Task 3.3: Write `BacktestsControllerTests` — happy paths {#task-33-write-backtestscontrollertests-happy-paths}

Write controller integration tests for the successful scenarios using `BaseControllerTests`.

- **Complexity**: Medium
- **Risk Factors**: Must correctly mock `IBacktestRunner` and `IBacktestRunRepository`; must configure Hyperliquid settings to prevent DI failures
- **Files**:
  - `tests/TradePilot.Api.Tests/Controllers/BacktestsControllerTests.cs` — new file
- **Success**:
  - POST happy path: valid request → 200 with full result
  - GET by ID happy path: valid ID → 200 with full result
  - GET validate happy path: valid symbol/intervals → 200 with coverage
  - All assertions check response body content
- **Dependencies**: Task 3.1, Task 3.2

#### Implementation Details

```csharp
// tests/TradePilot.Api.Tests/Controllers/BacktestsControllerTests.cs — new file
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TradePilot.Api.Models;
using TradePilot.Api.Tests.Infrastructure;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Backtesting.Models;
using TradePilot.Domain.Entities;

namespace TradePilot.Api.Tests.Controllers;

[TestClass]
public sealed class BacktestsControllerTests : BaseControllerTests
{
    private const string BaseUrl = "api/backtests";

    private readonly Mock<IBacktestRunner> _backtestRunnerMock = new();
    private readonly Mock<IBacktestRunRepository> _backtestRunRepositoryMock = new();
    private readonly Mock<ICandleRepository> _candleRepositoryMock = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<IBacktestRunner>();
        services.AddSingleton(_backtestRunnerMock.Object);

        services.RemoveAll<IBacktestRunRepository>();
        services.AddSingleton(_backtestRunRepositoryMock.Object);

        services.RemoveAll<ICandleRepository>();
        services.AddSingleton(_candleRepositoryMock.Object);
    }

    [TestMethod]
    public async Task GivenValidRequest_WhenPostBacktest_ThenReturnsOkWithResult()
    {
        // Arrange
        var backtestResult = CreateMockBacktestResult();
        _backtestRunnerMock
            .Setup(r => r.RunAsync(It.IsAny<BacktestConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(backtestResult);
        _backtestRunRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<BacktestRun>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var client = GetTestClient();
        var request = CreateValidRequest();

        // Act
        var response = await client.PostAsJsonAsync(BaseUrl, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.ReadAndAssertSuccessAsync<BacktestRunResponse>();
        result.Symbol.Should().Be("BTC");
        result.TotalTrades.Should().Be(10);
        result.Trades.Should().HaveCount(1);

        _backtestRunnerMock.Verify(
            r => r.RunAsync(It.IsAny<BacktestConfig>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _backtestRunRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<BacktestRun>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenExistingBacktestId_WhenGetById_ThenReturnsOkWithResult()
    {
        // Arrange
        var backtestRun = BacktestRun.Create(
            symbol: "BTC",
            intervalsJson: "[\"15m\",\"1h\",\"4h\"]",
            startDateUtc: 1704067200000,
            endDateUtc: 1735689599000,
            strategyConfigJson: "{\"gridLevels\":10,\"gridSpacing\":0.5}",
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

        _backtestRunRepositoryMock
            .Setup(r => r.GetByIdAsync(backtestRun.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(backtestRun);

        var client = GetTestClient();

        // Act
        var response = await client.GetAsync($"{BaseUrl}/{backtestRun.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.ReadAndAssertSuccessAsync<BacktestRunResponse>();
        result.Id.Should().Be(backtestRun.Id);
        result.Symbol.Should().Be("BTC");
        result.TotalTrades.Should().Be(847);
    }

    [TestMethod]
    public async Task GivenValidSymbolAndIntervals_WhenValidate_ThenReturnsOkWithCoverage()
    {
        // Arrange
        var candles = new List<TradePilot.Domain.Entities.Candle>
        {
            // Implementer: create test Candle entities using Candle.Create()
            // covering the expected date range for BTC/15m
        };
        _candleRepositoryMock
            .Setup(r => r.GetCandlesAsync("BTC", "15m", It.IsAny<long>(), It.IsAny<long>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(candles.AsReadOnly());

        var client = GetTestClient();

        // Act
        var response = await client.GetAsync($"{BaseUrl}/validate?symbol=BTC&intervals=15m");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static RunBacktestRequest CreateValidRequest() => new()
    {
        Symbol = "BTC",
        Intervals = ["15m", "1h", "4h"],
        StartDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        EndDate = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc),
        InitialCapital = 10000m,
        StrategyConfig = new GridStrategyConfigRequest
        {
            GridLevels = 10,
            GridSpacing = 0.5m,
            TakeProfitPercent = 1.0m,
            BreakdownThreshold = -3.0m,
            MakerFee = 0.0001m,
            TakerFee = 0.00035m,
            Slippage = 0m,
            PositionSize = 100.0m,
            Leverage = 3.0m,
            StopLossPercent = 5.0m
        }
    };

    private static BacktestResult CreateMockBacktestResult() => new()
    {
        TotalTrades = 10,
        WinningTrades = 7,
        LosingTrades = 3,
        WinRate = 70.0m,
        TotalPnL = 500.0m,
        MaxDrawdownAbsolute = -100.0m,
        MaxDrawdownPercent = -1.0m,
        AverageTradePnL = 50.0m,
        AverageHoldTime = TimeSpan.FromMinutes(120),
        HedgesOpened = 1,
        TotalFeesPaid = 5.0m,
        GridCycles = 2,
        FinalEquity = 10500m,
        CandlesReplayed = 35040,
        EquityTimeSeries = [],
        TradeLog =
        [
            new BacktestTrade
            {
                TradeId = Guid.NewGuid().ToString(),
                GridCycleId = Guid.NewGuid().ToString(),
                EntryTimeUtc = 1704067200000,
                EntryPrice = 42150.50m,
                ExitTimeUtc = 1704082800000,
                ExitPrice = 42361.25m,
                Side = TradePilot.Application.Trading.Models.OrderSide.Buy,
                Size = 0.001m,
                PnL = 0.21m,
                Fees = 0.015m,
                TradeType = TradePilot.Application.Trading.Models.TradeType.GridFill
            }
        ]
    };
}
```

> **Note**: The exact mock setup and Candle creation may need adjustment based on the actual `Candle.Create()` signature and `BacktestResult` property names. The implementer should verify mock return objects match the actual model properties. Also, `ConfigureWebHost` may need `UseSetting` calls for Hyperliquid config keys to prevent DI container failures at startup.

##### Pattern References

- `tests/TradePilot.Api.Tests/Controllers/CandlesControllerTests.cs` — `BaseControllerTests` inheritance, `ConfigureTestServices`, mock injection, `ReadAndAssertSuccessAsync<T>()`, `GetTestClient()`
- `tests/TradePilot.Api.Tests/Controllers/FundingRatesControllerTests.cs` — same pattern with mock verify
- `tests/TradePilot.Api.Tests/Infrastructure/BaseControllerTests.cs` — `GetStringContent()`, `GetTestClient()`, cleanup

### Task 3.4: Write `BacktestsControllerTests` — validation and error paths {#task-34-write-backtestscontrollertests-validation-and-error-paths}

Write controller integration tests for all validation and error scenarios from the PBI.

- **Complexity**: Medium
- **Risk Factors**: Must cover all PBI error states; must verify error response body structure (errorMessage, errorCode)
- **Files**:
  - `tests/TradePilot.Api.Tests/Controllers/BacktestsControllerTests.cs` — modification (add test methods)
- **Success**:
  - Invalid date range → 400
  - Unknown symbol → 400 with supported symbols list
  - Invalid interval → 400 with valid intervals list
  - Invalid strategy config (gridLevels=0) → 400
  - Non-existent backtest ID → 404
  - Missing required fields → 400
  - No candle data → 404 (mock runner throws `NotFoundException`)
  - Timeout/cancellation → 408 (mock runner throws `OperationCanceledException`)
  - All error responses contain `errorMessage` and `errorCode` fields
- **Dependencies**: Task 3.3

#### Implementation Details

Add the following test methods to the existing `BacktestsControllerTests` class:

```csharp
// tests/TradePilot.Api.Tests/Controllers/BacktestsControllerTests.cs — modification (add to existing class)

[TestMethod]
public async Task GivenEndDateBeforeStartDate_WhenPostBacktest_ThenReturnsBadRequest()
{
    var client = GetTestClient();
    var request = CreateValidRequest();
    request.StartDate = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc);
    request.EndDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    var response = await client.PostAsJsonAsync(BaseUrl, request);

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    var body = await response.Content.ReadFromJsonAsync<JsonElement>();
    body.GetProperty("errorMessage").GetString().Should().Contain("endDate must be after startDate");
    body.GetProperty("errorCode").GetString().Should().Be("validation_error");
}

[TestMethod]
public async Task GivenUnknownSymbol_WhenPostBacktest_ThenReturnsBadRequestWithSupportedSymbols()
{
    var client = GetTestClient();
    var request = CreateValidRequest();
    request.Symbol = "INVALID";

    var response = await client.PostAsJsonAsync(BaseUrl, request);

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    var body = await response.Content.ReadFromJsonAsync<JsonElement>();
    body.GetProperty("errorMessage").GetString().Should().Contain("Unknown symbol 'INVALID'");
    body.GetProperty("errorMessage").GetString().Should().Contain("Supported:");
}

[TestMethod]
public async Task GivenInvalidInterval_WhenPostBacktest_ThenReturnsBadRequestWithValidIntervals()
{
    var client = GetTestClient();
    var request = CreateValidRequest();
    request.Intervals = ["2m"];

    var response = await client.PostAsJsonAsync(BaseUrl, request);

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    var body = await response.Content.ReadFromJsonAsync<JsonElement>();
    body.GetProperty("errorMessage").GetString().Should().Contain("Invalid interval '2m'");
}

[TestMethod]
public async Task GivenGridLevelsZero_WhenPostBacktest_ThenReturnsBadRequest()
{
    var client = GetTestClient();
    var request = CreateValidRequest();
    request.StrategyConfig.GridLevels = 0;

    var response = await client.PostAsJsonAsync(BaseUrl, request);

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}

[TestMethod]
public async Task GivenNonExistentId_WhenGetById_ThenReturnsNotFound()
{
    _backtestRunRepositoryMock
        .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync((BacktestRun?)null);

    var client = GetTestClient();
    var nonExistentId = Guid.NewGuid();

    var response = await client.GetAsync($"{BaseUrl}/{nonExistentId}");

    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    var body = await response.Content.ReadFromJsonAsync<JsonElement>();
    body.GetProperty("errorCode").GetString().Should().Be("not_found");
}

[TestMethod]
public async Task GivenUnknownSymbol_WhenValidate_ThenReturnsBadRequest()
{
    var client = GetTestClient();

    var response = await client.GetAsync($"{BaseUrl}/validate?symbol=INVALID&intervals=15m");

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    var body = await response.Content.ReadFromJsonAsync<JsonElement>();
    body.GetProperty("errorMessage").GetString().Should().Contain("Unknown symbol 'INVALID'");
}

[TestMethod]
public async Task GivenMissingSymbol_WhenValidate_ThenReturnsBadRequest()
{
    var client = GetTestClient();

    var response = await client.GetAsync($"{BaseUrl}/validate?intervals=15m");

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}

[TestMethod]
public async Task GivenInvalidInterval_WhenValidate_ThenReturnsBadRequest()
{
    var client = GetTestClient();

    var response = await client.GetAsync($"{BaseUrl}/validate?symbol=BTC&intervals=2m");

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    var body = await response.Content.ReadFromJsonAsync<JsonElement>();
    body.GetProperty("errorMessage").GetString().Should().Contain("Invalid interval '2m'");
}

[TestMethod]
public async Task GivenNoCandleData_WhenPostBacktest_ThenReturnsNotFound()
{
    // Arrange — mock runner throws NotFoundException when no candle data exists
    _backtestRunnerMock
        .Setup(r => r.RunAsync(It.IsAny<BacktestConfig>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new TradePilot.Application.Abstractions.Exceptions.NotFoundException("Candle", "No candle data found for the requested range"));

    var client = GetTestClient();
    var request = CreateValidRequest();

    // Act
    var response = await client.PostAsJsonAsync(BaseUrl, request);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    var body = await response.Content.ReadFromJsonAsync<JsonElement>();
    body.GetProperty("errorCode").GetString().Should().Be("not_found");
}

[TestMethod]
public async Task GivenBacktestTimeout_WhenPostBacktest_ThenReturnsRequestTimeout()
{
    // Arrange — mock runner throws OperationCanceledException (timeout)
    _backtestRunnerMock
        .Setup(r => r.RunAsync(It.IsAny<BacktestConfig>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new OperationCanceledException());

    var client = GetTestClient();
    var request = CreateValidRequest();

    // Act
    var response = await client.PostAsJsonAsync(BaseUrl, request);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.RequestTimeout);
    var body = await response.Content.ReadFromJsonAsync<JsonElement>();
    body.GetProperty("errorCode").GetString().Should().Be("request_timeout");
}
```

##### Pattern References

- `tests/TradePilot.Api.Tests/Controllers/CandlesControllerTests.cs` — error status code assertions, JSON body assertions for `errorMessage` and `errorCode`
- `tests/TradePilot.Api.Tests/Controllers/FundingRatesControllerTests.cs` — validation error assertions with `correlationId` check

### Task 3.5: Build solution and run all tests {#task-35-build-solution-and-run-all-tests}

Final verification — build the entire solution and run all tests.

- **Complexity**: Low
- **Risk Factors**: Integration test setup may need tuning for DI configuration (Hyperliquid settings, etc.)
- **Files**: None (verification only)
- **Success**:
  - `dotnet build TradePilot.sln` succeeds with no errors
  - `dotnet test` on all test projects passes (including new BacktestsControllerTests and BacktestRunRepositoryTests)
  - All existing tests continue to pass
- **Dependencies**: All prior tasks in Phase 3

## Phase Success Criteria

- `RunBacktestRequest` model exists in `TradePilot.Api/Models/` with data annotation validation
- `BacktestsController` exists in `TradePilot.Api/Controllers/` with three endpoints (POST, GET validate, GET by ID)
- Controller validates symbol, intervals, date range, and strategy config — all returning appropriate 400 errors
- Controller integration tests cover: POST happy path, GET by ID happy path, GET validate happy path, invalid date range, unknown symbol, invalid interval, invalid config, non-existent ID, missing fields
- All tests pass and solution builds cleanly
- Handlers are tested indirectly through controller integration tests per project testing standards
