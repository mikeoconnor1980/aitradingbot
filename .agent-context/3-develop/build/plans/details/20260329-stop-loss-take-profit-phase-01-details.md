<!-- markdownlint-disable-file -->

# Task Details: Stop Loss & Take Profit

## Phase 1: Backend — Trigger Order Infrastructure & API

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — sealed classes, async suffix, CancellationToken, one class per file
- `.github/instructions/testing.instructions.md` — MSTest, FluentAssertions, Moq, `Given_When_Then` naming, BaseControllerTests
- `.github/instructions/api-controllers.instructions.md` — DataAnnotations validation, `[ProducesResponseType]`, route conventions
- `.github/instructions/dotnet-architecture.instructions.md` — exception handling (`DomainException` → 400), DI patterns
- `.agent-context/0-knowledge/10-architecture-decisions.md` — ADR 14: direct service injection for exchange operations (no MediatR for orders)
- `.agent-context/0-knowledge/02-hyperliquid-integration.md` — REST client, EIP-712 signing, exchange action patterns

## Design References

- **Hyperliquid trigger order format**: The `order` action type supports a `trigger` variant in the `["t"]` field: `{ "trigger": { "triggerPx": "64000.0", "isMarket": true, "tpsl": "sl" } }`. Combined with `"r": true` (reduce-only). Reference: [Hyperliquid API docs](https://hyperliquid.gitbook.io/hyperliquid-docs/for-developers/api/exchange-endpoint#place-an-order)
- **Modify trigger order**: Uses `batchModifyOrders` with the same `trigger` type variant in the inner order params
- **Cancel trigger order**: Uses the standard `cancel` action (identical to regular order cancellation)

---

### Task 1.1: Create trigger order request/response models {#task-11-create-trigger-order-request-response-models}

Create new request model for standalone trigger order placement (for existing positions) and a modify DTO for modifying trigger order prices.

- **Complexity**: Low
- **Risk Factors**: None — follows existing `PlaceOrderRequest` / `ModifyOrderDto` patterns
- **Files**:
  - `src/TradePilot.Api/Models/PlaceTriggerOrderRequest.cs` — new file
  - `src/TradePilot.Api/Models/ModifyTriggerOrderDto.cs` — new file
- **Success**:
  - `PlaceTriggerOrderRequest` has `Asset`, `Side`, `Size`, `TriggerPrice`, `TpslType` with DataAnnotations
  - `ModifyTriggerOrderDto` has `TriggerPrice`, `Size` with DataAnnotations
  - Solution builds cleanly

#### Implementation Details

```csharp
// src/TradePilot.Api/Models/PlaceTriggerOrderRequest.cs — new file
using System.ComponentModel.DataAnnotations;

namespace TradePilot.Api.Models;

public sealed class PlaceTriggerOrderRequest
{
    [Required]
    public string Asset { get; set; } = default!;

    [Required]
    [RegularExpression("^(buy|sell)$", ErrorMessage = "Side must be 'buy' or 'sell'")]
    public string Side { get; set; } = default!;

    [Required]
    [Range(0.000001, double.MaxValue, ErrorMessage = "Size must be positive")]
    public decimal Size { get; set; }

    [Required]
    [Range(0.000001, double.MaxValue, ErrorMessage = "Trigger price must be positive")]
    public decimal TriggerPrice { get; set; }

    [Required]
    [RegularExpression("^(sl|tp)$", ErrorMessage = "TpslType must be 'sl' or 'tp'")]
    public string TpslType { get; set; } = default!;
}
```

```csharp
// src/TradePilot.Api/Models/ModifyTriggerOrderDto.cs — new file
using System.ComponentModel.DataAnnotations;

namespace TradePilot.Api.Models;

public sealed class ModifyTriggerOrderDto
{
    [Required]
    [Range(0.000001, double.MaxValue, ErrorMessage = "Trigger price must be positive")]
    public decimal TriggerPrice { get; set; }

    [Required]
    [Range(0.000001, double.MaxValue, ErrorMessage = "Size must be positive")]
    public decimal Size { get; set; }
}
```

##### Pattern References

- `src/TradePilot.Api/Models/PlaceOrderRequest.cs` — DataAnnotations, sealed class, `= default!` pattern
- `src/TradePilot.Api/Models/ModifyOrderDto.cs` — modify DTO with `[Range]` validation

---

### Task 1.2: Extend OpenOrderDto with trigger order fields {#task-12-extend-openorderdto-with-trigger-order-fields}

Add `TriggerPrice`, `TpslType`, and `IsReduceOnly` fields to `OpenOrderDto` so trigger orders returned by the exchange are fully represented.

- **Complexity**: Low
- **Risk Factors**: None — additive change, nullable fields don't break existing callers
- **Files**:
  - `src/TradePilot.Api/Models/OpenOrderDto.cs` — modification
- **Success**:
  - `OpenOrderDto` has `TriggerPrice?`, `TpslType?`, `IsReduceOnly` properties
  - Solution builds cleanly

#### Implementation Details

```csharp
// src/TradePilot.Api/Models/OpenOrderDto.cs — modification
// Add the following properties to the existing class:

    /// <summary>Trigger price for tpsl orders. Null for limit/market orders.</summary>
    public decimal? TriggerPrice { get; set; }

    /// <summary>"sl" for stop loss, "tp" for take profit. Null for non-trigger orders.</summary>
    public string? TpslType { get; set; }

    /// <summary>Whether this order is reduce-only.</summary>
    public bool IsReduceOnly { get; set; }
```

##### Pattern References

- `src/TradePilot.Api/Models/OpenOrderDto.cs` — existing DTO structure

---

### Task 1.3: Add BuildTriggerOrderAction to HyperliquidEip712 {#task-13-add-buildtriggerorderaction-to-hyperliquideip712}

Add a new static method `BuildTriggerOrderAction` that constructs the dictionary-based action for Hyperliquid trigger orders. This is the core EIP-712 compatible action for placing SL/TP.

- **Complexity**: Medium
- **Risk Factors**: MessagePack serialization must produce correct bytes — the dictionary structure must exactly match the Hyperliquid wire format
- **Files**:
  - `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidEip712.cs` — modification (add new method)
- **Success**:
  - `BuildTriggerOrderAction` produces the correct dictionary structure with `trigger` type variant
  - `"r": true` (reduce-only) is always set for trigger orders
  - `"p"` is set to the trigger price for MessagePack hashing (Hyperliquid uses price field for trigger price in the signature)
  - Solution builds cleanly
- **Dependencies**:
  - None — independent of other tasks

#### Implementation Details

```csharp
// src/TradePilot.Infrastructure/Hyperliquid/HyperliquidEip712.cs — modification
// Add this method alongside the existing BuildOrderAction:

    public static Dictionary<string, object> BuildTriggerOrderAction(
        int assetIndex,
        bool isBuy,
        decimal triggerPrice,
        decimal size,
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
                    ["p"] = ToWireDecimal(triggerPrice),
                    ["s"] = ToWireDecimal(size),
                    ["r"] = true, // trigger orders are always reduce-only
                    ["t"] = new Dictionary<string, object>
                    {
                        ["trigger"] = new Dictionary<string, object>
                        {
                            ["triggerPx"] = ToWireDecimal(triggerPrice),
                            ["isMarket"] = true,
                            ["tpsl"] = tpsl
                        }
                    }
                }
            },
            ["grouping"] = "na"
        };
    }
```

##### Pattern References

- `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidEip712.cs` — existing `BuildOrderAction` method structure, `ToWireDecimal` utility

---

### Task 1.4: Extend HyperliquidModifyAction for trigger orders {#task-14-extend-hyperliquidmodifyaction-for-trigger-orders}

Add a `Trigger` property to `HyperliquidOrderType` so that `batchModifyOrders` can modify trigger orders (not just limit orders). Add the `HyperliquidTriggerParams` model.

- **Complexity**: Medium
- **Risk Factors**: Must use `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` to avoid sending both `limit` and `trigger` on the wire
- **Files**:
  - `src/TradePilot.Infrastructure/Hyperliquid/Models/HyperliquidModifyAction.cs` — modification
- **Success**:
  - `HyperliquidOrderType` has a nullable `Trigger` property
  - `HyperliquidTriggerParams` has `TriggerPx`, `IsMarket`, `Tpsl` properties
  - When `Trigger` is set and `Limit` is null, JSON serialization only emits the trigger object
  - Solution builds cleanly
- **Dependencies**:
  - None

#### Implementation Details

```csharp
// src/TradePilot.Infrastructure/Hyperliquid/Models/HyperliquidModifyAction.cs — modification
// Add HyperliquidTriggerParams class and extend HyperliquidOrderType

public sealed class HyperliquidTriggerParams
{
    [JsonPropertyName("triggerPx")]
    public string TriggerPx { get; set; } = default!;

    [JsonPropertyName("isMarket")]
    public bool IsMarket { get; set; } = true;

    [JsonPropertyName("tpsl")]
    public string Tpsl { get; set; } = default!;
}

// Modify existing HyperliquidOrderType:
public sealed class HyperliquidOrderType
{
    [JsonPropertyName("limit")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HyperliquidLimitParams? Limit { get; set; }

    [JsonPropertyName("trigger")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HyperliquidTriggerParams? Trigger { get; set; }
}
```

Note: The existing `Limit` property changes from non-nullable with default `new()` to nullable. Existing callers that set `Limit` will need to explicitly set it (e.g., `Limit = new HyperliquidLimitParams { Tif = "Gtc" }`). Verify the existing `ModifyOrderAsync` call site still works.

##### Pattern References

- `src/TradePilot.Infrastructure/Hyperliquid/Models/HyperliquidModifyAction.cs` — existing `HyperliquidOrderType` and `HyperliquidLimitParams`

---

### Task 1.5: Add trigger order methods to IHyperliquidOrderService and HyperliquidOrderService {#task-15-add-trigger-order-methods-to-ihyperliquidorderservice-and-hyperliquidorderservice}

Add `PlaceTriggerOrderAsync` and `ModifyTriggerOrderAsync` methods. Cancel is handled by the existing `CancelOrderAsync`. These methods follow the existing `SubmitExchangeActionAsync` pattern.

- **Complexity**: High
- **Risk Factors**: Must correctly determine `isBuy` for trigger orders (SL for a long = sell, TP for a long = sell; SL for a short = buy, TP for a short = buy — all trigger orders are reduce-only, so the trigger side is opposite the position side)
- **Files**:
  - `src/TradePilot.Api/Services/IHyperliquidOrderService.cs` — modification (add interface methods)
  - `src/TradePilot.Api/Services/HyperliquidOrderService.cs` — modification (add implementations)
- **Success**:
  - `PlaceTriggerOrderAsync` builds trigger action, signs, submits, and returns `PlaceOrderResponse`
  - `ModifyTriggerOrderAsync` builds modify action with trigger type, signs, and submits
  - Trigger side is correctly derived: opposite of the position side (all SL/TP are reduce-only)
  - Exchange errors surface as `DomainException`
  - Solution builds cleanly
- **Dependencies**:
  - Task 1.1 (PlaceTriggerOrderRequest model)
  - Task 1.3 (BuildTriggerOrderAction)
  - Task 1.4 (HyperliquidTriggerParams for modify)

#### Implementation Details

```csharp
// src/TradePilot.Api/Services/IHyperliquidOrderService.cs — modification
// Add to interface:

    Task<PlaceOrderResponse> PlaceTriggerOrderAsync(
        PlaceTriggerOrderRequest request,
        CancellationToken cancellationToken = default);

    Task ModifyTriggerOrderAsync(
        string orderId,
        string asset,
        string side,
        decimal triggerPrice,
        decimal size,
        string tpslType,
        CancellationToken cancellationToken = default);
```

```csharp
// src/TradePilot.Api/Services/HyperliquidOrderService.cs — modification
// Add implementations:

    public async Task<PlaceOrderResponse> PlaceTriggerOrderAsync(
        PlaceTriggerOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var coin = NormalizeAsset(request.Asset);
        var assetIndex = await ResolveAssetIndexAsync(coin, cancellationToken);

        // Trigger orders are reduce-only — side is as specified (closing side of position)
        var isBuy = request.Side.Equals("buy", StringComparison.OrdinalIgnoreCase);

        var action = HyperliquidEip712.BuildTriggerOrderAction(
            assetIndex,
            isBuy,
            request.TriggerPrice,
            request.Size,
            request.TpslType);

        // Must follow manual signing/submission pattern (same as PlaceOrderAsync)
        // because SubmitExchangeActionAsync returns void and we need the response.
        var isMainnet = _options.Network.Equals("mainnet", StringComparison.OrdinalIgnoreCase);
        var nonce = _nonceProvider.GetNextNonce();
        var connectionId = HyperliquidEip712.ComputeActionHash(action, nonce, vaultAddress: null);
        var eip712Hash = HyperliquidEip712.ComputeEip712Hash(connectionId, isMainnet);
        var (r, s, v) = _signer.SignHash(eip712Hash);

        var payload = new
        {
            action,
            nonce,
            signature = new { r, s, v },
            vaultAddress = (string?)null,
        };

        var exchangeResponse = await _restClient
            .PostExchangeAsync<HyperliquidExchangeResponse>(payload, cancellationToken);

        return MapExchangeResponse(exchangeResponse);
    }

    public async Task ModifyTriggerOrderAsync(
        string orderId,
        string asset,
        string side,
        decimal triggerPrice,
        decimal size,
        string tpslType,
        CancellationToken cancellationToken = default)
    {
        var orderIdLong = ParseOrderId(orderId);
        var isBuy = side.Equals("buy", StringComparison.OrdinalIgnoreCase);
        var assetIndex = await ResolveAssetIndexAsync(asset, cancellationToken);

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
                        Price = ToWireDecimal(triggerPrice),
                        Size = ToWireDecimal(size),
                        ReduceOnly = true,
                        OrderType = new HyperliquidOrderType
                        {
                            Trigger = new HyperliquidTriggerParams
                            {
                                TriggerPx = ToWireDecimal(triggerPrice),
                                IsMarket = true,
                                Tpsl = tpslType
                            }
                        }
                    }
                }
            ]
        };

        await SubmitExchangeActionAsync(action, cancellationToken);
    }
```

Note: `MapExchangeResponse` is the existing private method in `HyperliquidOrderService` that maps `HyperliquidExchangeResponse` to `PlaceOrderResponse`. `PlaceTriggerOrderAsync` follows the same manual signing/submission pattern as `PlaceOrderAsync` (not `SubmitExchangeActionAsync`) because it needs the exchange response to build the return value. Use `ResolveAssetIndexAsync` and `ParseOrderId` helper methods (matching existing `ModifyOrderAsync` pattern).

##### Pattern References

- `src/TradePilot.Api/Services/HyperliquidOrderService.cs` — existing `PlaceOrderAsync`, `ModifyOrderAsync`, `SubmitExchangeActionAsync` pattern
- `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidEip712.cs` — `BuildTriggerOrderAction`, `ToWireDecimal`

---

### Task 1.6: Parse trigger order details in HyperliquidAccountService.MapToOpenOrders {#task-16-parse-trigger-order-details-in-hyperliquidaccountservicemaptoopenorders}

Update `MapToOpenOrders` to extract `triggerPx`, `tpsl`, and `isMarket` from trigger order responses and populate the new `OpenOrderDto` fields.

- **Complexity**: Medium
- **Risk Factors**: The Hyperliquid `orderType` field is polymorphic (string for simple types, object for trigger). The existing `GetOrderType` method already handles this but discards the trigger details. Need to extract them without breaking the existing parsing.
- **Files**:
  - `src/TradePilot.Api/Services/HyperliquidAccountService.cs` — modification
- **Success**:
  - Trigger orders have `TriggerPrice`, `TpslType`, and `IsReduceOnly` populated in `OpenOrderDto`
  - Non-trigger orders have null `TriggerPrice` and `TpslType`
  - Existing non-trigger order mapping is unchanged
  - Solution builds cleanly
- **Dependencies**:
  - Task 1.2 (OpenOrderDto fields)

#### Implementation Details

```csharp
// src/TradePilot.Api/Services/HyperliquidAccountService.cs — modification
// Update the MapToOpenOrders method where OpenOrderDto is constructed.
// After GetOrderType extracts "trigger", parse the trigger details:

// In the open orders mapping loop, after extracting orderType:
var orderType = GetOrderType(order);
decimal? triggerPrice = null;
string? tpslType = null;
bool isReduceOnly = false;

if (orderType == "trigger" && order.TryGetProperty("orderType", out var orderTypeEl)
    && orderTypeEl.ValueKind == JsonValueKind.Object
    && orderTypeEl.TryGetProperty("trigger", out var triggerEl))
{
    if (triggerEl.TryGetProperty("triggerPx", out var triggerPxEl))
        triggerPrice = decimal.Parse(triggerPxEl.GetString()!, CultureInfo.InvariantCulture);

    if (triggerEl.TryGetProperty("tpsl", out var tpslEl))
        tpslType = tpslEl.GetString();
}

if (order.TryGetProperty("reduceOnly", out var reduceOnlyEl))
    isReduceOnly = reduceOnlyEl.GetBoolean();

// Then populate the new fields on OpenOrderDto:
results.Add(new OpenOrderDto
{
    // ... existing fields ...
    TriggerPrice = triggerPrice,
    TpslType = tpslType,
    IsReduceOnly = isReduceOnly
});
```

##### Pattern References

- `src/TradePilot.Api/Services/HyperliquidAccountService.cs` — existing `MapToOpenOrders` method and `GetOrderType` helper

---

### Task 1.7: Enrich PositionDto with SL/TP from open trigger orders {#task-17-enrich-positiondto-with-sltp-from-open-trigger-orders}

Add `StopLossPrice` and `TakeProfitPrice` (and their order IDs for edit/cancel operations) to `PositionDto`. Update `GetPositionsAsync` to fetch open orders in parallel with positions, then match trigger orders to positions by asset.

- **Complexity**: High
- **Risk Factors**: Matching logic — a trigger order for asset X with `tpsl=sl` corresponds to the position on asset X. Multiple SL or TP orders for the same asset could exist (use the first/closest one). Performance — additional API call per position fetch.
- **Files**:
  - `src/TradePilot.Api/Models/PositionDto.cs` — modification (add SL/TP fields)
  - `src/TradePilot.Api/Services/HyperliquidAccountService.cs` — modification (enrich positions with trigger orders)
  - `src/TradePilot.Api/Services/IHyperliquidAccountService.cs` — no change expected (existing `GetPositionsAsync` signature returns `IReadOnlyList<PositionDto>`)
- **Success**:
  - `PositionDto` has `StopLossPrice?`, `TakeProfitPrice?`, `StopLossOrderId?`, `TakeProfitOrderId?`
  - Positions with matching trigger orders show the correct SL/TP prices
  - Positions without trigger orders have null SL/TP
  - Solution builds cleanly
- **Dependencies**:
  - Task 1.2 (OpenOrderDto with trigger fields)
  - Task 1.6 (trigger details parsed in MapToOpenOrders)

#### Implementation Details

```csharp
// src/TradePilot.Api/Models/PositionDto.cs — modification
// Add these properties to the existing class:

    public decimal? StopLossPrice { get; set; }
    public string? StopLossOrderId { get; set; }
    public decimal? TakeProfitPrice { get; set; }
    public string? TakeProfitOrderId { get; set; }
```

```csharp
// src/TradePilot.Api/Services/HyperliquidAccountService.cs — modification
// In GetPositionsAsync, after building positions list, fetch open orders and correlate:

// 1. Fetch open orders (can reuse existing GetOpenOrdersAsync or inline the info call)
var openOrders = await GetOpenOrdersAsync(cancellationToken);

// 2. Build lookup: asset → trigger orders
var triggerOrdersByAsset = openOrders
    .Where(o => o.OrderType == "trigger" && o.TpslType != null)
    .GroupBy(o => o.Asset)
    .ToDictionary(g => g.Key, g => g.ToList());

// 3. Enrich each position
foreach (var position in positions)
{
    if (triggerOrdersByAsset.TryGetValue(position.Asset, out var triggerOrders))
    {
        var sl = triggerOrders.FirstOrDefault(o => o.TpslType == "sl");
        var tp = triggerOrders.FirstOrDefault(o => o.TpslType == "tp");

        if (sl != null)
        {
            position.StopLossPrice = sl.TriggerPrice;
            position.StopLossOrderId = sl.OrderId;
        }
        if (tp != null)
        {
            position.TakeProfitPrice = tp.TriggerPrice;
            position.TakeProfitOrderId = tp.OrderId;
        }
    }
}
```

##### Pattern References

- `src/TradePilot.Api/Services/HyperliquidAccountService.cs` — existing `GetPositionsAsync` with parallel `Task.WhenAll` pattern for merging data sources
- `src/TradePilot.Api/Models/PositionDto.cs` — existing DTO structure

---

### Task 1.8: Extend PlaceOrderRequest and PlaceOrderAsync for companion SL/TP trigger orders {#task-18-extend-placeorderrequest-and-placeorderasync-for-companion-sltp-trigger-orders}

Add optional `StopLossPrice` and `TakeProfitPrice` to `PlaceOrderRequest`. Extend `PlaceOrderAsync` to place companion trigger orders after the main order succeeds.

- **Complexity**: Medium
- **Risk Factors**: Multi-step atomicity — if the main order succeeds but a trigger order fails, the position is unprotected. Acceptable for POC — log and surface warning in response.
- **Files**:
  - `src/TradePilot.Api/Models/PlaceOrderRequest.cs` — modification
  - `src/TradePilot.Api/Services/HyperliquidOrderService.cs` — modification
- **Success**:
  - `PlaceOrderRequest` has optional `StopLossPrice?` and `TakeProfitPrice?`
  - When SL/TP are provided, trigger orders are placed after the main order
  - If trigger order placement fails, the main order response is still returned with a warning detail
  - Solution builds cleanly
- **Dependencies**:
  - Task 1.3 (BuildTriggerOrderAction)
  - Task 1.5 (PlaceTriggerOrderAsync can be reused internally)

#### Implementation Details

```csharp
// src/TradePilot.Api/Models/PlaceOrderRequest.cs — modification
// Add optional SL/TP fields:

    [Range(0.000001, double.MaxValue, ErrorMessage = "Stop loss price must be positive")]
    public decimal? StopLossPrice { get; set; }

    [Range(0.000001, double.MaxValue, ErrorMessage = "Take profit price must be positive")]
    public decimal? TakeProfitPrice { get; set; }
```

```csharp
// src/TradePilot.Api/Services/HyperliquidOrderService.cs — modification
// In PlaceOrderAsync, after the main order succeeds, place companion triggers:

    // ... existing main order placement ...
    var mainResponse = MapToPlaceOrderResponse(response);

    // Place companion SL/TP trigger orders if requested
    var warnings = new List<string>();

    if (request.StopLossPrice.HasValue)
    {
        try
        {
            // SL side is opposite of the main order side (reduce-only)
            var slSide = isBuy ? "sell" : "buy";
            var slAction = HyperliquidEip712.BuildTriggerOrderAction(
                metadata.Index, !isBuy, request.StopLossPrice.Value, request.Size, "sl");
            await SubmitExchangeActionAsync(slAction, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to place stop loss trigger order for {Asset}", request.Asset);
            warnings.Add($"Stop loss trigger order failed: {ex.Message}");
        }
    }

    if (request.TakeProfitPrice.HasValue)
    {
        try
        {
            var tpAction = HyperliquidEip712.BuildTriggerOrderAction(
                metadata.Index, !isBuy, request.TakeProfitPrice.Value, request.Size, "tp");
            await SubmitExchangeActionAsync(tpAction, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to place take profit trigger order for {Asset}", request.Asset);
            warnings.Add($"Take profit trigger order failed: {ex.Message}");
        }
    }

    if (warnings.Count > 0)
        mainResponse.Detail = string.Join("; ", warnings);

    return mainResponse;
```

##### Pattern References

- `src/TradePilot.Api/Services/HyperliquidOrderService.cs` — existing `PlaceOrderAsync` flow
- `src/TradePilot.Api/Models/PlaceOrderResponse.cs` — `Detail` field for warnings

---

### Task 1.9: Add trigger order controller endpoints to OrdersController {#task-19-add-trigger-order-controller-endpoints-to-orderscontroller}

Add `POST /api/orders/trigger`, `PUT /api/orders/trigger/{orderId}`, and `DELETE /api/orders/trigger/{orderId}` endpoints. The delete reuses the existing cancel mechanism.

- **Complexity**: Medium
- **Risk Factors**: Route conflict — `DELETE /api/orders` (cancel-all) vs `DELETE /api/orders/trigger/{orderId}`. No conflict because the sub-path `trigger/{orderId}` is more specific.
- **Files**:
  - `src/TradePilot.Api/Controllers/OrdersController.cs` — modification
- **Success**:
  - `POST /api/orders/trigger` → `Ok(PlaceOrderResponse)` or `BadRequest`
  - `PUT /api/orders/trigger/{orderId}` → `NoContent()` or `BadRequest`
  - `DELETE /api/orders/trigger/{orderId}` → `NoContent()` or `BadRequest`/`NotFound`
  - All endpoints have `[ProducesResponseType]` attributes
  - Solution builds cleanly
- **Dependencies**:
  - Task 1.1 (PlaceTriggerOrderRequest, ModifyTriggerOrderDto)
  - Task 1.5 (Service methods)

#### Implementation Details

```csharp
// src/TradePilot.Api/Controllers/OrdersController.cs — modification
// Add these action methods to the existing controller:

    [HttpPost("trigger")]
    [ProducesResponseType(typeof(PlaceOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PlaceTriggerOrder(
        [FromBody] PlaceTriggerOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _orderService.PlaceTriggerOrderAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPut("trigger/{orderId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ModifyTriggerOrder(
        string orderId,
        [FromBody] ModifyTriggerOrderDto dto,
        CancellationToken cancellationToken)
    {
        // Look up the existing order to get asset, side, and tpsl type
        var openOrders = await _accountService.GetOpenOrdersAsync(cancellationToken);
        var existingOrder = openOrders.FirstOrDefault(o => o.OrderId == orderId);
        if (existingOrder is null)
            throw new NotFoundException($"Trigger order {orderId} not found");

        await _orderService.ModifyTriggerOrderAsync(
            orderId,
            existingOrder.Asset,
            existingOrder.Side,
            dto.TriggerPrice,
            dto.Size,
            existingOrder.TpslType ?? "sl",
            cancellationToken);

        return NoContent();
    }

    [HttpDelete("trigger/{orderId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelTriggerOrder(
        string orderId,
        CancellationToken cancellationToken)
    {
        // Reuse existing cancel — same mechanism for regular and trigger orders
        var openOrders = await _accountService.GetOpenOrdersAsync(cancellationToken);
        var existingOrder = openOrders.FirstOrDefault(o => o.OrderId == orderId);
        if (existingOrder is null)
            throw new NotFoundException($"Trigger order {orderId} not found");

        await _orderService.CancelOrderAsync(orderId, existingOrder.Asset, cancellationToken);
        return NoContent();
    }
```

##### Pattern References

- `src/TradePilot.Api/Controllers/OrdersController.cs` — existing `PlaceOrder`, `CancelOrder`, `ModifyOrder` action methods
- `src/TradePilot.Api/Infrastructure/Filters/HttpGlobalExceptionFilter.cs` — `NotFoundException` → 404

---

### Task 1.10: Write unit tests for HyperliquidOrderService trigger methods {#task-110-write-unit-tests-for-hyperliquidorderservice-trigger-methods}

Add tests for `PlaceTriggerOrderAsync` and `ModifyTriggerOrderAsync` covering happy paths, unknown asset, and exchange error scenarios.

- **Complexity**: Medium
- **Risk Factors**: None — follows established patterns
- **Files**:
  - `tests/TradePilot.Api.Tests/Services/HyperliquidOrderServiceTests.cs` — modification (add new test methods)
- **Success**:
  - Tests cover: place trigger order happy path, place trigger order unknown asset, place trigger order exchange error, modify trigger order happy path, modify trigger order unknown asset
  - All tests use `Given_When_Then` naming
  - Tests pass
- **Dependencies**:
  - Task 1.5 (service implementations)

#### Implementation Details

```csharp
// tests/TradePilot.Api.Tests/Services/HyperliquidOrderServiceTests.cs — modification
// Add these test methods following the existing test patterns:

    [TestMethod]
    public async Task GivenValidTriggerRequest_WhenPlaceTriggerOrderAsync_ThenReturnsSuccessResponse()
    {
        // Arrange
        var request = new PlaceTriggerOrderRequest
        {
            Asset = "BTC",
            Side = "sell",
            Size = 0.1m,
            TriggerPrice = 64000m,
            TpslType = "sl"
        };

        _metadataCacheMock
            .Setup(m => m.GetAsync("BTC", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssetMetadata(0, 5, 50));

        _restClientMock
            .Setup(c => c.PostExchangeAsync<HyperliquidExchangeResponse>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HyperliquidExchangeResponse { Status = "ok", Response = /* ... */ });

        // Act
        var result = await _sut.PlaceTriggerOrderAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    [TestMethod]
    public async Task GivenUnknownAsset_WhenPlaceTriggerOrderAsync_ThenThrowsNotFoundException()
    {
        // Arrange
        var request = new PlaceTriggerOrderRequest
        {
            Asset = "UNKNOWN",
            Side = "sell",
            Size = 0.1m,
            TriggerPrice = 64000m,
            TpslType = "sl"
        };

        _metadataCacheMock
            .Setup(m => m.GetAsync("UNKNOWN", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Asset", "UNKNOWN"));

        // Act
        var act = () => _sut.PlaceTriggerOrderAsync(request);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
```

##### Pattern References

- `tests/TradePilot.Api.Tests/Services/HyperliquidOrderServiceTests.cs` — existing test structure, mock setup, assertion patterns

---

### Task 1.11: Write controller integration tests for trigger order endpoints {#task-111-write-controller-integration-tests-for-trigger-order-endpoints}

Add integration tests for `POST /api/orders/trigger`, `PUT /api/orders/trigger/{orderId}`, and `DELETE /api/orders/trigger/{orderId}`.

- **Complexity**: Medium
- **Risk Factors**: None — follows `OrdersControllerTests` pattern
- **Files**:
  - `tests/TradePilot.Api.Tests/Controllers/OrdersControllerTests.cs` — modification (add new test methods)
- **Success**:
  - Tests cover: place trigger order 200, place trigger order invalid body 400, modify trigger order 204, cancel trigger order 204, cancel trigger order not found 404
  - All tests use `Given_When_Then` naming
  - Tests pass
- **Dependencies**:
  - Task 1.9 (controller endpoints)
  - Task 1.10 (service tests should pass first)

#### Implementation Details

```csharp
// tests/TradePilot.Api.Tests/Controllers/OrdersControllerTests.cs — modification
// Add test methods following the existing pattern:

    [TestMethod]
    public async Task GivenValidTriggerRequest_WhenPostTriggerOrder_ThenReturnsOk()
    {
        // Arrange
        var request = new PlaceTriggerOrderRequest
        {
            Asset = "BTC", Side = "sell", Size = 0.1m,
            TriggerPrice = 64000m, TpslType = "sl"
        };

        _orderServiceMock
            .Setup(s => s.PlaceTriggerOrderAsync(It.IsAny<PlaceTriggerOrderRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaceOrderResponse { Success = true, OrderId = "123" });

        // Act
        var response = await _client.PostAsync("/api/orders/trigger", GetStringContent(request));

        // Assert
        var result = await response.ReadAndAssertSuccessAsync<PlaceOrderResponse>();
        result.Success.Should().BeTrue();
    }

    [TestMethod]
    public async Task GivenInvalidTriggerRequest_WhenPostTriggerOrder_ThenReturnsBadRequest()
    {
        // Arrange — missing required fields
        var request = new { };

        // Act
        var response = await _client.PostAsync("/api/orders/trigger", GetStringContent(request));

        // Assert
        await response.AssertStatusCode(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task GivenValidModifyDto_WhenPutTriggerOrder_ThenReturnsNoContent()
    {
        // Arrange
        var dto = new ModifyTriggerOrderDto { TriggerPrice = 65000m, Size = 0.1m };

        _accountServiceMock
            .Setup(s => s.GetOpenOrdersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenOrderDto>
            {
                new() { OrderId = "123", Asset = "BTC", Side = "sell", OrderType = "trigger", TpslType = "sl" }
            });

        _orderServiceMock
            .Setup(s => s.ModifyTriggerOrderAsync(
                "123", "BTC", "sell", 65000m, 0.1m, "sl", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var response = await _client.PutAsync("/api/orders/trigger/123", GetStringContent(dto));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [TestMethod]
    public async Task GivenExistingTriggerOrder_WhenDeleteTriggerOrder_ThenReturnsNoContent()
    {
        // Arrange
        _accountServiceMock
            .Setup(s => s.GetOpenOrdersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenOrderDto>
            {
                new() { OrderId = "456", Asset = "ETH", Side = "buy", OrderType = "trigger", TpslType = "tp" }
            });

        _orderServiceMock
            .Setup(s => s.CancelOrderAsync("456", "ETH", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var response = await _client.DeleteAsync("/api/orders/trigger/456");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
```

##### Pattern References

- `tests/TradePilot.Api.Tests/Controllers/OrdersControllerTests.cs` — existing test class, BaseControllerTests, mock service pattern

---

### Task 1.12: Build solution and run all tests {#task-112-build-solution-and-run-all-tests}

Build the full solution and run all tests to verify no regressions.

- **Complexity**: Low
- **Risk Factors**: Existing `ModifyOrderAsync` may break if `HyperliquidOrderType.Limit` becoming nullable causes a null reference. Verify and fix.
- **Files**: None (verification only)
- **Success**:
  - `dotnet build TradePilot.sln` succeeds with no errors
  - `dotnet test TradePilot.sln` — all tests pass
- **Dependencies**:
  - All previous tasks in Phase 1

## Phase Success Criteria

- `POST /api/orders/trigger` places a trigger order on Hyperliquid and returns success
- `PUT /api/orders/trigger/{orderId}` modifies an existing trigger order price
- `DELETE /api/orders/trigger/{orderId}` cancels a trigger order
- `POST /api/orders` with optional `StopLossPrice`/`TakeProfitPrice` places companion trigger orders
- `GET /api/account/positions` returns positions enriched with SL/TP prices
- `GET /api/account/orders` returns trigger orders with `TriggerPrice` and `TpslType` fields
- All unit and controller integration tests pass
- Solution builds cleanly
