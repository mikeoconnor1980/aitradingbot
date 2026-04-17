---
applyTo: ".agent-context/3-develop/build/changes/20260325-f5-order-placement-changes.md"
currentAgent: "3-Develop: 3 Reviewer"
agentStartedAt: "2026-03-25T21:15:00Z"
status: "complete"
lastUpdated: "2026-03-25T22:00:00Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: F5 — Order Placement

## Overview

Place market and limit orders on Hyperliquid testnet via the Angular UI, with EIP-712 typed data signing handled in .NET using Nethereum. Includes a standalone signing diagnostic endpoint and full error reporting.

## PBI Details

**PBI ID:** Draft
**Status:** Draft
**PBI File:** .agent-context/3-develop/backlog/draft/F5-order-placement.md
**Implementation Phase:** 4
**Risk Level:** High
**Depends On:** F1 (wallet config), F3 (market data — mid price)

### User Story

> As a **developer**, I want to **place orders on Hyperliquid testnet** so that **I can prove the EIP-712 signing and order submission flow works end-to-end from .NET**.

### Acceptance Criteria

- [ ] **AC1:** Given the developer is on the Order Entry tab and selects Buy, Market, and enters a valid size, When they click Submit and confirm in the confirmation dialog, Then the order is submitted to Hyperliquid testnet and a success message with order details is displayed
- [ ] **AC2:** Given the developer is on the Order Entry tab and selects Sell, Limit, and enters a valid price and size, When they click Submit and confirm, Then the order is submitted and appears in the F2 open orders table
- [ ] **AC3:** Given the developer selects Limit order type, When the price field is rendered, Then it is pre-populated with the current mid price from F3 market data
- [ ] **AC4:** Given the developer clicks Submit, When the confirmation dialog appears, Then it displays side, type, asset (BTC-PERP), price (if limit), and size for review before final confirmation
- [ ] **AC5:** Given the developer submits an order and Hyperliquid returns an error (e.g., insufficient margin, invalid size), When the error response is received, Then the full error payload from Hyperliquid is displayed in the UI
- [ ] **AC6:** Given the developer submits an order and the EIP-712 signature is rejected by Hyperliquid, When the error is received, Then the UI clearly identifies it as a signature rejection and the backend logs the signing parameters
- [ ] **AC7:** Given two orders are submitted in rapid succession (sub-millisecond), When nonces are generated, Then each order receives a unique monotonically increasing timestamp-based nonce with no collisions
- [ ] **AC8:** Given the developer calls `POST /api/orders/test-sign` with a dummy payload, When the signing completes, Then the response includes domain separator, type hash, message hash, and the (v, r, s) signature components — without sending anything to Hyperliquid
- [ ] **AC9:** Given an order is submitted successfully, When the backend logs the transaction, Then structured log fields include submit timestamp, response timestamp, and round-trip delta in milliseconds

## Objectives

- Prove EIP-712 signing compatibility between Nethereum (.NET) and Hyperliquid's exchange API — this is the critical risk retirement for the entire platform
- Implement market and limit order placement end-to-end (Angular UI → .NET backend → Hyperliquid testnet)
- Provide a standalone signing diagnostic endpoint for isolating signature issues from order flow
- Measure and log order round-trip latency with structured logging

### Discovery References

- Hyperliquid testnet uses "phantom agent" EIP-712 pattern: domain `{ name: "Exchange", version: "1", chainId: 1337, verifyingContract: 0x000...000 }`, primary type `"Agent"` with `source: "b"` (testnet) and `connectionId: sha256(msgpack(action) + nonce_bytes + vault_indicator)`
- `IHyperliquidSigner` currently only exposes `WalletAddress` — no signing method; `EthECKey` is discarded after address derivation in `HyperliquidSigner.Create()`
- `IHyperliquidRestClient` only has `/info` methods; `/exchange` endpoint for authenticated writes is not implemented
- Hyperliquid orders use integer asset index (`a` field), not coin symbol; BTC is always index 0
- `Nethereum.Signer 6.0.4` already installed; need `Nethereum.ABI` for EIP-712 `TypedData<Domain>` and `MessagePack` for action hash computation
- Frontend has no `ReactiveFormsModule`, `MatDialog`, `MatButtonToggle`, or `MatInput` usage yet — all must be introduced
- Dashboard orders table polls every 2 seconds; new orders appear automatically without special refresh

### Project Patterns

- `src/TradePilot.Api/Controllers/AccountController.cs` — Direct service injection controller pattern (ADR 14, POC)
- `src/TradePilot.Api/Services/HyperliquidAccountService.cs` — Api-layer service pattern for Hyperliquid interactions
- `src/TradePilot.Api/Services/IHyperliquidAccountService.cs` — Service interface in Api layer
- `src/TradePilot.Api/Infrastructure/Filters/HttpGlobalExceptionFilter.cs` — Global exception → HTTP status mapping
- `src/TradePilot.Infrastructure/Services/HyperliquidSigner.cs` — Existing signer (address derivation only)
- `src/TradePilot.Infrastructure/Services/HyperliquidRestClient.cs` — PostInfoAsync pattern (template for PostExchangeAsync)
- `src/TradePilot.Application/Abstractions/Services/IHyperliquidSigner.cs` — Signer interface to extend
- `src/TradePilot.Application/Abstractions/Services/IHyperliquidRestClient.cs` — REST client interface to extend
- `src/TradePilot.Api/Models/OpenOrderDto.cs` — Existing DTO pattern
- `src/TradePilot.Api/Program.cs` — Flat DI registration
- `tests/TradePilot.Api.Tests/Controllers/AccountControllerTests.cs` — WebApplicationFactory integration test pattern
- `tests/TradePilot.Api.Tests/Infrastructure/BaseControllerTests.cs` — Test base class with ConfigureTestServices
- `tests/TradePilot.Api.Tests/Infrastructure/FakeHttpMessageHandler.cs` — HTTP mock pattern
- `tests/TradePilot.Infrastructure.Tests/Services/HyperliquidSignerTests.cs` — Signer unit test pattern
- `frontend/trading-ui/src/app/core/services/api-rest-client.service.ts` — Generic REST wrapper for API calls
- `frontend/trading-ui/src/app/core/services/market-data.service.ts` — MarketDataService (midPrice source for pre-fill)
- `frontend/trading-ui/src/app/core/models/open-order.model.ts` — Existing OpenOrder interface
- `frontend/trading-ui/src/app/core/models/market-info.model.ts` — MarketInfo with midPrice field
- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — Dashboard polling pattern
- `frontend/trading-ui/src/app/features/market-data/market-data.component.ts` — MatFormField/MatSelect form pattern

### [x] Phase 1: EIP-712 Signing & Nonce Infrastructure

**Complexity**: High | **Risk**: High

This is the critical risk retirement phase. If Nethereum's EIP-712 implementation is not compatible with Hyperliquid's expected signature format, this is where the blocker will be discovered.

- [x] Task 1.1: Add NuGet dependencies for EIP-712 and MessagePack
  - Details: .agent-context/3-develop/build/plans/details/20260325-f5-order-placement-phase-01-details.md#task-11-add-nuget-dependencies

- [x] Task 1.2: Create Hyperliquid EIP-712 type definitions and hash computation
  - Details: .agent-context/3-develop/build/plans/details/20260325-f5-order-placement-phase-01-details.md#task-12-create-hyperliquid-eip-712-type-definitions

- [x] Task 1.3: Extend IHyperliquidSigner with EIP-712 signing method
  - Details: .agent-context/3-develop/build/plans/details/20260325-f5-order-placement-phase-01-details.md#task-13-extend-ihyperliquidsigner-with-signing-method

- [x] Task 1.4: Refactor HyperliquidSigner to retain EthECKey and implement signing
  - Details: .agent-context/3-develop/build/plans/details/20260325-f5-order-placement-phase-01-details.md#task-14-refactor-hyperliquidsigner-to-implement-signing

- [x] Task 1.5: Create thread-safe NonceProvider service
  - Details: .agent-context/3-develop/build/plans/details/20260325-f5-order-placement-phase-01-details.md#task-15-create-nonceprovider-service

- [x] Task 1.6: Unit tests for EIP-712 signing and hash computation
  - Details: .agent-context/3-develop/build/plans/details/20260325-f5-order-placement-phase-01-details.md#task-16-unit-tests-for-eip-712-signing

- [x] Task 1.7: Unit tests for NonceProvider
  - Details: .agent-context/3-develop/build/plans/details/20260325-f5-order-placement-phase-01-details.md#task-17-unit-tests-for-nonceprovider

- [x] Task 1.8: Run all existing tests to verify no regressions
  - Details: .agent-context/3-develop/build/plans/details/20260325-f5-order-placement-phase-01-details.md#task-18-run-all-existing-tests

### [x] Phase 2: Order Placement Backend (Service, Client, Controller)

**Complexity**: Medium | **Risk**: Medium

- [x] Task 2.1: Add PostExchangeAsync to IHyperliquidRestClient and HyperliquidRestClient
  - Details: .agent-context/3-develop/build/plans/details/20260325-f5-order-placement-phase-02-details.md#task-21-add-postexchangeasync-to-rest-client

- [x] Task 2.2: Create request and response DTOs
  - Details: .agent-context/3-develop/build/plans/details/20260325-f5-order-placement-phase-02-details.md#task-22-create-request-and-response-dtos

- [x] Task 2.3: Create IHyperliquidOrderService and HyperliquidOrderService
  - Details: .agent-context/3-develop/build/plans/details/20260325-f5-order-placement-phase-02-details.md#task-23-create-order-service

- [x] Task 2.4: Create OrdersController with POST endpoints
  - Details: .agent-context/3-develop/build/plans/details/20260325-f5-order-placement-phase-02-details.md#task-24-create-orderscontroller

- [x] Task 2.5: Register new services in Program.cs
  - Details: .agent-context/3-develop/build/plans/details/20260325-f5-order-placement-phase-02-details.md#task-25-register-services-in-di

- [x] Task 2.6: Unit tests for HyperliquidOrderService
  - Details: .agent-context/3-develop/build/plans/details/20260325-f5-order-placement-phase-02-details.md#task-26-unit-tests-for-order-service

- [x] Task 2.7: Integration tests for OrdersController
  - Details: .agent-context/3-develop/build/plans/details/20260325-f5-order-placement-phase-02-details.md#task-27-integration-tests-for-orderscontroller

- [x] Task 2.8: Run all tests
  - Details: .agent-context/3-develop/build/plans/details/20260325-f5-order-placement-phase-02-details.md#task-28-run-all-tests

### [x] Phase 3: Angular Order Entry UI

**Complexity**: Medium | **Risk**: Low

- [x] Task 3.1: Create TypeScript models for order placement
  - Details: .agent-context/3-develop/build/plans/details/20260325-f5-order-placement-phase-03-details.md#task-31-create-typescript-models

- [x] Task 3.2: Create OrderService
  - Details: .agent-context/3-develop/build/plans/details/20260325-f5-order-placement-phase-03-details.md#task-32-create-orderservice

- [x] Task 3.3: Create ConfirmDialogComponent
  - Details: .agent-context/3-develop/build/plans/details/20260325-f5-order-placement-phase-03-details.md#task-33-create-confirmdialogcomponent

- [x] Task 3.4: Create OrderEntryComponent with reactive form
  - Details: .agent-context/3-develop/build/plans/details/20260325-f5-order-placement-phase-03-details.md#task-34-create-orderentrycomponent

- [x] Task 3.5: Add route and navigation link
  - Details: .agent-context/3-develop/build/plans/details/20260325-f5-order-placement-phase-03-details.md#task-35-add-route-and-navigation

- [x] Task 3.6: Frontend build and lint verification
  - Details: .agent-context/3-develop/build/plans/details/20260325-f5-order-placement-phase-03-details.md#task-36-frontend-build-and-lint

## Scoping Summary

| Phase | Complexity | Risk |
|-------|-----------|------|
| Phase 1: EIP-712 Signing & Nonce Infrastructure | High | High |
| Phase 2: Order Placement Backend | Medium | Medium |
| Phase 3: Angular Order Entry UI | Medium | Low |
| **Overall** | **High** | **High** |

### Scoping Notes

- EIP-712 signing compatibility with Hyperliquid is the single highest-risk item — Phase 1 retires this risk first
- Uses testnet "phantom agent" EIP-712 pattern (chainId 1337, source "b"); mainnet uses different types
- Asset is hard-coded to BTC-PERP (index 0) per POC scope — no asset selector in UI
- Controller follows AccountController direct service injection pattern (ADR 14) with global exception filter
- No order persistence — Hyperliquid is the source of truth; F2 dashboard polls orders every 2s
- MessagePack serialization of order actions must produce identical bytes to Hyperliquid Python SDK — this is a subtle compatibility risk
- GTC (Good Till Cancel) is the only time-in-force supported; no reduce-only
- Serilog structured logging uses standard `ILogger` (no Serilog-specific setup needed)

## Dependencies

- `Nethereum.Signer 6.0.4` — already installed (EthECKey, Eip712TypedDataSigner)
- `Nethereum.ABI` — to be added to Infrastructure + Application (TypedData, MemberDescription, Domain classes for EIP-712)
- `MessagePack` — to be added (action hash computation matching Hyperliquid Python SDK)
- `@angular/material ^19.2` — already installed (MatDialog, MatButtonToggle, MatInput to be imported)
- `@angular/forms` — already available (ReactiveFormsModule to be imported)

## Success Criteria

- `POST /api/orders/test-sign` returns valid EIP-712 signature components for a dummy payload
- `POST /api/orders` successfully places a market order on Hyperliquid testnet (verified manually)
- `POST /api/orders` successfully places a limit order on Hyperliquid testnet (verified manually)
- All new unit and integration tests pass
- Angular order entry form validates and submits orders with confirmation dialog
- Error responses from Hyperliquid are displayed in the UI
- Structured logs include submit/response timestamps and latency delta
- Nonce provider produces unique monotonically increasing values under concurrent access
- All existing tests continue to pass (no regressions)
- Frontend builds and lints without errors

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-03-25T12:04:13Z | 2026-03-25T12:34:29Z |
| Plan Reviewer | approved | 2026-03-25T12:35:15Z | 2026-03-25T13:21:49Z |
| 3-Develop: 2 Implementer | completed | 2026-03-25T13:30:00Z | 2026-03-25T14:00:00Z |
| 3-Develop: 3 Reviewer | complete | 2026-03-25T21:15:00Z | 2026-03-25T22:00:00Z |
