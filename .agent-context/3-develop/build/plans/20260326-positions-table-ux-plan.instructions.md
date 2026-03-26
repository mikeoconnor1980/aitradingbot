---
applyTo: ".agent-context/3-develop/build/changes/20260326-positions-table-ux-changes.md"
currentAgent: "3-Develop: 2 Implementer"
agentStartedAt: "2026-03-26T17:00:00Z"
status: "implemented"
lastUpdated: "2026-03-26T17:30:00Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: F10 — Positions Table UX Enhancements

## Overview

Enhance the positions table and account summary with column sorting, asset filtering, a bulk Close All action, and a visual Cross Margin Ratio indicator — all client-side improvements to the existing Angular dashboard.

## PBI Details

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**PRD:** hyperliquid-poc-prd.md
**Implementation Phase:** 7
**Risk Level:** Low
**Depends On:** F2, F6.1

### Summary

> As a **trader**, I want to **sort, filter, and bulk-manage my positions, and visually assess my margin health** so that **I can quickly find relevant positions, act on multiple positions at once, and understand my liquidation risk at a glance**.

### Acceptance Criteria

- [ ] **Given** I have open positions, **When** I view the positions table, **Then** I see a "Close All Positions" button above the table
- [ ] **Given** I click "Close All Positions" and confirm, **When** all close orders are submitted, **Then** a progress indicator shows X/N and a summary toast is displayed
- [ ] **Given** some close orders fail during Close All, **When** the operation completes, **Then** the toast shows "Closed X/N positions (Y failed)" and failed positions remain
- [ ] **Given** I have no open positions, **When** I view the positions table, **Then** the "Close All Positions" button is not visible
- [ ] **Given** I click the "Unrealised PnL" column header, **When** the sort activates, **Then** positions are sorted by PnL (descending first) with a visual arrow indicator
- [ ] **Given** I click a sorted column header three times, **When** the third click occurs, **Then** the sort is removed and the original order is restored
- [ ] **Given** I type "ETH" in the filter field, **When** filtering is applied, **Then** only positions with "ETH" in the asset name are shown
- [ ] **Given** the filter matches no positions, **When** I view the table, **Then** "No positions matching 'X'" is displayed
- [ ] **Given** the Cross Margin Ratio is 0.06, **When** the account summary renders, **Then** a green progress bar is shown with "Low risk" tooltip
- [ ] **Given** the Cross Margin Ratio is 0.85, **When** the account summary renders, **Then** a red pulsing progress bar is shown with "Critical — near liquidation" tooltip
- [ ] **Given** the Cross Margin Ratio changes on refresh, **When** new data arrives, **Then** the progress bar color and fill update accordingly

## Objectives

- Add a color-coded Cross Margin Ratio progress bar with threshold labels and pulsing animation at critical levels
- Make all positions table columns sortable with ascending/descending/none cycle and visual sort indicators
- Add an instant client-side asset name filter with result count and clear button
- Implement a "Close All Positions" flow with confirmation dialog, sequential execution, progress tracking, and partial-failure handling
- Create Angular unit tests for all new and modified components

### Discovery References

**Design Decision — Frontend-Orchestrated Close All:**
No new backend endpoints. Close All is orchestrated on the frontend by calling `POST /api/orders` sequentially for each position (avoids Hyperliquid nonce collision from parallel calls sharing the same millisecond timestamp). This follows the existing Cancel All Orders pattern from `OrdersTableComponent`.

**Design Decision — Sequential Close Dispatch:**
`HyperliquidOrderService` uses `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()` for nonces. Parallel requests in the same millisecond would collide. Close All dispatches sequentially using RxJS `concat` + `scan`.

**crossMarginRatio Format:**
Value is a decimal ratio (0–1) computed server-side as `maintenanceMargin / equity`. The UI must multiply by 100 for percentage display and cap at 100% fill.

**Asset Format:**
`PositionDto.Asset` / `Position.asset` is the bare coin symbol (e.g., `"BTC"`). The existing `onClosePosition` flow uses `position.asset` directly — the backend `HyperliquidOrderService` handles any format conversion internally.

### Project Patterns

- `frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.ts` — Cancel All pattern with `globalLoading`, `@Output cancelAllOrders`, `setGlobalLoading()`
- `frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.html` — Cancel All button layout in header bar
- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — `onClosePosition()` close order pattern, `onCancelAllOrders()` bulk action pattern, optimistic UI + rollback
- `frontend/trading-ui/src/app/features/order-entry/confirm-dialog/confirm-dialog.component.ts` — `ConfirmDialogComponent` with `MAT_DIALOG_DATA`
- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts` — Current presentational component with row-level loading
- `frontend/trading-ui/src/app/features/dashboard/account-summary/account-summary.component.ts` — Current account summary rendering
- `frontend/trading-ui/src/app/core/services/order.service.ts` — `placeOrder()` for market close orders
- `frontend/trading-ui/src/app/core/services/notification.service.ts` — `success()`, `error()`, `warning()` snackbar toasts
- `frontend/trading-ui/src/styles.scss` — CSS custom properties (`--colour-profit`, `--colour-loss`, `--colour-muted`), Material dark theme, `@keyframes pulse` reference in `app.component.scss`

### [x] Phase 1: Cross Margin Ratio Visual Indicator

**Complexity**: Low | **Risk**: Low

- [x] Task 1.1: Create the `MarginRatioIndicatorComponent`
  - Details: .agent-context/3-develop/build/plans/details/20260326-positions-table-ux-phase-01-details.md#task-11-create-margin-ratio-indicator-component

- [x] Task 1.2: Add CSS custom properties for warning and critical thresholds
  - Details: .agent-context/3-develop/build/plans/details/20260326-positions-table-ux-phase-01-details.md#task-12-add-css-custom-properties-for-thresholds

- [x] Task 1.3: Integrate `MarginRatioIndicatorComponent` into `AccountSummaryComponent`
  - Details: .agent-context/3-develop/build/plans/details/20260326-positions-table-ux-phase-01-details.md#task-13-integrate-into-account-summary-component

- [x] Task 1.4: Write unit tests for `MarginRatioIndicatorComponent`
  - Details: .agent-context/3-develop/build/plans/details/20260326-positions-table-ux-phase-01-details.md#task-14-write-unit-tests

- [x] Task 1.5: Run frontend build and lint
  - Details: .agent-context/3-develop/build/plans/details/20260326-positions-table-ux-phase-01-details.md#task-15-run-frontend-build-and-lint

### [x] Phase 2: Column Sorting & Asset Filter

**Complexity**: Medium | **Risk**: Low

- [x] Task 2.1: Add sort state and sort logic to `PositionsTableComponent`
  - Details: .agent-context/3-develop/build/plans/details/20260326-positions-table-ux-phase-02-details.md#task-21-add-sort-state-and-logic

- [x] Task 2.2: Update positions table template with sortable headers and sort indicators
  - Details: .agent-context/3-develop/build/plans/details/20260326-positions-table-ux-phase-02-details.md#task-22-update-template-with-sortable-headers

- [x] Task 2.3: Add filter input with result count and clear button
  - Details: .agent-context/3-develop/build/plans/details/20260326-positions-table-ux-phase-02-details.md#task-23-add-filter-input

- [x] Task 2.4: Add SCSS for sort indicators, filter bar, and empty state
  - Details: .agent-context/3-develop/build/plans/details/20260326-positions-table-ux-phase-02-details.md#task-24-add-scss-styles

- [x] Task 2.5: Write unit tests for sorting and filtering
  - Details: .agent-context/3-develop/build/plans/details/20260326-positions-table-ux-phase-02-details.md#task-25-write-unit-tests

- [x] Task 2.6: Run frontend build and lint
  - Details: .agent-context/3-develop/build/plans/details/20260326-positions-table-ux-phase-02-details.md#task-26-run-frontend-build-and-lint

### [x] Phase 3: Close All Positions

**Complexity**: Medium | **Risk**: Medium

- [x] Task 3.1: Create `CloseAllDialogComponent` with position list and progress
  - Details: .agent-context/3-develop/build/plans/details/20260326-positions-table-ux-phase-03-details.md#task-31-create-close-all-dialog-component

- [x] Task 3.2: Add Close All button and output to `PositionsTableComponent`
  - Details: .agent-context/3-develop/build/plans/details/20260326-positions-table-ux-phase-03-details.md#task-32-add-close-all-button-and-output

- [x] Task 3.3: Add `closeAllPositions()` method to `OrderService`
  - Details: .agent-context/3-develop/build/plans/details/20260326-positions-table-ux-phase-03-details.md#task-33-add-close-all-to-order-service

- [x] Task 3.4: Implement Close All handler in `DashboardComponent`
  - Details: .agent-context/3-develop/build/plans/details/20260326-positions-table-ux-phase-03-details.md#task-34-implement-close-all-in-dashboard

- [x] Task 3.5: Write unit tests for Close All flow
  - Details: .agent-context/3-develop/build/plans/details/20260326-positions-table-ux-phase-03-details.md#task-35-write-unit-tests

- [x] Task 3.6: Run frontend build and lint
  - Details: .agent-context/3-develop/build/plans/details/20260326-positions-table-ux-phase-03-details.md#task-36-run-frontend-build-and-lint

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: Cross Margin Ratio Visual Indicator | Low | Low |
| Phase 2: Column Sorting & Asset Filter | Medium | Low |
| Phase 3: Close All Positions | Medium | Medium |
| **Overall** | **Medium** | **Low–Medium** |

### Scoping Notes

- No backend changes required — all features are client-side Angular or reuse existing `POST /api/orders` endpoint
- Close All dispatches sequentially to avoid Hyperliquid nonce collisions (Open Question #1 resolved)
- CMR thresholds use PBI-defined ranges: green 0–0.30, yellow 0.30–0.60, orange 0.60–0.80, red 0.80–1.00 (Open Question #2 resolved with PBI defaults)
- No MatTableModule migration — sorting is applied to the existing `@for` loop over a computed `sortedFilteredPositions` getter
- All new components are `standalone: true` per Angular instructions
- Single-tenant POC — no multi-user scoping concerns

## Dependencies

- Angular Material 19 (`@angular/material ^19.2.19`) — already installed
- `MatProgressBarModule` — already used in `market-data.component.ts`
- `MatIconModule` — already used elsewhere; needs import in `PositionsTableComponent`
- `MatFormFieldModule` + `MatInputModule` — for the filter search input
- `MatTooltipModule` — for CMR threshold labels
- RxJS `concat`, `scan`, `toArray` — for sequential Close All dispatch

## Success Criteria

- All 11 PBI acceptance criteria pass
- Cross Margin Ratio indicator renders correctly at all 4 threshold levels with correct colors and labels
- Column sorting cycles through ascending → descending → none with visual indicators
- Asset filter instantly filters positions with result count and clear button
- Close All executes sequentially, shows progress, handles partial failures, and displays accurate summary toast
- All new components have passing unit tests
- Frontend builds and lints without errors

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-03-26T16:17:37Z | 2026-03-26T16:24:37Z |
| Plan Reviewer | plan-reviewed | 2026-03-26T16:30:13Z | 2026-03-26T16:36:00Z |
| 3-Develop: 2 Implementer | implemented | 2026-03-26T17:00:00Z | 2026-03-26T17:30:00Z |
