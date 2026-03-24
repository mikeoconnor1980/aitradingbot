<!-- markdownlint-disable-file -->

# Task Details: F2 — Account Dashboard

## Phase 1: Backend — DTOs, Service Layer, and API Endpoints

## Standards and Knowledge References

**C# Standards** (`.github/instructions/csharp.instructions.md`):
- Classes `sealed` wherever possible
- `Async` suffix on all async methods
- Always pass `CancellationToken`
- Constructor injection with interfaces
- `IOptions<T>` for configuration
- PascalCase for public members, `_camelCase` for private fields

**API Controller Standards** (`.github/instructions/api-controllers.instructions.md`):
- `[ApiController]` attribute on controllers
- Kebab-case route segments
- `[ProducesResponseType]` for all possible responses
- `[Produces("application/json")]`

**Testing Standards** (`.github/instructions/testing.instructions.md`):
- MSTest — NEVER xUnit
- Moq for mocking, FluentAssertions ≤ v6 for assertions
- Test naming: `Given_When_Then`

**POC Simplifications** (alignment decisions):
- No MediatR/CQRS — direct service injection in controllers
- No IdentityService/auth — single-user POC
- No Autofac modules — use standard .NET DI in `Program.cs`
- `WebApplicationFactory<Program>` for integration tests (not `BaseControllerTests<Startup>`)

**Hyperliquid API** (`.agent-context/0-knowledge/02-hyperliquid-integration.md`):
- REST API uses POST for all queries (including reads) with typed JSON request body
- Account state retrieved via `POST /info` with `{"type": "clearinghouseState", "user": "<wallet_address>"}`
- Open orders retrieved via `POST /info` with `{"type": "openOrders", "user": "<wallet_address>"}`

## Design References

**Hyperliquid Info API** (https://hyperliquid.gitbook.io/hyperliquid-docs/for-developers/api/info-endpoint):
- All read queries go to `POST /info` endpoint with a typed JSON body
- `clearinghouseState` returns account summary with margin info and active positions
- `openOrders` returns current open orders for a user
- Response shapes documented in Hyperliquid API docs — field names should be matched during implementation

---

### Task 1.1: Create account data DTOs {#task-11-create-account-data-dtos}

Create DTOs for the three API response types: account summary, position, and order.

- **Complexity**: Low
- **Risk Factors**: Hyperliquid API field names may differ from PBI spec — verify at implementation time
- **Files**:
  - `src/TradingApp.HyperliquidPoc.Api/Models/AccountSummaryDto.cs` — new file
  - `src/TradingApp.HyperliquidPoc.Api/Models/PositionDto.cs` — new file
  - `src/TradingApp.HyperliquidPoc.Api/Models/OpenOrderDto.cs` — new file
- **Success**:
  - All three DTO classes exist with properties matching PBI requirements
  - DTOs compile without errors

#### Implementation Details

```csharp
// src/TradingApp.HyperliquidPoc.Api/Models/AccountSummaryDto.cs — new file
namespace TradingApp.HyperliquidPoc.Api.Models;

public sealed class AccountSummaryDto
{
    public decimal Equity { get; set; }
    public decimal AvailableMargin { get; set; }
    public decimal CrossMarginRatio { get; set; }
    public decimal MaintenanceMargin { get; set; }
    public decimal UnrealisedPnl { get; set; }
}
```

```csharp
// src/TradingApp.HyperliquidPoc.Api/Models/PositionDto.cs — new file
namespace TradingApp.HyperliquidPoc.Api.Models;

public sealed class PositionDto
{
    public string Asset { get; set; } = string.Empty;
    public decimal Size { get; set; }
    public string Side { get; set; } = string.Empty;
    public decimal EntryPrice { get; set; }
    public decimal MarkPrice { get; set; }
    public decimal UnrealisedPnl { get; set; }
    public decimal UnrealisedPnlPercent { get; set; }
    public decimal LiquidationPrice { get; set; }
}
```

```csharp
// src/TradingApp.HyperliquidPoc.Api/Models/OpenOrderDto.cs — new file
namespace TradingApp.HyperliquidPoc.Api.Models;

public sealed class OpenOrderDto
{
    public string OrderId { get; set; } = string.Empty;
    public string Asset { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Size { get; set; }
    public string OrderType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
```

##### Pattern References

- F1 `HealthResponse` model (assumed to exist in `src/TradingApp.HyperliquidPoc.Api/Models/` or similar) — same DTO style
- `.github/instructions/csharp.instructions.md` — sealed classes, `{ get; set; }` properties

---

### Task 1.2: Create Hyperliquid account service interface and implementation {#task-12-create-hyperliquid-account-service}

Create a service that calls the Hyperliquid REST API for account state, positions, and orders. The service wraps the existing `HyperliquidRestClient` (from F1) and maps Hyperliquid JSON responses to the DTOs.

- **Complexity**: Medium
- **Risk Factors**: Hyperliquid response shape for `clearinghouseState` and `openOrders` needs to be matched exactly; may need intermediate deserialization models
- **Files**:
  - `src/TradingApp.HyperliquidPoc.Api/Services/IHyperliquidAccountService.cs` — new file
  - `src/TradingApp.HyperliquidPoc.Api/Services/HyperliquidAccountService.cs` — new file
  - `src/TradingApp.HyperliquidPoc.Api/Models/Hyperliquid/` — new directory for raw Hyperliquid response models (if needed)
- **Success**:
  - `IHyperliquidAccountService` defines three async methods: `GetAccountSummaryAsync`, `GetPositionsAsync`, `GetOpenOrdersAsync`
  - `HyperliquidAccountService` calls Hyperliquid's `POST /info` endpoint with correct request bodies
  - Responses are mapped from Hyperliquid JSON format to API DTOs
  - All methods accept `CancellationToken`
- **Dependencies**:
  - Task 1.1 (DTOs must exist)
  - F1's `HyperliquidRestClient` (HTTP wrapper)
  - F1's `HyperliquidSigner` (provides wallet address)

#### Implementation Details

```csharp
// src/TradingApp.HyperliquidPoc.Api/Services/IHyperliquidAccountService.cs — new file
namespace TradingApp.HyperliquidPoc.Api.Services;

using TradingApp.HyperliquidPoc.Api.Models;

public interface IHyperliquidAccountService
{
    Task<AccountSummaryDto> GetAccountSummaryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PositionDto>> GetPositionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OpenOrderDto>> GetOpenOrdersAsync(CancellationToken cancellationToken = default);
}
```

```csharp
// src/TradingApp.HyperliquidPoc.Api/Services/HyperliquidAccountService.cs — new file
namespace TradingApp.HyperliquidPoc.Api.Services;

using System.Text.Json;
using TradingApp.HyperliquidPoc.Api.Models;

public sealed class HyperliquidAccountService : IHyperliquidAccountService
{
    private readonly HyperliquidRestClient _restClient;
    private readonly HyperliquidSigner _signer;
    private readonly ILogger<HyperliquidAccountService> _logger;

    public HyperliquidAccountService(
        HyperliquidRestClient restClient,
        HyperliquidSigner signer,
        ILogger<HyperliquidAccountService> logger)
    {
        _restClient = restClient;
        _signer = signer;
        _logger = logger;
    }

    public async Task<AccountSummaryDto> GetAccountSummaryAsync(CancellationToken cancellationToken = default)
    {
        // POST /info with {"type": "clearinghouseState", "user": "<wallet_address>"}
        // Map Hyperliquid response to AccountSummaryDto
        // Extract: equity (accountValue), available margin (withdrawable), cross margin ratio,
        //          maintenance margin, and aggregate unrealised PnL from margin summary + positions
        var request = new { type = "clearinghouseState", user = _signer.WalletAddress };
        var response = await _restClient.PostInfoAsync<JsonElement>(request, cancellationToken);

        // Parse margin summary from response and map to DTO
        // Exact field mapping depends on Hyperliquid API response shape — verify during implementation
        return MapToAccountSummary(response);
    }

    public async Task<IReadOnlyList<PositionDto>> GetPositionsAsync(CancellationToken cancellationToken = default)
    {
        // clearinghouseState also contains positions in the "assetPositions" array
        var request = new { type = "clearinghouseState", user = _signer.WalletAddress };
        var response = await _restClient.PostInfoAsync<JsonElement>(request, cancellationToken);

        return MapToPositions(response);
    }

    public async Task<IReadOnlyList<OpenOrderDto>> GetOpenOrdersAsync(CancellationToken cancellationToken = default)
    {
        // POST /info with {"type": "openOrders", "user": "<wallet_address>"}
        var request = new { type = "openOrders", user = _signer.WalletAddress };
        var response = await _restClient.PostInfoAsync<JsonElement>(request, cancellationToken);

        return MapToOpenOrders(response);
    }

    // Private mapping methods — implementation depends on exact Hyperliquid JSON shape
    // These should be implemented/adjusted when testing against actual testnet responses
    private static AccountSummaryDto MapToAccountSummary(JsonElement response) { /* ... */ }
    private static IReadOnlyList<PositionDto> MapToPositions(JsonElement response) { /* ... */ }
    private static IReadOnlyList<OpenOrderDto> MapToOpenOrders(JsonElement element) { /* ... */ }
}
```

> **Note:** The `HyperliquidRestClient.PostInfoAsync<T>` method signature assumes F1 established a generic POST method on the REST client. If F1's method has a different signature, adapt accordingly. The key point is that Hyperliquid uses `POST /info` for all read queries.

> **Note:** The mapping methods will need to handle the exact Hyperliquid response JSON structure. The implementer should make a test call to `POST /info` with `{"type": "clearinghouseState", "user": "..."}` to inspect the response shape and implement the mappers accordingly.

##### Pattern References

- F1's `HyperliquidRestClient` — HTTP wrapper with `PostInfoAsync` or similar method
- F1's `HyperliquidSigner` — provides `WalletAddress` property
- `.github/instructions/csharp.instructions.md` — sealed class, async with CancellationToken, ILogger

---

### Task 1.3: Create AccountController with three GET endpoints {#task-13-create-account-controller}

Create a controller exposing the three account data endpoints.

- **Complexity**: Low
- **Risk Factors**: None — straightforward controller implementation
- **Files**:
  - `src/TradingApp.HyperliquidPoc.Api/Controllers/AccountController.cs` — new file
- **Success**:
  - `GET /api/account` returns `AccountSummaryDto` with 200 OK
  - `GET /api/positions` returns `IReadOnlyList<PositionDto>` with 200 OK
  - `GET /api/orders` returns `IReadOnlyList<OpenOrderDto>` with 200 OK
  - All endpoints handle errors and return appropriate HTTP status codes
  - All endpoints accept `CancellationToken`
- **Dependencies**:
  - Task 1.2 (service must exist)

#### Implementation Details

```csharp
// src/TradingApp.HyperliquidPoc.Api/Controllers/AccountController.cs — new file
namespace TradingApp.HyperliquidPoc.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using TradingApp.HyperliquidPoc.Api.Models;
using TradingApp.HyperliquidPoc.Api.Services;

[ApiController]
[Route("api")]
[Produces("application/json")]
public sealed class AccountController : ControllerBase
{
    private readonly IHyperliquidAccountService _accountService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        IHyperliquidAccountService accountService,
        ILogger<AccountController> logger)
    {
        _accountService = accountService;
        _logger = logger;
    }

    [HttpGet("account")]
    [ProducesResponseType(typeof(AccountSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetAccountSummaryAsync(CancellationToken cancellationToken)
    {
        try
        {
            var summary = await _accountService.GetAccountSummaryAsync(cancellationToken);
            return Ok(summary);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch account summary from Hyperliquid");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Hyperliquid API is unavailable" });
        }
    }

    [HttpGet("positions")]
    [ProducesResponseType(typeof(IReadOnlyList<PositionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetPositionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var positions = await _accountService.GetPositionsAsync(cancellationToken);
            return Ok(positions);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch positions from Hyperliquid");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Hyperliquid API is unavailable" });
        }
    }

    /// <summary>
    /// Returns open orders. This endpoint will move to OrderController when F5 is implemented.
    /// </summary>
    [HttpGet("orders")]
    [ProducesResponseType(typeof(IReadOnlyList<OpenOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetOpenOrdersAsync(CancellationToken cancellationToken)
    {
        try
        {
            var orders = await _accountService.GetOpenOrdersAsync(cancellationToken);
            return Ok(orders);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch open orders from Hyperliquid");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Hyperliquid API is unavailable" });
        }
    }
}
```

##### Pattern References

- F1's `HealthController` (assumed) — same controller style with `ControllerBase` and direct service injection
- `.github/instructions/api-controllers.instructions.md` — `[ApiController]`, `[Produces]`, `[ProducesResponseType]`, kebab-case routes
- `.github/instructions/csharp.instructions.md` — sealed class, `Async` suffix, CancellationToken

---

### Task 1.4: Register new services in DI {#task-14-register-services-in-di}

Register `IHyperliquidAccountService` / `HyperliquidAccountService` in the DI container in `Program.cs`.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.HyperliquidPoc.Api/Program.cs` — modification
- **Success**:
  - `IHyperliquidAccountService` is registered as scoped/transient in the DI container
  - Application starts without DI resolution errors
- **Dependencies**:
  - Task 1.2 (service must exist)

#### Implementation Details

```csharp
// src/TradingApp.HyperliquidPoc.Api/Program.cs — modification
// Add after existing F1 service registrations:

// ... existing code ...
builder.Services.AddScoped<IHyperliquidAccountService, HyperliquidAccountService>();
// ... existing code ...
```

##### Pattern References

- F1's `Program.cs` — existing DI registrations for `HyperliquidRestClient`, `HyperliquidSigner`, `HyperliquidOptions`

---

### Task 1.5: Add backend integration tests for AccountController {#task-15-add-backend-integration-tests}

Create integration tests for the three endpoints. Use `WebApplicationFactory<Program>` with mocked `IHyperliquidAccountService` to test controller behaviour without calling the real Hyperliquid API.

- **Complexity**: Medium
- **Risk Factors**: Need to establish test project and `WebApplicationFactory` setup if not already done by F1
- **Files**:
  - `tests/TradingApp.HyperliquidPoc.Api.Tests/TradingApp.HyperliquidPoc.Api.Tests.csproj` — new project (if not created by F1)
  - `tests/TradingApp.HyperliquidPoc.Api.Tests/Controllers/AccountControllerTests.cs` — new file
  - `tests/TradingApp.HyperliquidPoc.Api.Tests/TestWebApplicationFactory.cs` — new file (if not created by F1)
- **Success**:
  - Tests cover happy path for all 3 endpoints (200 OK with correct DTO shapes)
  - Tests cover error scenario (service throws `HttpRequestException` → 503 response)
  - Tests cover empty collections (positions/orders return empty arrays)
  - All tests pass
- **Dependencies**:
  - Tasks 1.1–1.4 (complete backend implementation)

#### Implementation Details

```csharp
// tests/TradingApp.HyperliquidPoc.Api.Tests/TestWebApplicationFactory.cs — new file (if not from F1)
namespace TradingApp.HyperliquidPoc.Api.Tests;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TradingApp.HyperliquidPoc.Api.Services;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public Mock<IHyperliquidAccountService> MockAccountService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Provide dummy configuration so F1 services can resolve without errors
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Hyperliquid:PrivateKey"] = "0x0000000000000000000000000000000000000000000000000000000000000001",
                ["Hyperliquid:BaseUrl"] = "https://api.hyperliquid-testnet.xyz",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove real service registration and replace with mock
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IHyperliquidAccountService));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddSingleton(MockAccountService.Object);
        });
    }
}
```

```csharp
// tests/TradingApp.HyperliquidPoc.Api.Tests/Controllers/AccountControllerTests.cs — new file
namespace TradingApp.HyperliquidPoc.Api.Tests.Controllers;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TradingApp.HyperliquidPoc.Api.Models;

[TestClass]
public sealed class AccountControllerTests
{
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [TestInitialize]
    public void Setup()
    {
        _factory = new TestWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [TestMethod]
    public async Task GivenValidAccount_WhenGetAccountSummary_ThenReturnsOkWithSummary()
    {
        // Arrange
        var expected = new AccountSummaryDto
        {
            Equity = 10000m,
            AvailableMargin = 8000m,
            CrossMarginRatio = 0.8m,
            MaintenanceMargin = 500m,
            UnrealisedPnl = 150m
        };
        _factory.MockAccountService
            .Setup(s => s.GetAccountSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var response = await _client.GetAsync("api/account");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AccountSummaryDto>();
        result.Should().NotBeNull();
        result!.Equity.Should().Be(10000m);
        result.UnrealisedPnl.Should().Be(150m);
    }

    [TestMethod]
    public async Task GivenPositionsExist_WhenGetPositions_ThenReturnsOkWithPositions()
    {
        // Arrange
        var positions = new List<PositionDto>
        {
            new()
            {
                Asset = "BTC", Size = 0.1m, Side = "Long",
                EntryPrice = 60000m, MarkPrice = 61000m,
                UnrealisedPnl = 100m, UnrealisedPnlPercent = 1.67m,
                LiquidationPrice = 55000m
            }
        };
        _factory.MockAccountService
            .Setup(s => s.GetPositionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(positions);

        // Act
        var response = await _client.GetAsync("api/positions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<PositionDto>>();
        result.Should().HaveCount(1);
        result![0].Asset.Should().Be("BTC");
    }

    [TestMethod]
    public async Task GivenNoOpenOrders_WhenGetOrders_ThenReturnsOkWithEmptyArray()
    {
        // Arrange
        _factory.MockAccountService
            .Setup(s => s.GetOpenOrdersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenOrderDto>());

        // Act
        var response = await _client.GetAsync("api/orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<OpenOrderDto>>();
        result.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenHyperliquidUnavailable_WhenGetAccountSummary_ThenReturns503()
    {
        // Arrange
        _factory.MockAccountService
            .Setup(s => s.GetAccountSummaryAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        // Act
        var response = await _client.GetAsync("api/account");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [TestMethod]
    public async Task GivenHyperliquidUnavailable_WhenGetPositions_ThenReturns503()
    {
        // Arrange
        _factory.MockAccountService
            .Setup(s => s.GetPositionsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        // Act
        var response = await _client.GetAsync("api/positions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [TestMethod]
    public async Task GivenHyperliquidUnavailable_WhenGetOrders_ThenReturns503()
    {
        // Arrange
        _factory.MockAccountService
            .Setup(s => s.GetOpenOrdersAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        // Act
        var response = await _client.GetAsync("api/orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }
}
```

##### Pattern References

- `.github/instructions/testing.instructions.md` — MSTest, Moq, FluentAssertions, `Given_When_Then` naming
- Microsoft.AspNetCore.Mvc.Testing `WebApplicationFactory` — standard .NET integration test pattern

---

### Task 1.6: Build and run tests {#task-16-build-and-run-tests}

Build the solution and run all backend tests to verify Phase 1 is complete.

- **Complexity**: Low
- **Risk Factors**: Test project may need configuration adjustments for `WebApplicationFactory` (e.g. `InternalsVisibleTo`, `Program` visibility)
- **Files**: None — verification step only
- **Success**:
  - `dotnet build` completes without errors
  - `dotnet test tests/TradingApp.HyperliquidPoc.Api.Tests` — all tests pass
- **Dependencies**:
  - Tasks 1.1–1.5 (all Phase 1 tasks)

#### Implementation Details

```bash
# Build the solution
dotnet build TradingApp.HyperliquidPoc.sln

# Run the account controller tests
dotnet test tests/TradingApp.HyperliquidPoc.Api.Tests --filter "FullyQualifiedName~AccountController"

# Run all test projects (if architecture tests exist from F1)
dotnet test TradingApp.HyperliquidPoc.sln
```

> **Note:** If `Program` class is not visible to the test project, may need to add `[assembly: InternalsVisibleTo("TradingApp.HyperliquidPoc.Api.Tests")]` or make `Program` public.

##### Pattern References

- `.github/instructions/testing.instructions.md` — build/run commands

---

## Phase Success Criteria

- `dotnet build` succeeds without errors
- All 6 integration tests pass:
  - Happy path: account summary (200 + correct DTO)
  - Happy path: positions list (200 + correct DTO)
  - Happy path: empty orders (200 + empty array)
  - Error: account summary → 503
  - Error: positions → 503
  - Error: orders → 503
- `GET /api/account`, `GET /api/positions`, `GET /api/orders` endpoints are functional
- Service correctly calls Hyperliquid `POST /info` with appropriate request bodies
