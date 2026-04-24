---
title: Hyperliquid Integration Code Review
date: 2026-04-21T10:00:03Z
scope: src/TradePilot.Infrastructure/Hyperliquid/**, src/TradePilot.Infrastructure/Services/Hyperliquid*, src/TradePilot.Api/Services/HyperliquidOrderService.cs
type: code-review
---

# Hyperliquid Integration — Code Review

**Date**: 2026-04-21  
**Reviewer**: Adversarial Review (Claude Opus 4.6)  
**Scope**: All Hyperliquid integration code, related interfaces, DI registration, and supporting services

## Files Reviewed

### Primary (Infrastructure/Hyperliquid/)
- `HyperliquidAccountAdapter.cs` — Adapter implementing `IExchangeAccountClient`
- `HyperliquidAssetMapper.cs` — Static utility for symbol/timeframe normalization
- `HyperliquidCapabilities.cs` — Exchange capability descriptor
- `HyperliquidEip712.cs` — EIP-712 signing, MsgPack serialization, action hash computation
- `HyperliquidHistoricalDataClient.cs` — Historical candle data adapter
- `HyperliquidMarketMetadataProvider.cs` — Market info and max leverage provider
- `Models/` — 22 model files (request/response DTOs, WebSocket messages, actions)

### Secondary (Infrastructure/Services/)
- `HyperliquidRestClient.cs` — HTTP client for `/info` and `/exchange` endpoints
- `HyperliquidAccountService.cs` — Position/order/fill mapping with parallel data fetching
- `HyperliquidSigner.cs` — EthECKey-based ECDSA signing
- `MutableSignerProvider.cs` — Thread-safe runtime-swappable signer wrapper
- `HyperliquidWebSocketClient.cs` — Market data WebSocket (trades stream)
- `HyperliquidUserEventClient.cs` — Per-wallet user event WebSocket (fills, order updates)

### API Layer
- `HyperliquidOrderService.cs` — Order lifecycle (place, cancel, modify, leverage, trigger orders)

### Interfaces (Application/Abstractions/Services/)
- `IExchangeAccountClient.cs`, `IExchangeSymbolMapper.cs`, `IExchangeCapabilities.cs`
- `IExchangeHistoricalDataClient.cs`, `IExchangeMarketMetadataProvider.cs`
- `IHyperliquidRestClient.cs`, `IHyperliquidAccountService.cs`, `IHyperliquidSigner.cs`
- `IHyperliquidWebSocketClient.cs`, `IHyperliquidUserEventClient.cs`

---

## CRITICAL Findings

### C1: `HyperliquidAssetMapper` is static but registered in DI as `IExchangeSymbolMapper`

**Files**: `HyperliquidAssetMapper.cs`, `Program.cs` (Api L224, Worker L121)

`HyperliquidAssetMapper` is declared `public static class` but both `Program.cs` files register it as:
`csharp
builder.Services.AddSingleton<IExchangeSymbolMapper, HyperliquidAssetMapper>();
`

A static class cannot be used as a generic type argument in C# (CS0718). Additionally, the class methods are all static and it does not implement `IExchangeSymbolMapper` (missing `Exchange`, `ToExchangeSymbol`, `FromExchangeSymbol`, `CanMap` members).

This either:
1. Fails at compile time on a clean build (incremental caching may hide it)
2. Fails at runtime when DI attempts resolution

**Impact**: Any code path that resolves `IExchangeSymbolMapper` for Hyperliquid will fail. Multi-exchange symbol mapping is broken for Hyperliquid.

**Recommendation**: Convert `HyperliquidAssetMapper` to a non-static class implementing `IExchangeSymbolMapper`, or create a separate adapter class that wraps the static methods and implements the interface.

---

### C2: `HyperliquidHistoricalDataClient.GetCandleSnapshotsAsync` uses `ContinueWith` anti-pattern

**File**: `HyperliquidHistoricalDataClient.cs` L30-31

`csharp
return _restClient.GetCandleSnapshotsAsync(pair.Base, timeframe, startTime, endTime, cancellationToken)
    .ContinueWith(static task => (IReadOnlyList<CandleSnapshotDto>)task.Result, cancellationToken);
`

`ContinueWith` has multiple problems:
- Accessing `.Result` on a faulted task throws `AggregateException` instead of the original exception, corrupting the stack trace
- The default `TaskScheduler` may not be `TaskScheduler.Default`, risking thread pool starvation
- Does not propagate `TaskCanceledException` correctly

**Impact**: Exception handling in callers will receive wrapped `AggregateException` instead of `HyperliquidApiException` or `RateLimitException`, breaking error handling further up the stack.

**Recommendation**: Use `async/await` instead:
`csharp
public async Task<IReadOnlyList<CandleSnapshotDto>> GetCandleSnapshotsAsync(...)
{
    var result = await _restClient.GetCandleSnapshotsAsync(pair.Base, timeframe, startTime, endTime, cancellationToken);
    return result;
}
`

---

### C3: `GetFundingRatesAsync` throws `NotSupportedException` despite implementing the interface

**File**: `HyperliquidHistoricalDataClient.cs` L39-46

`csharp
public Task<IReadOnlyList<FundingRateDto>> GetFundingRatesAsync(...)
{
    throw new NotSupportedException(""Hyperliquid funding-rate history is not implemented..."");
}
`

This violates the Liskov Substitution Principle. Any consumer calling `IExchangeHistoricalDataClient.GetFundingRatesAsync` through polymorphic dispatch will crash at runtime with no indication during compilation.

**Impact**: Calling code must defensively check exchange type before calling this method, undermining the abstraction.

**Recommendation**: Either implement it (Hyperliquid does expose funding rate history via the `fundingHistory` info endpoint) or add a capability check to `IExchangeCapabilities` (e.g., `SupportsFundingRateHistory`) so callers can check before calling.

---

## MAJOR Findings

### M1: WebSocket clients lack automatic reconnection

**Files**: `HyperliquidWebSocketClient.cs`, `HyperliquidUserEventClient.cs`

Both WebSocket clients exit `ReceiveLoopAsync` on any `WebSocketException` or close frame. They notify `Disconnected` state but do not attempt reconnection. The market data client and user event client simply stop receiving data silently.

**Impact**: A transient network disruption will permanently stop trade data and user event streaming until the entire process is restarted. In a trading system, this means missing fills, order updates, and market data — potentially leading to unmanaged open positions.

**Recommendation**: Implement exponential backoff reconnection in `ReceiveLoopAsync`, or add a supervisor/watchdog that monitors connection state and reconnects. Consider a circuit-breaker pattern for repeated failures.

---

### M2: Market order slippage calculation is fixed at 5% — no configurability or size awareness

**File**: `HyperliquidOrderService.cs` L74-76

`csharp
var slippagePrice = isBuy ? midPrice * 1.05m : midPrice * 0.95m;
`

A hard-coded 5% slippage allowance is applied to all market orders regardless of:
- Order size relative to order book depth
- Market volatility
- Asset liquidity (BTC vs DOGE have vastly different order books)

**Impact**: For large orders on illiquid assets, 5% may be insufficient and the order fills partially or not at all. For small orders on liquid assets, 5% is excessive and could result in poor execution prices if the exchange matches at the limit price rather than mid.

**Recommendation**: Make slippage configurable per-asset or per-strategy. Consider using a percentage range with asset-specific defaults.

---

### M3: `RoundToSignificantFigures` uses floating-point math on decimals

**File**: `HyperliquidOrderService.cs` L694-701

`csharp
private static decimal RoundToSignificantFigures(decimal value, int significantFigures)
{
    var scale = (decimal)Math.Pow(10, Math.Floor(Math.Log10((double)Math.Abs(value))) + 1 - significantFigures);
    return scale * Math.Round(value / scale);
}
`

The `(double)Math.Abs(value)` cast introduces floating-point precision loss. `Math.Log10` and `Math.Pow` operate on doubles and can produce incorrect results for edge-case decimal values. For prices like `0.00001234`, the double conversion may lose precision.

**Impact**: Potential incorrect price rounding, especially for low-value assets. Could result in order rejection by Hyperliquid or incorrect fill prices.

**Recommendation**: Implement significant figure rounding using pure decimal arithmetic:
`csharp
private static decimal RoundToSignificantFigures(decimal value, int sf)
{
    if (value == 0m) return 0m;
    var digits = (int)Math.Ceiling(Math.Log10((double)Math.Abs(value)));
    var scale = (int)Math.Pow(10, sf - digits);
    return Math.Round(value * scale) / scale;
}
`
Or better yet, use string-based manipulation to avoid all float conversions.

---

### M4: `ToWireDecimal` is duplicated across `HyperliquidEip712` and `HyperliquidOrderService`

**Files**: `HyperliquidEip712.cs` L155-160, `HyperliquidOrderService.cs` L554-559

Both files contain identical `ToWireDecimal` methods. This is a maintenance risk — if one is updated but not the other, order signing will produce incorrect hashes (action hash mismatch vs. submitted price).

**Impact**: A future divergence between the two copies would cause signature validation failures that are extremely hard to debug.

**Recommendation**: Extract to a single shared utility (e.g., `HyperliquidFormatting.ToWireDecimal`) used by both.

---

### M5: `HyperliquidWebSocketClient` does not implement `IDisposable`/`IAsyncDisposable` for ping task

**File**: `HyperliquidWebSocketClient.cs` L123

`csharp
_ = RunPingLoopAsync(cancellationToken);
`

The ping loop is fire-and-forget. If `ReceiveLoopAsync` exits (e.g., via cancellation), the ping loop continues running until the `cancellationToken` is cancelled. But the `CancellationToken` passed to `ReceiveLoopAsync` may not be the one controlling the ping loop's lifetime.

In the user event client, this is handled better with `CancellationTokenSource.CreateLinkedTokenSource`, but the market data client does not link tokens.

**Impact**: Orphaned ping tasks that accumulate over reconnection cycles, consuming thread pool resources.

**Recommendation**: Use the same linked CancellationTokenSource pattern as `HyperliquidUserEventClient`.

---

### M6: `PlaceCompanionTriggerOrdersAsync` swallows failures — user may be unaware of missing stop-loss

**File**: `HyperliquidOrderService.cs` L454-500

Companion trigger orders (stop-loss, take-profit) are placed after the main order succeeds.If they fail, the failure is captured as a warning string in `response.Detail`, but `response.Success` remains `true`.

**Impact**: A user places a market order with a stop-loss. The main order fills, but the stop-loss trigger fails silently. The user believes they have a stop-loss but they don't — leading to unmanaged downside risk.

**Recommendation**: At minimum, add a `Warnings` field to `PlaceOrderResponse` so the UI can prominently display that companion orders failed. Consider returning `Success = false` or a partial-success status when critical protective orders fail.

---

### M7: Hardcoded 8-asset support in `HyperliquidAssetMapper`

**File**: `HyperliquidAssetMapper.cs` L10-19

Only 8 coins are supported: BTC, ETH, SOL, DOGE, AVAX, ARB, LINK, OP. Hyperliquid lists 100+ perpetual markets.

**Impact**: Users cannot trade any asset outside the hardcoded 8. The `IsValidCoin` check will reject them and the `ToCoin` normalization may produce incorrect results for unlisted coins (e.g., `WIF-PERP` → `WIF` which then fails `IsValidCoin`).

**Recommendation**: Fetch supported assets dynamically from the `meta` endpoint at startup and cache them, rather than hardcoding. The metadata cache (`HyperliquidAssetMetadataCache`) already fetches this data — unify the approach.

---

## MINOR Findings

### m1: `HyperliquidAccountService._signer` is nullable but constructor takes non-nullable

**File**: `HyperliquidAccountService.cs` L14, L18

`csharp
private readonly IHyperliquidSigner? _signer;

public HyperliquidAccountService(
    IHyperliquidRestClient restClient,
    IHyperliquidSigner signer,  // non-nullable parameter
    ILogger<HyperliquidAccountService> logger)
`

The field is nullable (`IHyperliquidSigner?`) but the constructor parameter is non-nullable. This sends mixed signals about design intent.

**Recommendation**: If the signer is truly optional (read-only account queries without a wallet), make the constructor parameter nullable. If it's always required, remove the `?` from the field.

---

### m2: `ParseDecimal` inconsistency between `HyperliquidRestClient` and `HyperliquidAccountService`

**Files**: `HyperliquidRestClient.cs` (throws `FormatException`), `HyperliquidAccountService.cs` (returns `0m`)

Two different `ParseDecimal` implementations:
- `HyperliquidRestClient.ParseDecimal` throws `FormatException` if parsing fails
- `HyperliquidAccountService.ParseDecimal` silently returns `0m`

**Impact**: A malformed price from the exchange causes a crash in one code path and silent incorrect data in another.

**Recommendation**: Standardize to a single `ParseDecimal` utility. For financial data, `0m` as a default is dangerous — a zero mark price or zero funding rate can cause incorrect PnL / margin calculations.

---

### m3: `MapOrderSide` is duplicated across 3 files

**Files**: `HyperliquidRestClient.cs`, `HyperliquidAccountService.cs`, `HyperliquidUserEventClient.cs`

The same `B` → `Buy`, `A` → `Sell` mapping appears in three separate files.

**Recommendation**: Extract to a shared static helper.

---

### m4: Wallet address logged at INFO level in `MutableSignerProvider.Configure`

**File**: `MutableSignerProvider.cs` L68

`csharp
_logger.LogInformation(""Wallet configured: Address={WalletAddress}"", signer.WalletAddress);
`

While wallet addresses are public information on Ethereum, logging them at INFO level means they appear in all default log outputs. Combined with any log aggregation, this creates a data correlation risk.

**Recommendation**: Log at DEBUG level, or log only the last 6 characters.

---

### m5: No request timeout on WebSocket `ConnectAsync`

**Files**: `HyperliquidWebSocketClient.cs` L62, `HyperliquidUserEventClient.cs` L48

`csharp
await _webSocket.ConnectAsync(uri, cancellationToken);
`

If the Hyperliquid WebSocket endpoint is unreachable, the connection attempt may hang indefinitely until the caller's `CancellationToken` fires. There's no explicit connect timeout.

**Recommendation**: Use a `CancellationTokenSource` with a reasonable timeout (e.g., 15 seconds):
`csharp
using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
cts.CancelAfter(TimeSpan.FromSeconds(15));
await _webSocket.ConnectAsync(uri, cts.Token);
`

---

### m6: `ReceiveBufferSize` of 4096 may be too small for batch messages

**Files**: `HyperliquidWebSocketClient.cs` L19, `HyperliquidUserEventClient.cs` L15

While the code correctly handles multi-frame messages via the `MemoryStream` accumulation loop, a 4096-byte buffer increases the number of `ReceiveAsync` calls needed for large messages (batch fills can be 10-50KB). This adds latency to message processing.

**Recommendation**: Increase to 8192 or 16384 for lower per-message overhead.

---

### m7: `HyperliquidCapabilities` hardcodes `SupportedTimeframes` from `HyperliquidAssetMapper`

**File**: `HyperliquidCapabilities.cs` L18

`csharp
SupportedTimeframes: new HashSet<string>(HyperliquidAssetMapper.GetSupportedTimeframes(), StringComparer.OrdinalIgnoreCase)
`

This couples the capability descriptor to the static hardcoded timeframes in `HyperliquidAssetMapper`. Hyperliquid actually supports additional timeframes (1m, 3m, 30m, 2h, 8h, 12h, 1d, 1w, 1M) that are excluded.

**Recommendation**: Either expand the supported timeframes in `HyperliquidAssetMapper` or decouple capabilities from the mapper.

---

## INFO Findings

### i1: Clean adapter pattern with proper abstraction boundaries

The adapter pattern (`HyperliquidAccountAdapter` wrapping `IHyperliquidAccountService`, `HyperliquidHistoricalDataClient` wrapping `IHyperliquidRestClient`) cleanly separates the exchange-specific implementation from the multi-exchange abstraction layer. Dependency direction is correct: Infrastructure → Application (interfaces).

### i2: EIP-712 implementation is thorough and well-tested

`HyperliquidEip712` implements both Nethereum-based and manual hash computation paths, with dedicated tests verifying byte-for-byte compatibility with Python SDK output. The manual implementation (`ComputeEip712Hash`) is a good defensive measure against Nethereum breaking changes.

### i3: MsgPack serialization handles compact encoding correctly

The custom `SerializeActionMsgPack` method explicitly handles `int` compaction (fixint for small values) to match Python's `msgpack.packb()` output. This is critical for action hash matching and is a subtle correctness detail that's handled well.

### i4: Case-sensitive JSON deserialization is correctly configured

`HyperliquidRestClient` uses `PropertyNameCaseInsensitive = false` to handle Hyperliquid's API which uses both lowercase `t` (timestamp) and uppercase `T` (time-in-force) in candle responses. Good defensive measure against `JsonException` at runtime.

### i5: Thread-safe handler management in `HyperliquidUserEventClient`

The `_handlerLock` with snapshot pattern (`GetHandlerSnapshot`) correctly prevents concurrent modification issues while allowing handlers to execute without holding the lock. This is a well-implemented observer pattern.

### i6: The `ExchangeResponseConverter` correctly handles Hyperliquid's polymorphic response field

The custom `JsonConverter` for `HyperliquidExchangeResponse.Response` properly handles both string error responses and object success responses. This is a common source of deserialization bugs with exchange APIs.

---

## Architectural Assessment

### Strengths
1. **Clean abstraction layer**: 7 well-defined exchange interfaces enable multi-exchange support
2. **Proper DI patterns**: Scoped (API) vs Singleton (Worker) lifetime management is correct for tenant isolation
3. **Keyed DI**: Runtime exchange resolution via keyed services is elegant
4. **Signing boundary**: Private key only available on Worker; API delegates signing operations
5. **Defensive JSON parsing**: `TryGetProperty` throughout the account service prevents crashes on unexpected API responses
6. **Parallel data fetching**: `Task.WhenAll` for independent API calls in `GetPositionsAsync`

### Weaknesses
1. **No circuit-breaker or retry policy** on the primary HTTP client (except the HttpClient factory retry via Polly — verify this is configured)
2. **No reconnection logic** for WebSocket clients
3. **Hardcoded asset/timeframe lists** instead of dynamic discovery
4. **Companion trigger orders can fail silently** — dangerous for a trading system
5. **Inconsistent error handling** across parsing utilities (throw vs. return 0)
6. **DI registration broken** for `IExchangeSymbolMapper` (static class)

### Security Assessment
- **Private key handling**: Good — `HyperliquidSigner` validates key format, `MutableSignerProvider` is thread-safe with runtime configuration. Private key never logged.
- **No key caching in logs**: Confirmed — signing operations log wallet address (public) but never the private key
- **Input validation**: Asset names, order IDs, and timeframes are validated at entry points
- **No injection risks**: All exchange API calls use typed objects, not string concatenation
- **Rate limit awareness**: `RateLimitException` with `RetryAfter` extraction is properly implemented

---

## Summary

| Severity | Count | Key Themes |
|----------|-------|------------|
| CRITICAL | 3 | Broken DI, `ContinueWith` anti-pattern, LSP violation |
| MAJOR | 7 | No WS reconnection, hardcoded assets, sig-fig rounding, silent failures |
| MINOR | 7 | Duplication, nullable inconsistency, log levels, buffer sizing |
| INFO | 6 | Good adapter pattern, thorough EIP-712, defensive parsing |

**Overall**: The Hyperliquid integration is architecturally sound with clean abstraction boundaries, but has several issues that need attention before production use. The CRITICAL findings (C1, C2, C3) should be addressed immediately. The MAJOR findings around WebSocket reconnection (M1) and silent companion order failures (M6) are high-priority for a trading system where reliability directly impacts financial outcomes.
