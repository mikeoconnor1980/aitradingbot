---
applyTo: ".agent-context/3-develop/build/changes/20260329-stop-loss-take-profit-changes.md"
currentAgent: "None"
agentStartedAt: "2026-03-29T18:11:11Z"
status: "complete"
lastUpdated: "2026-03-29T18:11:11Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: Stop Loss & Take Profit

## Overview

Add Stop Loss (SL) and Take Profit (TP) functionality to the trading platform — extending the Place Order form with optional SL/TP fields, displaying/editing SL/TP on the positions table, adding backend trigger order API endpoints, and integrating with Hyperliquid's native trigger order (`tpsl`) support.

## PBI Details

### User Story

> As a **trader**, I want to **set stop loss and take profit levels on my positions and new orders** so that **my downside risk is capped and profits are taken automatically without manual monitoring**.

### Business Value

- Reduces risk of catastrophic losses from unmonitored positions
- Matches baseline feature parity with all major perp trading interfaces (Binance, Bybit, Hyperliquid UI)
- Essential for any production trading tool — positions without SL are unbounded risk

### Acceptance Criteria

- [ ] **Given** a trader is placing a new order, **When** they toggle "Add SL/TP" and enter a stop loss price, **Then** a trigger order with that price is placed on Hyperliquid immediately alongside the main order
- [ ] **Given** a trader is placing a new order, **When** they toggle "Add SL/TP" and enter a take profit price, **Then** a trigger order with that price is placed on Hyperliquid immediately alongside the main order
- [ ] **Given** a trader enters a SL price above entry for a long position, **When** they attempt to submit, **Then** a validation error prevents submission with message "Stop loss must be below entry price for long positions"
- [ ] **Given** a trader enters a TP price below entry for a long position, **When** they attempt to submit, **Then** a validation error prevents submission with message "Take profit must be above entry price for long positions"
- [ ] **Given** a trader sets SL but not TP (or vice versa), **When** they review the order, **Then** a non-blocking warning is displayed
- [ ] **Given** an open position without SL/TP, **When** the trader clicks "Set SL/TP" on the position row, **Then** a dialog appears with SL and TP price inputs
- [ ] **Given** an open position with existing SL/TP, **When** the trader clicks the displayed SL or TP value, **Then** an inline edit activates allowing them to change the price
- [ ] **Given** an open position with an active SL trigger order, **When** the trader removes the SL, **Then** the trigger order is cancelled on the exchange and the UI updates
- [ ] **Given** trigger orders exist for a position on the exchange, **When** the positions table loads, **Then** the SL and TP values are displayed on the position row
- [ ] **Given** a trigger order placement fails on the exchange, **When** the error is returned, **Then** an error notification is shown and the main order is unaffected
- [ ] **Given** a trader enters a SL price beyond the liquidation price, **When** the value is entered, **Then** a warning is displayed: "Stop loss is beyond your liquidation price ($X)"

## Objectives

- Implement Hyperliquid trigger order (`tpsl`) placement, modification, and cancellation via the backend API
- Extend position responses with SL/TP data by enriching from open trigger orders
- Add SL/TP optional fields to the Place Order form with collapsible toggle and validation
- Add SL/TP display, set dialog, and inline editing to the Positions table
- Cover all backend changes with unit and integration tests

### Discovery References

- **ADR 14** (`.agent-context/0-knowledge/10-architecture-decisions.md`): Direct service injection for exchange reads/writes — no MediatR for order operations
- **Hyperliquid trigger format**: `"t": { "trigger": { "triggerPx": "...", "isMarket": true, "tpsl": "sl"|"tp" } }` with `"r": true` (reduce-only)
- **Exchange is source of truth**: No local persistence of SL/TP intent — trigger order state lives on Hyperliquid
- **Cancel mechanism is identical**: `HyperliquidCancelAction` works for both regular and trigger orders
- **Position enrichment pattern**: `HyperliquidAccountService.MapToPositions` already merges clearinghouse state + asset contexts; adding open-order correlation follows the same parallel-fetch pattern

### Project Patterns

- `src/TradingApp.Api/Services/HyperliquidOrderService.cs` — Order submission pattern with `SubmitExchangeActionAsync` (signing + submit)
- `src/TradingApp.Api/Services/HyperliquidAccountService.cs` — Position/order mapping with `MapToPositions`, `MapToOpenOrders`, `GetOrderType`
- `src/TradingApp.Infrastructure/Hyperliquid/HyperliquidEip712.cs` — `BuildOrderAction` dictionary-based action construction for MessagePack compatibility
- `src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidModifyAction.cs` — Typed modify action model (`HyperliquidOrderType`)
- `src/TradingApp.Api/Controllers/OrdersController.cs` — Direct service injection controller pattern (no MediatR)
- `src/TradingApp.Api/Models/PlaceOrderRequest.cs` — DataAnnotations-based request validation
- `frontend/trading-ui/src/app/features/order-entry/order-entry.component.ts` — Typed reactive form with conditional validators
- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts` — Presentation component with expandable rows
- `frontend/trading-ui/src/app/features/dashboard/orders-table/modify-order-modal/modify-order.modal.component.ts` — MatDialog modal pattern
- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — Smart container opening dialogs, handling API calls
- `tests/TradingApp.Api.Tests/Controllers/OrdersControllerTests.cs` — Controller integration test pattern (BaseControllerTests)
- `tests/TradingApp.Api.Tests/Services/HyperliquidOrderServiceTests.cs` — Service unit test pattern

### [x] Phase 1: Backend — Trigger Order Infrastructure & API

**Complexity**: High | **Risk**: Medium

- [x] Task 1.1: Create trigger order request/response models
  - Details: .agent-context/3-develop/build/plans/details/20260329-stop-loss-take-profit-phase-01-details.md#task-11-create-trigger-order-request-response-models

- [x] Task 1.2: Extend OpenOrderDto with trigger order fields
  - Details: .agent-context/3-develop/build/plans/details/20260329-stop-loss-take-profit-phase-01-details.md#task-12-extend-openorderdto-with-trigger-order-fields

- [x] Task 1.3: Add BuildTriggerOrderAction to HyperliquidEip712
  - Details: .agent-context/3-develop/build/plans/details/20260329-stop-loss-take-profit-phase-01-details.md#task-13-add-buildtriggerorderaction-to-hyperliquideip712

- [x] Task 1.4: Extend HyperliquidModifyAction for trigger orders
  - Details: .agent-context/3-develop/build/plans/details/20260329-stop-loss-take-profit-phase-01-details.md#task-14-extend-hyperliquidmodifyaction-for-trigger-orders

- [x] Task 1.5: Add trigger order methods to IHyperliquidOrderService and HyperliquidOrderService
  - Details: .agent-context/3-develop/build/plans/details/20260329-stop-loss-take-profit-phase-01-details.md#task-15-add-trigger-order-methods-to-ihyperliquidorderservice-and-hyperliquidorderservice

- [x] Task 1.6: Parse trigger order details in HyperliquidAccountService.MapToOpenOrders
  - Details: .agent-context/3-develop/build/plans/details/20260329-stop-loss-take-profit-phase-01-details.md#task-16-parse-trigger-order-details-in-hyperliquidaccountservicemaptoopenorders

- [x] Task 1.7: Enrich PositionDto with SL/TP from open trigger orders
  - Details: .agent-context/3-develop/build/plans/details/20260329-stop-loss-take-profit-phase-01-details.md#task-17-enrich-positiondto-with-sltp-from-open-trigger-orders

- [x] Task 1.8: Extend PlaceOrderRequest and PlaceOrderAsync for companion SL/TP trigger orders
  - Details: .agent-context/3-develop/build/plans/details/20260329-stop-loss-take-profit-phase-01-details.md#task-18-extend-placeorderrequest-and-placeorderasync-for-companion-sltp-trigger-orders

- [x] Task 1.9: Add trigger order controller endpoints to OrdersController
  - Details: .agent-context/3-develop/build/plans/details/20260329-stop-loss-take-profit-phase-01-details.md#task-19-add-trigger-order-controller-endpoints-to-orderscontroller

- [x] Task 1.10: Write unit tests for HyperliquidOrderService trigger methods
  - Details: .agent-context/3-develop/build/plans/details/20260329-stop-loss-take-profit-phase-01-details.md#task-110-write-unit-tests-for-hyperliquidorderservice-trigger-methods

- [x] Task 1.11: Write controller integration tests for trigger order endpoints
  - Details: .agent-context/3-develop/build/plans/details/20260329-stop-loss-take-profit-phase-01-details.md#task-111-write-controller-integration-tests-for-trigger-order-endpoints

- [x] Task 1.12: Build solution and run all tests
  - Details: .agent-context/3-develop/build/plans/details/20260329-stop-loss-take-profit-phase-01-details.md#task-112-build-solution-and-run-all-tests

### [x] Phase 2: Frontend — Order Entry with SL/TP

**Complexity**: Medium | **Risk**: Low

- [x] Task 2.1: Extend PlaceOrderRequest model with SL/TP fields
  - Details: .agent-context/3-develop/build/plans/details/20260329-stop-loss-take-profit-phase-02-details.md#task-21-extend-placeorderrequest-model-with-sltp-fields

- [x] Task 2.2: Add SL/TP toggle section to order-entry component
  - Details: .agent-context/3-develop/build/plans/details/20260329-stop-loss-take-profit-phase-02-details.md#task-22-add-sltp-toggle-section-to-order-entry-component

- [x] Task 2.3: Add cross-field validation for SL/TP prices
  - Details: .agent-context/3-develop/build/plans/details/20260329-stop-loss-take-profit-phase-02-details.md#task-23-add-cross-field-validation-for-sltp-prices

- [x] Task 2.4: Update confirm dialog to display SL/TP values
  - Details: .agent-context/3-develop/build/plans/details/20260329-stop-loss-take-profit-phase-02-details.md#task-24-update-confirm-dialog-to-display-sltp-values

- [x] Task 2.5: Add partial SL/TP warning
  - Details: .agent-context/3-develop/build/plans/details/20260329-stop-loss-take-profit-phase-02-details.md#task-25-add-partial-sltp-warning

- [x] Task 2.6: Build frontend and lint
  - Details: .agent-context/3-develop/build/plans/details/20260329-stop-loss-take-profit-phase-02-details.md#task-26-build-frontend-and-lint

### [x] Phase 3: Frontend — Positions Table SL/TP Management

**Complexity**: High | **Risk**: Medium

- [x] Task 3.1: Extend Position and OpenOrder models with SL/TP fields
  - Details: .agent-context/3-develop/build/plans/details/20260329-stop-loss-take-profit-phase-03-details.md#task-31-extend-position-and-openorder-models-with-sltp-fields

- [x] Task 3.2: Add trigger order API methods to OrderService
  - Details: .agent-context/3-develop/build/plans/details/20260329-stop-loss-take-profit-phase-03-details.md#task-32-add-trigger-order-api-methods-to-orderservice

- [x] Task 3.3: Display SL/TP columns in positions table
  - Details: .agent-context/3-develop/build/plans/details/20260329-stop-loss-take-profit-phase-03-details.md#task-33-display-sltp-columns-in-positions-table

- [x] Task 3.4: Create Set SL/TP dialog component
  - Details: .agent-context/3-develop/build/plans/details/20260329-stop-loss-take-profit-phase-03-details.md#task-34-create-set-sltp-dialog-component

- [x] Task 3.5: Add inline editing for existing SL/TP values
  - Details: .agent-context/3-develop/build/plans/details/20260329-stop-loss-take-profit-phase-03-details.md#task-35-add-inline-editing-for-existing-sltp-values

- [x] Task 3.6: Wire SL/TP actions in dashboard component
  - Details: .agent-context/3-develop/build/plans/details/20260329-stop-loss-take-profit-phase-03-details.md#task-36-wire-sltp-actions-in-dashboard-component

- [x] Task 3.7: Add SL/TP removal (cancel trigger order)
  - Details: .agent-context/3-develop/build/plans/details/20260329-stop-loss-take-profit-phase-03-details.md#task-37-add-sltp-removal-cancel-trigger-order

- [x] Task 3.8: Build frontend and lint
  - Details: .agent-context/3-develop/build/plans/details/20260329-stop-loss-take-profit-phase-03-details.md#task-38-build-frontend-and-lint

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: Backend — Trigger Order Infrastructure & API | High | Medium |
| Phase 2: Frontend — Order Entry with SL/TP | Medium | Low |
| Phase 3: Frontend — Positions Table SL/TP Management | High | Medium |
| **Total** | **High** | **Medium** |

### Scoping Notes

- Exchange is the sole source of truth for trigger order state — no new DB tables or migrations
- SL/TP trigger orders execute as market orders when triggered (Hyperliquid `tpsl` type with `isMarket: true`)
- When both SL and TP are placed alongside a main order, each is a separate exchange call — if one fails, the main order is unaffected (acceptable POC tradeoff)
- Cancel mechanism for trigger orders is identical to regular orders (`HyperliquidCancelAction`)
- Position enrichment with SL/TP is derived by correlating open trigger orders to positions by asset at read time
- Cross-field validation (SL below/above entry based on side) is client-side for all orders and server-side via DomainException for standalone trigger orders
- Partial SL/TP warning is non-blocking (informational only)
- `HyperliquidModifyAction` for trigger orders requires the `trigger` type variant instead of `limit`

## Dependencies

- Hyperliquid Exchange API (`tpsl` trigger order type)
- Angular Material (MatDialog, MatFormField, MatSnackBar)
- Existing `HyperliquidOrderService`, `HyperliquidAccountService`, `HyperliquidEip712` infrastructure

## Success Criteria

- All trigger order API endpoints (POST/PUT/DELETE) work with Hyperliquid exchange
- Positions response includes SL/TP prices derived from open trigger orders
- Place Order form supports optional SL/TP with validation and confirmation
- Positions table displays SL/TP, supports initial setup via dialog, inline editing, and removal
- All backend tests pass (unit + controller integration)
- Frontend builds and lints cleanly

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-03-29T16:06:39Z | 2026-03-29T16:21:14Z |
| Plan Reviewer | plan-reviewed | 2026-03-29T16:21:55Z | 2026-03-29T16:31:07Z |
| Plan Implementer | implemented | 2026-03-29T16:38:18Z | 2026-03-29T17:03:25Z |
| Implementation Reviewer | complete | 2026-03-29T17:44:33Z | 2026-03-29T18:11:11Z |
