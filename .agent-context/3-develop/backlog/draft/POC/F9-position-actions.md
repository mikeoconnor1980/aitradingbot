# PBI Specification: F9 — Position Actions

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-26
**PRD:** [hyperliquid-poc-prd.md](../../prd-approved/hyperliquid-poc-prd.md)
**Implementation Phase:** 7
**Risk Level:** Medium
**Depends On:** F2, F5, F6, F6.1

---

## Summary

Extend the positions table with advanced per-position actions: Take Profit / Stop Loss placement, partial close, reverse position, and an expandable position detail view. These actions upgrade the positions table from basic close-only to a full position management surface.

### User Story

> As a **trader**, I want to **set TP/SL, partially close, reverse, and inspect positions directly from the positions table** so that **I can manage risk and adjust exposure without navigating away from the dashboard**.

### Business Value

The current positions table supports only full close (F6.1). Real-world trading requires granular risk management — TP/SL orders protect against adverse moves, partial closes allow scaling out of winners, and reverse lets a trader flip conviction quickly. A detail view provides context for better decisions. Together, these complete the position lifecycle and make the dashboard a viable trading surface.

---

## Problem Statement

The positions table only supports a full market-close action. Traders cannot:
- Set take-profit or stop-loss orders to automate exits
- Close a fraction of a position to scale out or reduce risk
- Reverse a position direction without two manual operations
- View detailed position information without switching to the Hyperliquid UI

These gaps force users back to the exchange UI for routine risk management tasks.

---

## Requirements

### Functional Requirements

#### TP/SL (Take Profit / Stop Loss)

- [ ] Each position row has a "TP/SL" button that opens a dialog
- [ ] TP/SL dialog allows setting a take-profit limit price and/or a stop-loss trigger price
- [ ] TP order is submitted as a limit order at the specified price on the opposite side
- [ ] SL order is submitted as a stop-market (trigger) order at the specified price on the opposite side
- [ ] Both TP and SL can be set simultaneously or individually
- [ ] Dialog shows current entry price and mark price for reference
- [ ] Validation: TP price must be above entry for longs (below for shorts); SL price must be below entry for longs (above for shorts)
- [ ] Success toast shown after order(s) placed; error toast on failure
- [ ] Existing TP/SL orders for a position are pre-populated in the dialog if they exist

#### Partial Close

- [ ] Each position row has a "Partial Close" option (accessible via the Actions menu or the Close button dropdown)
- [ ] Partial close dialog allows entering a size or percentage (25%, 50%, 75%, custom) to close
- [ ] Validation: size must be > 0 and ≤ position size
- [ ] Confirmation dialog shows asset, side, and the partial size being closed
- [ ] Submits a market order via `POST /api/orders` with the partial size
- [ ] After success, dashboard refreshes to show reduced position size
- [ ] Row-level loading state while API call is in flight

#### Reverse Position

- [ ] Each position row has a "Reverse" option in the Actions menu
- [ ] Reverse places a market order for double the position size on the opposite side (closes current + opens same size in opposite direction)
- [ ] Confirmation dialog: "Reverse Long BTC → Short BTC? This will place a market Sell order for 0.010 BTC (2× position size)."
- [ ] Validation: warn if resulting position would exceed risk limits (informational in POC)
- [ ] Success toast on completion; error toast on failure
- [ ] Dashboard refreshes to show the new reversed position

#### Position Detail View

- [ ] Clicking a position row (excluding action buttons) expands an inline detail panel below the row
- [ ] Detail panel shows: entry price, mark price, liquidation price, margin used, notional value, leverage, margin type, funding rate, time in position, and associated TP/SL orders
- [ ] Clicking the same row again collapses the detail panel
- [ ] Only one detail panel is expanded at a time (expanding another collapses the previous)

### Non-Functional Requirements

- [ ] All actions reuse the existing `POST /api/orders` endpoint where possible — no new backend endpoints for close/reverse
- [ ] TP/SL placement requires backend support for limit and trigger order types (may require F5 endpoint extension)
- [ ] UI remains responsive during API calls — all actions have row-level loading states
- [ ] Actions are disabled while another action is in-flight for the same position

---

## User Flow

### Happy Path — Set TP/SL

1. User clicks "TP/SL" button on a Long BTC position (entry: 71,464)
2. Dialog opens showing current entry price (71,464) and mark price
3. User enters TP: 75,000 and SL: 69,000
4. User clicks "Set TP/SL"
5. Two orders are placed: limit sell at 75,000 (TP) and stop-market sell at 69,000 (SL)
6. Success toast: "TP/SL orders placed for BTC"
7. Orders appear in the Orders tab

### Happy Path — Partial Close

1. User clicks the Close button dropdown → "Partial Close" on a Long ETH position (size: 0.2000)
2. Dialog shows size slider/input; user selects 50%
3. Confirmation: "Close 50% of Long ETH? This will place a market Sell order for 0.1000 ETH."
4. User confirms; order placed via `POST /api/orders`
5. Success toast; position size updates to 0.1000

### Happy Path — Reverse Position

1. User clicks "Reverse" on a Short SUI position (size: 100.0000)
2. Confirmation: "Reverse Short SUI → Long SUI? This will place a market Buy order for 200.0000 SUI."
3. User confirms; order placed
4. Success toast; position flips to Long 100.0000 SUI

### Happy Path — Position Detail

1. User clicks the BTC position row
2. Inline detail panel expands below the row showing entry price, mark price, liquidation price, margin used, notional value, leverage, funding rate, TP/SL orders, and time in position
3. User clicks the row again; panel collapses

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| TP price invalid (below entry for long) | Inline validation error in dialog; submit disabled |
| SL price invalid (above entry for long) | Inline validation error in dialog; submit disabled |
| Partial close size exceeds position | Inline validation error; submit disabled |
| Partial close size is 0 | Inline validation error; submit disabled |
| Reverse order fails (insufficient margin) | Error toast with Hyperliquid error message |
| Position closed by another action during dialog | Error toast; dashboard refreshes to updated state |
| Network error during any action | Error toast: "Failed to [action]. Please try again." |

---

## Technical Considerations

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/orders` | Place market/limit/trigger orders (existing — may need extension for trigger orders) |
| GET | `/api/positions` | Fetch positions with extended detail fields (existing — may need enrichment) |

### Key Components

| Component | Action |
|-----------|--------|
| `PositionsTableComponent` | Add actions menu, detail row expansion, loading states per row |
| `TpSlDialogComponent` | New dialog for setting TP/SL prices with validation |
| `PartialCloseDialogComponent` | New dialog with size/percentage input and slider |
| `ConfirmDialogComponent` | Reuse for reverse confirmation |
| `OrderService` (Angular) | Reuse `placeOrder()` for all actions |
| `PositionDetailPanelComponent` | New inline expandable component for position details |

### Backend Considerations

- **Trigger orders (SL):** Hyperliquid supports trigger orders. The existing `POST /api/orders` endpoint and `PlaceOrderRequest` model may need to be extended to support `triggerPrice` for stop-loss orders.
- **Position detail data:** Some detail fields (margin used, funding rate) may require additional Hyperliquid API calls or enrichment of the existing `GET /api/positions` response.

---

## Out of Scope

- Trailing stop-loss orders
- Bracket orders (OCO — one-cancels-other)
- Editing existing TP/SL orders (cancel + re-place is the workaround)
- Position PnL charting or historical performance
- Multi-position bulk TP/SL setting

---

## Open Questions

1. Does the Hyperliquid API support trigger orders (stop-market) directly, or do they need to be simulated client-side?
2. Should the reverse action use a single 2× order or two separate orders (close + open)?
3. What position detail fields are available from the Hyperliquid API without additional calls?

---

## Acceptance Criteria

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

---

## Related Features

- **F5** — Order Placement (provides the order submission endpoint)
- **F6** — Order Management (cancel/modify orders)
- **F6.1** — Close Position (basic full-close, extended by this PBI)
- **F11** — Position Data Enrichment (provides mark price, notional value, margin used, funding rate for the detail view)
