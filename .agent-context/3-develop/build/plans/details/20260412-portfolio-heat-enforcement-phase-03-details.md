<!-- markdownlint-disable-file -->

# Task Details: Portfolio Heat Enforcement

## Phase 3: API Endpoint

## Standards and Knowledge References

- **api-controllers.instructions.md**: `ApiController` base (or `ControllerBase` direct), kebab-case routes, `[ProducesResponseType]`, `Envelope` for errors
- **csharp.instructions.md**: Sealed classes, async/await, CancellationToken passing
- **testing.instructions.md**: MSTest, controller tests with `WebApplicationFactory`, `HttpResponseExtensions`
- **dotnet-architecture.instructions.md**: Direct service injection for account-read endpoints (per ADR 14)

## Design References

The API endpoint computes heat **independently** from the `LiveRiskEngine`'s tracked state. It queries exchange positions and equity on-the-fly via `IHyperliquidAccountService`, then uses `PortfolioHeatCalculator` to compute heat. This is correct because:
- The API uses `PassThroughRiskEngine` (not `LiveRiskEngine`)
- The endpoint shows the trader their current real exchange state, not the engine's internal tracking
- `AccountController` already follows this direct-service-injection pattern

---

### Task 3.1: Create `PortfolioHeatResponse` DTO {#task-31-create-portfolioheatresponse-dto}

Create the response model for the portfolio heat endpoint.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Application/Trading/Models/PortfolioHeatResponse.cs` — New file
- **Success**:
  - DTO with `HeatPercent`, `MaxHeatPercent`, `Equity`, `Positions` array
  - Position entries with `Symbol`, `RiskUsd`, `RiskPercent`
  - Empty factory method for when no wallet is configured
- **Dependencies**: Phase 1

#### Implementation Details

```csharp
// src/TradingApp.Application/Trading/Models/PortfolioHeatResponse.cs — new file
namespace TradingApp.Application.Trading.Models;

/// <summary>
/// API response for the portfolio heat endpoint.
/// </summary>
public sealed class PortfolioHeatResponse
{
    public decimal HeatPercent { get; init; }
    public decimal MaxHeatPercent { get; init; }
    public decimal Equity { get; init; }
    public IReadOnlyList<PortfolioHeatPositionResponse> Positions { get; init; } = [];

    public static PortfolioHeatResponse Empty() => new();
}

/// <summary>
/// Risk contribution of a single position in the portfolio heat response.
/// </summary>
public sealed class PortfolioHeatPositionResponse
{
    public string Symbol { get; init; } = string.Empty;
    public decimal RiskUsd { get; init; }
    public decimal RiskPercent { get; init; }
}
```

##### Pattern References

- `src/TradingApp.Application/MarketData/Models/AccountSummaryDto.cs` — DTO property pattern
- `src/TradingApp.Application/MarketData/Models/PositionDto.cs` — nested DTO pattern

---

### Task 3.2: Create `RiskController` {#task-32-create-riskcontroller}

Create a new controller with the `GET /api/risk/portfolio-heat` endpoint.

- **Complexity**: Medium
- **Risk Factors**: Wallet address resolution pattern needs to match `AccountController`
- **Files**:
  - `src/TradingApp.Api/Controllers/RiskController.cs` — New file
- **Success**:
  - Route: `api/risk`
  - `GET portfolio-heat` returns `PortfolioHeatResponse` with 200 OK
  - Returns empty response if no wallet configured
  - Injects `IHyperliquidAccountService`, `IUserWalletAddressRepository`, `IOptions<RiskLimitsConfig>`
  - `[Authorize]` inherited from base or explicitly set
  - `[ProducesResponseType]` attributes on the action
- **Dependencies**: Tasks 3.1, 3.3

#### Implementation Details

```csharp
// src/TradingApp.Api/Controllers/RiskController.cs — new file
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TradingApp.Api.Infrastructure;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Application.Trading.Services;

namespace TradingApp.Api.Controllers;

[ApiController]
[Route("api/risk")]
[Produces("application/json")]
[Authorize]
public sealed class RiskController : ControllerBase
{
    private readonly IHyperliquidAccountService _accountService;
    private readonly IUserWalletAddressRepository _walletRepo;
    private readonly RiskLimitsConfig _limits;

    public RiskController(
        IHyperliquidAccountService accountService,
        IUserWalletAddressRepository walletRepo,
        IOptions<RiskLimitsConfig> limits)
    {
        _accountService = accountService;
        _walletRepo = walletRepo;
        _limits = limits.Value;
    }

    [HttpGet("portfolio-heat")]
    [ProducesResponseType(typeof(PortfolioHeatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetPortfolioHeatAsync(CancellationToken cancellationToken)
    {
        var address = await GetWalletAddressAsync(cancellationToken);
        if (address is null)
        {
            return Ok(PortfolioHeatResponse.Empty());
        }

        var summaryTask = _accountService.GetAccountSummaryAsync(address, cancellationToken);
        var positionsTask = _accountService.GetPositionsAsync(address, cancellationToken);

        await Task.WhenAll(summaryTask, positionsTask);

        var summary = await summaryTask;
        var positions = await positionsTask;

        var heatResult = PortfolioHeatCalculator.CalculateFromPositions(
            positions, summary.Equity, _limits.MaxPortfolioHeatPercent);

        var response = new PortfolioHeatResponse
        {
            HeatPercent = heatResult.HeatPercent,
            MaxHeatPercent = heatResult.MaxHeatPercent,
            Equity = heatResult.Equity,
            Positions = heatResult.Entries.Select(e => new PortfolioHeatPositionResponse
            {
                Symbol = e.Symbol,
                RiskUsd = e.RiskUsd,
                RiskPercent = e.RiskPercent
            }).ToList()
        };

        return Ok(response);
    }

    private async Task<string?> GetWalletAddressAsync(CancellationToken cancellationToken)
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (claim is null || !Guid.TryParse(claim, out var userId))
            return null;

        var wallet = await _walletRepo.GetActiveByUserIdAsync(userId, cancellationToken);
        return wallet?.WalletAddress;
    }
}
```

##### Pattern References

- `src/TradingApp.Api/Controllers/AccountController.cs` — direct service injection, wallet address resolution, parallel async calls
- `src/TradingApp.Api/Infrastructure/Envelope.cs` — error response type

---

### Task 3.3: Register `RiskLimitsConfig` in API `Program.cs` {#task-33-register-risklimitsconfig-in-api-programcs}

Register `RiskLimitsConfig` options binding in the API host so `IOptions<RiskLimitsConfig>` can be injected into `RiskController`.

- **Complexity**: Low
- **Risk Factors**: None — follows exact established pattern
- **Files**:
  - `src/TradingApp.Api/Program.cs` — Add options registration
- **Success**:
  - `IOptions<RiskLimitsConfig>` resolvable from API DI container
  - Bound from `"RiskLimits"` config section
  - Validates on start
- **Dependencies**: Phase 1 (Task 1.1, 1.4)

#### Implementation Details

```csharp
// src/TradingApp.Api/Program.cs — modification
// Add alongside other AddOptions registrations:

builder.Services.AddOptions<RiskLimitsConfig>()
    .Bind(builder.Configuration.GetSection(RiskLimitsConfig.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

##### Pattern References

- `src/TradingApp.Worker/Program.cs` line 147 — existing `RiskLimitsConfig` registration pattern

---

### Task 3.4: Controller integration tests {#task-34-controller-integration-tests}

Create integration tests for the `RiskController.GetPortfolioHeatAsync` endpoint.

- **Complexity**: Medium
- **Risk Factors**: Need to mock `IHyperliquidAccountService` and `IUserWalletAddressRepository`
- **Files**:
  - `tests/TradingApp.Api.Tests/Controllers/RiskControllerTests.cs` — New file
- **Success**:
  - Test: returns heat data for authenticated user with positions
  - Test: returns empty response when no wallet configured
  - Test: returns correct heat percentage and position breakdown
  - Test: returns 401 for unauthenticated request
  - All tests pass: `dotnet test tests/TradingApp.Api.Tests/ --filter "FullyQualifiedName~RiskController"`
- **Dependencies**: Tasks 3.1, 3.2, 3.3

#### Implementation Details

```csharp
// tests/TradingApp.Api.Tests/Controllers/RiskControllerTests.cs — new file
// Follow the AccountControllerTests inline WebApplicationFactory pattern:

[TestClass]
public sealed class RiskControllerTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private Mock<IHyperliquidAccountService> _accountServiceMock = null!;
    private Mock<IUserWalletAddressRepository> _walletRepoMock = null!;

    [TestInitialize]
    public void Setup()
    {
        _accountServiceMock = new Mock<IHyperliquidAccountService>();
        _walletRepoMock = new Mock<IUserWalletAddressRepository>();

        _walletRepoMock
            .Setup(r => r.GetActiveByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserWalletAddress { WalletAddress = "0xTestWallet" });

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Jwt:SecretKey", BaseControllerTests.TestJwtSecretKey);
                // ... other required settings
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IHyperliquidAccountService>();
                    services.AddSingleton(_accountServiceMock.Object);
                    services.RemoveAll<IUserWalletAddressRepository>();
                    services.AddSingleton(_walletRepoMock.Object);
                });
            });
        _client = _factory.CreateClient();
        // Add JWT auth header (follow AccountControllerTests pattern)
    }

    [TestMethod]
    public async Task GivenOpenPositions_WhenGetPortfolioHeat_ThenReturnsHeatData()
    {
        // Arrange
        _accountServiceMock.Setup(s => s.GetAccountSummaryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountSummaryDto { Equity = 10_000m });
        _accountServiceMock.Setup(s => s.GetPositionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PositionDto>
            {
                new() { Asset = "BTC", Size = 0.1m, EntryPrice = 50_000m, StopLossPrice = 49_000m, MarginUsed = 500m },
                new() { Asset = "ETH", Size = 1m, EntryPrice = 3_000m, StopLossPrice = null, MarginUsed = 300m }
            });

        // Act
        var response = await _client.GetAsync("/api/risk/portfolio-heat");

        // Assert
        var result = await response.ReadAndAssertSuccessAsync<PortfolioHeatResponse>();
        result.HeatPercent.Should().Be(4m); // BTC R=100 + ETH R=300 = 400/10000 = 4%
        result.Positions.Should().HaveCount(2);
        result.Equity.Should().Be(10_000m);
    }

    [TestMethod]
    public async Task GivenNoWallet_WhenGetPortfolioHeat_ThenReturnsEmpty()
    {
        // Arrange
        _walletRepoMock
            .Setup(r => r.GetActiveByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserWalletAddress?)null);

        // Act
        var response = await _client.GetAsync("/api/risk/portfolio-heat");

        // Assert
        var result = await response.ReadAndAssertSuccessAsync<PortfolioHeatResponse>();
        result.HeatPercent.Should().Be(0m);
        result.Positions.Should().BeEmpty();
    }
}
```

> **Note**: Adapt the JWT token generation, `WebApplicationFactory` setup, and `HttpResponseExtensions` usage from the existing `AccountControllerTests.cs` patterns. Copy the exact auth header setup.

##### Pattern References

- `tests/TradingApp.Api.Tests/Controllers/AccountControllerTests.cs` — inline `WebApplicationFactory`, mock replacement, JWT auth
- `tests/TradingApp.Api.Tests/Infrastructure/BaseControllerTests.cs` — `HttpResponseExtensions`, `BaseControllerTestsJson`

---

### Task 3.5: Build and test verification {#task-35-build-and-test-verification}

Build and run all API tests.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification only)
- **Success**:
  - `dotnet build src/TradingApp.Api/TradingApp.Api.csproj` succeeds
  - `dotnet test tests/TradingApp.Api.Tests/ --filter "FullyQualifiedName~RiskController"` — all tests pass
  - `dotnet test TradingApp.sln --no-build` — all tests pass
- **Dependencies**: Tasks 3.1–3.4

## Phase Success Criteria

- `GET /api/risk/portfolio-heat` returns correct heat data from exchange positions
- Returns empty response when no wallet configured
- `RiskLimitsConfig` registered in API DI container
- All controller tests pass
- All existing tests pass without regression
