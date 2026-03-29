# Stop Loss & Take Profit

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-29T14:00:00Z

## Summary

Add Stop Loss (SL) and Take Profit (TP) functionality to the trading platform in two areas:

1. **Positions table** — allow users to set/edit/remove SL and TP on existing open positions
2. **Place Order form** — allow users to attach optional SL and TP when placing a new order

This is a core risk management feature. Hyperliquid supports trigger orders (TP/SL) natively via the exchange API using the `tpsl` trigger type.

### User Story

> As a **trader**, I want to **set stop loss and take profit levels on my positions and new orders** so that **my downside risk is capped and profits are taken automatically without manual monitoring**.

### Business Value

- Reduces risk of catastrophic losses from unmonitored positions
- Matches baseline feature parity with all major perp trading interfaces (Binance, Bybit, Hyperliquid UI)
- Essential for any production trading tool — positions without SL are unbounded risk

---

## Requirements

### Functional Requirements

#### Place Order Form (order-entry component)

- [ ] Add optional **Stop Loss Price** field (number input, nullable)
- [ ] Add optional **Take Profit Price** field (number input, nullable)
- [ ] SL/TP fields appear below the Size field, collapsed by default with a toggle ("Add SL/TP")
- [ ] Validate SL price is below entry for longs, above entry for shorts
- [ ] Validate TP price is above entry for longs, below entry for shorts
- [ ] On order submission, if SL and/or TP are set, place trigger orders immediately alongside the main order
- [ ] Show a non-blocking warning if the user sets SL but not TP (or vice versa)
- [ ] Confirmation dialog shows SL/TP values when set

#### Positions Table (positions-table component)

- [ ] Display SL and TP columns (or inline indicators) for positions that have active trigger orders
- [ ] Add "Set SL/TP" action button per position row — opens a dialog/popover with both SL and TP fields for initial setup
- [ ] Allow inline editing of existing SL/TP values by clicking the displayed value (hybrid UX: dialog for first set, inline for quick edits)
- [ ] Allow removing SL/TP (cancels the trigger order on the exchange)
- [ ] SL/TP values should update in real-time when trigger orders change

#### Backend API

- [ ] Extend `PlaceOrderRequest` with optional `StopLossPrice` and `TakeProfitPrice` fields
- [ ] Add new endpoint `POST /api/orders/trigger` to place a standalone trigger order (SL or TP) for an existing position
- [ ] Add new endpoint `PUT /api/orders/trigger/{orderId}` to modify a trigger order price
- [ ] Add new endpoint `DELETE /api/orders/trigger/{orderId}` to cancel a trigger order
- [ ] Return trigger order information in the open orders list (already includes order type — ensure trigger orders are distinguishable)

#### Hyperliquid Integration

- [ ] Implement trigger order placement using Hyperliquid's trigger order API (`tpsl` trigger type)
- [ ] SL/TP trigger orders execute as **market orders** when the trigger price is hit (guaranteed fill)
- [ ] Exchange is the sole source of truth for trigger order state — no local persistence of SL/TP intent

### Non-Functional Requirements

- [ ] SL/TP order placement should complete within 2s (same as regular order latency)
- [ ] Trigger order state must survive API restarts (orders live on the exchange)
- [ ] No additional authentication required (uses same wallet signing flow)

---

## User Flow

### Happy Path — Place Order with SL/TP

1. User selects asset, sets side/type/price/size as normal
2. User clicks "Add SL/TP" toggle to expand the section
3. User enters a Stop Loss price (e.g., $64,000 for a long BTC position)
4. User optionally enters a Take Profit price (e.g., $70,000)
5. User clicks Submit Order → confirmation dialog shows order details including SL/TP
6. User confirms → main order and SL/TP trigger orders are placed immediately
7. Positions table shows SL: $64,000 / TP: $70,000 next to the position

### Happy Path — Add SL/TP to Existing Position

1. User sees an open position in the positions table without SL/TP
2. User clicks "Set SL/TP" button on the position row
3. A dialog/popover appears with SL and TP price inputs (pre-populated with suggested values based on entry price)
4. User enters values and confirms
5. Trigger orders are placed on Hyperliquid
6. Position row updates to show the active SL/TP levels

### Happy Path — Modify SL/TP on Existing Position

1. User clicks the displayed SL or TP value on the position row (inline edit activates)
2. User changes the price and confirms
3. Existing trigger order is modified on the exchange
4. UI updates immediately

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| SL price on wrong side of entry (e.g., SL above entry for long) | Validation error before submission — "Stop loss must be below entry price for long positions" |
| TP price on wrong side of entry | Validation error — "Take profit must be above entry price for long positions" |
| Trigger order placement fails on exchange | Show error notification, main order is unaffected |
| Position already closed when SL/TP trigger fires | Exchange handles this — trigger order becomes invalid and auto-cancels |
| User enters SL beyond liquidation price | Warning — "Stop loss is beyond your liquidation price ($X)" |

---

## Technical Considerations

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/orders` | Extended — accepts optional `stopLossPrice` and `takeProfitPrice` |
| POST | `/api/orders/trigger` | Place a standalone trigger order (SL or TP) for an existing position |
| PUT | `/api/orders/trigger/{orderId}` | Modify an existing trigger order price |
| DELETE | `/api/orders/trigger/{orderId}` | Cancel a trigger order |

### Position Enrichment

To show SL/TP on positions, the backend must:
1. Fetch open orders from Hyperliquid
2. Match trigger orders to positions by asset + side
3. Include matched SL/TP prices in the position response returned to the frontend

---

## Out of Scope

- Trailing stop loss
- OCO (one-cancels-other) order type at the UI level (Hyperliquid's tpsl handles the close)
- Automated SL/TP based on ATR or percentage (strategy engine feature, not manual trading)
- Bracket orders (combined entry + SL + TP as a single atomic operation)
- Partial fill SL/TP sizing (MVP handles fully filled orders only)
- Local persistence of SL/TP intent (exchange is the source of truth)

---

## Resolved Decisions

| Decision | Resolution |
|----------|------------|
| SL/TP trigger execution type | **Market order** — guaranteed fill when trigger price is hit |
| Warning when only SL or TP is set | **Yes** — show non-blocking warning |
| Positions table UX pattern | **Hybrid** — dialog/popover for initial setup, inline edit for modifications |
| SL/TP persistence | **Exchange only** — Hyperliquid trigger order state is the source of truth |
| SL/TP placement timing | **Immediate** — placed alongside the main order, not after fill |
| Partial fill handling | **Out of scope for MVP** — only fully filled orders |

---

## Acceptance Criteria

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

### Release Notes Information

- **Heading**: Stop Loss & Take Profit for Manual Trading
- **Release note type**: Feature
- **Release Note Summary**: Traders can now set stop loss and take profit levels on new orders and existing positions. SL/TP trigger orders are placed directly on Hyperliquid for reliable execution.
- **Release Notes Audience**: Product
- **Breaking Change**: No

---

## References

- [Hyperliquid Exchange API — Order Types](https://hyperliquid.gitbook.io/hyperliquid-docs/for-developers/api/exchange-endpoint#place-an-order)
- [Current PlaceOrderRequest](../../../src/TradingApp.Api/Models/PlaceOrderRequest.cs)
- [Current Position model](../../../frontend/trading-ui/src/app/core/models/position.model.ts)
- [Current order-entry component](../../../frontend/trading-ui/src/app/features/order-entry/order-entry.component.ts)
- [Current positions-table component](../../../frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts)
