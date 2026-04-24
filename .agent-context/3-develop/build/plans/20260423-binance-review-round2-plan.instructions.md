---
applyTo: ".agent-context/3-develop/build/changes/20260423-binance-review-round2-changes.md"
currentAgent: "Plan Reviewer"
agentStartedAt: "2026-04-23T21:37:13Z"
status: "plan-in-review"
lastUpdated: "2026-04-23T21:37:13Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: Binance Integration Review Round 2 Fixes

## Overview

Fix the remaining CRITICAL, MAJOR, and MINOR findings from the second Binance integration tribunal review (2026-04-23). These findings were identified after the first round of fixes (20260422 plan) was completed.

## PBI Details

The second tribunal review identified 4 findings that represent real trading safety and correctness risks:

1. **CRITICAL — Cancel-after-restart leaves orphaned orders on Binance.** The in-memory `_orderAssetMap` (`ConcurrentDictionary`) is not persisted. After process restart, `CancelOrderAsync(orderId)` throws `DomainException` for every pre-restart order. Upstream callers (`TriggerOrderManager.CancelTriggerAsync`, `FillProcessor.CancelCounterpartOrderAsync`) catch all exceptions as warnings, so stop-loss/take-profit counterpart orders silently remain live on Binance and can execute unexpectedly. Additionally, Binance throws while Hyperliquid silently returns — a contract divergence.

2. **MAJOR — Max leverage is hardcoded, not exchange-authoritative.** `BinanceExchangeInfoCache.MaxLeverageByAsset` only maps BTC (125x) and ETH (125x); all other assets fall back to 25x. Binance exposes authoritative leverage brackets via `GET /fapi/v1/leverageBracket` (authenticated), but this endpoint is not wired up. The system reports incorrect ceilings for SOL, DOGE, AVAX, ARB, LINK, and OP.

3. **MINOR — Sequential fills retrieval scales linearly with asset count.** `BinanceAccountAdapter.GetRecentFillsAsync` makes N sequential authenticated HTTP calls (currently 8 assets, limit 100 each) with no cursor/pagination. Latency grows linearly as assets are added.

4. **MINOR — Order normalization assumes power-of-ten step/tick increments.** The cache reduces `stepSize`/`tickSize` to decimal-place counts, and the engine truncates via `(decimal)Math.Pow(10, n)`. This is only correct while Binance filters remain pure power-of-ten increments. A non-power-of-ten filter would pass local validation but get rejected by Binance.

### Acceptance Criteria

- After process restart, cancelling pre-restart orders works correctly (no orphaned orders, no thrown DomainException that gets swallowed)
- Cancel behaviour is consistent between Binance and Hyperliquid implementations of `IExecutionEngine`
- Max leverage values for all 8 supported assets are fetched from Binance's authoritative `leverageBracket` endpoint
- `BinanceExchangeSymbolMetadata.MaxLeverage` reflects real exchange limits, not hardcoded values
- Fills retrieval uses bounded parallelism to reduce latency while respecting rate limits
- Order normalization uses raw step/tick size values and modular arithmetic, not decimal-count truncation via `Math.Pow`
- All existing tests continue to pass
- New unit tests cover the changed behavior
- Solution builds without errors

## Objectives

- Eliminate the orphaned-order risk after process restart (trading safety)
- Bring max leverage metadata in line with exchange-authoritative values
- Improve fills retrieval performance while respecting rate limits
- Make order normalization structurally correct for arbitrary step/tick increments

### Discovery References

- Tribunal review: `.agent-context/1-discover/code-review/binance-integration-review.md`
- Previous plan: `.agent-context/3-develop/build/plans/20260422-binance-integration-fixes-plan.instructions.md`
- Knowledge file 23: `.agent-context/0-knowledge/23-binance-integration.md`
- Knowledge file 38: `.agent-context/0-knowledge/38-exchange-abstraction-architecture.md`

### Project Patterns

- `src/TradePilot.Infrastructure/Services/LiveExecutionEngine.cs` — Hyperliquid cancel pattern (silent return on missing mapping)
- `src/TradePilot.Infrastructure/Binance/BinanceExecutionEngine.cs` — Primary fix target
- `src/TradePilot.Infrastructure/Binance/BinanceExchangeInfoCache.cs` — Leverage and normalization fix target
- `src/TradePilot.Infrastructure/Binance/BinanceAccountAdapter.cs` — Fills parallelism fix target
- `src/TradePilot.Infrastructure/Services/BinanceFuturesAuthClient.cs` — New leverage bracket endpoint
- `src/TradePilot.Application/Abstractions/Services/IBinanceFuturesAuthClient.cs` — Interface extension
- `src/TradePilot.Application/Abstractions/Services/IBinanceExchangeInfoCache.cs` — Metadata record change
- `tests/TradePilot.Infrastructure.Tests/Binance/BinanceExecutionEngineTests.cs` — Test updates
- `tests/TradePilot.Infrastructure.Tests/Binance/BinanceExchangeInfoCacheTests.cs` — Test updates

### [ ] Phase 1: Cancel-After-Restart — Rehydrate Order Map from Exchange State

**Complexity**: High | **Risk**: High

This phase eliminates the orphaned-order risk by (a) rehydrating the in-memory order-asset map on first use from Binance open orders and (b) aligning the CancelOrderAsync contract with Hyperliquid's behavior (log warning + silent return instead of throwing).

- [ ] Task 1.1: Add order map rehydration from Binance open orders
  - Details: .agent-context/3-develop/build/plans/details/20260423-binance-review-round2-phase-01-details.md#task-11-add-order-map-rehydration
  - Files: `BinanceExecutionEngine.cs`
  - **What**: Add a `RehydrateOrderMapAsync` private method that calls `_authClient.GetOpenOrdersAsync()` (no symbol filter = all open orders), iterates result, and populates `_orderAssetMap[orderId] = asset` for every open order. Add a `bool _rehydrated` flag and a `SemaphoreSlim(1,1)` to ensure rehydration runs once on first cancel/query.
  - **Why**: After process restart, the engine can discover all open orders from Binance and rebuild the map. This means `CancelOrderAsync(orderId)` can resolve the asset for any pre-restart order.
  - **Pattern**: The rehydration should be lazy (triggered on first `CancelOrderAsync(orderId)` call where the map misses), not eager at construction, because the engine may be constructed before credentials are available.

- [ ] Task 1.2: Align CancelOrderAsync contract — log warning + return instead of throw
  - Details: .agent-context/3-develop/build/plans/details/20260423-binance-review-round2-phase-01-details.md#task-12-align-cancel-contract
  - Files: `BinanceExecutionEngine.cs`
  - **What**: Change `CancelOrderAsync(orderId)` to: (1) try the in-memory map, (2) if miss, call `RehydrateOrderMapAsync`, (3) retry the map, (4) if still missing, `_logger.LogWarning(...)` and `return` — matching `LiveExecutionEngine` behavior. Remove the `throw new DomainException(...)`.
  - **Why**: Aligns the `IExecutionEngine` contract between Binance and Hyperliquid. Callers already catch exceptions as warnings, so the current throw is swallowed anyway — but the silent-return pattern is cleaner and makes the behavior exchange-independent.
  - **Edge case**: If `RehydrateOrderMapAsync` fails (network error), catch and log, then fall back to the warning+return path. Never let infrastructure failures propagate from a best-effort cancel.

- [ ] Task 1.3: Update unit tests for cancel behavior change
  - Details: .agent-context/3-develop/build/plans/details/20260423-binance-review-round2-phase-01-details.md#task-13-update-cancel-tests
  - Files: `BinanceExecutionEngineTests.cs`
  - **What**: Update `GivenUnknownOrderId_WhenCancelOrderAsync_ThenThrowsDomainException` → rename to `GivenUnknownOrderId_WhenCancelOrderAsync_ThenLogsWarningAndReturns` and assert no exception + verify `GetOpenOrdersAsync` rehydration was called. Add test: `GivenRestartedProcess_WhenCancelOrderForPreRestartOrder_ThenRehydratesAndCancels` — mock `GetOpenOrdersAsync` to return the order, verify `CancelOrderAsync` succeeds. Add test: `GivenRehydrationFailure_WhenCancelOrderAsync_ThenLogsWarningAndReturns`.

- [ ] Task 1.4: Build and verify all tests pass
  - Details: .agent-context/3-develop/build/plans/details/20260423-binance-review-round2-phase-01-details.md#task-14-build-and-verify

### [ ] Phase 2: Exchange-Authoritative Max Leverage via Leverage Bracket API

**Complexity**: Medium | **Risk**: Medium

Replace the hardcoded `MaxLeverageByAsset` dictionary with live data from Binance's `GET /fapi/v1/leverageBracket` authenticated endpoint.

- [ ] Task 2.1: Add leverage bracket response models and interface method
  - Details: .agent-context/3-develop/build/plans/details/20260423-binance-review-round2-phase-02-details.md#task-21-add-leverage-bracket-models
  - Files: `IBinanceFuturesAuthClient.cs`, `BinanceFuturesAuthClient.cs`
  - **What**: Add `Task<IReadOnlyList<BinanceLeverageBracketResponse>> GetLeverageBracketsAsync(CancellationToken)` to `IBinanceFuturesAuthClient`. Add response models:
    ```csharp
    public sealed class BinanceLeverageBracketResponse
    {
        public string Symbol { get; init; } = string.Empty;
        public IReadOnlyList<BinanceLeverageBracket> Brackets { get; init; } = [];
    }

    public sealed class BinanceLeverageBracket
    {
        public int Bracket { get; init; }
        public int InitialLeverage { get; init; }
        public decimal NotionalCap { get; init; }
        public decimal NotionalFloor { get; init; }
        public decimal MaintMarginRatio { get; init; }
    }
    ```
  - **Endpoint**: `GET /fapi/v1/leverageBracket` — authenticated, returns all symbols when no symbol param. The first bracket (bracket 1) has the highest `InitialLeverage` which is the max leverage for that symbol.
  - Implement in `BinanceFuturesAuthClient` using existing `SendReadOnlyListAsync` pattern.

- [ ] Task 2.2: Integrate leverage brackets into BinanceExchangeInfoCache
  - Details: .agent-context/3-develop/build/plans/details/20260423-binance-review-round2-phase-02-details.md#task-22-integrate-leverage-brackets
  - Files: `BinanceExchangeInfoCache.cs`, `IBinanceExchangeInfoCache.cs`
  - **What**: Inject `IBinanceFuturesAuthClient` into `BinanceExchangeInfoCache`. During `EnsureCacheAsync`, after fetching `exchangeInfo`, also call `GetLeverageBracketsAsync()`. For each supported symbol, extract `Brackets[0].InitialLeverage` (the max leverage). Use this value for `BinanceExchangeSymbolMetadata.MaxLeverage` instead of the hardcoded `MaxLeverageByAsset` dictionary. Remove the static `MaxLeverageByAsset` dictionary. Fall back to 25 if the bracket data is missing for a symbol.
  - **Error handling**: If `GetLeverageBracketsAsync` fails (e.g., no credentials for public cache use), log a warning and fall back to a conservative default (25x for all). The cache should not fail entirely just because leverage brackets aren't available.
  - **Note**: The `BinanceExchangeInfoCache` currently only depends on `IHttpClientFactory` (public API). Adding `IBinanceFuturesAuthClient` (authenticated) means the cache can only fetch leverage brackets when credentials are available. Consider making the leverage bracket fetch optional — the cache should still work for public-only consumers (e.g., historical data client).

- [ ] Task 2.3: Add unit tests for leverage bracket integration
  - Details: .agent-context/3-develop/build/plans/details/20260423-binance-review-round2-phase-02-details.md#task-23-add-leverage-bracket-tests
  - Files: `BinanceExchangeInfoCacheTests.cs`, `BinanceFuturesAuthClientTests.cs`
  - **What**: Test that cache returns exchange-authoritative max leverage. Test fallback when bracket endpoint fails. Test that bracket endpoint is deserialized correctly.

- [ ] Task 2.4: Build and verify all tests pass
  - Details: .agent-context/3-develop/build/plans/details/20260423-binance-review-round2-phase-02-details.md#task-24-build-and-verify

### [ ] Phase 3: Bounded-Parallel Fills Retrieval

**Complexity**: Low | **Risk**: Low

Replace sequential per-symbol fills fetching with bounded parallelism to reduce latency while respecting Binance rate limits.

- [ ] Task 3.1: Implement bounded-parallel fills fetching
  - Details: .agent-context/3-develop/build/plans/details/20260423-binance-review-round2-phase-03-details.md#task-31-implement-bounded-parallel-fills
  - Files: `BinanceAccountAdapter.cs`
  - **What**: Replace the `foreach` loop in `GetRecentFillsAsync` with `Parallel.ForEachAsync` using `MaxDegreeOfParallelism = 3`. Use a thread-safe collection (`ConcurrentBag<BinanceUserTradeSnapshot>`) for accumulation, then sort after completion.
  - **Why**: At 3 concurrent requests, 8 assets complete in ~3 round-trips instead of 8 sequential. Stays well within Binance's authenticated rate limit (2400 weight/min, each userTrades call is 5 weight).
  - **Alternative**: `SemaphoreSlim(3)` + `Task.WhenAll` is equally valid.

- [ ] Task 3.2: Update unit tests for parallel fills
  - Details: .agent-context/3-develop/build/plans/details/20260423-binance-review-round2-phase-03-details.md#task-32-update-fills-tests
  - Files: `BinanceAccountAdapterTests.cs`
  - **What**: Update existing fills tests. Add a test verifying that when no pair filter is provided, all supported assets are fetched. Add a test verifying that the `maxConcurrency == 1` assertion (if it exists) is updated to allow parallelism.

- [ ] Task 3.3: Build and verify all tests pass
  - Details: .agent-context/3-develop/build/plans/details/20260423-binance-review-round2-phase-03-details.md#task-33-build-and-verify

### [ ] Phase 4: Structurally Correct Order Normalization

**Complexity**: Medium | **Risk**: Medium

Replace decimal-count-based normalization with raw step/tick size modular arithmetic that works for arbitrary increments.

- [ ] Task 4.1: Store raw step/tick size in metadata instead of decimal counts
  - Details: .agent-context/3-develop/build/plans/details/20260423-binance-review-round2-phase-04-details.md#task-41-store-raw-step-tick-size
  - Files: `IBinanceExchangeInfoCache.cs`, `BinanceExchangeInfoCache.cs`
  - **What**: Change `BinanceExchangeSymbolMetadata` from `int SizeDecimals, int PriceDecimals` to `decimal StepSize, decimal TickSize`. Parse the raw step/tick values from exchange info filters. Remove `GetDecimals` helper. Keep the integer decimal-count properties as computed values if needed downstream:
    ```csharp
    public sealed record BinanceExchangeSymbolMetadata(
        string Asset,
        string Symbol,
        decimal StepSize,
        decimal TickSize,
        int MaxLeverage)
    {
        public int SizeDecimals => CountDecimals(StepSize);
        public int PriceDecimals => CountDecimals(TickSize);
        private static int CountDecimals(decimal value) { ... }
    }
    ```
  - **Fallback**: If step/tick size is missing or zero, fall back to `0.001m` (size) and `0.01m` (price).

- [ ] Task 4.2: Update normalization methods to use modular arithmetic
  - Details: .agent-context/3-develop/build/plans/details/20260423-binance-review-round2-phase-04-details.md#task-42-update-normalization-methods
  - Files: `BinanceExecutionEngine.cs`
  - **What**: Change `NormalizeOrderSize(decimal size, int sizeDecimals)` to `NormalizeOrderSize(decimal size, decimal stepSize)`. Implement as `Math.Truncate(absoluteSize / stepSize) * stepSize` — this works for any step size, not just powers of ten. Same for `NormalizeOrderPrice(decimal price, decimal tickSize)` → `Math.Truncate(price / tickSize) * tickSize`. Update all call sites to pass `metadata.StepSize`/`metadata.TickSize`.
  - **Guard**: If stepSize/tickSize is zero, throw `ArgumentOutOfRangeException`.

- [ ] Task 4.3: Update all consumers of BinanceExchangeSymbolMetadata
  - Details: .agent-context/3-develop/build/plans/details/20260423-binance-review-round2-phase-04-details.md#task-43-update-consumers
  - Files: `BinanceSymbolMetadataProvider.cs`, `OrdersController.cs` (if applicable), test files
  - **What**: Any code that reads `SizeDecimals` or `PriceDecimals` from metadata — verify it still works via the computed properties. Update test fixtures to construct `BinanceExchangeSymbolMetadata` with the new `StepSize`/`TickSize` constructor.

- [ ] Task 4.4: Add unit tests for non-power-of-ten normalization
  - Details: .agent-context/3-develop/build/plans/details/20260423-binance-review-round2-phase-04-details.md#task-44-add-normalization-tests
  - Files: `BinanceExecutionEngineTests.cs`, `BinanceExchangeInfoCacheTests.cs`
  - **What**: Add tests for non-power-of-ten step sizes (e.g., stepSize = 0.025). Verify `NormalizeOrderSize(1.037, 0.025)` returns `1.025`. Verify power-of-ten case still works. Add test for zero step-size guard.

- [ ] Task 4.5: Build and verify all tests pass
  - Details: .agent-context/3-develop/build/plans/details/20260423-binance-review-round2-phase-04-details.md#task-45-build-and-verify

## Scoping Summary

| Phase | Complexity | Risk | Finding |
|-------|-----------|------|---------|
| Phase 1: Cancel-After-Restart Rehydration | High | High | CRITICAL |
| Phase 2: Exchange-Authoritative Leverage | Medium | Medium | MAJOR |
| Phase 3: Bounded-Parallel Fills | Low | Low | MINOR |
| Phase 4: Structurally Correct Normalization | Medium | Medium | MINOR |
| **Total** | **Medium-High** | **Medium** | |

### Scoping Notes

- Phase 1 is highest risk: modifying the cancel code path that protects positions. Must be tested thoroughly with rehydration success, rehydration failure, and mixed pre/post-restart orders.
- Phase 2 introduces an authenticated dependency into `BinanceExchangeInfoCache` which currently only uses public APIs. The leverage bracket fetch must be optional/fallback-safe.
- Phase 3 is low risk: bounded parallelism with `MaxDegreeOfParallelism = 3` is conservative. The test that asserts sequential behavior (`maxConcurrency == 1`) needs updating.
- Phase 4 changes the `BinanceExchangeSymbolMetadata` record shape. All test fixtures constructing this record need updates. The computed `SizeDecimals`/`PriceDecimals` properties maintain backward compatibility for any downstream readers.
- Phases are independent and can be implemented in any order, but Phase 1 (CRITICAL) should be prioritized.

## Dependencies

- .NET 10 SDK
- Moq + FluentAssertions ≤ v6 (test dependencies, already in place)
- MSTest (test framework, already in place)
- Binance `GET /fapi/v1/leverageBracket` authenticated endpoint (Phase 2)

## Success Criteria

- Solution builds without errors (`dotnet build TradePilot.sln`)
- All existing tests continue to pass
- New and updated unit tests pass
- Cancel-after-restart scenario works correctly (rehydration from exchange)
- Max leverage values are exchange-authoritative for all 8 supported assets
- Fills retrieval completes in ~3 round-trips instead of 8
- Order normalization handles non-power-of-ten increments correctly
- No new warnings introduced in modified files

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-04-23T19:55:45Z | 2026-04-23T21:36:25Z |
| Plan Reviewer | plan-in-review | 2026-04-23T21:37:13Z | - |
