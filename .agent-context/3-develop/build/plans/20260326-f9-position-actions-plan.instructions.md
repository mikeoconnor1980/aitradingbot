---
applyTo: ".agent-context/3-develop/build/changes/20260326-f9-position-actions-changes.md"
currentAgent: "None"
agentStartedAt: "2026-03-26T16:40:48Z"
status: "plan-reviewed"
lastUpdated: "2026-03-26T16:48:50Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: F9 — Position Actions

## Overview

Extend the positions table with advanced per-position actions: Take Profit / Stop Loss placement, partial close, reverse position, and an expandable position detail view.

## PBI Details

**PBI ID:** Draft — F9
**Status:** Draft
**Depends On:** F2, F5, F6, F6.1

### Summary

As a **trader**, I want to **set TP/SL, partially close, reverse, and inspect positions directly from the positions table** so that **I can manage risk and adjust exposure without navigating away from the dashboard**.

### Acceptance Criteria

- [ ] **Given** I have an open position, **When** I view the positions table, **Then** I see TP/SL, Partial Close, and Reverse actions available for each row
- [ ] **Given** I click TP/SL on a Long position, **When** the dialog opens, **Then** I see fields for take-profit and stop-loss prices with the entry price shown for reference
- [ ] **Given** I set valid TP and SL prices, **When** I confirm, **Then** two orders are placed (limit for TP, trigger for SL) and a success toast is shown
- [ ] **Given** I enter an invalid TP price (below entry for a long), **When** I try to submit, **Then** a validation error is shown and submission is prevented
- [ ] **Given** I select Partial Close on a position, **When** the dialog opens, **Then** I can choose a percentage (25%, 50%, 75%) or enter a custom size
- [ ] **Given** I confirm a 50% partial close, **When** the order is placed, **Then** the position size is reduced by half after dashboard refresh
- [ ] **Given** I click Reverse on a Short position, **When** I confirm, **Then** a market buy order for 2× the position size is placed and the position flips to Long
- [ ] **Given** I click a position row, **When** the detail panel expands, **Then** I see entry price, mark price, liquidation price, margin used, notional value, leverage, funding rate, and associated TP/SL orders
- [ ] **Given** a detail panel is expanded, **When** I click another position row, **Then** the previous panel collapses and the new one expands
- [ ] **Given** any action is in-flight, **When** I view the position row, **Then** the action buttons are disabled with a loading indicator

## Objectives

- Support Hyperliquid trigger orders (TP/SL) through the existing `POST /api/orders` endpoint
- Expose `reduceOnly` flag in the PlaceOrderRequest for partial close and TP/SL orders
- Enrich `PositionDto` with `marginUsed` and `positionValue` for the position detail panel
- Create frontend modals for TP/SL and partial close with proper validation
- Add expandable position detail rows to the positions table
- All position actions use row-level loading and optimistic UI patterns

### Discovery References

**Backend gaps identified:**
- `PlaceOrderRequest.OrderType` regex (`^(market|limit)$`) blocks trigger orders — must be extended
- `HyperliquidEip712.BuildOrderAction` only builds `limit` type wire payload — trigger branch needed: `{ "trigger": { "triggerPx", "isMarket", "tpsl" } }`
- `reduceOnly` parameter exists in `BuildOrderAction` but not exposed through `PlaceOrderRequest`
- `PositionDto` is missing `marginUsed` and `positionValue` fields (available in `clearinghouseState`)
- Grouping field hardcoded to `"na"` — TP/SL orders require `"normalTpsl"`
- Trigger orders require `limit_px` to be set even for stop-market (Python SDK uses `0.0` for market triggers)

**Frontend gaps identified:**
- No expandable row pattern exists — net-new component
- `Position` model missing `marginUsed`, `positionValue`
- `PlaceOrderRequest` model missing `triggerPrice`, `reduceOnly`, `tpSlType`
- Angular naming standard requires `.modal.component.ts` suffix (not "dialog")

### Project Patterns

- `src/TradingApp.Api/Controllers/OrdersController.cs` — Order endpoints; direct service injection (ADR-14)
- `src/TradingApp.Api/Models/PlaceOrderRequest.cs` — Order request DTO (extend for trigger orders)
- `src/TradingApp.Api/Services/HyperliquidOrderService.cs` — Order placement pipeline (signing, mid-price, slippage)
- `src/TradingApp.Infrastructure/Hyperliquid/HyperliquidEip712.cs` — EIP-712 `BuildOrderAction` (add trigger branch)
- `src/TradingApp.Infrastructure/Hyperliquid/Models/HyperliquidModifyAction.cs` — Wire format models (`HyperliquidOrderType`)
- `src/TradingApp.Api/Services/HyperliquidAccountService.cs` — Position mapping (`MapToPositions`)
- `src/TradingApp.Api/Models/PositionDto.cs` — Position DTO (extend for detail panel)
- `frontend/.../positions-table/positions-table.component.ts` — Row-level loading, close emit
- `frontend/.../dashboard/dashboard.component.ts` — Position action orchestration, optimistic UI
- `frontend/.../orders-table/modify-order-modal/modify-order.modal.component.ts` — Form modal pattern
- `frontend/.../order-entry/confirm-dialog/confirm-dialog.component.ts` — Confirmation dialog (reuse for reverse)
- `frontend/.../core/models/position.model.ts` — Angular position model
- `frontend/.../core/models/place-order.model.ts` — Angular place order request model
- `tests/TradingApp.Api.Tests/Controllers/OrdersControllerTests.cs` — Controller test pattern
- `tests/TradingApp.Api.Tests/Services/HyperliquidOrderServiceTests.cs` — Service unit test pattern

### [ ] Phase 1: Backend — Trigger Order Support & Position Enrichment

**Complexity**: High | **Risk**: Medium

- [ ] Task 1.1: Extend PlaceOrderRequest with trigger order fields
  - Details: .agent-context/3-develop/build/plans/details/20260326-f9-position-actions-phase-01-details.md#task-11-extend-placeorderrequest-with-trigger-order-fields

- [ ] Task 1.2: Add HyperliquidTriggerParams infrastructure model
  - Details: .agent-context/3-develop/build/plans/details/20260326-f9-position-actions-phase-01-details.md#task-12-add-hyperliquidtriggerparams-infrastructure-model

- [ ] Task 1.3: Extend BuildOrderAction with trigger order type branch
  - Details: .agent-context/3-develop/build/plans/details/20260326-f9-position-actions-phase-01-details.md#task-13-extend-buildorderaction-with-trigger-order-type-branch

- [ ] Task 1.4: Extend HyperliquidOrderService for trigger orders
  - Details: .agent-context/3-develop/build/plans/details/20260326-f9-position-actions-phase-01-details.md#task-14-extend-hyperliquidorderservice-for-trigger-orders

- [ ] Task 1.5: Enrich PositionDto with margin and notional fields
  - Details: .agent-context/3-develop/build/plans/details/20260326-f9-position-actions-phase-01-details.md#task-15-enrich-positiondto-with-margin-and-notional-fields

- [ ] Task 1.6: Backend tests for trigger orders and position enrichment
  - Details: .agent-context/3-develop/build/plans/details/20260326-f9-position-actions-phase-01-details.md#task-16-backend-tests-for-trigger-orders-and-position-enrichment

- [ ] Task 1.7: Build and run all backend tests
  - Details: .agent-context/3-develop/build/plans/details/20260326-f9-position-actions-phase-01-details.md#task-17-build-and-run-all-backend-tests

### [ ] Phase 2: Frontend — Actions Menu & TP/SL Modal

**Complexity**: High | **Risk**: Medium

- [ ] Task 2.1: Update Angular models for trigger orders and position enrichment
  - Details: .agent-context/3-develop/build/plans/details/20260326-f9-position-actions-phase-02-details.md#task-21-update-angular-models-for-trigger-orders-and-position-enrichment

- [ ] Task 2.2: Add actions column to positions table
  - Details: .agent-context/3-develop/build/plans/details/20260326-f9-position-actions-phase-02-details.md#task-22-add-actions-column-to-positions-table

- [ ] Task 2.3: Create TpSlModalComponent
  - Details: .agent-context/3-develop/build/plans/details/20260326-f9-position-actions-phase-02-details.md#task-23-create-tpslmodalcomponent

- [ ] Task 2.4: Wire TP/SL flow in DashboardComponent
  - Details: .agent-context/3-develop/build/plans/details/20260326-f9-position-actions-phase-02-details.md#task-24-wire-tpsl-flow-in-dashboardcomponent

- [ ] Task 2.5: Frontend build and lint
  - Details: .agent-context/3-develop/build/plans/details/20260326-f9-position-actions-phase-02-details.md#task-25-frontend-build-and-lint

### [ ] Phase 3: Frontend — Partial Close & Reverse Position

**Complexity**: Medium | **Risk**: Low

- [ ] Task 3.1: Create PartialCloseModalComponent
  - Details: .agent-context/3-develop/build/plans/details/20260326-f9-position-actions-phase-03-details.md#task-31-create-partialclosemodalcomponent

- [ ] Task 3.2: Wire partial close flow in DashboardComponent
  - Details: .agent-context/3-develop/build/plans/details/20260326-f9-position-actions-phase-03-details.md#task-32-wire-partial-close-flow-in-dashboardcomponent

- [ ] Task 3.3: Wire reverse position flow in DashboardComponent
  - Details: .agent-context/3-develop/build/plans/details/20260326-f9-position-actions-phase-03-details.md#task-33-wire-reverse-position-flow-in-dashboardcomponent

- [ ] Task 3.4: Frontend build and lint
  - Details: .agent-context/3-develop/build/plans/details/20260326-f9-position-actions-phase-03-details.md#task-34-frontend-build-and-lint

### [ ] Phase 4: Frontend — Position Detail Panel

**Complexity**: Medium | **Risk**: Medium

- [ ] Task 4.1: Create PositionDetailPanelComponent
  - Details: .agent-context/3-develop/build/plans/details/20260326-f9-position-actions-phase-04-details.md#task-41-create-positiondetailpanelcomponent

- [ ] Task 4.2: Add expandable row behavior to positions table
  - Details: .agent-context/3-develop/build/plans/details/20260326-f9-position-actions-phase-04-details.md#task-42-add-expandable-row-behavior-to-positions-table

- [ ] Task 4.3: Wire position detail data and TP/SL order display
  - Details: .agent-context/3-develop/build/plans/details/20260326-f9-position-actions-phase-04-details.md#task-43-wire-position-detail-data-and-tpsl-order-display

- [ ] Task 4.4: Frontend build and lint
  - Details: .agent-context/3-develop/build/plans/details/20260326-f9-position-actions-phase-04-details.md#task-44-frontend-build-and-lint

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: Backend — Trigger Order Support & Position Enrichment | High | Medium |
| Phase 2: Frontend — Actions Menu & TP/SL Modal | High | Medium |
| Phase 3: Frontend — Partial Close & Reverse Position | Medium | Low |
| Phase 4: Frontend — Position Detail Panel | Medium | Medium |
| **Total** | **High** | **Medium** |

### Scoping Notes

- All position actions reuse `POST /api/orders` — no new backend endpoints except extending the existing request model
- Trigger order wire format based on Hyperliquid Python SDK reference (verified: `{ "trigger": { "triggerPx", "isMarket", "tpsl" } }`)
- `reduceOnly` must be `true` for all TP/SL and partial close orders to prevent accidental position opening
- TP/SL orders use `grouping: "normalTpsl"` (not `"na"`)
- Funding rate for position detail panel sourced from frontend market data (SignalR stream or `/api/market-data/info`) — no additional backend enrichment needed
- Angular modal naming convention: `*.modal.component.ts` with `ModalComponent` suffix (not "dialog")
- Existing `ConfirmDialogComponent` reused as-is for reverse position confirmation (predates naming convention)
- Position detail panel is a new pattern (no existing expandable row) — uses `@if` toggle with tracked `expandedPositionKey`

## Dependencies

- **F5** — Order Placement (provides `POST /api/orders`, `HyperliquidOrderService`, EIP-712 signing pipeline)
- **F6** — Order Management (cancel/modify orders, open orders query)
- **F6.1** — Close Position (provides close-position flow pattern in `DashboardComponent`, row-level loading)
- **Angular Material** — `MatDialogModule`, `MatMenuModule`, `MatFormFieldModule`, `MatInputModule`, `MatSliderModule`, `MatButtonToggleModule`
- **Hyperliquid API** — Trigger order support (`orderAction` with `trigger` type), `clearinghouseState` (position enrichment)

## Success Criteria

- All 10 acceptance criteria from the PBI pass
- `POST /api/orders` correctly places trigger (TP/SL) orders with proper EIP-712 signing and grouping
- `reduceOnly` flag properly forwarded to Hyperliquid for partial close and TP/SL orders
- Position detail panel displays `marginUsed`, `positionValue`, and funding rate from available data
- All backend tests pass (new trigger order tests + existing order tests unchanged)
- Frontend builds and lints without errors after each phase
- Row-level loading states prevent duplicate action submissions

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-03-26T16:17:36Z | 2026-03-26T16:40:00Z |
| Plan Reviewer | plan-reviewed | 2026-03-26T16:40:48Z | 2026-03-26T16:48:50Z |
