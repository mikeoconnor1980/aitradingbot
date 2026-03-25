<!-- markdownlint-disable-file -->

# Task Details: F5 — Order Placement

## Phase 2: Order Placement Backend (Service, Client, Controller)

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — sealed classes, `_camelCase` fields, Guard.Against
- `.github/instructions/api-controllers.instructions.md` — Controller patterns, `[ApiController]`, route conventions
- `.github/instructions/testing.instructions.md` — MSTest, Moq, FluentAssertions, `Given_When_Then`, WebApplicationFactory
- `.github/instructions/dotnet-architecture.instructions.md` — Layer boundaries, DTO patterns, service interfaces
- `.agent-context/0-knowledge/02-hyperliquid-integration.md` — Extending IHyperliquidRestClient: "New non-info endpoints — add a method"
- `.agent-context/0-knowledge/10-architecture-decisions.md` — ADR 14: Direct service injection for POC

## Design References

### Controller Pattern Decision

Following ADR 14 (direct service injection for POC), `OrdersController` mirrors the `AccountController` pattern:
- Inherits `ControllerBase` with `[ApiController]` attribute (not `ApiController` base)
- Directly injects `IHyperliquidOrderService`
- Relies on `HttpGlobalExceptionFilter` for exception → HTTP status mapping (no per-action try/catch)
- Service lives in `TradingApp.Api/Services/` with interface alongside

### Hyperliquid Exchange Endpoint

Order submission uses `POST /exchange` (not `/info`). The request shape:
```json
{
  "action": { "type": "order", "orders": [...], "grouping": "na" },
  "nonce": 1716499200000,
  "signature": { "r": "0x...", "s": "0x...", "v": 27 },
  "vaultAddress": null
}
```

The response from Hyperliquid contains a `status` field (`"ok"` or `"err"`) and optional statuses per order.

---

### Task 2.1: Add PostExchangeAsync to REST client {#task-21-add-postexchangeasync-to-rest-client}

Add a `PostExchangeAsync` method to `IHyperliquidRestClient` and `HyperliquidRestClient` for authenticated order submission to Hyperliquid's `/exchange` endpoint.

- **Complexity**: Medium
- **Risk Factors**: Response format differs from `/info`; error responses may have different structure
- **Files**:
  - `src/TradingApp.Application/Abstractions/Services/IHyperliquidRestClient.cs` — Add method signature
  - `src/TradingApp.Infrastructure/Services/HyperliquidRestClient.cs` — Implement method
- **Success**:
  - `PostExchangeAsync` sends JSON POST to `/exchange` and returns typed response
  - Error responses from exchange are propagated (not swallowed)
  - Existing `/info` methods unchanged
- **Dependencies**: None (Phase 1 provides signing; this is the transport layer)

#### Implementation Details

```csharp
// src/TradingApp.Application/Abstractions/Services/IHyperliquidRestClient.cs — modification
// Add to existing interface:

/// <summary>
/// Posts a signed action to the Hyperliquid /exchange endpoint.
/// </summary>
Task<TResponse> PostExchangeAsync<TResponse>(object signedPayload, CancellationToken cancellationToken = default);
```

```csharp
// src/TradingApp.Infrastructure/Services/HyperliquidRestClient.cs — modification
// Add new method following PostInfoAsync pattern:

public async Task<TResponse> PostExchangeAsync<TResponse>(object signedPayload, CancellationToken cancellationToken = default)
{
    using var response = await _httpClient.PostAsJsonAsync("/exchange", signedPayload, cancellationToken);

    var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
        throw new HttpRequestException(
            $"Hyperliquid exchange returned {response.StatusCode}: {responseBody}",
            null,
            response.StatusCode);
    }

    var result = JsonSerializer.Deserialize<TResponse>(responseBody, CaseSensitiveOptions);
    if (result is null)
        throw new InvalidOperationException($"Failed to deserialize exchange response: {responseBody}");

    return result;
}
```

##### Pattern References

- `src/TradingApp.Infrastructure/Services/HyperliquidRestClient.cs` — `PostInfoAsync<T>` method (existing pattern)
- `src/TradingApp.Application/Abstractions/Services/IHyperliquidRestClient.cs` — Interface extension point

---

### Task 2.2: Create request and response DTOs {#task-22-create-request-and-response-dtos}

Create the DTOs for the order placement API: request model, response model, and test-sign response model.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `src/TradingApp.Api/Models/PlaceOrderRequest.cs` — New file
  - `src/TradingApp.Api/Models/PlaceOrderResponse.cs` — New file
  - `src/TradingApp.Api/Models/TestSignResponse.cs` — New file
  - `src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidExchangeResponse.cs` — New file: wire model for Hyperliquid `/exchange` response
- **Success**:
  - DTOs match the F5 PBI API contract
  - Properties use appropriate types (nullable where specified)
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Api/Models/PlaceOrderRequest.cs — new file
using System.ComponentModel.DataAnnotations;

namespace TradingApp.Api.Models;

public sealed class PlaceOrderRequest
{
    [Required]
    public string Asset { get; set; } = default!;

    [Required]
    public string Side { get; set; } = default!; // "buy" or "sell"

    [Required]
    public string OrderType { get; set; } = default!; // "market" or "limit"

    public decimal? Price { get; set; } // Required for limit, null for market

    [Required]
    [Range(0.000001, double.MaxValue)]
    public decimal Size { get; set; }
}
```

```csharp
// src/TradingApp.Api/Models/PlaceOrderResponse.cs — new file
namespace TradingApp.Api.Models;

public sealed class PlaceOrderResponse
{
    public bool Success { get; set; }
    public string? OrderId { get; set; }
    public string? Status { get; set; }
    public string? Detail { get; set; }
}
```

```csharp
// src/TradingApp.Api/Models/TestSignResponse.cs — new file
namespace TradingApp.Api.Models;

public sealed class TestSignResponse
{
    public string DomainSeparator { get; set; } = default!;
    public string TypeHash { get; set; } = default!;
    public string MessageHash { get; set; } = default!;
    public SignatureDto Signature { get; set; } = default!;
}

public sealed class SignatureDto
{
    public int V { get; set; }
    public string R { get; set; } = default!;
    public string S { get; set; } = default!;
}
```

```csharp
// src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidExchangeResponse.cs — new file
using System.Text.Json.Serialization;

namespace TradingApp.Infrastructure.Hyperliquid.Models;

/// <summary>
/// Response from Hyperliquid /exchange endpoint.
/// </summary>
public sealed class HyperliquidExchangeResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = default!;

    [JsonPropertyName("response")]
    public HyperliquidExchangeResponseData? Response { get; set; }
}

public sealed class HyperliquidExchangeResponseData
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = default!;

    [JsonPropertyName("data")]
    public HyperliquidOrderResponseData? Data { get; set; }
}

public sealed class HyperliquidOrderResponseData
{
    [JsonPropertyName("statuses")]
    public List<HyperliquidOrderStatus>? Statuses { get; set; }
}

public sealed class HyperliquidOrderStatus
{
    /// <summary>
    /// Present on success — contains the resting order details.
    /// </summary>
    [JsonPropertyName("resting")]
    public HyperliquidRestingOrder? Resting { get; set; }

    /// <summary>
    /// Present on success for filled orders.
    /// </summary>
    [JsonPropertyName("filled")]
    public HyperliquidFilledOrder? Filled { get; set; }

    /// <summary>
    /// Present on error — contains the error message string.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public sealed class HyperliquidRestingOrder
{
    [JsonPropertyName("oid")]
    public long Oid { get; set; }
}

public sealed class HyperliquidFilledOrder
{
    [JsonPropertyName("totalSz")]
    public string TotalSz { get; set; } = default!;

    [JsonPropertyName("avgPx")]
    public string AvgPx { get; set; } = default!;

    [JsonPropertyName("oid")]
    public long Oid { get; set; }
}
```

**Note**: The Hyperliquid exchange response structure should be verified against the actual testnet response. The implementing agent should make a test call and inspect the response format.

##### Pattern References

- `src/TradingApp.Api/Models/OpenOrderDto.cs` — Existing DTO pattern (settable properties)
- `src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidAssetCtx.cs` — Wire model pattern

---

### Task 2.3: Create order service {#task-23-create-order-service}

Create `IHyperliquidOrderService` and `HyperliquidOrderService` in the API services layer. The service orchestrates: building the order action, computing the action hash, signing with EIP-712, submitting to the exchange, and logging latency.

- **Complexity**: High
- **Risk Factors**: Correct orchestration of signing → submission flow; latency logging; error classification (signature rejection vs other errors)
- **Files**:
  - `src/TradingApp.Api/Services/IHyperliquidOrderService.cs` — New file: service interface
  - `src/TradingApp.Api/Services/HyperliquidOrderService.cs` — New file: service implementation
- **Success**:
  - `PlaceOrderAsync` builds action, signs, submits, and returns response
  - `TestSignAsync` signs a dummy payload and returns diagnostic info without submitting
  - Structured logs include `{SubmitTimestampUtc}`, `{ResponseTimestampUtc}`, `{LatencyMs}`
  - Signing errors are distinguished from exchange errors in logs
- **Dependencies**: Phase 1 tasks (signing infrastructure), Task 2.1 (exchange client), Task 2.2 (DTOs)

#### Implementation Details

```csharp
// src/TradingApp.Api/Services/IHyperliquidOrderService.cs — new file
using TradingApp.Api.Models;

namespace TradingApp.Api.Services;

public interface IHyperliquidOrderService
{
    Task<PlaceOrderResponse> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken cancellationToken = default);
    Task<TestSignResponse> TestSignAsync(CancellationToken cancellationToken = default);
}
```

```csharp
// src/TradingApp.Api/Services/HyperliquidOrderService.cs — new file
using System.Diagnostics;
using Microsoft.Extensions.Options;
using TradingApp.Api.Models;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Infrastructure.Hyperliquid;
using TradingApp.Infrastructure.Hyperliquid.Models;

namespace TradingApp.Api.Services;

public sealed class HyperliquidOrderService : IHyperliquidOrderService
{
    private readonly IHyperliquidRestClient _restClient;
    private readonly IHyperliquidSigner _signer;
    private readonly INonceProvider _nonceProvider;
    private readonly HyperliquidOptions _options;
    private readonly ILogger<HyperliquidOrderService> _logger;

    /// <summary>
    /// BTC-PERP is always index 0 on Hyperliquid. Hard-coded for POC scope.
    /// </summary>
    private const int BtcAssetIndex = 0;

    public HyperliquidOrderService(
        IHyperliquidRestClient restClient,
        IHyperliquidSigner signer,
        INonceProvider nonceProvider,
        IOptions<HyperliquidOptions> options,
        ILogger<HyperliquidOrderService> logger)
    {
        _restClient = restClient;
        _signer = signer;
        _nonceProvider = nonceProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PlaceOrderResponse> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken cancellationToken = default)
    {
        var coin = HyperliquidAssetMapper.ToCoin(request.Asset);
        var isBuy = request.Side.Equals("buy", StringComparison.OrdinalIgnoreCase);
        var isMainnet = _options.Network.Equals("mainnet", StringComparison.OrdinalIgnoreCase);

        // For market orders, use a very high/low price to ensure fill
        var price = request.OrderType.Equals("market", StringComparison.OrdinalIgnoreCase)
            ? (isBuy ? 999_999_999m : 0.01m)
            : request.Price ?? throw new Application.Abstractions.Exceptions.DomainException("Price is required for limit orders.");

        var action = HyperliquidEip712.BuildOrderAction(
            assetIndex: BtcAssetIndex,
            isBuy: isBuy,
            price: price,
            size: request.Size);

        var nonce = _nonceProvider.GetNextNonce();

        // Compute action hash and build EIP-712 typed data
        var connectionId = HyperliquidEip712.ComputeActionHash(action, nonce, vaultAddress: null);
        var typedData = HyperliquidEip712.BuildPhantomAgentTypedData(connectionId, isMainnet);

        // Sign
        var (r, s, v) = _signer.SignTypedData(typedData);

        // Build exchange payload
        var payload = new
        {
            action,
            nonce,
            signature = new { r, s, v },
            vaultAddress = (string?)null
        };

        // Submit with latency tracking
        var submitTimestamp = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var exchangeResponse = await _restClient.PostExchangeAsync<HyperliquidExchangeResponse>(payload, cancellationToken);
            stopwatch.Stop();
            var responseTimestamp = DateTimeOffset.UtcNow;

            _logger.LogInformation(
                "Order submitted. SubmitTimestampUtc={SubmitTimestampUtc}, ResponseTimestampUtc={ResponseTimestampUtc}, LatencyMs={LatencyMs}",
                submitTimestamp, responseTimestamp, stopwatch.ElapsedMilliseconds);

            return MapExchangeResponse(exchangeResponse);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("signature", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex,
                "EIP-712 signature rejected by Hyperliquid. WalletAddress={WalletAddress}, Nonce={Nonce}, V={V}",
                _signer.WalletAddress, nonce, v);
            return new PlaceOrderResponse
            {
                Success = false,
                Status = "signature_rejected",
                Detail = ex.Message
            };
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex,
                "Order submission failed. SubmitTimestampUtc={SubmitTimestampUtc}, LatencyMs={LatencyMs}",
                submitTimestamp, stopwatch.ElapsedMilliseconds);
            return new PlaceOrderResponse
            {
                Success = false,
                Status = "rejected",
                Detail = ex.Message
            };
        }
    }

    public Task<TestSignResponse> TestSignAsync(CancellationToken cancellationToken = default)
    {
        var isMainnet = _options.Network.Equals("mainnet", StringComparison.OrdinalIgnoreCase);

        // Build a dummy order action for signing
        var action = HyperliquidEip712.BuildOrderAction(
            assetIndex: BtcAssetIndex, isBuy: true, price: 65000m, size: 0.001m);

        var nonce = _nonceProvider.GetNextNonce();
        var connectionId = HyperliquidEip712.ComputeActionHash(action, nonce, vaultAddress: null);
        var typedData = HyperliquidEip712.BuildPhantomAgentTypedData(connectionId, isMainnet);

        // Sign (without submitting)
        var (r, s, v) = _signer.SignTypedData(typedData);

        // Compute intermediate hashes for diagnostic output
        var signer = new Nethereum.Signer.EIP712.Eip712TypedDataSigner();
        var encodedData = signer.EncodeTypedData(typedData);
        var messageHash = "0x" + Convert.ToHexString(
            Nethereum.Util.Sha3Keccack.Current.CalculateHash(encodedData)).ToLowerInvariant();

        // Compute type hash: Keccak256("Agent(string source,bytes32 connectionId)")
        var typeHashBytes = Nethereum.Util.Sha3Keccack.Current.CalculateHash(
            System.Text.Encoding.UTF8.GetBytes("Agent(string source,bytes32 connectionId)"));
        var typeHash = "0x" + Convert.ToHexString(typeHashBytes).ToLowerInvariant();

        // Extract domain separator from EIP-712 encoded data.
        // Eip712TypedDataSigner.EncodeTypedData returns: 0x1901 + 32-byte domainSeparator + 32-byte structHash
        // So domainSeparator is bytes 2–33 of the encoded payload.
        var domainSeparatorBytes = encodedData[2..34];
        var domainSeparator = "0x" + Convert.ToHexString(domainSeparatorBytes).ToLowerInvariant();

        var response = new TestSignResponse
        {
            DomainSeparator = domainSeparator,
            TypeHash = typeHash,
            MessageHash = messageHash,
            Signature = new SignatureDto { V = v, R = r, S = s }
        };

        return Task.FromResult(response);
    }

    private static PlaceOrderResponse MapExchangeResponse(HyperliquidExchangeResponse exchangeResponse)
    {
        if (exchangeResponse.Status == "ok" && exchangeResponse.Response?.Data?.Statuses is { Count: > 0 } statuses)
        {
            var firstStatus = statuses[0];

            if (firstStatus.Resting is not null)
            {
                return new PlaceOrderResponse
                {
                    Success = true,
                    OrderId = firstStatus.Resting.Oid.ToString(),
                    Status = "open"
                };
            }

            if (firstStatus.Filled is not null)
            {
                return new PlaceOrderResponse
                {
                    Success = true,
                    OrderId = firstStatus.Filled.Oid.ToString(),
                    Status = "filled"
                };
            }

            if (firstStatus.Error is not null)
            {
                return new PlaceOrderResponse
                {
                    Success = false,
                    Status = "rejected",
                    Detail = firstStatus.Error
                };
            }
        }

        return new PlaceOrderResponse
        {
            Success = false,
            Status = "rejected",
            Detail = $"Unexpected response: {exchangeResponse.Status}"
        };
    }
}
```

**Key design decisions:**
- Market orders use extreme price (999,999,999 for buy, 0.01 for sell) to ensure immediate fill — this is the standard Hyperliquid approach
- Signature rejections are caught specifically and logged with signing parameters for debugging
- Latency is measured via `Stopwatch` and logged with structured fields

##### Pattern References

- `src/TradingApp.Api/Services/HyperliquidAccountService.cs` — Api-layer service pattern (injection of signer + rest client)
- `src/TradingApp.Api/Services/IHyperliquidAccountService.cs` — Service interface pattern

---

### Task 2.4: Create OrdersController {#task-24-create-orderscontroller}

Create `OrdersController` with `POST /api/orders` and `POST /api/orders/test-sign` endpoints.

- **Complexity**: Medium
- **Risk Factors**: Must follow existing controller patterns; response types must be annotated
- **Files**:
  - `src/TradingApp.Api/Controllers/OrdersController.cs` — New file
- **Success**:
  - `POST /api/orders` accepts `PlaceOrderRequest` and returns `PlaceOrderResponse`
  - `POST /api/orders/test-sign` returns `TestSignResponse`
  - ProducesResponseType annotations for Swagger
  - Global exception filter handles errors (no try/catch)
- **Dependencies**: Task 2.2 (DTOs), Task 2.3 (service)

#### Implementation Details

```csharp
// src/TradingApp.Api/Controllers/OrdersController.cs — new file
using Microsoft.AspNetCore.Mvc;
using TradingApp.Api.Infrastructure;
using TradingApp.Api.Models;
using TradingApp.Api.Services;

namespace TradingApp.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Produces("application/json")]
public sealed class OrdersController : ControllerBase
{
    private readonly IHyperliquidOrderService _orderService;

    public OrdersController(IHyperliquidOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(PlaceOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> PlaceOrderAsync([FromBody] PlaceOrderRequest request, CancellationToken ct)
    {
        var result = await _orderService.PlaceOrderAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("test-sign")]
    [ProducesResponseType(typeof(TestSignResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> TestSignAsync(CancellationToken ct)
    {
        var result = await _orderService.TestSignAsync(ct);
        return Ok(result);
    }
}
```

**Note**: The controller returns `200 OK` with `PlaceOrderResponse` (which has its own `Success` flag) rather than `201 Created`, because the order is not persisted locally — Hyperliquid is the source of truth. The `Success` field in the response indicates whether the exchange accepted the order.

**Note**: This controller intentionally does NOT use per-action try/catch (unlike `AccountController`). It relies on `HttpGlobalExceptionFilter` to map exceptions to HTTP status codes. This is the preferred pattern — `AccountController`'s try/catch is redundant with the global filter and should not be replicated.

##### Pattern References

- `src/TradingApp.Api/Controllers/AccountController.cs` — Direct service injection controller pattern (ADR 14)
- `src/TradingApp.Api/Infrastructure/Envelope.cs` — Error response type for ProducesResponseType

---

### Task 2.5: Register services in DI {#task-25-register-services-in-di}

Register `IHyperliquidOrderService`, `INonceProvider`, and their implementations in `Program.cs`.

- **Complexity**: Low
- **Risk Factors**: Service lifetime must match usage (NonceProvider = singleton, OrderService = scoped)
- **Files**:
  - `src/TradingApp.Api/Program.cs` — Add service registrations
- **Success**:
  - `INonceProvider` registered as singleton (must survive across requests for monotonic nonces)
  - `IHyperliquidOrderService` registered as scoped (following `IHyperliquidAccountService` pattern)
  - Application starts without DI errors
- **Dependencies**: Tasks 2.3, 1.5

#### Implementation Details

```csharp
// src/TradingApp.Api/Program.cs — modification
// Add after existing service registrations (after AddScoped<IHyperliquidAccountService>):

builder.Services.AddSingleton<INonceProvider, NonceProvider>();
builder.Services.AddScoped<IHyperliquidOrderService, HyperliquidOrderService>();
```

Add required `using` statements:
```csharp
using TradingApp.Application.Abstractions.Services; // for INonceProvider
using TradingApp.Infrastructure.Services;           // for NonceProvider
using TradingApp.Api.Services;                      // for IHyperliquidOrderService, HyperliquidOrderService
```

##### Pattern References

- `src/TradingApp.Api/Program.cs` — Existing flat DI registration pattern

---

### Task 2.6: Unit tests for order service {#task-26-unit-tests-for-order-service}

Write unit tests for `HyperliquidOrderService` covering happy path, error handling, and latency logging.

- **Complexity**: High
- **Risk Factors**: Mocking multiple dependencies; verifying structured log output
- **Files**:
  - `tests/TradingApp.Api.Tests/Services/HyperliquidOrderServiceTests.cs` — New file
- **Success**:
  - Happy path: PlaceOrderAsync builds action, signs, submits, returns success response
  - Error path: Exchange error returns response with error detail
  - Signature rejection: Specifically classified in response
  - TestSignAsync: Returns signature components without exchange call
  - Latency logging: Verify logger called with expected structured fields
- **Dependencies**: Task 2.3 (service implementation)

#### Implementation Details

```csharp
// tests/TradingApp.Api.Tests/Services/HyperliquidOrderServiceTests.cs — new file
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingApp.Api.Models;
using TradingApp.Api.Services;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Infrastructure.Hyperliquid.Models;

namespace TradingApp.Api.Tests.Services;

[TestClass]
public sealed class HyperliquidOrderServiceTests
{
    private Mock<IHyperliquidRestClient> _restClientMock = default!;
    private Mock<IHyperliquidSigner> _signerMock = default!;
    private Mock<INonceProvider> _nonceProviderMock = default!;
    private Mock<ILogger<HyperliquidOrderService>> _loggerMock = default!;
    private IOptions<HyperliquidOptions> _options = default!;
    private HyperliquidOrderService _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _restClientMock = new Mock<IHyperliquidRestClient>();
        _signerMock = new Mock<IHyperliquidSigner>();
        _nonceProviderMock = new Mock<INonceProvider>();
        _loggerMock = new Mock<ILogger<HyperliquidOrderService>>();
        _options = Options.Create(new HyperliquidOptions
        {
            BaseUrl = "https://api.hyperliquid-testnet.xyz",
            WsBaseUrl = "wss://api.hyperliquid-testnet.xyz/ws",
            Network = "testnet"
        });

        _signerMock.Setup(s => s.WalletAddress).Returns("0xTestAddress");
        _signerMock.Setup(s => s.SignTypedData(It.IsAny<Nethereum.ABI.EIP712.TypedData<Infrastructure.Hyperliquid.PhantomAgentDomain>>()))
            .Returns(("0x" + new string('a', 64), "0x" + new string('b', 64), 27));
        _nonceProviderMock.Setup(n => n.GetNextNonce()).Returns(1716499200000L);

        _sut = new HyperliquidOrderService(
            _restClientMock.Object,
            _signerMock.Object,
            _nonceProviderMock.Object,
            _options,
            _loggerMock.Object);
    }

    [TestMethod]
    public async Task GivenValidLimitOrder_WhenPlaceOrderAsync_ThenReturnsSuccessWithOrderId()
    {
        // Arrange
        var request = new PlaceOrderRequest
        {
            Asset = "BTC-PERP", Side = "buy", OrderType = "limit",
            Price = 65000m, Size = 0.001m
        };

        _restClientMock
            .Setup(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HyperliquidExchangeResponse
            {
                Status = "ok",
                Response = new HyperliquidExchangeResponseData
                {
                    Type = "order",
                    Data = new HyperliquidOrderResponseData
                    {
                        Statuses = new List<HyperliquidOrderStatus>
                        {
                            new() { Resting = new HyperliquidRestingOrder { Oid = 12345 } }
                        }
                    }
                }
            });

        // Act
        var result = await _sut.PlaceOrderAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.OrderId.Should().Be("12345");
        result.Status.Should().Be("open");
    }

    [TestMethod]
    public async Task GivenValidMarketOrder_WhenPlaceOrderAsync_ThenSignsAndSubmitsWithExtremePrice()
    {
        // Arrange
        var request = new PlaceOrderRequest
        {
            Asset = "BTC-PERP", Side = "buy", OrderType = "market", Size = 0.001m
        };

        _restClientMock
            .Setup(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HyperliquidExchangeResponse
            {
                Status = "ok",
                Response = new HyperliquidExchangeResponseData
                {
                    Type = "order",
                    Data = new HyperliquidOrderResponseData
                    {
                        Statuses = new List<HyperliquidOrderStatus>
                        {
                            new() { Filled = new HyperliquidFilledOrder { Oid = 99, TotalSz = "0.001", AvgPx = "65100.0" } }
                        }
                    }
                }
            });

        // Act
        var result = await _sut.PlaceOrderAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Status.Should().Be("filled");
        _signerMock.Verify(s => s.SignTypedData(It.IsAny<Nethereum.ABI.EIP712.TypedData<Infrastructure.Hyperliquid.PhantomAgentDomain>>()), Times.Once);
    }

    [TestMethod]
    public async Task GivenExchangeReturnsError_WhenPlaceOrderAsync_ThenReturnsErrorDetail()
    {
        var request = new PlaceOrderRequest
        {
            Asset = "BTC-PERP", Side = "buy", OrderType = "limit",
            Price = 65000m, Size = 0.001m
        };

        _restClientMock
            .Setup(r => r.PostExchangeAsync<HyperliquidExchangeResponse>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HyperliquidExchangeResponse
            {
                Status = "ok",
                Response = new HyperliquidExchangeResponseData
                {
                    Type = "order",
                    Data = new HyperliquidOrderResponseData
                    {
                        Statuses = new List<HyperliquidOrderStatus>
                        {
                            new() { Error = "Insufficient margin" }
                        }
                    }
                }
            });

        var result = await _sut.PlaceOrderAsync(request);

        result.Success.Should().BeFalse();
        result.Status.Should().Be("rejected");
        result.Detail.Should().Be("Insufficient margin");
    }

    [TestMethod]
    public async Task GivenTestSign_WhenTestSignAsync_ThenReturnsSignatureWithoutExchangeCall()
    {
        var result = await _sut.TestSignAsync();

        result.Signature.Should().NotBeNull();
        result.Signature.V.Should().BeOneOf(27, 28);
        result.Signature.R.Should().StartWith("0x");
        result.Signature.S.Should().StartWith("0x");
        result.MessageHash.Should().StartWith("0x");

        _restClientMock.Verify(
            r => r.PostExchangeAsync<It.IsAnyType>(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GivenLimitOrderWithoutPrice_WhenPlaceOrderAsync_ThenThrowsDomainException()
    {
        var request = new PlaceOrderRequest
        {
            Asset = "BTC-PERP", Side = "buy", OrderType = "limit",
            Price = null, Size = 0.001m
        };

        var act = () => _sut.PlaceOrderAsync(request);

        await act.Should().ThrowAsync<Application.Abstractions.Exceptions.DomainException>()
            .WithMessage("*Price*required*limit*");
    }
}
```

##### Pattern References

- `tests/TradingApp.Api.Tests/Services/MarketDataStreamServiceTests.cs` — Service unit test pattern with multiple mocks

---

### Task 2.7: Integration tests for OrdersController {#task-27-integration-tests-for-orderscontroller}

Write integration tests for `OrdersController` using `WebApplicationFactory<Program>`.

- **Complexity**: Medium
- **Risk Factors**: Must mock `IHyperliquidOrderService` in DI; test infrastructure must include all required settings
- **Files**:
  - `tests/TradingApp.Api.Tests/Controllers/OrdersControllerTests.cs` — New file
- **Success**:
  - POST /api/orders returns 200 with PlaceOrderResponse
  - POST /api/orders with invalid body returns 400
  - POST /api/orders/test-sign returns 200 with TestSignResponse
  - Error scenarios return appropriate HTTP status codes
- **Dependencies**: Task 2.4 (controller), Task 2.5 (DI registration)

#### Implementation Details

```csharp
// tests/TradingApp.Api.Tests/Controllers/OrdersControllerTests.cs — new file
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TradingApp.Api.Models;
using TradingApp.Api.Services;
using TradingApp.Api.Tests.Infrastructure;

namespace TradingApp.Api.Tests.Controllers;

[TestClass]
public sealed class OrdersControllerTests : BaseControllerTests
{
    private const string TestPrivateKey = "0x4c0883a69102937d6231471b5dbb6204fe512961708279f2a4c5890a0c1f9b2e";

    private HttpClient _client = default!;
    private Mock<IHyperliquidOrderService> _orderServiceMock = default!;

    [TestInitialize]
    public void Setup()
    {
        _orderServiceMock = new Mock<IHyperliquidOrderService>();
        _client = GetTestClient();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Hyperliquid:PrivateKey", TestPrivateKey);
        builder.UseSetting("Hyperliquid:WsBaseUrl", "wss://test.example.com/ws");
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<IHyperliquidOrderService>();
        services.AddSingleton(_orderServiceMock.Object);
    }

    [TestMethod]
    public async Task GivenValidLimitOrder_WhenPostOrders_ThenReturns200WithResponse()
    {
        _orderServiceMock
            .Setup(s => s.PlaceOrderAsync(It.IsAny<PlaceOrderRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaceOrderResponse { Success = true, OrderId = "12345", Status = "open" });

        var request = new PlaceOrderRequest
        {
            Asset = "BTC-PERP", Side = "buy", OrderType = "limit",
            Price = 65000m, Size = 0.001m
        };

        var response = await _client.PostAsJsonAsync("api/orders", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PlaceOrderResponse>();
        result!.Success.Should().BeTrue();
        result.OrderId.Should().Be("12345");
    }

    [TestMethod]
    public async Task GivenEmptyBody_WhenPostOrders_ThenReturns400()
    {
        var response = await _client.PostAsJsonAsync<object?>("api/orders", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task GivenTestSign_WhenPostTestSign_ThenReturns200WithSignature()
    {
        _orderServiceMock
            .Setup(s => s.TestSignAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestSignResponse
            {
                DomainSeparator = "0xabc",
                TypeHash = "0xdef",
                MessageHash = "0x123",
                Signature = new SignatureDto { V = 27, R = "0xr", S = "0xs" }
            });

        var response = await _client.PostAsync("api/orders/test-sign", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TestSignResponse>();
        result!.Signature.V.Should().Be(27);
    }
}
```

##### Pattern References

- `tests/TradingApp.Api.Tests/Controllers/AccountControllerTests.cs` — WebApplicationFactory setup with mock replacement
- `tests/TradingApp.Api.Tests/Infrastructure/BaseControllerTests.cs` — Test utility methods

---

### Task 2.8: Run all tests {#task-28-run-all-tests}

Run the complete test suite to verify all Phase 2 changes work correctly and no regressions exist.

- **Complexity**: Low
- **Risk Factors**: New `PostExchangeAsync` method on `IHyperliquidRestClient` interface might require mock updates in tests that create the mock (if Moq strict mode is used)
- **Files**: None (verification only)
- **Success**:
  - All Phase 2 tests pass
  - All Phase 1 tests still pass
  - All pre-existing tests pass
  - Solution builds without errors
- **Dependencies**: All previous Phase 2 tasks

Run:
```bash
dotnet test TradingApp.sln
```

---

## Phase Success Criteria

- `POST /api/orders` endpoint accepts order requests and returns `PlaceOrderResponse`
- `POST /api/orders/test-sign` endpoint returns EIP-712 signing diagnostics
- `HyperliquidOrderService` orchestrates signing, submission, and latency logging
- `PostExchangeAsync` method on REST client handles `/exchange` endpoint
- All new unit and integration tests pass
- All existing tests pass (no regressions)
- Solution builds without errors
