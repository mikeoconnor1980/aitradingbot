# Tribunal Verdict — Binance Integration Review

**Date**: 2026-04-21T09:51:22Z
**Target**: `src/TradePilot.Infrastructure/Binance/` and related interfaces
**Type**: Code Review
**Models**: Claude Opus 4.6 (Code Reviewer), Claude Opus 4.6 (Explore), Claude Opus 4.6 (Review 3)

---

## Consensus (all 3 agree)

| # | Severity | Finding | Detail |
|---|----------|---------|--------|
| 1 | CRITICAL | **`_orderAssetMap` in-memory state → silent cancel failures** | `BinanceExecutionEngine` stores order-to-asset mappings in a `ConcurrentDictionary` that is lost on process restart, deployment, or scope change. The single-arg `CancelOrderAsync(orderId)` silently returns without cancelling when the orderId isn't in the map. Orphaned orders remain on the exchange — direct financial risk. |
| 2 | MINOR | **`ParseDecimal` duplicated across 3+ classes** | Identical `ParseDecimal` helper in `BinanceAccountAdapter`, `BinanceExecutionEngine`, and `BinanceMarketMetadataProvider`. Should be a shared static utility. |

## Majority (2 of 3 agree)

| # | Severity | Finding | Raised by | Not raised by | Detail |
|---|----------|---------|-----------|---------------|--------|
| 3 | CRITICAL | **Non-atomic `ModifyTriggerOrderAsync`** | Code Reviewer, Review 3 | Explore | Cancel-then-place sequence: if cancel succeeds but place fails (network error, rate limit), the position is left without stop-loss/take-profit protection. No compensation logic exists. |
| 4 | CRITICAL | **No order size/price normalization** | Code Reviewer, Review 3 | Explore | `PlaceOrderAsync` sends raw `order.Size` to Binance without rounding to symbol's `stepSize`/`tickSize`. `BinanceExchangeInfoCache` stores precision data but `BinanceExecutionEngine` never consults it. Will produce Binance `-1111 QUANTITY_NOT_VALID` rejections. Hyperliquid's `LiveExecutionEngine` has explicit `NormalizeOrderSize()` logic. |
| 5 | CRITICAL | **`SemaphoreSlim` lifecycle bug in `BinanceExchangeInfoCache`** | Code Reviewer, Explore | Review 3 | If `WaitAsync()` is cancelled via `CancellationToken`, the `finally` block calls `Release()` on a lock never acquired, corrupting semaphore state. Subsequent threads deadlock permanently. Also: no `IDisposable` implementation. |
| 6 | MAJOR | **`SetLeverageAsync` ignores `isIsolated` parameter** | Code Reviewer, Review 3 | Explore | Binance requires explicit `/fapi/v1/marginType` call to switch ISOLATED/CROSSED margin. The `isIsolated` flag is silently dropped. If a strategy requests isolated margin but position is in cross mode, other positions face unexpected liquidation risk. |
| 7 | MAJOR | **`SupportedAssets` inconsistency: 2 assets vs 8** | Code Reviewer, Review 3 | Explore | `BinanceCapabilities` and `BinanceAccountAdapter` hardcode BTC/ETH. `BinanceAssetMapper` maps 8 assets (adds SOL, DOGE, AVAX, ARB, LINK, OP). Positions for mapped-but-unsupported assets are invisible through the account adapter — silent data loss. |
| 8 | MAJOR | **`ParseDecimal` returns `0m` on parse failure — silent data corruption** | Code Reviewer, Explore | Review 3 | All three account/market classes use `decimal.TryParse` returning `0m` on failure. Malformed API responses silently produce zero equity/prices. The fallback chain (`if (equity == 0m)`) cannot distinguish "zero balance" from "parse error". May trigger false liquidation signals. |
| 9 | MINOR | **`_orderAssetMap` grows unbounded** | Explore, Review 3 | Code Reviewer | Filled orders are never removed from the map. Over weeks of 24/7 operation, the dictionary accumulates thousands of stale entries — slow memory leak. |
| 10 | MINOR | **`BinanceKline` minimum element check ignores fields 9-11** | Explore, Review 3 | Code Reviewer | Checks `< 9` but Binance returns 12 elements. Quote volume, taker volumes are silently dropped. If API format changes, fields could shift. |

## Unique Insights (1 model only)

| # | Severity | Finding | Model | Detail |
|---|----------|---------|-------|--------|
| 11 | MAJOR | **`RateLimitException` inherits from `HyperliquidApiException`** | Code Reviewer | Cross-exchange coupling: any `catch (HyperliquidApiException)` also catches Binance rate limits. Misleading stack traces and incorrect error routing. |
| 12 | MAJOR | **`IBinanceExchangeInfoCache` in Application layer, injected into `OrdersController`** | Code Reviewer | Exchange-specific interface leaks into Application and API layers. Controllers should use exchange-agnostic abstractions resolved at runtime. |
| 13 | MAJOR | **`binance-public` HTTP client has no resilience handler** | Code Reviewer | The named client used by `BinanceExchangeInfoCache` and `BinanceMarketMetadataProvider` has no retry/circuit-breaker policy, unlike the typed auth clients. A transient failure immediately crashes the cache refresh. |
| 14 | MAJOR | **`long.Parse` on external orderId throws `FormatException`** | Code Reviewer | `CancelOrderAsync` uses `long.Parse(orderId)` — a corrupted or non-numeric orderId produces an unhandled exception instead of a meaningful domain error. |
| 15 | MAJOR | **Cache expiry uses wall clock (`DateTimeOffset.UtcNow`)** | Explore | NTP corrections or clock adjustments can cause rapid re-fetches or serve stale cache indefinitely. Should use monotonic `Stopwatch`. |
| 16 | MAJOR | **Incomplete error type coverage in `BinanceFuturesAuthClient`** | Explore | Only 401, 429, 418, 451 are specifically handled. 403 (disabled key) and 5xx (transient) map to generic `DomainException`. No distinction between permanent and transient errors for retry decisions. |
| 17 | MAJOR | **`OpenInterest` hardcoded to `0m`** | Review 3 | Binance exposes open interest via `/fapi/v1/openInterest`, but the provider always returns zero. Strategies or AI context using OI for regime detection get incorrect data. Hyperliquid populates this correctly. |
| 18 | MAJOR | **`GetRecentFillsAsync` fires parallel requests with no rate-limit awareness** | Review 3 | When `pair` is null, parallel HTTP requests fire for every supported symbol. Safe with 2 assets but dangerous if the list grows to 8 (as the mapper suggests). |
| 19 | MINOR | **Duplicate `BinanceSigningHandler` in API and Worker** | Code Reviewer | Nearly identical HMAC signing logic in two projects. Security-critical code duplication increases maintenance risk. |
| 20 | MINOR | **Leverage parsed as `int`, Binance can return float** | Explore | `ParseInt(position.Leverage)` fails silently for `"10.5"`, returning 0. Causes `marginUsed = 0` — incorrect risk calculations. |
| 21 | MINOR | **`NumberStyles.Number` rejects scientific notation in `BinanceFundingRate`** | Review 3 | If Binance returns `"1.5e-4"` for a funding rate, parsing throws. Other parsers in the codebase use `NumberStyles.Any`. |
| 22 | MINOR | **Rate limit `Retry-After` cap at 60s** | Explore | Resilience handler caps `MaxDelay` at 60s. If Binance returns `Retry-After: 3600` (1-hour ban), the handler retries at 60s, violating the ban and causing cascading IP bans. |
| 23 | MINOR | **`EnrichPositionsWithTriggerOrders` last-write-wins for TP/SL** | Review 3 | Multiple stop/take-profit orders for the same asset: last in iteration overwrites previous. Only one SL and one TP reported per position. |
| 24 | MINOR | **5s per-attempt timeout too short for batch kline fetches** | Review 3 | 1500-candle kline responses can be large. The 5s per-attempt timeout triggers unnecessary retries for slow but valid responses. |
| 25 | INFO | **No unit tests for `BinanceExecutionEngine`, `BinanceAccountAdapter`, `BinanceMarketMetadataProvider`** | Review 3 | Tests exist for mapper, REST client, and signing handler, but the adapter/engine/provider classes containing mapping logic have no direct tests. |
| 26 | INFO | **Capability flags (`SupportsPublicTradesStream` etc.) all `false`** | Code Reviewer | Binance Futures supports WebSocket streams but capabilities report `false`. May mislead consumers. Intentional for now but undocumented. |
| 27 | INFO | **Multi-tenancy threading model differs from Hyperliquid** | Explore | Binance `WorkerBinanceSigningHandler` uses `_credentialAccessor.GetActiveCredentialAsync()` per-request. If credentials rotate mid-flight under concurrency, signing may pick the wrong key. |

## Overall Assessment

The Binance integration has **four high-severity issues that pose direct financial risk**: (1) silent cancellation failures from ephemeral in-memory state, (2) non-atomic trigger order modification leaving positions unprotected, (3) missing order size normalization causing exchange rejections, and (4) a semaphore lifecycle bug that can deadlock the exchange info cache. Beyond these, the `SupportedAssets` fragmentation across three classes, the ignored `isIsolated` margin mode flag, and the silent `ParseDecimal` fallback to `0m` represent significant correctness gaps. Architecturally, exchange-specific types leak into the Application/API layers, and the `RateLimitException` inheritance from `HyperliquidApiException` creates cross-exchange coupling. The code follows good patterns established by the Hyperliquid integration (interface separation, DI registration, async patterns) but falls short on the precision handling, error escalation, and resilience policies that a live trading system requires. **This code should not trade real capital until the CRITICAL findings are resolved.**

**Confidence**: Medium — two of three reviewers converged on the top findings, but several MAJOR issues were raised by only one reviewer especially around resilience and error handling. Human review recommended for findings #15-18.

## Severity Summary

| Severity | Count |
|----------|-------|
| CRITICAL | 4 (consensus: 1, majority: 3) |
| MAJOR | 10 (majority: 3, unique: 7) |
| MINOR | 8 (majority: 2, unique: 6) |
| INFO | 3 (all unique) |

## Recommended Priority

1. **Immediate** (before any live trading): Fix #1 (silent cancel), #3 (non-atomic modify), #4 (size normalization), #5 (semaphore bug)
2. **Before GA**: Fix #6 (margin mode), #7 (asset set mismatch), #8 (ParseDecimal), #11 (exception hierarchy), #13 (resilience handler), #14 (orderId parsing)
3. **Post-GA / Tech Debt**: Address remaining MAJOR/MINOR findings, add unit test coverage (#25)
