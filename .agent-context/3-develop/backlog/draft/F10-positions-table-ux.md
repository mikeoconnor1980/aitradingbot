# PBI Specification: F10 — Positions Table UX Enhancements

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-26
**PRD:** [hyperliquid-poc-prd.md](../../prd-approved/hyperliquid-poc-prd.md)
**Implementation Phase:** 7
**Risk Level:** Low
**Depends On:** F2, F6.1

---

## Summary

Enhance the positions table and account summary with bulk actions (Close All), column sorting, asset filtering, and a visual Cross Margin Ratio indicator. These are table-level UX improvements that make the dashboard more usable as position count grows.

### User Story

> As a **trader**, I want to **sort, filter, and bulk-manage my positions, and visually assess my margin health** so that **I can quickly find relevant positions, act on multiple positions at once, and understand my liquidation risk at a glance**.

### Business Value

As the number of open positions increases, the flat unsorted table becomes harder to navigate. Sorting by PnL lets traders prioritise attention on winners or losers. Filtering by asset reduces noise. Close All is a critical safety mechanism for emergency exits. The Cross Margin Ratio indicator provides an at-a-glance visual for how close the account is to liquidation — a fundamental risk metric that is currently only displayed as a raw number.

---

## Problem Statement

The positions table is a flat, unsorted, unfiltered list with no bulk actions. The Cross Margin Ratio in the account summary card is a raw decimal number (0.0629) with no visual context — the user must know what "good" vs "dangerous" values are. These limitations reduce usability and situational awareness as position count grows.

---

## Requirements

### Functional Requirements

#### Close All Positions

- [ ] "Close All Positions" button displayed above the positions table (visible only when positions exist)
- [ ] Clicking opens a confirmation dialog listing all open positions and their sizes
- [ ] On confirmation, market close orders are submitted for each open position (sequentially or in parallel, depending on API constraints)
- [ ] Progress indicator showing X/N positions closed
- [ ] Summary toast on completion: "Closed N positions" or "Closed N/M positions (K failed)"
- [ ] Button disabled while any individual position action is in-flight
- [ ] Failed individual closes do not block remaining closes — best-effort approach

#### Column Sorting

- [ ] Positions table column headers are clickable to sort
- [ ] Sortable columns: Asset (alphabetical), Size (absolute value, descending), Unrealised PnL (value, descending), Entry Price, Liquidation Price
- [ ] Default sort: none (order as returned by API)
- [ ] Sort cycles through: ascending → descending → no sort
- [ ] Visual sort indicator (arrow icon) on the active sort column
- [ ] Sorting is client-side only (no API changes needed for POC position counts)

#### Filter/Search

- [ ] Search input field above the positions table
- [ ] Filters positions by asset name (case-insensitive substring match)
- [ ] Filtering is instant (client-side) as the user types
- [ ] "X results" count shown when filter is active
- [ ] Clear button (×) to reset the filter
- [ ] Empty state: "No positions matching 'X'" when filter yields no results

#### Cross Margin Ratio Visual Indicator

- [ ] Cross Margin Ratio in the account summary card includes a progress bar or gauge
- [ ] Color coding: green (0–0.30), yellow (0.30–0.60), orange (0.60–0.80), red (0.80–1.00)
- [ ] Tooltip or label explaining the thresholds: "Low risk", "Moderate", "Elevated", "Critical — near liquidation"
- [ ] The numeric value remains displayed alongside the visual indicator
- [ ] At values ≥ 0.80, an additional warning icon or pulsing animation draws attention

### Non-Functional Requirements

- [ ] Sorting and filtering must be instant — client-side operations only
- [ ] Close All must handle up to 20 positions without UI freezing
- [ ] Visual indicator recalculates on each data refresh (2s polling from F2)

---

## User Flow

### Happy Path — Close All Positions

1. User has 5 open positions visible in the table
2. User clicks "Close All Positions" button
3. Confirmation dialog lists all 5 positions: BTC Short, ETH Long, OP Long, SUI Short, DOGE Short
4. User confirms
5. Progress indicator: "Closing positions... 1/5, 2/5, ... 5/5"
6. Success toast: "Closed 5 positions"
7. Dashboard refreshes; positions table shows empty state

### Happy Path — Sort by PnL

1. User clicks "Unrealised PnL" column header
2. Positions sort descending (best PnL first): BTC (+64.13), SUI (+2.15), OP (+0.02), ETH (−22.19)
3. Arrow indicator shows ↓ on PnL column
4. User clicks again → ascending sort (worst first)
5. User clicks a third time → sort removed, original order restored

### Happy Path — Filter by Asset

1. User types "BT" in the search field
2. Table filters to show only the BTC position
3. "1 result" indicator shown
4. User clicks × to clear; all positions return

### Happy Path — Cross Margin Ratio Indicator

1. Dashboard loads with Cross Margin Ratio: 0.0629
2. Progress bar shows a thin green fill (~6.3%)
3. Tooltip: "Low risk"
4. If ratio increases to 0.85 after a market move, bar turns red with a pulsing warning icon

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| Close All — 2 of 5 positions fail | Toast: "Closed 3/5 positions (2 failed)"; failed positions remain in table |
| Close All — all fail (exchange down) | Toast: "Failed to close positions"; all remain |
| Close All — user cancels confirmation | No action taken |
| Filter — no matches | "No positions matching 'XYZ'" shown in table area |
| Sort — tie in sort value | Positions with equal sort values retain their relative API order |

---

## Technical Considerations

### API Endpoints

No new endpoints required. All operations use existing endpoints.

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/orders` | Place market close orders (one per position for Close All) |
| GET | `/api/account` | Account summary including Cross Margin Ratio |
| GET | `/api/positions` | Positions list (sorting/filtering is client-side) |

### Key Components

| Component | Action |
|-----------|--------|
| `PositionsTableComponent` | Add sort headers, filter input, Close All button |
| `CloseAllDialogComponent` | New confirmation dialog with position list and progress |
| `AccountSummaryComponent` | Add margin ratio progress bar with color thresholds |
| `MarginRatioIndicatorComponent` | New reusable component for the visual gauge |
| `OrderService` (Angular) | Reuse `placeOrder()` for Close All (multiple calls) |

### Sorting Implementation

- Use Angular `MatSort` or a custom sort pipe
- Sort state tracked in component: `{ column: string, direction: 'asc' | 'desc' | null }`
- Applied to the in-memory positions array before rendering

### Cross Margin Ratio Thresholds

| Range | Color | Label |
|-------|-------|-------|
| 0.00–0.30 | Green | Low risk |
| 0.30–0.60 | Yellow | Moderate |
| 0.60–0.80 | Orange | Elevated |
| 0.80–1.00 | Red (pulsing) | Critical — near liquidation |

---

## Out of Scope

- Server-side sorting or pagination (not needed for POC position counts)
- Multi-column sort
- Saved sort/filter preferences
- Close All with limit orders
- Column visibility customisation
- Export positions to CSV

---

## Open Questions

1. Should Close All fire requests in parallel or sequentially? Parallel is faster but may hit rate limits.
2. What are the actual Hyperliquid Cross Margin Ratio thresholds that correspond to meaningful risk levels? The proposed thresholds are sensible defaults but may need tuning.

---

## Acceptance Criteria

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

---

## Related Features

- **F2** — Account Dashboard (the base table and summary card being enhanced)
- **F6.1** — Close Position (Close All reuses the same close logic per position)
- **F9** — Position Actions (per-row actions complement these table-level actions)
