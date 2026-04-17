<!-- markdownlint-disable-file -->

# Task Details: F6 — Order Management

## Phase 1: Backend — Cancel & Modify Endpoints + Tests

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — Sealed classes, `_camelCase` private fields, async methods suffixed with `Async`, `CancellationToken` on all async methods, one class per file
- `.github/instructions/api-controllers.instructions.md` — `[ProducesResponseType]` per endpoint, `Envelope` for error responses, DELETE returns 204, PUT returns 204
- `.github/instructions/testing.instructions.md` — MSTest, Moq, FluentAssertions v6, `Given_When_Then` naming, `BaseControllerTests` pattern, tests within the phase
- `.github/instructions/dotnet-architecture.instructions.md` — Interfaces in Application layer, implementations in Infrastructure or Api/Services layer
- `.agent-context/0-knowledge/02-hyperliquid-integration.md` — Cancel/modify action payloads, `/exchange` endpoint, EIP-712 signing
- `.agent-context/0-knowledge/10-architecture-decisions.md` — ADR 14: bypass MediatR for simple exchange operations

## Design References

- Hyperliquid cancel action: `{ "type": "cancel", "cancels": [{ "a": assetIndex, "o": orderId }] }`
- Hyperliquid modify action: `{ "type": "batchModifyOrders", "modifies": [{ "oid": orderId, "order": { "a": assetIndex, "b": isBuy, "p": price, "s": size, "r": false, "t": { "limit": { "tif": "Gtc" } } } }] }`
- Both actions use the same EIP-712 signing flow as placement: construct action → compute connectionId via msgpack hash → sign Agent typed data → POST to `/exchange`
- F5 provides `IHyperliquidSigner.SignAsync(object action, long nonce)` and `IHyperliquidRestClient.PostExchangeAsync<T>(object request)`

### Task 1.1: Create cancel and modify action payload models {#task-11-create-action-payload-models}

Create the C# models that represent the Hyperliquid cancel and modify action payloads for serialization to JSON when posting to the `/exchange` endpoint.

- **Complexity**: Medium
- **Risk Factors**: JSON serialization must match Hyperliquid's expected field names exactly (lowercase `a`, `o`, `b`, `p`, `s`, `r`, `t`)
- **Files**:
  - `src/TradePilot.Infrastructure/Hyperliquid/Models/HyperliquidCancelAction.cs` — new file
  - `src/TradePilot.Infrastructure/Hyperliquid/Models/HyperliquidModifyAction.cs` — new file
- **Success**:
  - Models serialize to JSON matching Hyperliquid's expected payload format
  - Cancel action wraps one or more `{ a, o }` pairs
  - Modify action wraps `{ oid, order: { a, b, p, s, r, t } }` structure
- **Dependencies**:
  - F5 must have established the Infrastructure Hyperliquid models pattern

#### Implementation Details

```csharp
// src/TradePilot.Infrastructure/Hyperliquid/Models/HyperliquidCancelAction.cs — new file
using System.Text.Json.Serialization;

namespace TradePilot.Infrastructure.Hyperliquid.Models;

public sealed class HyperliquidCancelAction
{
    [JsonPropertyName("type")]
    public string Type { get; } = "cancel";

    [JsonPropertyName("cancels")]
    public List<HyperliquidCancelEntry> Cancels { get; set; } = [];
}

public sealed class HyperliquidCancelEntry
{
    [JsonPropertyName("a")]
    public int AssetIndex { get; set; }

    [JsonPropertyName("o")]
    public long OrderId { get; set; }
}
```

```csharp
// src/TradePilot.Infrastructure/Hyperliquid/Models/HyperliquidModifyAction.cs — new file
using System.Text.Json.Serialization;

namespace TradePilot.Infrastructure.Hyperliquid.Models;

public sealed class HyperliquidModifyAction
{
    [JsonPropertyName("type")]
    public string Type { get; } = "batchModifyOrders";

    [JsonPropertyName("modifies")]
    public List<HyperliquidModifyEntry> Modifies { get; set; } = [];
}

public sealed class HyperliquidModifyEntry
{
    [JsonPropertyName("oid")]
    public long OrderId { get; set; }

    [JsonPropertyName("order")]
    public HyperliquidModifyOrderParams Order { get; set; } = new();
}

public sealed class HyperliquidModifyOrderParams
{
    [JsonPropertyName("a")]
    public int AssetIndex { get; set; }

    [JsonPropertyName("b")]
    public bool IsBuy { get; set; }

    [JsonPropertyName("p")]
    public string Price { get; set; } = string.Empty;

    [JsonPropertyName("s")]
    public string Size { get; set; } = string.Empty;

    [JsonPropertyName("r")]
    public bool ReduceOnly { get; set; } = false;

    [JsonPropertyName("t")]
    public HyperliquidOrderType OrderType { get; set; } = new();
}

// Note: Reuse HyperliquidOrderType and HyperliquidLimitParams from F5's placement models.
```

##### Pattern References

- `src/TradePilot.Infrastructure/Hyperliquid/Models/` — existing Hyperliquid model files from F3/F5
- `src/TradePilot.Api/Models/OpenOrderDto.cs` — sealed class DTO pattern with `JsonPropertyName`

---

### Task 1.2: Create ModifyOrderDto request model with validation {#task-12-create-modifyorderdto-request-model}

Create the API request body model for the PUT `/api/orders/{orderId}` endpoint with data annotation validation.

- **Complexity**: Low
- **Risk Factors**: None — straightforward DTO with validation attributes
- **Files**:
  - `src/TradePilot.Api/Models/ModifyOrderDto.cs` — new file
- **Success**:
  - Model has `Price` and `Size` decimal properties
  - Validation attributes enforce price > 0 and size > 0
  - Model follows existing DTO pattern (sealed class)
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradePilot.Api/Models/ModifyOrderDto.cs — new file
using System.ComponentModel.DataAnnotations;

namespace TradePilot.Api.Models;

public sealed class ModifyOrderDto
{
    [Range(0.000001, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
    public decimal Price { get; set; }

    [Range(0.000001, double.MaxValue, ErrorMessage = "Size must be greater than 0")]
    public decimal Size { get; set; }
}
```

##### Pattern References

- `src/TradePilot.Api/Models/OpenOrderDto.cs` — existing sealed DTO pattern

---

### Task 1.3: Add CancelOrderAsync and CancelAllOrdersAsync to IHyperliquidOrderService {#task-13-add-cancel-methods-to-order-service}

Extend the existing `IHyperliquidOrderService` interface and `HyperliquidOrderService` implementation with cancel single and cancel all methods. These construct the cancel action payload and use the existing signing + exchange flow from F5.

- **Complexity**: Medium
- **Risk Factors**: Order ID parsing (string → long), asset index resolution (hard-coded 0 for POC)
- **Files**:
  - `src/TradePilot.Api/Services/IHyperliquidOrderService.cs` — modification (add methods)
  - `src/TradePilot.Api/Services/HyperliquidOrderService.cs` — modification (implement methods)
- **Success**:
  - `CancelOrderAsync(string orderId, string asset, CancellationToken)` constructs cancel action and submits to `/exchange`
  - `CancelAllOrdersAsync(string asset, CancellationToken)` fetches open orders for asset, constructs cancel action with all order IDs, submits to `/exchange`
  - Both methods reuse F5's signing pipeline
- **Dependencies**:
  - Task 1.1 (cancel action payload models)
  - F5's `IHyperliquidSigner.SignAsync` and `IHyperliquidRestClient.PostExchangeAsync`

#### Implementation Details

> **Note**: `CancelAllOrdersAsync` requires `IHyperliquidAccountService` to fetch open orders. This must be added as a new constructor dependency to `HyperliquidOrderService` (not present in F5's version).

```csharp
// src/TradePilot.Api/Services/IHyperliquidOrderService.cs — modification
public interface IHyperliquidOrderService
{
    // ... existing PlaceOrderAsync from F5 ...
    Task CancelOrderAsync(string orderId, string asset, CancellationToken cancellationToken = default);
    Task CancelAllOrdersAsync(string asset, CancellationToken cancellationToken = default);
}
```

```csharp
// src/TradePilot.Api/Services/HyperliquidOrderService.cs — modification
// Add to existing class:

public async Task CancelOrderAsync(string orderId, string asset, CancellationToken cancellationToken)
{
    var orderIdLong = long.Parse(orderId);
    var assetIndex = 0; // BTC hard-coded for POC

    var action = new HyperliquidCancelAction
    {
        Cancels = [new HyperliquidCancelEntry { AssetIndex = assetIndex, OrderId = orderIdLong }]
    };

    var nonce = _nonceProvider.GetNonce();
    var signature = await _signer.SignAsync(action, nonce, cancellationToken);
    var request = new HyperliquidExchangeRequest(action, nonce, signature);
    await _restClient.PostExchangeAsync<HyperliquidExchangeResponse>(request, cancellationToken);

    _logger.LogInformation("Cancelled order {OrderId} for asset {Asset}", orderId, asset);
}

public async Task CancelAllOrdersAsync(string asset, CancellationToken cancellationToken)
{
    // Fetch current open orders to get all order IDs
    var openOrders = await _accountService.GetOpenOrdersAsync(cancellationToken);
    var ordersForAsset = openOrders.Where(o => o.Asset.Equals(asset, StringComparison.OrdinalIgnoreCase)).ToList();

    if (ordersForAsset.Count == 0)
    {
        _logger.LogInformation("No open orders to cancel for asset {Asset}", asset);
        return;
    }

    var assetIndex = 0; // BTC hard-coded for POC

    var action = new HyperliquidCancelAction
    {
        Cancels = ordersForAsset.Select(o => new HyperliquidCancelEntry
        {
            AssetIndex = assetIndex,
            OrderId = long.Parse(o.OrderId)
        }).ToList()
    };

    var nonce = _nonceProvider.GetNonce();
    var signature = await _signer.SignAsync(action, nonce, cancellationToken);
    var request = new HyperliquidExchangeRequest(action, nonce, signature);
    await _restClient.PostExchangeAsync<HyperliquidExchangeResponse>(request, cancellationToken);

    _logger.LogInformation("Cancelled {Count} orders for asset {Asset}", ordersForAsset.Count, asset);
}
```

##### Pattern References

- `src/TradePilot.Api/Services/HyperliquidAccountService.cs` — service pattern with `IHyperliquidRestClient` + `IHyperliquidSigner` injection
- F5's `PlaceOrderAsync` in `HyperliquidOrderService` — sign → exchange flow template

---

### Task 1.4: Add ModifyOrderAsync to IHyperliquidOrderService {#task-14-add-modify-method-to-order-service}

Add the modify order method that constructs the `batchModifyOrders` action payload and submits it through the signing + exchange flow.

- **Complexity**: Medium
- **Risk Factors**: Price/size must be serialized as strings in the action payload (Hyperliquid expects string representation). Side (buy/sell) must be resolved from the existing order.
- **Files**:
  - `src/TradePilot.Api/Services/IHyperliquidOrderService.cs` — modification (add method)
  - `src/TradePilot.Api/Services/HyperliquidOrderService.cs` — modification (implement method)
- **Success**:
  - `ModifyOrderAsync(string orderId, string asset, string side, decimal price, decimal size, CancellationToken)` constructs modify action and submits to `/exchange`
  - Price and size are serialized as strings in the action payload
  - Reuses F5's signing pipeline
- **Dependencies**:
  - Task 1.1 (modify action payload models)
  - F5's signing and exchange infrastructure

#### Implementation Details

```csharp
// src/TradePilot.Api/Services/IHyperliquidOrderService.cs — modification
public interface IHyperliquidOrderService
{
    // ... existing methods ...
    Task ModifyOrderAsync(string orderId, string asset, string side, decimal price, decimal size, CancellationToken cancellationToken = default);
}
```

```csharp
// src/TradePilot.Api/Services/HyperliquidOrderService.cs — modification

public async Task ModifyOrderAsync(
    string orderId, string asset, string side, decimal price, decimal size,
    CancellationToken cancellationToken)
{
    var orderIdLong = long.Parse(orderId);
    var assetIndex = 0; // BTC hard-coded for POC
    var isBuy = side.Equals("Buy", StringComparison.OrdinalIgnoreCase);

    var action = new HyperliquidModifyAction
    {
        Modifies =
        [
            new HyperliquidModifyEntry
            {
                OrderId = orderIdLong,
                Order = new HyperliquidModifyOrderParams
                {
                    AssetIndex = assetIndex,
                    IsBuy = isBuy,
                    Price = price.ToString("G"),
                    Size = size.ToString("G"),
                    ReduceOnly = false,
                    OrderType = new HyperliquidOrderType
                    {
                        Limit = new HyperliquidLimitParams { Tif = "Gtc" }
                    }
                }
            }
        ]
    };

    var nonce = _nonceProvider.GetNonce();
    var signature = await _signer.SignAsync(action, nonce, cancellationToken);
    var request = new HyperliquidExchangeRequest(action, nonce, signature);
    await _restClient.PostExchangeAsync<HyperliquidExchangeResponse>(request, cancellationToken);

    _logger.LogInformation(
        "Modified order {OrderId} for asset {Asset}: price={Price}, size={Size}",
        orderId, asset, price, size);
}
```

##### Pattern References

- F5's `PlaceOrderAsync` — same signing → exchange pattern
- `src/TradePilot.Infrastructure/Hyperliquid/Models/` — Hyperliquid model serialization patterns

---

### Task 1.5: Add DELETE and PUT endpoints to OrdersController {#task-15-add-controller-endpoints}

Extend the existing `OrdersController` from F5 with three new endpoints for cancel (single), cancel (all for asset), and modify order.

- **Complexity**: Medium
- **Risk Factors**: Route parameter binding for orderId (string), query parameter for asset on cancel-all
- **Files**:
  - `src/TradePilot.Api/Controllers/OrdersController.cs` — modification (add endpoints)
- **Success**:
  - `DELETE /api/orders/{orderId}` returns 204 on success
  - `DELETE /api/orders?asset={asset}` returns 204 on success
  - `PUT /api/orders/{orderId}` with `ModifyOrderDto` body returns 204 on success
  - All endpoints declare `ProducesResponseType` for error responses
  - Global exception filter handles all exceptions
- **Dependencies**:
  - Tasks 1.2–1.4 (service methods and DTOs)
  - F5's `OrdersController` with POST endpoint

#### Implementation Details

```csharp
// src/TradePilot.Api/Controllers/OrdersController.cs — modification
// Add these endpoints to the existing controller:

[HttpDelete("{orderId}")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
public async Task<IActionResult> CancelOrder(string orderId, CancellationToken cancellationToken)
{
    await _orderService.CancelOrderAsync(orderId, "BTC", cancellationToken);
    return NoContent();
}

[HttpDelete]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
public async Task<IActionResult> CancelAllOrders(
    [FromQuery][Required] string asset, CancellationToken cancellationToken)
{
    await _orderService.CancelAllOrdersAsync(asset, cancellationToken);
    return NoContent();
}

[HttpPut("{orderId}")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
public async Task<IActionResult> ModifyOrder(
    string orderId, [FromBody] ModifyOrderDto dto, CancellationToken cancellationToken)
{
    // Resolve side from existing order (needed for modify payload)
    var openOrders = await _accountService.GetOpenOrdersAsync(cancellationToken);
    var existingOrder = openOrders.FirstOrDefault(o => o.OrderId == orderId)
        ?? throw new DomainException($"Order {orderId} not found in open orders");

    await _orderService.ModifyOrderAsync(
        orderId, existingOrder.Asset, existingOrder.Side,
        dto.Price, dto.Size, cancellationToken);

    return NoContent();
}
```

##### Pattern References

- `src/TradePilot.Api/Controllers/AccountController.cs` — direct service injection pattern, try/catch delegated to global filter
- `src/TradePilot.Api/Infrastructure/Filters/HttpGlobalExceptionFilter.cs` — DomainException → 400, HttpRequestException → 503

---

### Task 1.6: Unit tests for HyperliquidOrderService cancel and modify {#task-16-unit-tests-for-order-service}

Create unit tests for the cancel and modify methods in `HyperliquidOrderService`. Mock `IHyperliquidSigner`, `IHyperliquidRestClient`, `IHyperliquidAccountService`, and `INonceProvider`.

- **Complexity**: Medium
- **Risk Factors**: Verifying correct action payload construction and signing flow
- **Files**:
  - `tests/TradePilot.Api.Tests/Services/HyperliquidOrderServiceTests.cs` — new file or extend existing from F5
- **Success**:
  - Tests verify cancel single constructs correct action payload
  - Tests verify cancel all fetches orders and constructs batch cancel
  - Tests verify modify constructs correct action payload with price/size as strings
  - Tests verify signing and exchange submission
  - Tests verify error cases (invalid order ID, no orders to cancel)
- **Dependencies**:
  - Tasks 1.1–1.4

#### Implementation Details

```csharp
// tests/TradePilot.Api.Tests/Services/HyperliquidOrderServiceTests.cs — new methods
// (Extend existing test class from F5, or add to it)

[TestMethod]
public async Task GivenValidOrderId_WhenCancelOrder_ThenSignsAndSubmitsCancelAction()
{
    // Arrange
    _signerMock.Setup(s => s.SignAsync(It.IsAny<object>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new HyperliquidSignature("0x1", "0x2", 27));
    _restClientMock.Setup(c => c.PostExchangeAsync<HyperliquidExchangeResponse>(
        It.IsAny<object>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new HyperliquidExchangeResponse { Status = "ok" });

    // Act
    await _service.CancelOrderAsync("12345", "BTC", CancellationToken.None);

    // Assert
    _signerMock.Verify(s => s.SignAsync(
        It.Is<HyperliquidCancelAction>(a =>
            a.Cancels.Count == 1 &&
            a.Cancels[0].OrderId == 12345 &&
            a.Cancels[0].AssetIndex == 0),
        It.IsAny<long>(),
        It.IsAny<CancellationToken>()), Times.Once);
    _restClientMock.Verify(c => c.PostExchangeAsync<HyperliquidExchangeResponse>(
        It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
}

[TestMethod]
public async Task GivenOpenOrders_WhenCancelAllOrders_ThenCancelsAllMatchingOrders()
{
    // Arrange
    var orders = new List<OpenOrderDto>
    {
        new() { OrderId = "111", Asset = "BTC", Side = "Buy", Price = 60000m, Size = 0.01m },
        new() { OrderId = "222", Asset = "BTC", Side = "Sell", Price = 70000m, Size = 0.02m },
        new() { OrderId = "333", Asset = "ETH", Side = "Buy", Price = 3000m, Size = 1m }
    };
    _accountServiceMock.Setup(s => s.GetOpenOrdersAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(orders);
    _signerMock.Setup(s => s.SignAsync(It.IsAny<object>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new HyperliquidSignature("0x1", "0x2", 27));
    _restClientMock.Setup(c => c.PostExchangeAsync<HyperliquidExchangeResponse>(
        It.IsAny<object>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new HyperliquidExchangeResponse { Status = "ok" });

    // Act
    await _service.CancelAllOrdersAsync("BTC", CancellationToken.None);

    // Assert
    _signerMock.Verify(s => s.SignAsync(
        It.Is<HyperliquidCancelAction>(a => a.Cancels.Count == 2),
        It.IsAny<long>(),
        It.IsAny<CancellationToken>()), Times.Once);
}

[TestMethod]
public async Task GivenNoOpenOrders_WhenCancelAllOrders_ThenDoesNotSubmitRequest()
{
    // Arrange
    _accountServiceMock.Setup(s => s.GetOpenOrdersAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(new List<OpenOrderDto>());

    // Act
    await _service.CancelAllOrdersAsync("BTC", CancellationToken.None);

    // Assert
    _restClientMock.Verify(c => c.PostExchangeAsync<HyperliquidExchangeResponse>(
        It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
}

[TestMethod]
public async Task GivenValidParameters_WhenModifyOrder_ThenSignsAndSubmitsModifyAction()
{
    // Arrange
    _signerMock.Setup(s => s.SignAsync(It.IsAny<object>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new HyperliquidSignature("0x1", "0x2", 27));
    _restClientMock.Setup(c => c.PostExchangeAsync<HyperliquidExchangeResponse>(
        It.IsAny<object>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new HyperliquidExchangeResponse { Status = "ok" });

    // Act
    await _service.ModifyOrderAsync("12345", "BTC", "Buy", 64500m, 0.002m, CancellationToken.None);

    // Assert
    _signerMock.Verify(s => s.SignAsync(
        It.Is<HyperliquidModifyAction>(a =>
            a.Modifies.Count == 1 &&
            a.Modifies[0].OrderId == 12345 &&
            a.Modifies[0].Order.IsBuy == true &&
            a.Modifies[0].Order.Price == "64500" &&
            a.Modifies[0].Order.Size == "0.002"),
        It.IsAny<long>(),
        It.IsAny<CancellationToken>()), Times.Once);
}
```

##### Pattern References

- `tests/TradePilot.Api.Tests/Controllers/AccountControllerTests.cs` — Moq setup and verify patterns
- `tests/TradePilot.Infrastructure.Tests/Services/HyperliquidSignerTests.cs` — unit test structure

---

### Task 1.7: Integration tests for OrdersController cancel and modify endpoints {#task-17-integration-tests-for-controller}

Create integration tests using `WebApplicationFactory` for the three new endpoints. Mock `IHyperliquidOrderService` and `IHyperliquidAccountService` to isolate controller behaviour.

- **Complexity**: Medium
- **Risk Factors**: DELETE with query parameters, PUT with request body, handling 204 No Content assertions
- **Files**:
  - `tests/TradePilot.Api.Tests/Controllers/OrdersControllerTests.cs` — extend existing from F5 or create
- **Success**:
  - Tests cover: cancel single (204), cancel all (204), modify (204)
  - Tests cover: cancel with invalid order ID, modify with invalid body (400)
  - Tests cover: service unavailable (503)
  - All tests use `GivenOrdersController_When{Action}_Then{Result}` naming
- **Dependencies**:
  - Tasks 1.2–1.5 (endpoints and models)

#### Implementation Details

```csharp
// tests/TradePilot.Api.Tests/Controllers/OrdersControllerTests.cs — new test methods
// (Extend existing test class from F5)

[TestMethod]
public async Task GivenOpenOrder_WhenCancelOrder_ThenReturnsNoContent()
{
    // Arrange
    _orderServiceMock.Setup(s => s.CancelOrderAsync(
        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);

    // Act
    var response = await _client.DeleteAsync($"{BASE_URL}/12345");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    _orderServiceMock.Verify(s => s.CancelOrderAsync("12345", "BTC", It.IsAny<CancellationToken>()), Times.Once);
}

[TestMethod]
public async Task GivenOpenOrders_WhenCancelAllOrders_ThenReturnsNoContent()
{
    _orderServiceMock.Setup(s => s.CancelAllOrdersAsync(
        It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);

    var response = await _client.DeleteAsync($"{BASE_URL}?asset=BTC");

    response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    _orderServiceMock.Verify(s => s.CancelAllOrdersAsync("BTC", It.IsAny<CancellationToken>()), Times.Once);
}

[TestMethod]
public async Task GivenValidModifyRequest_WhenModifyOrder_ThenReturnsNoContent()
{
    var openOrders = new List<OpenOrderDto>
    {
        new() { OrderId = "12345", Asset = "BTC", Side = "Buy", Price = 60000m, Size = 0.01m }
    };
    _accountServiceMock.Setup(s => s.GetOpenOrdersAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(openOrders);
    _orderServiceMock.Setup(s => s.ModifyOrderAsync(
        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
        It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);

    var dto = new { price = 64500m, size = 0.002m };
    var response = await _client.PutAsJsonAsync($"{BASE_URL}/12345", dto);

    response.StatusCode.Should().Be(HttpStatusCode.NoContent);
}

[TestMethod]
public async Task GivenInvalidModifyRequest_WhenModifyOrder_ThenReturnsBadRequest()
{
    var dto = new { price = -1m, size = 0m };
    var response = await _client.PutAsJsonAsync($"{BASE_URL}/12345", dto);

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}

[TestMethod]
public async Task GivenHyperliquidUnavailable_WhenCancelOrder_ThenReturnsServiceUnavailable()
{
    _orderServiceMock.Setup(s => s.CancelOrderAsync(
        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new HttpRequestException("Connection refused"));

    var response = await _client.DeleteAsync($"{BASE_URL}/12345");

    response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
}

[TestMethod]
public async Task GivenOrderNotFoundForModify_WhenModifyOrder_ThenReturnsBadRequest()
{
    _accountServiceMock.Setup(s => s.GetOpenOrdersAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(new List<OpenOrderDto>());

    var dto = new { price = 64500m, size = 0.002m };
    var response = await _client.PutAsJsonAsync($"{BASE_URL}/99999", dto);

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}
```

##### Pattern References

- `tests/TradePilot.Api.Tests/Controllers/AccountControllerTests.cs` — `WebApplicationFactory`, mock setup/verify, HTTP status assertions
- `tests/TradePilot.Api.Tests/Infrastructure/BaseControllerTests.cs` — test base class pattern

---

### Task 1.8: Run all tests to verify no regressions {#task-18-run-all-tests}

Run the complete test suite to ensure no regressions from the changes.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification only)
- **Success**:
  - All new tests pass
  - All existing tests continue to pass
  - `dotnet test` exits with code 0
- **Dependencies**:
  - Tasks 1.6–1.7

## Phase Success Criteria

- `DELETE /api/orders/{orderId}` signs and submits cancel action to Hyperliquid `/exchange`
- `DELETE /api/orders?asset=BTC` cancels all BTC orders in a single signed request
- `PUT /api/orders/{orderId}` signs and submits modify action with new price/size
- Backend validation rejects modify requests with price ≤ 0 or size ≤ 0
- All new unit and integration tests pass
- All existing tests continue to pass (no regressions)
