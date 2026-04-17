---
applyTo: ".agent-context/3-develop/build/changes/20260325-f6-order-management-changes.md"
currentAgent: "Implementation Reviewer"
agentStartedAt: "2026-03-25T20:12:28Z"
status: "reviewing"
lastUpdated: "2026-03-25T20:12:28Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: F6 — Order Management

## Overview

Cancel and modify existing orders on Hyperliquid testnet via the Angular UI, extending F5's order placement infrastructure with cancel/modify API endpoints, optimistic UI updates, confirmation dialogs, and toast notifications.

## PBI Details

**PBI ID:** Draft
**Status:** Draft
**PBI File:** .agent-context/3-develop/backlog/draft/F6-order-management.md
**Implementation Phase:** 5
**Risk Level:** Medium
**Depends On:** F1 (wallet config), F2 (dashboard orders table), F5 (order placement, signing, /exchange endpoint)

### User Story

> As a **developer**, I want to **cancel and modify existing orders** so that **I can manage my open positions and correct mistakes without needing to interact directly with the exchange API**.

### Acceptance Criteria

- [ ] **AC1:** Given I have an open order in the orders table, When I click the cancel button on the order row, Then a confirmation dialog appears asking me to confirm the cancellation
- [ ] **AC2:** Given the confirmation dialog is shown for a single cancel, When I confirm, Then the order is removed from the table optimistically and a cancel request is sent to Hyperliquid
- [ ] **AC3:** Given the confirmation dialog is shown for a single cancel, When I dismiss/cancel the dialog, Then no action is taken and the order remains
- [ ] **AC4:** Given I have open orders for BTC-PERP, When I click "Cancel All", Then a confirmation dialog appears stating the number of orders to be cancelled
- [ ] **AC5:** Given I confirm Cancel All, When the API call succeeds, Then all orders for BTC-PERP are removed from the table and a success toast is shown
- [ ] **AC6:** Given I have an open order, When I click "Modify", Then a modal dialog opens pre-filled with the order's current price and size
- [ ] **AC7:** Given I am in the modify dialog, When I enter a valid new price and/or size and confirm, Then the order is updated optimistically and a modify request is sent to Hyperliquid
- [ ] **AC8:** Given I enter an invalid value in the modify dialog (e.g., price ≤ 0), When I try to submit, Then a validation error is shown and the form does not submit
- [ ] **AC9:** Given a cancel or modify API call fails, When the error response is received, Then the optimistic update is reverted and an error toast is shown with a meaningful message
- [ ] **AC10:** Given an API call is in flight for a specific order, When I look at that order row, Then the action buttons are disabled and a spinner is shown on the row
- [ ] **AC11:** Given I right-click on an order row, When the context menu appears, Then I see cancel and modify options
- [ ] **AC12:** Given I am in an order detail view, When I click the cancel button, Then the same confirmation and cancel flow is triggered as from the table row *(Deferred — no order detail view exists; see Scoping Notes)*

## Objectives

- Extend F5's signing and exchange infrastructure to support cancel and modify action types
- Provide a complete order lifecycle management UI (cancel single, cancel all, modify)
- Implement optimistic UI updates with automatic revert on failure for responsive UX
- Add confirmation dialogs before all destructive actions to prevent accidental operations

### Discovery References

- Hyperliquid cancel action payload: `{ "type": "cancel", "cancels": [{ "a": assetIndex, "o": orderId }] }` — uses same EIP-712 "phantom agent" signing as placement
- Hyperliquid modify action payload: `{ "type": "batchModifyOrders", "modifies": [{ "oid": orderId, "order": { "a": assetIndex, "b": isBuy, "p": price, "s": size, "r": false, "t": { "limit": { "tif": "Gtc" } } } }] }`
- Cancel and modify use the SAME signing flow as order placement — construct action → hash → sign Agent typed data → POST to `/exchange`. Only the action payload shape differs.
- Asset index for BTC is 0 (hard-coded in POC scope); `OpenOrderDto.OrderId` is stored as string but Hyperliquid `oid` is a numeric long
- `MatDialog` and `ConfirmDialogComponent` already established by F5 — F6 reuses them
- `OrderService` (Angular) created by F5 — F6 extends it with cancel/modify methods
- `MatSnackBar` already used in `DashboardComponent` — F6 extends toast usage to order operations
- No context menu or reactive form validation exists in the orders table — both introduced by F6
- Dashboard polls every 2 seconds via `_refresh$` Subject; after mutations, `_refresh$.next()` forces immediate refresh

### Project Patterns

- `src/TradePilot.Api/Controllers/AccountController.cs` — Direct service injection controller pattern (F5 OrdersController follows this)
- `src/TradePilot.Api/Services/HyperliquidAccountService.cs` — Api-layer service pattern for Hyperliquid interactions
- `src/TradePilot.Api/Services/IHyperliquidAccountService.cs` — Service interface in Api layer
- `src/TradePilot.Api/Infrastructure/Filters/HttpGlobalExceptionFilter.cs` — Global exception → HTTP status mapping
- `src/TradePilot.Api/Infrastructure/Envelope.cs` — Error response wrapper with ErrorMessage + Timestamp
- `src/TradePilot.Api/Models/OpenOrderDto.cs` — Existing DTO pattern (OrderId as string)
- `src/TradePilot.Api/Program.cs` — Flat DI registration (AddScoped, AddSingleton)
- `src/TradePilot.Application/Abstractions/Services/IHyperliquidSigner.cs` — Signer interface (extended by F5 with SignAsync)
- `src/TradePilot.Application/Abstractions/Services/IHyperliquidRestClient.cs` — REST client (extended by F5 with PostExchangeAsync)
- `src/TradePilot.Application/Abstractions/Exceptions/DomainException.cs` — Maps to 400 via global filter
- `src/TradePilot.Application/Abstractions/Exceptions/NotFoundException.cs` — Maps to 404 via global filter
- `src/TradePilot.Infrastructure/Services/HyperliquidSigner.cs` — EIP-712 signing (extended by F5)
- `src/TradePilot.Infrastructure/Services/HyperliquidRestClient.cs` — REST client impl (extended by F5)
- `tests/TradePilot.Api.Tests/Infrastructure/BaseControllerTests.cs` — WebApplicationFactory test base
- `tests/TradePilot.Api.Tests/Controllers/AccountControllerTests.cs` — Controller integration test pattern
- `frontend/trading-ui/src/app/core/services/api-rest-client.service.ts` — Generic REST wrapper (delete, put, post)
- `frontend/trading-ui/src/app/core/models/open-order.model.ts` — OpenOrder TypeScript interface
- `frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.ts` — Read-only orders table (to be extended)
- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — Dashboard with polling + MatSnackBar

### [x] Phase 1: Backend — Cancel & Modify Endpoints + Tests

**Complexity**: Medium | **Risk**: Medium

Extends F5's `IHyperliquidOrderService` and `OrdersController` with cancel (single + all) and modify operations. The signing flow from F5 is reused as-is — only the action payload shapes differ. Backend validation is added for modify requests.

- [x] Task 1.1: Create cancel and modify action payload models
  - Details: .agent-context/3-develop/build/plans/details/20260325-f6-order-management-phase-01-details.md#task-11-create-action-payload-models

- [x] Task 1.2: Create ModifyOrderDto request model with validation
  - Details: .agent-context/3-develop/build/plans/details/20260325-f6-order-management-phase-01-details.md#task-12-create-modifyorderdto-request-model

- [x] Task 1.3: Add CancelOrderAsync and CancelAllOrdersAsync to IHyperliquidOrderService
  - Details: .agent-context/3-develop/build/plans/details/20260325-f6-order-management-phase-01-details.md#task-13-add-cancel-methods-to-order-service

- [x] Task 1.4: Add ModifyOrderAsync to IHyperliquidOrderService
  - Details: .agent-context/3-develop/build/plans/details/20260325-f6-order-management-phase-01-details.md#task-14-add-modify-method-to-order-service

- [x] Task 1.5: Add DELETE and PUT endpoints to OrdersController
  - Details: .agent-context/3-develop/build/plans/details/20260325-f6-order-management-phase-01-details.md#task-15-add-controller-endpoints

- [x] Task 1.6: Unit tests for HyperliquidOrderService cancel and modify
  - Details: .agent-context/3-develop/build/plans/details/20260325-f6-order-management-phase-01-details.md#task-16-unit-tests-for-order-service

- [x] Task 1.7: Integration tests for OrdersController cancel and modify endpoints
  - Details: .agent-context/3-develop/build/plans/details/20260325-f6-order-management-phase-01-details.md#task-17-integration-tests-for-controller

- [x] Task 1.8: Run all tests to verify no regressions
  - Details: .agent-context/3-develop/build/plans/details/20260325-f6-order-management-phase-01-details.md#task-18-run-all-tests

### [x] Phase 2: Frontend — Order Management Service + Modify Modal

**Complexity**: Medium | **Risk**: Low

Extends the Angular `OrderService` from F5 with cancel/modify methods and creates the `ModifyOrderModalComponent` with reactive form validation. Reuses `ConfirmDialogComponent` and `MatDialog` from F5.

- [x] Task 2.1: Create ModifyOrderDto TypeScript interface
  - Details: .agent-context/3-develop/build/plans/details/20260325-f6-order-management-phase-02-details.md#task-21-create-modifyorderdto-interface

- [x] Task 2.2: Add cancel and modify methods to OrderService
  - Details: .agent-context/3-develop/build/plans/details/20260325-f6-order-management-phase-02-details.md#task-22-add-cancel-modify-to-orderservice

- [x] Task 2.3: Create ModifyOrderModalComponent with reactive form
  - Details: .agent-context/3-develop/build/plans/details/20260325-f6-order-management-phase-02-details.md#task-23-create-modify-order-modal

- [x] Task 2.4: Frontend build and lint verification
  - Details: .agent-context/3-develop/build/plans/details/20260325-f6-order-management-phase-02-details.md#task-24-frontend-build-and-lint

### [x] Phase 3: Frontend — Orders Table Actions + Optimistic UI

**Complexity**: High | **Risk**: Medium

The core UI phase — adds cancel/modify action buttons, Cancel All button, row-level loading states, context menu, optimistic update with revert, toast notifications, and dashboard refresh integration.

- [x] Task 3.1: Add Cancel and Modify action buttons to order table rows
  - Details: .agent-context/3-develop/build/plans/details/20260325-f6-order-management-phase-03-details.md#task-31-add-action-buttons-to-rows

- [x] Task 3.2: Add Cancel All button above orders table (extends Task 3.1 template)
  - Details: .agent-context/3-develop/build/plans/details/20260325-f6-order-management-phase-03-details.md#task-32-add-cancel-all-button

- [x] Task 3.3: Implement row-level loading state (extends Task 3.1 component)
  - Details: .agent-context/3-develop/build/plans/details/20260325-f6-order-management-phase-03-details.md#task-33-implement-row-loading-state

- [x] Task 3.4: Add context menu with cancel and modify options
  - Details: .agent-context/3-develop/build/plans/details/20260325-f6-order-management-phase-03-details.md#task-34-add-context-menu

- [x] Task 3.5: Wire cancel single order flow with optimistic UI
  - Details: .agent-context/3-develop/build/plans/details/20260325-f6-order-management-phase-03-details.md#task-35-wire-cancel-single-order

- [x] Task 3.6: Wire cancel all orders flow with optimistic UI
  - Details: .agent-context/3-develop/build/plans/details/20260325-f6-order-management-phase-03-details.md#task-36-wire-cancel-all-orders

- [x] Task 3.7: Wire modify order flow with optimistic UI
  - Details: .agent-context/3-develop/build/plans/details/20260325-f6-order-management-phase-03-details.md#task-37-wire-modify-order

- [x] Task 3.8: Wire refresh trigger from orders-table to dashboard
  - Details: .agent-context/3-develop/build/plans/details/20260325-f6-order-management-phase-03-details.md#task-38-wire-refresh-trigger

- [x] Task 3.9: Frontend build and lint verification
  - Details: .agent-context/3-develop/build/plans/details/20260325-f6-order-management-phase-03-details.md#task-39-frontend-build-and-lint

## Scoping Summary

| Phase | Complexity | Risk |
|-------|-----------|------|
| Phase 1: Backend — Cancel & Modify Endpoints + Tests | Medium | Medium |
| Phase 2: Frontend — Order Management Service + Modify Modal | Medium | Low |
| Phase 3: Frontend — Orders Table Actions + Optimistic UI | High | Medium |
| **Overall** | **Medium** | **Medium** |

### Scoping Notes

- **F5 dependency**: This plan assumes F5 is complete. F5 provides: `OrdersController` with POST, `IHyperliquidOrderService` with `PlaceOrderAsync`, EIP-712 signing via `IHyperliquidSigner.SignAsync`, `PostExchangeAsync` on `IHyperliquidRestClient`, `NonceProvider`, `ConfirmDialogComponent`, and `OrderService` (Angular). If F5 is not complete, this plan cannot be executed.
- **Controller pattern consistency**: F5 establishes `OrdersController` with direct service injection (ADR 14). F6 extends the same controller with the same pattern for consistency — not MediatR. Future features may migrate to MediatR when domain logic is added.
- **Asset hard-coded to BTC-PERP (index 0)**: Per POC scope, no asset selector. The cancel/modify actions use integer asset index 0.
- **Order detail view (AC12) deferred**: No order detail view component exists in the codebase. AC12 references an entry point that will be addressed in a future feature. All other entry points (row buttons, context menu) are implemented.
- **Same signing flow**: Cancel and modify reuse F5's signing pipeline — only action payload differs. No new signing types needed.
- **GTC only**: Modify operations default to GTC (Good Till Cancel) time-in-force, matching F5.
- **No reduce-only**: Modify does not support `reduce_only` flag (hardcoded `false`).

## Dependencies

- **F5 — Order Placement** (must be complete): OrdersController, signing, /exchange, NonceProvider, ConfirmDialogComponent, OrderService
- `@angular/material ^19.2` — MatDialog (from F5), MatMenu (new for context menu), MatSnackBar (existing)
- `@angular/forms` — ReactiveFormsModule (from F5)
- No new NuGet or npm packages required beyond what F5 introduces

## Success Criteria

- `DELETE /api/orders/{orderId}` cancels a single order on Hyperliquid testnet
- `DELETE /api/orders?asset=BTC` cancels all open orders for BTC on Hyperliquid testnet
- `PUT /api/orders/{orderId}` modifies an order's price and/or size on Hyperliquid testnet
- Cancel and modify operations correctly sign and submit to Hyperliquid's `/exchange` endpoint
- Confirmation dialog appears before all destructive actions
- Optimistic UI updates remove/modify orders immediately and revert on failure
- Error toasts display meaningful messages from Hyperliquid error responses
- Row-level loading state disables buttons and shows spinner during API calls
- Context menu provides cancel and modify options on right-click
- Modify dialog validates price > 0 and size > 0 before submission
- Backend validation rejects invalid modify parameters (price ≤ 0, size ≤ 0)
- All new and existing tests pass
- Frontend builds and lints without errors

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-03-25T12:04:26Z | 2026-03-25T12:26:27Z |
| Plan Reviewer | plan-reviewed | 2026-03-25T12:27:11Z | 2026-03-25T12:32:28Z |
| Plan Implementer | implemented | 2026-03-25T16:17:25Z | 2026-03-25T18:28:25Z |
| Implementation Reviewer | reviewing | 2026-03-25T20:12:28Z | |
