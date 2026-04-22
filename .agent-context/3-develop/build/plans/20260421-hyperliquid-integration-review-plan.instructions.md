---
applyTo: ".agent-context/3-develop/build/changes/20260421-hyperliquid-integration-review-changes.md"
currentAgent: "Implementation Reviewer"
agentStartedAt: "2026-04-21T19:38:24Z"
status: "reviewing"
lastUpdated: "2026-04-21T19:38:24Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: Hyperliquid Integration Code Review Remediation

## Overview

Address all CRITICAL, MAJOR, and MINOR findings from the Hyperliquid integration adversarial code review, improving code quality, resilience, and correctness across the exchange integration layer.

## Code Review Reference

- Review file: `.agent-context/1-discover/code-review/hyperliquid-integration-review.md`
- Reviewer: Adversarial Review (Claude Opus 4.6), 2026-04-21
- Scope: All Hyperliquid integration code, related interfaces, DI registration, and supporting services

### Findings Summary

| Severity | Count | Status |
|----------|-------|--------|
| CRITICAL | 3 | C1 already fixed (DI registration); C2 and C3 remain |
| MAJOR | 7 | M1 by design (reconnect is consumer-managed); M2–M7 remain |
| MINOR | 7 | All remain (m1–m7) |

### Excluded Findings

- **C1 (Static class DI)**: Already resolved — `HyperliquidExchangeSymbolMapper` is the registered implementation in both Program.cs files. The review references outdated line numbers.
- **M1 (WebSocket auto-reconnection)**: By design — architecture docs (02-hyperliquid-integration.md, 30-worker-execution-pipeline.md) confirm reconnection is consumer-managed by `TradingSession` in the Worker with exponential backoff. Adding auto-reconnect to the WebSocket clients would violate the existing architecture. Phase 2 addresses the related IDisposable/cleanup gaps instead.

## Objectives

- Extract shared Hyperliquid utility methods to eliminate code duplication (ToWireDecimal, MapOrderSide, ParseDecimal)
- Fix critical async anti-pattern (`ContinueWith`) and LSP violation (`NotSupportedException`)
- Harden WebSocket clients with proper CancellationToken linking, connect timeouts, and increased buffer sizes
- Make market order slippage configurable and fix floating-point precision in price rounding
- Surface companion trigger order failures via explicit `Warnings` on PlaceOrderResponse
- Expand hardcoded asset and timeframe support to match Hyperliquid's full offering

### Discovery References

- Code review: `.agent-context/1-discover/code-review/hyperliquid-integration-review.md`
- Architecture: `.agent-context/0-knowledge/02-hyperliquid-integration.md`
- Exchange abstraction: `.agent-context/0-knowledge/38-exchange-abstraction-architecture.md`
- Worker pipeline: `.agent-context/0-knowledge/30-worker-execution-pipeline.md`
- Domain model: `.agent-context/0-knowledge/04-domain-model.md`
- HIP-3 assets: `.agent-context/0-knowledge/32-hyperliquid-rwa-stock-perps.md`

### Project Patterns

- `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidEip712.cs` — canonical ToWireDecimal location
- `src/TradePilot.Infrastructure/Hyperliquid/HyperliquidExchangeSymbolMapper.cs` — adapter pattern wrapping static class
- `src/TradePilot.Infrastructure/Services/HyperliquidUserEventClient.cs` — reference pattern for linked CancellationTokenSource in ping loop
- `src/TradePilot.Api/Services/HyperliquidAssetMetadataCache.cs` — dynamic asset loading pattern already implemented
- `src/TradePilot.Application/Abstractions/Configuration/HyperliquidOptions.cs` — configuration options class
- `tests/TradePilot.Infrastructure.Tests/Services/HyperliquidAssetMapperTests.cs` — test pattern for static mapper
- `tests/TradePilot.Api.Tests/Services/HyperliquidOrderServiceTests.cs` — test pattern for order service

### [x] Phase 1: Shared Utilities & Critical Code Fixes

**Complexity**: Medium | **Risk**: Low

- [x] Task 1.1: Create `HyperliquidFormatting` shared utility class
  - Details: .agent-context/3-develop/build/plans/details/20260421-hyperliquid-integration-review-phase-01-details.md#task-11-create-hyperliquidformatting-shared-utility-class

- [x] Task 1.2: Fix ContinueWith anti-pattern in `HyperliquidHistoricalDataClient`
  - Details: .agent-context/3-develop/build/plans/details/20260421-hyperliquid-integration-review-phase-01-details.md#task-12-fix-continuewith-anti-pattern-in-hyperliquidhistoricaldataclient

- [x] Task 1.3: Fix `GetFundingRatesAsync` LSP violation
  - Details: .agent-context/3-develop/build/plans/details/20260421-hyperliquid-integration-review-phase-01-details.md#task-13-fix-getfundingratesasync-lsp-violation

- [x] Task 1.4: Fix nullable signer field inconsistency in `HyperliquidAccountService`
  - Details: .agent-context/3-develop/build/plans/details/20260421-hyperliquid-integration-review-phase-01-details.md#task-14-fix-nullable-signer-field-inconsistency

- [x] Task 1.5: Replace duplicated `MapOrderSide` across 3 files with shared utility
  - Details: .agent-context/3-develop/build/plans/details/20260421-hyperliquid-integration-review-phase-01-details.md#task-15-replace-duplicated-maporderside-with-shared-utility

- [x] Task 1.6: Replace duplicated `ParseDecimal` with standardized shared implementation
  - Details: .agent-context/3-develop/build/plans/details/20260421-hyperliquid-integration-review-phase-01-details.md#task-16-replace-duplicated-parsedecimal-with-standardized-implementation

- [x] Task 1.7: Add unit tests for `HyperliquidHistoricalDataClient` and `HyperliquidFormatting`
  - Details: .agent-context/3-develop/build/plans/details/20260421-hyperliquid-integration-review-phase-01-details.md#task-17-add-unit-tests

- [x] Task 1.8: Build and run all tests to verify Phase 1 changes
  - Details: .agent-context/3-develop/build/plans/details/20260421-hyperliquid-integration-review-phase-01-details.md#task-18-build-and-run-all-tests

### [x] Phase 2: WebSocket Client Hardening

**Complexity**: Medium | **Risk**: Medium

- [x] Task 2.1: Link ping loop CancellationToken in `HyperliquidWebSocketClient`
  - Details: .agent-context/3-develop/build/plans/details/20260421-hyperliquid-integration-review-phase-02-details.md#task-21-link-ping-loop-cancellationtoken

- [x] Task 2.2: Add connect timeout to both WebSocket clients
  - Details: .agent-context/3-develop/build/plans/details/20260421-hyperliquid-integration-review-phase-02-details.md#task-22-add-connect-timeout-to-websocket-clients

- [x] Task 2.3: Increase receive buffer size from 4096 to 8192
  - Details: .agent-context/3-develop/build/plans/details/20260421-hyperliquid-integration-review-phase-02-details.md#task-23-increase-receive-buffer-size

- [x] Task 2.4: Update WebSocket tests to cover new behavior
  - Details: .agent-context/3-develop/build/plans/details/20260421-hyperliquid-integration-review-phase-02-details.md#task-24-update-websocket-tests

- [x] Task 2.5: Build and run all tests to verify Phase 2 changes
  - Details: .agent-context/3-develop/build/plans/details/20260421-hyperliquid-integration-review-phase-02-details.md#task-25-build-and-run-all-tests

### [x] Phase 3: Order Execution Improvements

**Complexity**: Medium | **Risk**: Medium

- [x] Task 3.1: Add `MarketOrderSlippageBps` to `HyperliquidOptions`
  - Details: .agent-context/3-develop/build/plans/details/20260421-hyperliquid-integration-review-phase-03-details.md#task-31-add-marketorderslippagebps-to-hyperliquidoptions

- [x] Task 3.2: Rewrite `RoundToSignificantFigures` using pure decimal math
  - Details: .agent-context/3-develop/build/plans/details/20260421-hyperliquid-integration-review-phase-03-details.md#task-32-rewrite-roundtosignificantfigures-using-pure-decimal-math

- [x] Task 3.3: Use shared `ToWireDecimal` from `HyperliquidFormatting` in `HyperliquidOrderService`
  - Details: .agent-context/3-develop/build/plans/details/20260421-hyperliquid-integration-review-phase-03-details.md#task-33-use-shared-towiredecimal-in-hyperliquidorderservice

- [x] Task 3.4: Add `Warnings` to `PlaceOrderResponse` for companion trigger order failures
  - Details: .agent-context/3-develop/build/plans/details/20260421-hyperliquid-integration-review-phase-03-details.md#task-34-add-warnings-to-placeorderresponse

- [x] Task 3.5: Downgrade wallet address logging to Debug level in `MutableSignerProvider`
  - Details: .agent-context/3-develop/build/plans/details/20260421-hyperliquid-integration-review-phase-03-details.md#task-35-downgrade-wallet-address-logging-to-debug

- [x] Task 3.6: Add and update unit tests for order execution changes
  - Details: .agent-context/3-develop/build/plans/details/20260421-hyperliquid-integration-review-phase-03-details.md#task-36-add-and-update-unit-tests

- [x] Task 3.7: Build and run all tests to verify Phase 3 changes
  - Details: .agent-context/3-develop/build/plans/details/20260421-hyperliquid-integration-review-phase-03-details.md#task-37-build-and-run-all-tests

### [x] Phase 4: Asset & Timeframe Expansion

**Complexity**: High | **Risk**: Medium

- [x] Task 4.1: Remove hardcoded coin validation from `HyperliquidAssetMapper`
  - Details: .agent-context/3-develop/build/plans/details/20260421-hyperliquid-integration-review-phase-04-details.md#task-41-remove-hardcoded-coin-validation

- [x] Task 4.2: Expand supported timeframes in `HyperliquidAssetMapper`
  - Details: .agent-context/3-develop/build/plans/details/20260421-hyperliquid-integration-review-phase-04-details.md#task-42-expand-supported-timeframes

- [x] Task 4.3: Update `HyperliquidCapabilities` to reflect expanded support
  - Details: .agent-context/3-develop/build/plans/details/20260421-hyperliquid-integration-review-phase-04-details.md#task-43-update-hyperliquidcapabilities

- [x] Task 4.4: Update `HyperliquidExchangeSymbolMapper.CanMap` to not rely on hardcoded list
  - Details: .agent-context/3-develop/build/plans/details/20260421-hyperliquid-integration-review-phase-04-details.md#task-44-update-hyperliquidexchangesymbolmapper-canmap

- [x] Task 4.5: Update and add tests for asset mapper and capabilities
  - Details: .agent-context/3-develop/build/plans/details/20260421-hyperliquid-integration-review-phase-04-details.md#task-45-update-tests

- [x] Task 4.6: Build and run all tests to verify Phase 4 changes
  - Details: .agent-context/3-develop/build/plans/details/20260421-hyperliquid-integration-review-phase-04-details.md#task-46-build-and-run-all-tests

## Scoping Summary

| Phase | Complexity | Risk |
|-------|-----------|------|
| Phase 1: Shared Utilities & Critical Code Fixes | Medium | Low |
| Phase 2: WebSocket Client Hardening | Medium | Medium |
| Phase 3: Order Execution Improvements | Medium | Medium |
| Phase 4: Asset & Timeframe Expansion | High | Medium |
| **Total** | **Medium** | **Medium** |

### Scoping Notes

- C1 (static class DI registration) is already resolved in current codebase — skipped
- M1 (WebSocket auto-reconnection) is by design per architecture docs — not implemented; Phase 2 addresses related cleanup gaps instead
- Phase 1 must be completed first as it extracts shared utilities used by Phase 3
- `HyperliquidFormatting` must be `public static` (not `internal`) because it is consumed from `TradePilot.Api` (Phase 3 Task 3.3) in addition to `TradePilot.Infrastructure`. `InternalsVisibleTo` is only configured for the test project.
- Phases 2, 3, and 4 are independent of each other (can be done in any order after Phase 1)
- `ParseDecimal` inconsistency fix is scoped to Hyperliquid files only; Binance has its own copies which are out of scope
- `HyperliquidAssetMetadataCache` lives in Api project — moving it to Infrastructure is out of scope for this plan but noted as a future improvement
- MutableSignerProvider manual LoggerFactory in Api Program.cs — out of scope for this plan (not in code review findings)

## Dependencies

- .NET 10 SDK
- MSTest / Moq / FluentAssertions ≤ 6.x (existing test framework)
- No new NuGet packages required

## Success Criteria

- All CRITICAL findings (C2, C3) resolved
- All MAJOR findings (M2–M7) resolved
- All MINOR findings (m1–m7) resolved
- Zero code duplication for `ToWireDecimal`, `MapOrderSide`, `ParseDecimal`
- All existing tests pass after each phase
- New tests cover extracted utilities and modified behavior
- Solution builds cleanly with no warnings related to changes

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-04-21T18:06:48Z | 2026-04-21T18:13:51Z |
| Plan Reviewer | plan-reviewed | 2026-04-21T18:19:24Z | 2026-04-21T18:25:05Z |
| Plan Implementer | implemented | 2026-04-21T18:38:25Z | 2026-04-21T19:34:55Z |
| Implementation Reviewer | reviewing | 2026-04-21T19:38:24Z | |
