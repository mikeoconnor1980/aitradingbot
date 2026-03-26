<!-- markdownlint-disable-file -->

# Task Details: F9 — Position Actions

## Phase 1: Backend — Trigger Order Support & Position Enrichment

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — `sealed` classes, `Async` suffix, `CancellationToken` propagation, constructor injection
- `.github/instructions/api-controllers.instructions.md` — POST returns 201 + `CreatedResultEnvelope`, error responses via `Envelope`
- `.github/instructions/testing.instructions.md` — MSTest + Moq + FluentAssertions ≤ v6, `Given_When_Then` naming, handler tests via controller tests only
- `.github/instructions/dotnet-architecture.instructions.md` — Layer boundaries, exception-to-HTTP mapping
- `.agent-context/0-knowledge/02-hyperliquid-integration.md` — REST client, EIP-712, exchange action wire format
- `.agent-context/0-knowledge/10-architecture-decisions.md` — ADR-14: direct service injection for POC exchange reads/writes

## Design References

- Hyperliquid Python SDK `signing.py` — trigger order wire format: `{ "trigger": { "triggerPx": str, "isMarket": bool, "tpsl": "tp"|"sl" } }`
- Hyperliquid Python SDK — grouping: `"na"` for regular orders, `"normalTpsl"` for TP/SL orders
- Hyperliquid Python SDK — `limit_px` for market trigger orders should be set to a sentinel (Python SDK uses `0.0` for market triggers)

---

### Task 1.1: Extend PlaceOrderRequest with trigger order fields {#task-11-extend-placeorderrequest-with-trigger-order-fields}

Extend the `PlaceOrderRequest` model to support trigger order types (stop-market), the `reduceOnly` flag, and the TP/SL direction indicator.

- **Complexity**: Medium
- **Risk Factors**: Regex change must not break existing market/limit orders; new fields must be optional for backward compatibility
- **Files**:
  - `src/TradingApp.Api/Models/PlaceOrderRequest.cs` — Add `TriggerPrice`, `ReduceOnly`, `TpSlType` fields; update `OrderType` regex
- **Success**:
  - `PlaceOrderRequest` accepts `"stop-market"` as a valid `OrderType`
  - `TriggerPrice`, `ReduceOnly`, `TpSlType` are optional properties with correct validation
  - Existing `market` and `limit` orders continue to work unchanged
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Api/Models/PlaceOrderRequest.cs — modification
using System.ComponentModel.DataAnnotations;

namespace TradingApp.Api.Models;

public sealed class PlaceOrderRequest
{
    [Required]
    public string Asset { get; set; } = default!;

    [Required]
    [RegularExpression("^(buy|sell)$", ErrorMessage = "Side must be 'buy' or 'sell'.")]
    public string Side { get; set; } = default!;

    [Required]
    [RegularExpression("^(market|limit|stop-market)$", ErrorMessage = "OrderType must be 'market', 'limit', or 'stop-market'.")]
    public string OrderType { get; set; } = default!;

    public decimal? Price { get; set; }

    [Required]
    [Range(0.000001, double.MaxValue)]
    public decimal Size { get; set; }

    /// <summary>
    /// Trigger price for stop-market orders (TP/SL).
    /// Required when OrderType is 'stop-market'.
    /// </summary>
    public decimal? TriggerPrice { get; set; }

    /// <summary>
    /// When true, the order can only reduce an existing position, not open a new one.
    /// Should be true for TP/SL and partial close orders.
    /// </summary>
    public bool ReduceOnly { get; set; }

    /// <summary>
    /// TP/SL direction: "tp" for take-profit, "sl" for stop-loss.
    /// Required when OrderType is 'stop-market'.
    /// </summary>
    [RegularExpression("^(tp|sl)$", ErrorMessage = "TpSlType must be 'tp' or 'sl'.")]
    public string? TpSlType { get; set; }
}
```

##### Pattern References

- `src/TradingApp.Api/Models/PlaceOrderRequest.cs` — existing model with `RegularExpression` validation pattern

---

### Task 1.2: Add HyperliquidTriggerParams infrastructure model {#task-12-add-hyperliquidtriggerparams-infrastructure-model}

Create a new model class for the Hyperliquid trigger order type wire format, alongside the existing `HyperliquidLimitParams`.

- **Complexity**: Low
- **Risk Factors**: None — additive change
- **Files**:
  - `src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidModifyAction.cs` — Add `HyperliquidTriggerParams` class (co-located with existing `HyperliquidLimitParams`)
- **Success**:
  - `HyperliquidTriggerParams` has `TriggerPx` (string), `IsMarket` (bool), `Tpsl` (string) properties
  - Properties match the Hyperliquid wire format naming
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidModifyAction.cs — add new class at end of file

/// <summary>
/// Wire format for Hyperliquid trigger order type (TP/SL).
/// Maps to: { "trigger": { "triggerPx": "69000.0", "isMarket": true, "tpsl": "sl" } }
/// </summary>
public sealed class HyperliquidTriggerParams
{
    [JsonPropertyName("triggerPx")]
    public string TriggerPx { get; set; } = string.Empty;

    [JsonPropertyName("isMarket")]
    public bool IsMarket { get; set; }

    [JsonPropertyName("tpsl")]
    public string Tpsl { get; set; } = string.Empty;
}
```

##### Pattern References

- `src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidModifyAction.cs` — existing `HyperliquidLimitParams` and `HyperliquidOrderType` classes

---

### Task 1.3: Extend BuildOrderAction with trigger order type branch {#task-13-extend-buildorderaction-with-trigger-order-type-branch}

Add a new overload or extend the existing `BuildOrderAction` method in `HyperliquidEip712` to support trigger order wire format. Trigger orders use `{ "trigger": { "triggerPx", "isMarket", "tpsl" } }` instead of `{ "limit": { "tif" } }`, and use `grouping: "normalTpsl"`.

- **Complexity**: High
- **Risk Factors**: Wire format must match Hyperliquid API exactly; MessagePack serialization must produce byte-compatible output; incorrect `limit_px` for market triggers could cause order rejection
- **Files**:
  - `src/TradingApp.Infrastructure/Hyperliquid/HyperliquidEip712.cs` — Add `BuildTriggerOrderAction` method
- **Success**:
  - `BuildTriggerOrderAction` produces the correct trigger order wire format dictionary
  - `grouping` is set to `"normalTpsl"` for trigger orders
  - `limit_px` (the `p` field) is set correctly: for stop-market, use a sentinel value (see implementation notes)
  - `reduceOnly` is always `true` for trigger orders
  - Existing `BuildOrderAction` is unchanged (non-breaking)
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Infrastructure/Hyperliquid/HyperliquidEip712.cs — add new method after existing BuildOrderAction

/// <summary>
/// Builds a trigger order action payload for TP/SL orders.
/// Trigger orders use { "trigger": { "triggerPx", "isMarket", "tpsl" } } wire format
/// with grouping "normalTpsl".
/// </summary>
/// <param name="assetIndex">Hyperliquid asset index</param>
/// <param name="isBuy">True = buy (close short), False = sell (close long)</param>
/// <param name="limitPrice">For limit triggers: the limit price. For market triggers: a sentinel price
/// that ensures fill (e.g., triggerPrice * 0.8 for sells, triggerPrice * 1.2 for buys).</param>
/// <param name="size">Order size</param>
/// <param name="triggerPrice">Price at which the trigger activates</param>
/// <param name="isMarketTrigger">True = stop-market (fills at market on trigger), False = stop-limit</param>
/// <param name="tpsl">"tp" for take-profit, "sl" for stop-loss</param>
public static Dictionary<string, object> BuildTriggerOrderAction(
    int assetIndex,
    bool isBuy,
    decimal limitPrice,
    decimal size,
    decimal triggerPrice,
    bool isMarketTrigger,
    string tpsl)
{
    return new Dictionary<string, object>
    {
        ["type"] = "order",
        ["orders"] = new[]
        {
            new Dictionary<string, object>
            {
                ["a"] = assetIndex,
                ["b"] = isBuy,
                ["p"] = ToWireDecimal(limitPrice),
                ["s"] = ToWireDecimal(size),
                ["r"] = true, // TP/SL orders are always reduce-only
                ["t"] = new Dictionary<string, object>
                {
                    ["trigger"] = new Dictionary<string, object>
                    {
                        ["triggerPx"] = ToWireDecimal(triggerPrice),
                        ["isMarket"] = isMarketTrigger,
                        ["tpsl"] = tpsl,
                    }
                }
            }
        },
        ["grouping"] = "normalTpsl"
    };
}
```

**Implementation notes on `limitPrice` for market triggers:**
- For stop-market buys (closing a short): set `limitPrice` to `triggerPrice * 1.2` (20% above trigger to ensure fill)
- For stop-market sells (closing a long): set `limitPrice` to `triggerPrice * 0.8` (20% below trigger to ensure fill)
- The Hyperliquid exchange uses the trigger price to activate, then fills at the limit price — a wide limit ensures market-like execution

##### Pattern References

- `src/TradingApp.Infrastructure/Hyperliquid/HyperliquidEip712.cs` — existing `BuildOrderAction` method (lines 95–118)

---

### Task 1.4: Extend HyperliquidOrderService for trigger orders {#task-14-extend-hyperliquidorderservice-for-trigger-orders}

Extend `PlaceOrderAsync` in `HyperliquidOrderService` to detect `stop-market` order type and route to `BuildTriggerOrderAction`. Also pass `reduceOnly` to `BuildOrderAction` for regular orders.

- **Complexity**: High
- **Risk Factors**: Must correctly compute sentinel limit price for market triggers; must pass `reduceOnly` for existing order types without breaking them; nonce/signing pipeline is shared
- **Files**:
  - `src/TradingApp.Api/Services/HyperliquidOrderService.cs` — Modify `PlaceOrderAsync` to handle `stop-market` order type
  - `src/TradingApp.Api/Services/IHyperliquidOrderService.cs` — No change needed (same `PlaceOrderAsync` signature)
- **Success**:
  - `stop-market` orders are routed through `BuildTriggerOrderAction` with correct trigger wire format
  - `triggerPrice` is validated as required for `stop-market` orders
  - `reduceOnly` flag is forwarded to `BuildOrderAction` for `market` and `limit` orders
  - Existing market/limit order flow is unchanged
  - EIP-712 signing pipeline works correctly for trigger orders
- **Dependencies**: Task 1.1, Task 1.3

#### Implementation Details

```csharp
// src/TradingApp.Api/Services/HyperliquidOrderService.cs — modification to PlaceOrderAsync
// Replace the section after metadata resolution with order type branching

public async Task<PlaceOrderResponse> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(request);

    var coin = HyperliquidAssetMapper.ToCoin(request.Asset);
    var metadata = await _metadataCache.GetAsync(coin, cancellationToken);

    var isBuy = request.Side.Equals("buy", StringComparison.OrdinalIgnoreCase);
    var isMainnet = _options.Network.Equals("mainnet", StringComparison.OrdinalIgnoreCase);
    var isMarket = request.OrderType.Equals("market", StringComparison.OrdinalIgnoreCase);
    var isStopMarket = request.OrderType.Equals("stop-market", StringComparison.OrdinalIgnoreCase);

    Dictionary<string, object> action;

    if (isStopMarket)
    {
        if (!request.TriggerPrice.HasValue)
        {
            throw new DomainException("TriggerPrice is required for stop-market orders.");
        }

        if (string.IsNullOrWhiteSpace(request.TpSlType))
        {
            throw new DomainException("TpSlType is required for stop-market orders.");
        }

        // For market triggers, set a sentinel limit price to ensure fill
        var sentinelPrice = isBuy
            ? RoundToSignificantFigures(request.TriggerPrice.Value * 1.2m, 5)
            : RoundToSignificantFigures(request.TriggerPrice.Value * 0.8m, 5);

        action = HyperliquidEip712.BuildTriggerOrderAction(
            assetIndex: metadata.Index,
            isBuy: isBuy,
            limitPrice: sentinelPrice,
            size: request.Size,
            triggerPrice: request.TriggerPrice.Value,
            isMarketTrigger: true,
            tpsl: request.TpSlType);

        _logger.LogInformation(
            "Trigger order: Coin={Coin}, TriggerPrice={TriggerPrice}, SentinelPrice={SentinelPrice}, IsBuy={IsBuy}, TpSl={TpSl}",
            coin, request.TriggerPrice.Value, sentinelPrice, isBuy, request.TpSlType);
    }
    else if (isMarket)
    {
        var midPrice = await GetMidPriceAsync(coin, cancellationToken);
        var slippagePrice = isBuy ? midPrice * 1.05m : midPrice * 0.95m;
        var price = RoundToSignificantFigures(slippagePrice, 5);

        action = HyperliquidEip712.BuildOrderAction(
            assetIndex: metadata.Index,
            isBuy: isBuy,
            price: price,
            size: request.Size,
            reduceOnly: request.ReduceOnly,
            tif: "Ioc");

        _logger.LogInformation(
            "Market order: Coin={Coin}, MidPrice={MidPrice}, SlippagePrice={SlippagePrice}, IsBuy={IsBuy}, ReduceOnly={ReduceOnly}",
            coin, midPrice, price, isBuy, request.ReduceOnly);
    }
    else
    {
        // Limit order
        var price = request.Price ?? throw new DomainException("Price is required for limit orders.");

        action = HyperliquidEip712.BuildOrderAction(
            assetIndex: metadata.Index,
            isBuy: isBuy,
            price: price,
            size: request.Size,
            reduceOnly: request.ReduceOnly,
            tif: "Gtc");
    }

    // ... existing nonce/signing/submission pipeline unchanged ...
}
```

##### Pattern References

- `src/TradingApp.Api/Services/HyperliquidOrderService.cs` — existing `PlaceOrderAsync` method (lines 45–140)
- `src/TradingApp.Infrastructure/Hyperliquid/HyperliquidEip712.cs` — `BuildOrderAction` and new `BuildTriggerOrderAction`

---

### Task 1.5: Enrich PositionDto with margin and notional fields {#task-15-enrich-positiondto-with-margin-and-notional-fields}

Add `MarginUsed` and `PositionValue` properties to `PositionDto` and map them from the `clearinghouseState` API response (which already contains these fields but they are currently ignored).

- **Complexity**: Low
- **Risk Factors**: Field names in the Hyperliquid response must be verified (`marginUsed`, `positionValue`)
- **Files**:
  - `src/TradingApp.Api/Models/PositionDto.cs` — Add `MarginUsed` and `PositionValue` properties
  - `src/TradingApp.Api/Services/HyperliquidAccountService.cs` — Map new fields in `MapToPositions()`
- **Success**:
  - `GET /api/account/positions` returns `marginUsed` and `positionValue` for each position
  - Values are correctly parsed from the `clearinghouseState` JSON response
- **Dependencies**: None

#### Implementation Details

```csharp
// src/TradingApp.Api/Models/PositionDto.cs — modification
namespace TradingApp.Api.Models;

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
    public int Leverage { get; set; }
    public string MarginMode { get; set; } = string.Empty;
    public decimal MarginUsed { get; set; }
    public decimal PositionValue { get; set; }
}
```

```csharp
// src/TradingApp.Api/Services/HyperliquidAccountService.cs — modification to MapToPositions
// Add these two lines to the PositionDto initializer in the foreach loop:

results.Add(new PositionDto
{
    // ... existing fields ...
    Leverage = leverage,
    MarginMode = marginMode,
    MarginUsed = ParseDecimal(GetPropertyOrDefault(position, "marginUsed")),
    PositionValue = ParseDecimal(GetPropertyOrDefault(position, "positionValue")),
});
```

##### Pattern References

- `src/TradingApp.Api/Models/PositionDto.cs` — existing DTO with decimal properties
- `src/TradingApp.Api/Services/HyperliquidAccountService.cs` — `MapToPositions()` method using `ParseDecimal` + `GetPropertyOrDefault` pattern

---

### Task 1.6: Backend tests for trigger orders and position enrichment {#task-16-backend-tests-for-trigger-orders-and-position-enrichment}

Add tests for the new trigger order flow in both `OrdersControllerTests` (integration) and `HyperliquidOrderServiceTests` (unit). Also add a test verifying the enriched position fields.

- **Complexity**: Medium
- **Risk Factors**: Trigger order mock setup requires matching the new code path; position enrichment test needs mock JSON with new fields
- **Files**:
  - `tests/TradingApp.Api.Tests/Controllers/OrdersControllerTests.cs` — Add trigger order integration tests
  - `tests/TradingApp.Api.Tests/Services/HyperliquidOrderServiceTests.cs` — Add trigger order unit tests
  - `tests/TradingApp.Api.Tests/Controllers/AccountControllerTests.cs` — Add position enrichment test (if not already covered)
- **Success**:
  - Test: `GivenValidStopMarketOrder_WhenPlaceOrderAsync_ThenReturnsSuccessWithOrderId`
  - Test: `GivenStopMarketOrderWithoutTriggerPrice_WhenPlaceOrderAsync_ThenReturnsBadRequest`
  - Test: `GivenStopMarketOrderWithoutTpSlType_WhenPlaceOrderAsync_ThenReturnsBadRequest`
  - Test: `GivenMarketOrderWithReduceOnly_WhenPlaceOrderAsync_ThenReturnsSuccess`
  - Test: `GivenPositions_WhenGetPositions_ThenReturnsMarginUsedAndPositionValue`
  - All existing order tests continue to pass
- **Dependencies**: Tasks 1.1–1.5

#### Implementation Details

```csharp
// tests/TradingApp.Api.Tests/Services/HyperliquidOrderServiceTests.cs — add new tests

[TestMethod]
public async Task GivenValidStopMarketOrder_WhenPlaceOrderAsync_ThenSubmitsTriggerOrderToExchange()
{
    // Arrange
    var request = new PlaceOrderRequest
    {
        Asset = "BTC",
        Side = "sell",
        OrderType = "stop-market",
        Size = 0.01m,
        TriggerPrice = 69000m,
        TpSlType = "sl",
        ReduceOnly = true,
    };

    _restClientMock
        .Setup(x => x.PostExchangeAsync<HyperliquidExchangeResponse>(
            It.IsAny<object>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(CreateSuccessExchangeResponse());

    // Act
    var result = await _sut.PlaceOrderAsync(request);

    // Assert
    result.Success.Should().BeTrue();
    _restClientMock.Verify(x => x.PostExchangeAsync<HyperliquidExchangeResponse>(
        It.Is<object>(p => JsonSerializer.Serialize(p).Contains("\"grouping\":\"normalTpsl\"")),
        It.IsAny<CancellationToken>()), Times.Once);
}

[TestMethod]
public async Task GivenStopMarketOrderWithoutTriggerPrice_WhenPlaceOrderAsync_ThenThrowsDomainException()
{
    // Arrange
    var request = new PlaceOrderRequest
    {
        Asset = "BTC",
        Side = "sell",
        OrderType = "stop-market",
        Size = 0.01m,
        TpSlType = "sl",
    };

    // Act
    var action = () => _sut.PlaceOrderAsync(request);

    // Assert
    await action.Should().ThrowAsync<DomainException>()
        .WithMessage("*TriggerPrice*required*");
}

[TestMethod]
public async Task GivenStopMarketOrderWithoutTpSlType_WhenPlaceOrderAsync_ThenThrowsDomainException()
{
    // Arrange
    var request = new PlaceOrderRequest
    {
        Asset = "BTC",
        Side = "sell",
        OrderType = "stop-market",
        Size = 0.01m,
        TriggerPrice = 69000m,
    };

    // Act
    var action = () => _sut.PlaceOrderAsync(request);

    // Assert
    await action.Should().ThrowAsync<DomainException>()
        .WithMessage("*TpSlType*required*");
}

[TestMethod]
public async Task GivenMarketOrderWithReduceOnly_WhenPlaceOrderAsync_ThenPassesReduceOnlyToExchange()
{
    // Arrange
    var request = new PlaceOrderRequest
    {
        Asset = "BTC",
        Side = "sell",
        OrderType = "market",
        Size = 0.01m,
        ReduceOnly = true,
    };

    _restClientMock
        .Setup(x => x.PostExchangeAsync<HyperliquidExchangeResponse>(
            It.IsAny<object>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(CreateSuccessExchangeResponse());

    // Act
    var result = await _sut.PlaceOrderAsync(request);

    // Assert
    result.Success.Should().BeTrue();
}
```

```csharp
// tests/TradingApp.Api.Tests/Controllers/OrdersControllerTests.cs — add integration test

[TestMethod]
public async Task GivenValidStopMarketOrder_WhenPostOrder_ThenReturnsOk()
{
    // Arrange
    var request = new
    {
        asset = "BTC",
        side = "sell",
        orderType = "stop-market",
        size = 0.01,
        triggerPrice = 69000.0,
        tpSlType = "sl",
        reduceOnly = true,
    };

    _orderServiceMock
        .Setup(x => x.PlaceOrderAsync(It.IsAny<PlaceOrderRequest>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new PlaceOrderResponse { Success = true, OrderId = "12345", Status = "resting" });

    // Act
    var response = await _client.PostAsJsonAsync($"{BASE_URL}", request);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
}

[TestMethod]
public async Task GivenInvalidStopMarketOrder_WhenPostOrder_ThenReturnsBadRequest()
{
    // Arrange — missing triggerPrice and tpSlType
    var request = new
    {
        asset = "BTC",
        side = "sell",
        orderType = "stop-market",
        size = 0.01,
    };

    _orderServiceMock
        .Setup(x => x.PlaceOrderAsync(It.IsAny<PlaceOrderRequest>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new DomainException("TriggerPrice is required for stop-market orders."));

    // Act
    var response = await _client.PostAsJsonAsync($"{BASE_URL}", request);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}
```

##### Pattern References

- `tests/TradingApp.Api.Tests/Services/HyperliquidOrderServiceTests.cs` — existing test structure, `_restClientMock`, `CreateSuccessExchangeResponse`
- `tests/TradingApp.Api.Tests/Controllers/OrdersControllerTests.cs` — existing `PostAsJsonAsync` + `_orderServiceMock` pattern

---

### Task 1.7: Build and run all backend tests {#task-17-build-and-run-all-backend-tests}

Build all test projects and run the full test suite to verify no regressions.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (build/test only)
- **Success**:
  - `dotnet build TradingApp.sln` succeeds with no errors
  - `dotnet test` passes all existing and new tests
- **Dependencies**: Tasks 1.1–1.6

## Phase Success Criteria

- `POST /api/orders` accepts `stop-market` order type with `triggerPrice`, `reduceOnly`, and `tpSlType`
- Trigger orders are signed via EIP-712 and submitted with `{ "trigger": {...} }` wire format and `grouping: "normalTpsl"`
- `reduceOnly` flag is forwarded to `BuildOrderAction` for market and limit orders
- `GET /api/account/positions` returns `marginUsed` and `positionValue` for each position
- All backend tests pass (new + existing)
