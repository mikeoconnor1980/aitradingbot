---
applyTo: ".agent-context/3-develop/build/changes/20260422-binance-integration-fixes-changes.md"
currentAgent: "None"
agentStartedAt: "2026-04-23T07:31:43Z"
status: "complete"
lastUpdated: "2026-04-23T08:23:16Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: Binance Integration Fixes

## Overview

Fix all CRITICAL and MAJOR findings from the Binance integration tribunal review to make the integration production-safe for live trading.

## PBI Details

The Binance integration tribunal review (2026-04-21) identified 4 CRITICAL and 11 MAJOR findings across the Binance execution engine, account adapter, market metadata provider, exchange info cache, exception hierarchy, HTTP resilience, and API layer architecture. These findings represent direct financial risk (silent order cancellation, unprotected positions, exchange rejections) and correctness gaps (wrong asset counts, zero-fallback parsing, hardcoded open interest).

### Acceptance Criteria

- All 4 CRITICAL findings resolved: silent cancel (#1), non-atomic modify (#3), no normalization (#4), semaphore bug (#5)
- All 11 MAJOR findings resolved: margin mode (#6), asset fragmentation (#7), ParseDecimal (#8), exception hierarchy (#11), layer leak (#12), resilience handler (#13), orderId parsing (#14), wall-clock cache (#15), error coverage (#16), open interest (#17), parallel fills (#18)
- Unit tests added for BinanceExecutionEngine, BinanceAccountAdapter, BinanceMarketMetadataProvider, BinanceExchangeInfoCache
- All existing tests continue to pass
- Solution builds without errors

## Objectives

- Eliminate all financial-risk code paths in the Binance integration
- Bring Binance integration to parity with Hyperliquid patterns (normalization, error handling, resilience)
- Add comprehensive unit test coverage for previously untested adapter/engine/provider classes
- Fix exception hierarchy cross-exchange coupling
- Harden HTTP resilience for all Binance HTTP clients

### Discovery References

- Tribunal review: `.agent-context/1-discover/code-review/binance-integration-review.md`
- Knowledge file 23: `.agent-context/0-knowledge/23-binance-integration.md` (needs updating — says read-only but execution engine exists)
- Knowledge file 38: `.agent-context/0-knowledge/38-exchange-abstraction-architecture.md`

### Project Patterns

- `src/TradePilot.Infrastructure/Services/LiveExecutionEngine.cs` — Reference implementation for NormalizeOrderSize, NormalizeOrderPrice, SetLeverageAsync with margin mode
- `src/TradePilot.Infrastructure/Binance/BinanceExecutionEngine.cs` — Primary fix target
- `src/TradePilot.Infrastructure/Binance/BinanceAccountAdapter.cs` — Fix target for assets, parsing, fills
- `src/TradePilot.Infrastructure/Binance/BinanceMarketMetadataProvider.cs` — Fix target for OI, parsing
- `src/TradePilot.Infrastructure/Binance/BinanceExchangeInfoCache.cs` — Fix target for semaphore, cache
- `src/TradePilot.Infrastructure/Services/BinanceFuturesAuthClient.cs` — Fix target for error mapping, margin type
- `src/TradePilot.Application/Abstractions/Exceptions/RateLimitException.cs` — Exception hierarchy fix
- `tests/TradePilot.Api.Tests/Services/HyperliquidExecutionEngineTests.cs` — Test pattern reference
- `tests/TradePilot.Api.Tests/Services/HyperliquidAccountServiceTests.cs` — Test pattern reference
- `tests/TradePilot.Api.Tests/Infrastructure/FakeHttpMessageHandler.cs` — HTTP test double pattern

### [x] Phase 1: Foundation — Exception Hierarchy, Shared Parsing & Cache Hardening

**Complexity**: Medium | **Risk**: Medium

- [x] Task 1.1: Create ExchangeApiException base class and decouple RateLimitException (#11)
  - Details: .agent-context/3-develop/build/plans/details/20260422-binance-integration-fixes-phase-01-details.md#task-11-create-exchangeapiexception-and-decouple-ratelimitexception
  - Note: RateLimitException constructor signature changes — all `new RateLimitException(...)` call sites must be updated (exchangeStatusCode parameter added)

- [x] Task 1.2: Create shared BinanceParsing utility and fix ParseDecimal behavior (#8, #14)
  - Details: .agent-context/3-develop/build/plans/details/20260422-binance-integration-fixes-phase-01-details.md#task-12-create-shared-binanceparsing-utility

- [x] Task 1.3: Fix BinanceExchangeInfoCache: verify semaphore pattern, replace wall-clock cache with Stopwatch, add IDisposable (#5, #15)
  - Details: .agent-context/3-develop/build/plans/details/20260422-binance-integration-fixes-phase-01-details.md#task-13-fix-binanceexchangeinfocache-semaphore-and-cache-expiry
  - Note: Actual code places WaitAsync() before try block (correct pattern). Semaphore may only need verification — primary fixes are wall-clock → Stopwatch and IDisposable. Consider expanding MaxLeverageByAsset to all 8 supported assets.

- [x] Task 1.4: Fix BinanceFundingRate NumberStyles (#16 partial)
  - Details: .agent-context/3-develop/build/plans/details/20260422-binance-integration-fixes-phase-01-details.md#task-14-fix-binancefundingrate-numberstyles

- [x] Task 1.5: Add unit tests for Phase 1 changes
  - Details: .agent-context/3-develop/build/plans/details/20260422-binance-integration-fixes-phase-01-details.md#task-15-add-unit-tests-for-phase-1

- [x] Task 1.6: Build and verify all tests pass
  - Details: .agent-context/3-develop/build/plans/details/20260422-binance-integration-fixes-phase-01-details.md#task-16-build-and-verify

### [x] Phase 2: Execution Engine Safety — Normalization, Cancel, Modify & Margin

**Complexity**: High | **Risk**: High

- [x] Task 2.1: Add order size and price normalization (#4)
  - Details: .agent-context/3-develop/build/plans/details/20260422-binance-integration-fixes-phase-02-details.md#task-21-add-order-size-and-price-normalization

- [x] Task 2.2: Fix silent cancel failure to throw on missing asset mapping (#1)
  - Details: .agent-context/3-develop/build/plans/details/20260422-binance-integration-fixes-phase-02-details.md#task-22-fix-silent-cancel-failure

- [x] Task 2.3: Add compensation logic to ModifyTriggerOrderAsync (#3)
  - Details: .agent-context/3-develop/build/plans/details/20260422-binance-integration-fixes-phase-02-details.md#task-23-add-compensation-logic-to-modifytriggerorderasync

- [x] Task 2.4: Implement margin type switching for SetLeverageAsync (#6)
  - Details: .agent-context/3-develop/build/plans/details/20260422-binance-integration-fixes-phase-02-details.md#task-24-implement-margin-type-switching

- [x] Task 2.5: Add unit tests for BinanceExecutionEngine
  - Details: .agent-context/3-develop/build/plans/details/20260422-binance-integration-fixes-phase-02-details.md#task-25-add-unit-tests-for-binanceexecutionengine

- [x] Task 2.6: Build and verify all tests pass
  - Details: .agent-context/3-develop/build/plans/details/20260422-binance-integration-fixes-phase-02-details.md#task-26-build-and-verify

### [x] Phase 3: Account & Market Data Consistency

**Complexity**: Medium | **Risk**: Medium

- [x] Task 3.1: Unify SupportedAssets to single source of truth (#7)
  - Details: .agent-context/3-develop/build/plans/details/20260422-binance-integration-fixes-phase-03-details.md#task-31-unify-supportedassets

- [x] Task 3.2: Implement real OpenInterest fetching (#17)
  - Details: .agent-context/3-develop/build/plans/details/20260422-binance-integration-fixes-phase-03-details.md#task-32-implement-real-openinterest-fetching

- [x] Task 3.3: Add rate-limit-aware sequential fills fetching (#18)
  - Details: .agent-context/3-develop/build/plans/details/20260422-binance-integration-fixes-phase-03-details.md#task-33-rate-limit-aware-sequential-fills-fetching

- [x] Task 3.4: Replace ParseDecimal usage in AccountAdapter and MarketMetadataProvider
  - Details: .agent-context/3-develop/build/plans/details/20260422-binance-integration-fixes-phase-03-details.md#task-34-replace-parsedecimal-usage

- [x] Task 3.5: Add unit tests for BinanceAccountAdapter and BinanceMarketMetadataProvider
  - Details: .agent-context/3-develop/build/plans/details/20260422-binance-integration-fixes-phase-03-details.md#task-35-add-unit-tests

- [x] Task 3.6: Build and verify all tests pass
  - Details: .agent-context/3-develop/build/plans/details/20260422-binance-integration-fixes-phase-03-details.md#task-36-build-and-verify

### [x] Phase 4: Resilience & Error Handling

**Complexity**: Medium | **Risk**: Low

- [x] Task 4.1: Add resilience handler for binance-public named client (#13)
  - Details: .agent-context/3-develop/build/plans/details/20260422-binance-integration-fixes-phase-04-details.md#task-41-add-resilience-handler-for-binance-public

- [x] Task 4.2: Improve error type coverage in BinanceFuturesAuthClient (#16)
  - Details: .agent-context/3-develop/build/plans/details/20260422-binance-integration-fixes-phase-04-details.md#task-42-improve-error-type-coverage

- [x] Task 4.3: Add unit tests for error mapping improvements
  - Details: .agent-context/3-develop/build/plans/details/20260422-binance-integration-fixes-phase-04-details.md#task-43-add-unit-tests-for-error-mapping

- [x] Task 4.4: Build and verify all tests pass
  - Details: .agent-context/3-develop/build/plans/details/20260422-binance-integration-fixes-phase-04-details.md#task-44-build-and-verify

### [x] Phase 5: Architecture Cleanup — Exchange-Agnostic Symbol Metadata

**Complexity**: Medium | **Risk**: Medium

Note: `IExchangeSymbolMetadataProvider` (static exchange config — size/price decimals, max leverage) is intentionally separate from the existing `IExchangeMarketMetadataProvider` (runtime market data — OI, prices, funding rates).

- [x] Task 5.1: Create IExchangeSymbolMetadataProvider abstraction (#12)
  - Details: .agent-context/3-develop/build/plans/details/20260422-binance-integration-fixes-phase-05-details.md#task-51-create-iexchangesymbolmetadataprovider-abstraction

- [x] Task 5.2: Implement Binance and Hyperliquid adapters
  - Details: .agent-context/3-develop/build/plans/details/20260422-binance-integration-fixes-phase-05-details.md#task-52-implement-exchange-adapters

- [x] Task 5.3: Update OrdersController to use exchange-agnostic abstraction
  - Details: .agent-context/3-develop/build/plans/details/20260422-binance-integration-fixes-phase-05-details.md#task-53-update-orderscontroller

- [x] Task 5.4: Register keyed services in DI
  - Details: .agent-context/3-develop/build/plans/details/20260422-binance-integration-fixes-phase-05-details.md#task-54-register-keyed-services

- [x] Task 5.5: Add unit tests for new abstraction
  - Details: .agent-context/3-develop/build/plans/details/20260422-binance-integration-fixes-phase-05-details.md#task-55-add-unit-tests

- [x] Task 5.6: Build and verify all tests pass
  - Details: .agent-context/3-develop/build/plans/details/20260422-binance-integration-fixes-phase-05-details.md#task-56-build-and-verify

## Scoping Summary

| Phase | Complexity | Risk |
|-------|-----------|------|
| Phase 1: Foundation — Exception Hierarchy, Shared Parsing & Cache | Medium | Medium |
| Phase 2: Execution Engine Safety | High | High |
| Phase 3: Account & Market Data Consistency | Medium | Medium |
| Phase 4: Resilience & Error Handling | Medium | Low |
| Phase 5: Architecture Cleanup | Medium | Medium |
| **Total** | **High** | **Medium** |

### Scoping Notes

- Phase 2 is highest risk: modifying active order flow (normalization, cancel behavior, trigger order compensation)
- Exception hierarchy change (Phase 1) must be validated against all catch blocks referencing `HyperliquidApiException`
- `IBinanceFuturesAuthClient` interface changes span Phase 2 (SetMarginType) — coordinated with implementation
- Open Interest (#17) requires new HTTP call to Binance public API — uses existing `binance-public` client
- Layer leak fix (Phase 5) touches OrdersController and DI wiring — potential for integration test regression
- Knowledge doc `.agent-context/0-knowledge/23-binance-integration.md` should be updated post-implementation (says read-only but execution engine exists)

## Dependencies

- .NET 10 SDK
- Microsoft.Extensions.Http.Resilience (already referenced)
- Moq + FluentAssertions ≤ v6 (test dependencies, already in place)
- MSTest (test framework, already in place)

## Success Criteria

- All 15 CRITICAL+MAJOR findings addressed with code changes
- Solution builds without errors (`dotnet build TradePilot.sln`)
- All existing tests continue to pass
- New unit tests added for BinanceExecutionEngine, BinanceAccountAdapter, BinanceMarketMetadataProvider, BinanceExchangeInfoCache
- No new warnings introduced in modified files
- Exception hierarchy is exchange-agnostic (RateLimitException no longer inherits HyperliquidApiException)
- binance-public HTTP client has resilience handler in both API and Worker
- Post-implementation: update `.agent-context/0-knowledge/23-binance-integration.md` to reflect execution engine and expanded capabilities

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| 3-Develop: 3 Reviewer | complete | 2026-04-23T07:31:43Z | 2026-04-23T08:23:16Z |
| 3-Develop: 2 Implementer | implemented | 2026-04-22T21:23:01Z | 2026-04-23T07:27:45Z |
| Plan Reviewer | reviewed | 2026-04-22T20:36:02Z | 2026-04-22T20:41:44Z |
| Implementation Planner | planned | 2026-04-22T20:21:08Z | 2026-04-22T20:35:06Z |
