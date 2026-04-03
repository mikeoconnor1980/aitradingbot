<!-- markdownlint-disable-file -->

# Task Details: F9 — Natural Language Strategy Interpreter

## Phase 3: API Endpoint and Rate Limiting

## Standards and Knowledge References

- **api-controllers.instructions.md**: Extend `ApiController` base, `[ProducesResponseType]` on all actions, kebab-case routes not needed here (action on existing controller)
- **csharp.instructions.md**: `sealed` request classes, DataAnnotations for input validation
- **dotnet-architecture.instructions.md**: Request DTOs in `Api/Models/`, dispatch via `IMediator.Send()`
- **testing.instructions.md**: Controller integration tests using `BaseControllerTests` + `WebApplicationFactory`

### Task 3.1: Create InterpretStrategyRequest DTO with validation {#task-31-create-request-dto}

Create the request model with DataAnnotations validation for the interpret endpoint.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Api/Models/InterpretStrategyRequest.cs` — new request DTO
- **Success**:
  - `[Required]` rejects empty/null text
  - `[MaxLength(500)]` enforces character limit
  - API returns 400 for invalid input automatically via `[ApiController]`
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Api/Models/InterpretStrategyRequest.cs — new file
using System.ComponentModel.DataAnnotations;

namespace TradingApp.Api.Models;

public sealed class InterpretStrategyRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Please enter a strategy description")]
    [MaxLength(500, ErrorMessage = "Strategy description must be 500 characters or fewer")]
    public string Text { get; set; } = default!;
}
```

##### Pattern References

- `src/TradingApp.Api/Models/PlaceOrderRequest.cs` — DTO with DataAnnotations validation

### Task 3.2: Add interpret endpoint to StrategiesController {#task-32-add-interpret-endpoint}

Add the `POST /api/strategies/interpret` endpoint to the existing `StrategiesController`.

- **Complexity**: Medium
- **Risk Factors**: Must apply rate limiting attribute; must handle `OperationCanceledException` for timeout
- **Files**:
  - `src/TradingApp.Api/Controllers/StrategiesController.cs` — add interpret action
- **Success**:
  - `POST /api/strategies/interpret` accepts `InterpretStrategyRequest` and returns `StrategyIntentDto`
  - Rate limiting policy applied via `[EnableRateLimiting]`
  - Returns 200 OK with interpretation result
  - Returns 400 for invalid input
  - Returns 429 when rate limited
- **Dependencies**: Task 3.1, Phase 2 (InterpretStrategyCommand)

#### Implementation Details

```csharp
// src/TradingApp.Api/Controllers/StrategiesController.cs — add new action method
// Add these usings:
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using TradingApp.Api.Models;
using TradingApp.Application.StrategyAuthoring.Commands;
using TradingApp.Application.StrategyAuthoring.Models;

// Add this action to the existing StrategiesController class:

[HttpPost("interpret")]
[EnableRateLimiting("interpret-strategy")]
[ProducesResponseType(typeof(StrategyIntentDto), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(Envelope), StatusCodes.Status429TooManyRequests)]
public async Task<IActionResult> InterpretStrategy(
    [FromBody] InterpretStrategyRequest request,
    CancellationToken cancellationToken)
{
    var result = await Mediator.Send(
        new InterpretStrategyCommand(request.Text),
        cancellationToken);

    return Ok(result);
}
```

##### Pattern References

- `src/TradingApp.Api/Controllers/StrategiesController.cs` — existing `CreateStrategy` action pattern with `[FromBody]` + MediatR dispatch

### Task 3.3: Configure ASP.NET Core rate limiting {#task-33-configure-rate-limiting}

Set up ASP.NET Core built-in rate limiting with a fixed window policy for the interpret endpoint (10 requests per minute per IP).

- **Complexity**: Medium
- **Risk Factors**: Rate limiting middleware must be placed correctly in the pipeline; IP extraction must handle proxies
- **Files**:
  - `src/TradingApp.Api/Program.cs` — add rate limiting services and middleware
- **Success**:
  - 11th request within 1 minute from same IP returns 429
  - Other endpoints are unaffected
  - Rate limit response includes Retry-After header
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Api/Program.cs — add rate limiting configuration

// Add to service registration section (before builder.Build()):
using System.Threading.RateLimiting;

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("interpret-strategy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        var envelope = new { errorMessage = "Too many requests. Please wait a moment.", errorCode = "rate_limit" };
        await context.HttpContext.Response.WriteAsJsonAsync(envelope, cancellationToken);
    };
});

// Add to middleware pipeline (after CORS, before MapControllers):
app.UseRateLimiter();
```

The rate limiting uses `AddPolicy` with `RateLimitPartition.GetFixedWindowLimiter` keyed on `RemoteIpAddress`, ensuring each client IP gets its own 10-request-per-minute window.

**Note**: The `AddPolicy` with a policy name "interpret-strategy" is applied only to the endpoint decorated with `[EnableRateLimiting("interpret-strategy")]`, so other endpoints remain unaffected.

##### Pattern References

- `src/TradingApp.Api/Program.cs` — middleware pipeline order (CorrelationIdMiddleware → CORS → MapControllers)
- `src/TradingApp.Api/Infrastructure/Filters/HttpGlobalExceptionFilter.cs` — existing 429 mapping for `RateLimitException`

### Task 3.4: Add controller integration tests {#task-34-add-integration-tests}

Add integration tests for the interpret endpoint covering success, validation, and rate limiting scenarios.

- **Complexity**: Medium
- **Risk Factors**: Must mock ILlmClient in test services to avoid real LLM calls; rate limiting test needs rapid-fire requests
- **Files**:
  - `tests/TradingApp.Api.Tests/Controllers/InterpretStrategyTests.cs` — new test class
- **Success**:
  - Given valid text, returns 200 with StrategyIntentDto
  - Given empty text, returns 400
  - Given text over 500 chars, returns 400
  - Given 11 rapid requests, 11th returns 429
  - All tests pass
- **Dependencies**: Tasks 3.1-3.3

#### Implementation Details

```csharp
// tests/TradingApp.Api.Tests/Controllers/InterpretStrategyTests.cs — new file
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TradingApp.Api.Tests.Infrastructure;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.StrategyAuthoring.Models;

namespace TradingApp.Api.Tests.Controllers;

[TestClass]
public sealed class InterpretStrategyTests : BaseControllerTests
{
    private Mock<ILlmClient> _llmClientMock = default!;

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);

        _llmClientMock = new Mock<ILlmClient>();
        _llmClientMock
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateValidLlmResponse());

        // Replace the real LLM client with mock
        services.AddSingleton(_llmClientMock.Object);
    }

    [TestMethod]
    public async Task GivenValidText_WhenInterpretStrategy_ThenReturns200WithResult()
    {
        // Arrange
        var client = GetTestClient();
        var request = new { text = "Buy ETH when RSI drops below 30" };

        // Act
        var response = await client.PostAsJsonAsync("/api/strategies/interpret", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<StrategyIntentDto>();
        result.Should().NotBeNull();
    }

    [TestMethod]
    public async Task GivenEmptyText_WhenInterpretStrategy_ThenReturns400()
    {
        // Arrange
        var client = GetTestClient();
        var request = new { text = "" };

        // Act
        var response = await client.PostAsJsonAsync("/api/strategies/interpret", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task GivenTextExceeding500Chars_WhenInterpretStrategy_ThenReturns400()
    {
        // Arrange
        var client = GetTestClient();
        var request = new { text = new string('a', 501) };

        // Act
        var response = await client.PostAsJsonAsync("/api/strategies/interpret", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static string CreateValidLlmResponse()
    {
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            config = new
            {
                schemaVersion = 1,
                strategyMode = "signal",
                strategyName = "Test Strategy",
                exchange = "Hyperliquid",
                market = "ETH",
                timeframe = "15m",
                direction = "long",
                enabled = true,
                entryConditions = new[]
                {
                    new
                    {
                        id = "cond-1",
                        enabled = true,
                        type = "rsi",
                        label = "RSI Oversold",
                        @params = new { period = 14, @operator = "lt", value = 30 }
                    }
                },
                exit = new { takeProfit = new { enabled = true, type = "fixed_percent", value = 2 }, stopLoss = new { enabled = true, type = "fixed_percent", value = 1.5 } },
                risk = new { positionSizeType = "percent_wallet", positionSizeValue = 10, leverage = 1 }
            },
            confidence = 0.9,
            assumptions = System.Array.Empty<object>(),
            clarificationNeeded = (string?)null
        });
    }
}
```

##### Pattern References

- `tests/TradingApp.Api.Tests/Controllers/StrategiesControllerTests.cs` — controller integration test pattern
- `tests/TradingApp.Api.Tests/Infrastructure/BaseControllerTests.cs` — WebApplicationFactory base with `ConfigureTestServices`

### Task 3.5: Build verification and architecture tests {#task-35-build-verification}

Verify the solution builds and all tests pass including new integration tests.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: No files to create
- **Success**:
  - `dotnet build TradingApp.sln` succeeds
  - `dotnet test` passes for all test projects
  - Rate limiting does not affect existing endpoint tests
- **Dependencies**: All previous tasks in phase

## Phase Success Criteria

- `POST /api/strategies/interpret` endpoint is accessible and returns interpreted config
- Input validation rejects empty and oversized text with 400
- Rate limiting returns 429 after 10 requests per minute per IP
- Other endpoints remain unaffected by rate limiting
- Integration tests pass
