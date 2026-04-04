<!-- markdownlint-disable-file -->

# Task Details: AI Strategy Review

## Phase 3: API Endpoints & Integration Tests

## Standards and Knowledge References

- `.github/instructions/api-controllers.instructions.md` — Controller inherits ApiController, MediatR dispatch, ProducesResponseType annotations, kebab-case routes
- `.github/instructions/testing.instructions.md` — API tests extend BaseControllerTests, WebApplicationFactory, per-test isolated SQLite database, mock services via ConfigureTestServices
- `.github/instructions/csharp.instructions.md` — Sealed classes, async/await, CancellationToken

### Task 3.1: Add review endpoints to StrategiesController {#task-31-add-review-endpoints-to-strategiescontroller}

Add two new endpoints to the existing `StrategiesController`: POST to trigger a review and GET to retrieve a stored review.

- **Complexity**: Medium
- **Risk Factors**: Must follow existing controller conventions; POST uses rate limiting; revision number passed as route parameter
- **Files**:
  - `src/TradingApp.Api/Controllers/StrategiesController.cs` - Modify (add 2 new action methods)
- **Success**:
  - `POST /api/strategies/{id}/versions/{rev}/review` triggers and returns the review
  - `GET /api/strategies/{id}/versions/{rev}/review` retrieves stored review
  - Proper `ProducesResponseType` annotations
  - Rate limiting applied to POST endpoint
- **Dependencies**: Phase 2 (commands/queries)

#### Implementation Details

```csharp
// src/TradingApp.Api/Controllers/StrategiesController.cs — add these methods

[HttpPost("{id:guid}/versions/{rev:int}/review")]
[EnableRateLimiting("review-strategy")]
[ProducesResponseType(typeof(StrategyReviewDto), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(Envelope), StatusCodes.Status429TooManyRequests)]
public async Task<IActionResult> ReviewStrategy(
    Guid id,
    int rev,
    CancellationToken cancellationToken)
{
    if (rev < 1)
    {
        throw new DomainException("rev must be greater than or equal to 1");
    }

    var review = await Mediator.Send(
        new RequestStrategyReviewCommand(id, rev, IdentityService.Identity),
        cancellationToken);

    return Ok(review);
}

[HttpGet("{id:guid}/versions/{rev:int}/review")]
[ProducesResponseType(typeof(StrategyReviewDto), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
public async Task<IActionResult> GetReview(
    Guid id,
    int rev,
    CancellationToken cancellationToken)
{
    if (rev < 1)
    {
        throw new DomainException("rev must be greater than or equal to 1");
    }

    var review = await Mediator.Send(
        new GetStrategyReviewQuery(id, rev, IdentityService.Identity),
        cancellationToken);

    if (review is null)
    {
        return NotFound(new Envelope("No review found for this revision.", "not_found"));
    }

    return Ok(review);
}
```

Add using statements at the top of the controller file:
```csharp
using TradingApp.Application.StrategyAuthoring.Models; // if not already present — for StrategyReviewDto
```

##### Pattern References

- `src/TradingApp.Api/Controllers/StrategiesController.cs` — Existing `InterpretStrategy` (POST with rate limiting), `GetVersion` (GET with rev parameter)

---

### Task 3.2: Add review-strategy rate limiting policy {#task-32-add-review-strategy-rate-limiting-policy}

Add a `review-strategy` rate limiting policy in `Program.cs`. Set to 1 request per minute per IP (stricter than `interpret-strategy` which is 10/min).

- **Complexity**: Low
- **Risk Factors**: None — follows existing `interpret-strategy` policy pattern exactly
- **Files**:
  - `src/TradingApp.Api/Program.cs` - Modify (add new policy inside existing `AddRateLimiter` block)
- **Success**:
  - Policy named `review-strategy` with 1 request/minute/IP
  - Reuses existing `OnRejected` handler
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Api/Program.cs — add inside the AddRateLimiter block, after the interpret-strategy policy:

options.AddPolicy("review-strategy", httpContext =>
{
    var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString();

    if (partitionKey is null)
    {
        return RateLimitPartition.GetNoLimiter("unknown");
    }

    return RateLimitPartition.GetFixedWindowLimiter(
        partitionKey,
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 1,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        });
});
```

##### Pattern References

- `src/TradingApp.Api/Program.cs` lines 187–208 — Existing `interpret-strategy` rate limiting policy

---

### Task 3.3: Write API integration tests for review endpoints {#task-33-write-api-integration-tests-for-review-endpoints}

Write integration tests for both review endpoints. Tests must create a strategy first, then exercise the review flow.

- **Complexity**: High
- **Risk Factors**: Need to set up a strategy + revision in the test database before testing review endpoints; must mock both ILlmClient and IReviewLlmClient
- **Files**:
  - `tests/TradingApp.Api.Tests/Controllers/StrategyReviewTests.cs` - New file
- **Success**:
  - Tests cover: successful review creation, review retrieval, review overwrite, 404 for missing strategy, 404 for missing revision, 404 for missing review, rate limiting
  - All tests pass
- **Dependencies**: Task 3.1, Task 3.2

#### Implementation Details

```csharp
// tests/TradingApp.Api.Tests/Controllers/StrategyReviewTests.cs — new file
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TradingApp.Api.Tests.Infrastructure;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.StrategyAuthoring.Models;

namespace TradingApp.Api.Tests.Controllers;

[TestClass]
public sealed class StrategyReviewTests : BaseControllerTests
{
    private const string TestPrivateKey = "0x4c0883a69102937d6231471b5dbb6204fe512961708279f2a4c5890a0c1f9b2e";

    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"tradingapp-review-tests-{Guid.NewGuid():N}.db");

    private Mock<ILlmClient> _llmClientMock = default!;
    private Mock<IReviewLlmClient> _reviewLlmClientMock = default!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Hyperliquid:PrivateKey", TestPrivateKey);
        builder.UseSetting("Hyperliquid:BaseUrl", "https://api.hyperliquid-testnet.xyz");
        builder.UseSetting("Hyperliquid:Network", "testnet");
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_databasePath}");
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<IHostedService>();

        _llmClientMock = new Mock<ILlmClient>();
        _llmClientMock
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{}");
        services.RemoveAll<ILlmClient>();
        services.AddSingleton(_llmClientMock.Object);

        _reviewLlmClientMock = new Mock<IReviewLlmClient>();
        _reviewLlmClientMock
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("## 1. Strategy Summary\n- This is a grid strategy.");
        services.RemoveAll<IReviewLlmClient>();
        services.AddSingleton(_reviewLlmClientMock.Object);
    }

    [TestMethod]
    public async Task GivenSavedStrategy_WhenReviewRequested_ThenReturns200WithReview()
    {
        var client = GetTestClient();
        var strategyId = await CreateTestStrategy(client);

        var response = await client.PostAsync($"/api/strategies/{strategyId}/versions/1/review", null);

        var review = await response.ReadAndAssertSuccessAsync<StrategyReviewDto>();
        review.StrategyId.Should().Be(strategyId);
        review.RevisionNumber.Should().Be(1);
        review.ReviewMarkdown.Should().Contain("Strategy Summary");
        review.ModelName.Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public async Task GivenReviewExists_WhenGetReview_ThenReturns200()
    {
        var client = GetTestClient();
        var strategyId = await CreateTestStrategy(client);

        // Create the review first
        await client.PostAsync($"/api/strategies/{strategyId}/versions/1/review", null);

        // Now retrieve it
        var response = await client.GetAsync($"/api/strategies/{strategyId}/versions/1/review");

        var review = await response.ReadAndAssertSuccessAsync<StrategyReviewDto>();
        review.ReviewMarkdown.Should().Contain("Strategy Summary");
    }

    [TestMethod]
    public async Task GivenNoReviewExists_WhenGetReview_ThenReturns404()
    {
        var client = GetTestClient();
        var strategyId = await CreateTestStrategy(client);

        var response = await client.GetAsync($"/api/strategies/{strategyId}/versions/1/review");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task GivenNonExistentStrategy_WhenReviewRequested_ThenReturns404()
    {
        var client = GetTestClient();
        var fakeId = Guid.NewGuid();

        var response = await client.PostAsync($"/api/strategies/{fakeId}/versions/1/review", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task GivenInvalidRevision_WhenReviewRequested_ThenReturns400()
    {
        var client = GetTestClient();
        var strategyId = await CreateTestStrategy(client);

        var response = await client.PostAsync($"/api/strategies/{strategyId}/versions/0/review", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task GivenReviewExists_WhenReviewRequestedAgain_ThenOverwritesPreviousReview()
    {
        var client = GetTestClient();
        var strategyId = await CreateTestStrategy(client);

        // First review
        await client.PostAsync($"/api/strategies/{strategyId}/versions/1/review", null);

        // Change mock response for second review
        _reviewLlmClientMock
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("## Updated review content");

        // Second review (overwrite)
        var response = await client.PostAsync($"/api/strategies/{strategyId}/versions/1/review", null);

        var review = await response.ReadAndAssertSuccessAsync<StrategyReviewDto>();
        review.ReviewMarkdown.Should().Contain("Updated review content");
    }

    [TestMethod]
    public async Task GivenTwoRapidRequests_WhenReviewRequested_ThenSecondReturns429()
    {
        var client = GetTestClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.20");
        var strategyId = await CreateTestStrategy(client);

        var firstResponse = await client.PostAsync($"/api/strategies/{strategyId}/versions/1/review", null);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondResponse = await client.PostAsync($"/api/strategies/{strategyId}/versions/1/review", null);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    private static async Task<Guid> CreateTestStrategy(HttpClient client)
    {
        // Create strategy via POST /api/strategies with minimal valid config
        var config = new
        {
            schemaVersion = 1,
            strategyMode = "grid",
            strategyName = $"Test Strategy {Guid.NewGuid():N}",
            exchange = "Hyperliquid",
            market = "BTC",
            timeframe = "15m",
            direction = "long",
            enabled = true,
            grid = new
            {
                levels = 5,
                spacing = 0.5m,
                entryMode = "auto_from_signal_candle",
                breakdownThreshold = 2.0m,
            },
            exit = new
            {
                takeProfit = new { enabled = true, type = "fixed_percent", value = 2.0m },
                stopLoss = new { enabled = true, type = "fixed_percent", value = 1.5m },
                exitOnOppositeSignal = false,
            },
            risk = new
            {
                positionSizeType = "percent_wallet",
                positionSizeValue = 10,
                leverage = 1,
                maxOpenTrades = 1,
                cooldownValue = 0,
                cooldownUnit = "candles",
                allowSameCandleReentry = false,
            },
        };

        var createResponse = await client.PostAsJsonAsync("/api/strategies", config);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }
}
```

##### Pattern References

- `tests/TradingApp.Api.Tests/Controllers/InterpretStrategyTests.cs` — Integration test setup with WebApplicationFactory, mock ILlmClient, rate limiting test, ConfigureWebHost/ConfigureTestServices
- `tests/TradingApp.Api.Tests/Infrastructure/BaseControllerTests.cs` — Base class with GetTestClient, ReadAndAssertSuccessAsync extension

---

### Task 3.4: Build and run all backend tests {#task-34-build-and-run-all-backend-tests}

Build the full solution and run all backend test projects to catch any regressions.

- **Complexity**: Low
- **Risk Factors**: Possible regressions from DI registration changes
- **Files**: None (verification only)
- **Success**:
  - Solution builds without errors
  - All domain, AI, application, and API tests pass
- **Dependencies**: All previous tasks in Phase 3

Run:
```bash
dotnet build
dotnet test tests/TradingApp.Domain.Tests
dotnet test tests/TradingApp.AI.Tests
dotnet test tests/TradingApp.Application.Tests
dotnet test tests/TradingApp.Api.Tests
```

## Phase Success Criteria

- POST and GET review endpoints accessible on `StrategiesController`
- Rate limiting policy `review-strategy` enforces 1 request/minute/IP
- All API integration tests pass covering success, error, overwrite, and rate limiting scenarios
- Full backend test suite passes without regressions
