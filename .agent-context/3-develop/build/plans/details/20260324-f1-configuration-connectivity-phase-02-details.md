<!-- markdownlint-disable-file -->

# Task Details: F1 — Configuration & Connectivity

## Phase 2: Backend — Hyperliquid Services, Health Endpoint, Tests

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — sealed classes, static factory methods, `_camelCase` private fields, CancellationToken, IOptions<T>
- `.github/instructions/dotnet-architecture.instructions.md` — CQRS queries in Application layer, handlers colocated with query record, infrastructure services behind interfaces in Application/Abstractions
- `.github/instructions/api-controllers.instructions.md` — Controllers inherit ApiController, dispatch via `Mediator.Send(...)`, `[ProducesResponseType]` on every action
- `.github/instructions/testing.instructions.md` — MSTest + Moq + FluentAssertions ≤v6, Given_When_Then naming, controller tests via BaseControllerTests
- `.agent-context/0-knowledge/02-hyperliquid-integration.md` — REST API at `https://api.hyperliquid-testnet.xyz`, wallet-based auth, Nethereum for key derivation
- `.agent-context/3-develop/backlog/draft/F1-configuration-connectivity.md` — Health endpoint response shape, 5-second timeout, fail-fast config validation

## Design References

- **Hyperliquid Info API**: `POST https://api.hyperliquid-testnet.xyz/info` with body `{"type": "meta"}` — unauthenticated, returns exchange metadata. A successful 200 response confirms testnet reachability.
- **Nethereum.Signer**: `EthECKey` class derives public address from a hex-encoded private key. The private key must be a 64-character hex string (with or without `0x` prefix).
- **MediatR health query**: The health check follows the CQRS query pattern — `GetHealthQuery` dispatched from controller, `GetHealthQueryHandler` orchestrates the signer + rest client.

---

### Task 2.1: Implement HyperliquidOptions configuration model {#task-21-implement-hyperliquidoptions-configuration-model}

Create the strongly-typed options class that binds to the `Hyperliquid` configuration section.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Application/Abstractions/Configuration/HyperliquidOptions.cs` — Options model (Application layer — accessible to handlers without circular dependency)
- **Success**:
  - Class compiles and can be bound via `IOptions<HyperliquidOptions>`
  - Application project has `Microsoft.Extensions.Options` package for `IOptions<T>` support
- **Dependencies**: Phase 1 complete

#### Implementation Details

Add `Microsoft.Extensions.Options` package to the Application project (needed for `IOptions<T>` in query handlers):

```bash
dotnet add src/TradingApp.Application/TradingApp.Application.csproj package Microsoft.Extensions.Options
```

```csharp
// src/TradingApp.Application/Abstractions/Configuration/HyperliquidOptions.cs — new file
namespace TradingApp.Application.Abstractions.Configuration;

public sealed class HyperliquidOptions
{
    public const string SectionName = "Hyperliquid";

    public string PrivateKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.hyperliquid-testnet.xyz";
    public string Network { get; set; } = "testnet";
}
```

##### Pattern References

- `.github/instructions/csharp.instructions.md` — sealed class, IOptions<T> binding pattern
- `.agent-context/3-develop/backlog/draft/F1-configuration-connectivity.md` — config shape with `PrivateKey`, `BaseUrl`

---

### Task 2.2: Implement HyperliquidSigner with Nethereum key derivation {#task-22-implement-hyperliquidsigner-with-nethereum-key-derivation}

Create the signer service that derives an Ethereum-compatible wallet address from a private key. Registered as singleton since the wallet address is derived once on startup.

- **Complexity**: Medium
- **Risk Factors**: Nethereum API — must handle hex keys with/without `0x` prefix; malformed keys must throw clearly
- **Files**:
  - `src/TradingApp.Application/Abstractions/Services/IHyperliquidSigner.cs` — Signer interface (Application layer)
  - `src/TradingApp.Infrastructure/Services/HyperliquidSigner.cs` — Signer implementation
- **Success**:
  - Given a valid 64-char hex private key, derives the correct checksummed Ethereum address
  - Given a malformed key, throws an informative exception
  - `IHyperliquidSigner` interface is in Application layer, implementation is in Infrastructure
- **Dependencies**: Task 2.1

#### Implementation Details

```csharp
// src/TradingApp.Application/Abstractions/Services/IHyperliquidSigner.cs — new file
namespace TradingApp.Application.Abstractions.Services;

public interface IHyperliquidSigner
{
    string WalletAddress { get; }
}
```

```csharp
// src/TradingApp.Infrastructure/Services/HyperliquidSigner.cs — new file
using Nethereum.Signer;
using TradingApp.Application.Abstractions.Services;

namespace TradingApp.Infrastructure.Services;

public sealed class HyperliquidSigner : IHyperliquidSigner
{
    private readonly string _walletAddress;

    private HyperliquidSigner(string walletAddress)
    {
        _walletAddress = walletAddress;
    }

    public string WalletAddress => _walletAddress;

    public static HyperliquidSigner Create(string privateKey)
    {
        if (string.IsNullOrWhiteSpace(privateKey))
        {
            throw new ArgumentException(
                "Hyperliquid private key is missing. Set 'Hyperliquid__PrivateKey' environment variable or add 'Hyperliquid:PrivateKey' to appsettings.Development.json.",
                nameof(privateKey));
        }

        var normalised = privateKey.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? privateKey[2..]
            : privateKey;

        if (normalised.Length != 64 || !IsHex(normalised))
        {
            throw new ArgumentException(
                $"Hyperliquid private key is malformed. Expected a 64-character hex string (with optional '0x' prefix). Received {privateKey.Length} characters.",
                nameof(privateKey));
        }

        try
        {
            var ecKey = new EthECKey(privateKey);
            var address = ecKey.GetPublicAddress();
            return new HyperliquidSigner(address);
        }
        catch (Exception ex)
        {
            throw new ArgumentException(
                $"Failed to derive wallet address from private key: {ex.Message}. Ensure the key is a valid Ethereum-compatible private key.",
                nameof(privateKey),
                ex);
        }
    }

    private static bool IsHex(string value)
    {
        foreach (var c in value)
        {
            if (!Uri.IsHexDigit(c))
                return false;
        }

        return true;
    }
}
```

##### Pattern References

- `.github/instructions/csharp.instructions.md` — sealed class, static factory method `Create(...)` with private constructor, `_camelCase` private fields

---

### Task 2.3: Implement HyperliquidRestClient for connectivity check {#task-23-implement-hyperliquidrestclient-for-connectivity-check}

Create the HTTP client that calls the Hyperliquid `/info` endpoint to verify testnet reachability. Uses typed HttpClient pattern.

- **Complexity**: Medium
- **Risk Factors**: HTTP timeout must be 5 seconds per PBI; must handle non-2xx status codes as disconnected (not throw)
- **Files**:
  - `src/TradingApp.Application/Abstractions/Services/IHyperliquidRestClient.cs` — REST client interface (Application layer)
  - `src/TradingApp.Infrastructure/Services/HyperliquidRestClient.cs` — REST client implementation
- **Success**:
  - Calls `POST /info` with `{"type": "meta"}` and returns boolean connectivity result
  - Respects 5-second timeout configured at HttpClient level
  - `IHyperliquidRestClient` interface is in Application layer, implementation is in Infrastructure
- **Dependencies**: Task 2.1

#### Implementation Details

```csharp
// src/TradingApp.Application/Abstractions/Services/IHyperliquidRestClient.cs — new file
namespace TradingApp.Application.Abstractions.Services;

public interface IHyperliquidRestClient
{
    Task<bool> CheckConnectivityAsync(CancellationToken cancellationToken = default);
}
```

```csharp
// src/TradingApp.Infrastructure/Services/HyperliquidRestClient.cs — new file
using System.Net.Http.Json;
using TradingApp.Application.Abstractions.Services;

namespace TradingApp.Infrastructure.Services;

public sealed class HyperliquidRestClient : IHyperliquidRestClient
{
    private readonly HttpClient _httpClient;

    public HyperliquidRestClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> CheckConnectivityAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "/info",
            new { type = "meta" },
            cancellationToken);

        return response.IsSuccessStatusCode;
    }
}
```

##### Pattern References

- `.github/instructions/csharp.instructions.md` — sealed class, CancellationToken on async methods
- `.agent-context/0-knowledge/02-hyperliquid-integration.md` — REST API, `/info` endpoint

---

### Task 2.4: Create GetHealthQuery and handler using MediatR {#task-24-create-gethealthquery-and-handler-using-mediatr}

Create the CQRS query and handler that orchestrates the connectivity check. The handler uses HyperliquidRestClient and HyperliquidSigner to produce the health response DTO.

- **Complexity**: Medium
- **Risk Factors**: Handler must catch HTTP exceptions and return a valid DTO in all cases (connected, disconnected, timeout, network error)
- **Files**:
  - `src/TradingApp.Application/Health/Models/HealthDto.cs` — Health response DTO
  - `src/TradingApp.Application/Health/Queries/GetHealthQuery.cs` — Query record + handler (colocated per convention)
- **Success**:
  - `GetHealthQuery` is a MediatR `Query<HealthDto>`
  - Handler returns connected DTO when REST client succeeds, disconnected with error when it fails
  - Handler catches `TaskCanceledException`, `HttpRequestException` and returns meaningful DTOs
- **Dependencies**: Task 2.2, Task 2.3

#### Implementation Details

```csharp
// src/TradingApp.Application/Health/Models/HealthDto.cs — new file
namespace TradingApp.Application.Health.Models;

public sealed class HealthDto
{
    public string Status { get; set; } = string.Empty;
    public string WalletAddress { get; set; } = string.Empty;
    public string Network { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Error { get; set; }
}
```

```csharp
// src/TradingApp.Application/Health/Queries/GetHealthQuery.cs — new file
using Microsoft.Extensions.Options;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Queries;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Health.Models;

namespace TradingApp.Application.Health.Queries;

public sealed record GetHealthQuery : Query<HealthDto>;

public sealed class GetHealthQueryHandler : QueryHandler<GetHealthQuery, HealthDto>
{
    private readonly IHyperliquidRestClient _restClient;
    private readonly IHyperliquidSigner _signer;
    private readonly HyperliquidOptions _options;

    public GetHealthQueryHandler(
        IHyperliquidRestClient restClient,
        IHyperliquidSigner signer,
        IOptions<HyperliquidOptions> options)
    {
        _restClient = restClient;
        _signer = signer;
        _options = options.Value;
    }

    public override async Task<HealthDto> Handle(GetHealthQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var isConnected = await _restClient.CheckConnectivityAsync(cancellationToken);

            return new HealthDto
            {
                Status = isConnected ? "connected" : "disconnected",
                WalletAddress = TruncateAddress(_signer.WalletAddress),
                Network = _options.Network,
                Timestamp = DateTime.UtcNow,
                Error = isConnected ? null : "Hyperliquid testnet API did not respond successfully"
            };
        }
        catch (TaskCanceledException)
        {
            return new HealthDto
            {
                Status = "disconnected",
                WalletAddress = TruncateAddress(_signer.WalletAddress),
                Network = _options.Network,
                Timestamp = DateTime.UtcNow,
                Error = "Hyperliquid testnet API did not respond within 5 seconds"
            };
        }
        catch (HttpRequestException ex)
        {
            return new HealthDto
            {
                Status = "disconnected",
                WalletAddress = TruncateAddress(_signer.WalletAddress),
                Network = _options.Network,
                Timestamp = DateTime.UtcNow,
                Error = $"Failed to reach Hyperliquid testnet: {ex.Message}"
            };
        }
    }

    private static string TruncateAddress(string address)
    {
        return address.Length > 10
            ? $"{address[..6]}...{address[^4..]}"
            : address;
    }
}
```

##### Pattern References

- `.github/instructions/dotnet-architecture.instructions.md` — Query<T> pattern, handler colocated with query record, bounded context folder structure (Health/), interfaces in Application/Abstractions with implementations in Infrastructure
- `.github/instructions/csharp.instructions.md` — sealed classes, CancellationToken

---

### Task 2.5: Create HealthController using ApiController base {#task-25-create-healthcontroller-using-apicontroller-base}

Create the API controller that dispatches the health query via MediatR. The controller contains no business logic — it delegates to the handler.

- **Complexity**: Low
- **Risk Factors**: None — standard controller pattern
- **Files**:
  - `src/TradingApp.Api/Controllers/HealthController.cs` — API controller
- **Success**:
  - `GET /api/health` dispatches `GetHealthQuery` via MediatR
  - Returns 200 OK with HealthDto payload
  - Controller inherits from ApiController base
- **Dependencies**: Task 2.4

#### Implementation Details

```csharp
// src/TradingApp.Api/Controllers/HealthController.cs — new file
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TradingApp.Api.Infrastructure;
using TradingApp.Application.Health.Models;
using TradingApp.Application.Health.Queries;

namespace TradingApp.Api.Controllers;

[Route("api/health")]
public sealed class HealthController : ApiController
{
    public HealthController(IMediator mediator, IdentityService identityService)
        : base(mediator, identityService)
    {
    }

    [HttpGet]
    [ProducesResponseType(typeof(HealthDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHealthAsync(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetHealthQuery(), cancellationToken);
        return Ok(result);
    }
}
```

##### Pattern References

- `.github/instructions/api-controllers.instructions.md` — inherits ApiController, `Mediator.Send(...)`, `[Route("api/...")]`, `[ProducesResponseType]`

---

### Task 2.6: Configure Program.cs — DI, config validation, CORS, fail-fast {#task-26-configure-programcs--di-config-validation-cors-fail-fast}

Update the Program.cs shell from Phase 1 to add Hyperliquid configuration binding, signer registration (fail-fast), HttpClient for REST client, and CORS for Angular dev server.

- **Complexity**: Medium
- **Risk Factors**: Config validation must run before the app starts; fail-fast must produce clear error messages; must not break the MediatR setup from Phase 1
- **Files**:
  - `src/TradingApp.Api/Program.cs` — Update existing file
- **Success**:
  - App starts with valid config, logs derived wallet address
  - App throws on startup with clear error when private key is missing or malformed
  - CORS allows requests from `http://localhost:4200`
  - HyperliquidRestClient HttpClient configured with 5-second timeout and correct base URL
- **Dependencies**: Task 2.1, Task 2.2, Task 2.3, Task 2.5

#### Implementation Details

```csharp
// src/TradingApp.Api/Program.cs — replace content from Phase 1
using TradingApp.Api.Infrastructure;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// MediatR — scan Application assembly for handlers
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<TradingApp.Application.Abstractions.Commands.Command>());

// Identity stub (replace with real auth service in production)
builder.Services.AddSingleton<IdentityService>();

// Bind Hyperliquid configuration
builder.Services.Configure<HyperliquidOptions>(
    builder.Configuration.GetSection(HyperliquidOptions.SectionName));

// Validate and register HyperliquidSigner (fail-fast on startup)
var hyperliquidConfig = builder.Configuration
    .GetSection(HyperliquidOptions.SectionName)
    .Get<HyperliquidOptions>()
    ?? throw new InvalidOperationException(
        "Hyperliquid configuration section is missing. Add a 'Hyperliquid' section to appsettings.json or appsettings.Development.json.");

var signer = HyperliquidSigner.Create(hyperliquidConfig.PrivateKey);
builder.Services.AddSingleton<IHyperliquidSigner>(signer);

// Register HyperliquidRestClient as typed HttpClient with interface
builder.Services.AddHttpClient<IHyperliquidRestClient, HyperliquidRestClient>(client =>
{
    client.BaseAddress = new Uri(hyperliquidConfig.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

// CORS for Angular dev server
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllers();

var app = builder.Build();

// Log wallet address on startup (never log the private key)
app.Logger.LogInformation(
    "Hyperliquid wallet configured: {WalletAddress} on {Network}",
    signer.WalletAddress,
    hyperliquidConfig.Network);

app.UseCors();
app.MapControllers();

app.Run();

// Required for WebApplicationFactory in tests
public partial class Program { }
```

##### Pattern References

- `.github/instructions/csharp.instructions.md` — IOptions<T> binding, fail-fast configuration validation
- `.agent-context/3-develop/backlog/draft/F1-configuration-connectivity.md` — CORS for `http://localhost:4200`, 5-second timeout, fail-fast behaviour

---

### Task 2.7: Update .gitignore and create appsettings files {#task-27-update-gitignore-and-create-appsettings-files}

Add `appsettings.Development.json` to `.gitignore`. Create base `appsettings.json` (committed, no secrets) and dev config with private key placeholder (gitignored).

- **Complexity**: Low
- **Risk Factors**: Must not break existing `.gitignore` entries; append only
- **Files**:
  - `.gitignore` — Append exclusion for `appsettings.Development.json`
  - `src/TradingApp.Api/appsettings.json` — Base config (committed, no secrets)
  - `src/TradingApp.Api/appsettings.Development.json` — Dev config with placeholder (gitignored)
- **Success**:
  - `git status` does not show `appsettings.Development.json` as tracked
  - `appsettings.json` contains Hyperliquid section with BaseUrl but no PrivateKey
- **Dependencies**: Task 2.1

#### Implementation Details

Append to `.gitignore`:

```gitignore
# Hyperliquid POC - development config with private keys
appsettings.Development.json
```

```json
// src/TradingApp.Api/appsettings.json — modify (replace template content)
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Hyperliquid": {
    "BaseUrl": "https://api.hyperliquid-testnet.xyz",
    "Network": "testnet"
  }
}
```

```json
// src/TradingApp.Api/appsettings.Development.json — new file (gitignored)
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Hyperliquid": {
    "PrivateKey": "<your-testnet-private-key-here>"
  }
}
```

##### Pattern References

- `.agent-context/3-develop/backlog/draft/F1-configuration-connectivity.md` — `.gitignore` requirement, config hierarchy

---

### Task 2.8: Write unit tests — signer and controller {#task-28-write-unit-tests--signer-and-controller}

Write MSTest + FluentAssertions + Moq tests covering: HyperliquidSigner key derivation (valid key, missing key, malformed key), and HealthController integration through BaseControllerTests.

- **Complexity**: Medium
- **Risk Factors**: Need a known test private key for Nethereum; controller tests require configuring WebApplicationFactory with valid config
- **Files**:
  - `tests/TradingApp.Infrastructure.Tests/Services/HyperliquidSignerTests.cs` — Signer unit tests
  - `tests/TradingApp.Api.Tests/Controllers/HealthControllerTests.cs` — Controller integration tests
- **Success**:
  - Signer tests: valid key → correct address, missing key → ArgumentException, malformed → ArgumentException
  - Controller tests: connected → 200 with "connected", disconnected → 200 with "disconnected"
  - All tests pass with `dotnet test`
- **Dependencies**: Task 2.2, Task 2.5, Task 2.6

#### Implementation Details

```csharp
// tests/TradingApp.Infrastructure.Tests/Services/HyperliquidSignerTests.cs — new file
using TradingApp.Infrastructure.Services;

namespace TradingApp.Infrastructure.Tests.Services;

[TestClass]
public sealed class HyperliquidSignerTests
{
    // Well-known test key pair (Ethereum testnet — never use on mainnet)
    private const string ValidPrivateKey = "0x4c0883a69102937d6231471b5dbb6204fe512961708279f2a4c5890a0c1f9b2e";
    private const string ExpectedAddress = "0x2c7536E3605D9C16a7a3D7b1898e529396a65c23";

    [TestMethod]
    public void GivenValidPrivateKey_WhenCreate_ThenDerivesCorrectWalletAddress()
    {
        var signer = HyperliquidSigner.Create(ValidPrivateKey);

        signer.WalletAddress.Should().BeEquivalentTo(ExpectedAddress);
    }

    [TestMethod]
    public void GivenPrivateKeyWithout0xPrefix_WhenCreate_ThenDerivesCorrectWalletAddress()
    {
        var keyWithoutPrefix = ValidPrivateKey[2..];

        var signer = HyperliquidSigner.Create(keyWithoutPrefix);

        signer.WalletAddress.Should().BeEquivalentTo(ExpectedAddress);
    }

    [TestMethod]
    public void GivenEmptyPrivateKey_WhenCreate_ThenThrowsArgumentException()
    {
        var act = () => HyperliquidSigner.Create(string.Empty);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*missing*");
    }

    [TestMethod]
    public void GivenNullPrivateKey_WhenCreate_ThenThrowsArgumentException()
    {
        var act = () => HyperliquidSigner.Create(null!);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*missing*");
    }

    [TestMethod]
    public void GivenMalformedPrivateKey_WhenCreate_ThenThrowsArgumentException()
    {
        var act = () => HyperliquidSigner.Create("not-a-valid-key");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*malformed*");
    }

    [TestMethod]
    public void GivenTooShortPrivateKey_WhenCreate_ThenThrowsArgumentException()
    {
        var act = () => HyperliquidSigner.Create("0x1234");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*malformed*");
    }
}
```

```csharp
// tests/TradingApp.Api.Tests/Controllers/HealthControllerTests.cs — new file
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TradingApp.Api.Tests.Infrastructure;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Health.Models;
using TradingApp.Infrastructure.Services;

namespace TradingApp.Api.Tests.Controllers;

[TestClass]
public sealed class HealthControllerTests : BaseControllerTests
{
    private const string BaseUrl = "api/health";
    private const string TestPrivateKey = "0x4c0883a69102937d6231471b5dbb6204fe512961708279f2a4c5890a0c1f9b2e";

    [TestMethod]
    public async Task GivenConnectedTestnet_WhenGetHealth_ThenReturnsConnectedStatus()
    {
        // Arrange
        var fakeHandler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateTestClientWithFakeHttp(fakeHandler);

        // Act
        var response = await client.GetAsync(BaseUrl);

        // Assert
        var health = await response.ReadAndAssertSuccessAsync<HealthDto>();
        health.Status.Should().Be("connected");
        health.Network.Should().Be("testnet");
        health.Error.Should().BeNull();
        health.WalletAddress.Should().Contain("...");
    }

    [TestMethod]
    public async Task GivenDisconnectedTestnet_WhenGetHealth_ThenReturnsDisconnectedStatus()
    {
        // Arrange
        var fakeHandler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = CreateTestClientWithFakeHttp(fakeHandler);

        // Act
        var response = await client.GetAsync(BaseUrl);

        // Assert
        var health = await response.ReadAndAssertSuccessAsync<HealthDto>();
        health.Status.Should().Be("disconnected");
        health.Error.Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public async Task GivenNetworkError_WhenGetHealth_ThenReturnsDisconnectedWithError()
    {
        // Arrange
        var fakeHandler = new FakeHttpMessageHandler(
            new HttpRequestException("Network unreachable"));
        var client = CreateTestClientWithFakeHttp(fakeHandler);

        // Act
        var response = await client.GetAsync(BaseUrl);

        // Assert
        var health = await response.ReadAndAssertSuccessAsync<HealthDto>();
        health.Status.Should().Be("disconnected");
        health.Error.Should().Contain("Network unreachable");
    }

    private HttpClient CreateTestClientWithFakeHttp(FakeHttpMessageHandler fakeHandler)
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Hyperliquid:PrivateKey", TestPrivateKey);
                builder.UseSetting("Hyperliquid:BaseUrl", "https://api.hyperliquid-testnet.xyz");
                builder.UseSetting("Hyperliquid:Network", "testnet");

                builder.ConfigureServices(services =>
                {
                    // Replace the real HttpClient handler with our fake
                    services.AddHttpClient<IHyperliquidRestClient, HyperliquidRestClient>()
                        .ConfigurePrimaryHttpMessageHandler(() => fakeHandler);
                });
            });

        return factory.CreateClient();
    }
}
```

> **Note**: The well-known key pair (`0x4c0883a6...`) is from Nethereum documentation — safe for tests. The controller tests use `WebApplicationFactory<Program>` to test the full MediatR pipeline end-to-end, overriding the HTTP handler to fake Hyperliquid responses. If the expected address differs at runtime, update the constant after verification.

##### Pattern References

- `.github/instructions/testing.instructions.md` — MSTest + FluentAssertions (`.Should()`) + Moq, Given_When_Then naming, `[TestClass]`/`[TestMethod]`, controller tests via BaseControllerTests/WebApplicationFactory
- `.github/instructions/api-controllers.instructions.md` — controller actions tested through HTTP client

---

### Task 2.9: Build solution and run all tests {#task-29-build-solution-and-run-all-tests}

Build the entire solution and run all tests to verify everything compiles and passes.

- **Complexity**: Low
- **Risk Factors**: NuGet restore for Nethereum may need internet; test key pair must produce expected address
- **Files**: None (verification step)
- **Success**:
  - `dotnet build TradingApp.sln` succeeds with no errors
  - `dotnet test TradingApp.sln` passes all tests
- **Dependencies**: All prior Phase 2 tasks

#### Implementation Details

```bash
dotnet build TradingApp.sln
dotnet test TradingApp.sln --verbosity normal
```

If any test fails, debug and fix before committing.

##### Pattern References

- `.github/instructions/testing.instructions.md` — tests must pass within the phase

---

## Phase Success Criteria

- All Hyperliquid services (Options, Signer, RestClient) compile and have tests
- GetHealthQuery + handler dispatches through MediatR
- HealthController inherits ApiController, dispatches via `Mediator.Send()`
- Program.cs has config validation, fail-fast, CORS, and logs wallet address
- Signer tests pass: valid key derivation, missing key error, malformed key error
- Controller tests pass: connected response, disconnected response, network error response
- `appsettings.Development.json` is in `.gitignore`
- `dotnet build` and `dotnet test` both succeed
- Phase is ready for a single commit
