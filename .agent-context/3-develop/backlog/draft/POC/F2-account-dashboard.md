# PBI Specification: F2 — Account Dashboard

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-24
**PRD:** [hyperliquid-poc-prd.md](../../prd-approved/hyperliquid-poc-prd.md)
**Implementation Phase:** 3
**Risk Level:** Medium
**Depends On:** F1

---

## Summary

Display the testnet account state — balance, open positions, and open orders — in an Angular dashboard with tabbed navigation, auto-refresh polling, and visual indicators for data staleness and PnL.

### User Story

> As a **developer**, I want to **see my testnet account state at a glance** so that **I can monitor my equity, positions, and open orders without switching to the Hyperliquid UI**.

### Business Value

Proves the backend can make authenticated read requests and the Angular–.NET API contract works for account data. This is a prerequisite for order placement (F5) and order management (F6).

---

## Problem Statement

During the POC phase, developers and testers need visibility into their Hyperliquid testnet account state (balances, positions, orders) through the Angular frontend, proving that authenticated REST reads work end-to-end from exchange to UI.

---

## Requirements

### Functional Requirements

- [ ] Fetch and display **account summary**: equity, available margin, cross-margin ratio, maintenance margin, and aggregate unrealised PnL
- [ ] Display **open positions table** with columns: Asset, Size, Entry Price, Unrealised PnL, Liquidation Price
- [ ] Display **open orders table** with columns: Asset, Side, Price, Size, Order Type, Status
- [ ] **Auto-refresh** account data on a 2-second polling interval (interim mechanism until F7 provides reactive updates via WebSocket)
- [ ] Provide a **manual refresh button** to force an immediate data reload
- [ ] Show a **"Last updated: X seconds ago" timestamp** that updates in real-time
- [ ] Apply a **visual stale indicator** (dimming) when data is older than 10 seconds (e.g. after failed polls)
- [ ] Show **empty state text messages** (e.g. "No open positions", "No open orders") when tables have no data
- [ ] Display **unrealised PnL color-coded**: green for profit, red for loss, with both absolute value and percentage
- [ ] Use a **tabbed layout**: account summary always visible at the top; positions and orders in separate tabs below
- [ ] Show **inline error banner** for persistent errors (stays until resolved) and **toast notifications** for transient errors (e.g. single failed poll)

### Non-Functional Requirements

- [ ] Polling must not degrade UI responsiveness — use Angular async pipes or RxJS with appropriate operators
- [ ] API response time for each dashboard endpoint should be < 1 second under normal conditions
- [ ] Dashboard must handle Hyperliquid API downtime gracefully without crashing

---

## User Flow

### Happy Path

1. Developer navigates to Dashboard tab in Angular UI
2. Account summary card shows equity, available margin, cross-margin ratio, maintenance margin, and total unrealised PnL
3. Positions tab shows any open positions with Asset, Size, Entry Price, Unrealised PnL (color-coded), Liquidation Price
4. Orders tab shows any open orders with Asset, Side, Price, Size, Order Type, Status
5. "Last updated" timestamp updates continuously
6. Data refreshes automatically every 2 seconds
7. Developer can click manual refresh button to force an immediate reload

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| Single API poll fails | Toast notification shown; previous data retained; stale indicator activates after 10s |
| Multiple consecutive polls fail | Inline error banner shown above dashboard content; data visually dimmed |
| No open positions | Positions tab shows "No open positions" text |
| No open orders | Orders tab shows "No open orders" text |
| Backend unreachable | Error banner displayed; retries on next poll interval |

---

## Technical Considerations

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/account` | Returns account summary (equity, available margin, cross-margin ratio, maintenance margin, unrealised PnL) |
| GET | `/api/positions` | Returns list of open positions |
| GET | `/api/orders` | Returns list of open orders |

> **Note:** `GET /api/orders` will move to `OrderController` when F5 is implemented. During F2, it may live on `AccountController` alongside the other read endpoints.

### Key Components

| Component | Action |
|-----------|--------|
| `AccountController` | Exposes account, positions, and orders endpoints |
| `HyperliquidRestClient` | Calls Hyperliquid REST API for account state |
| `hyperliquid-api.service.ts` | Angular service making HTTP calls to account endpoints |
| Dashboard feature component | Renders account summary, positions table, orders table with polling |

### Data Models

**Account Summary:**
- Equity, available margin, cross-margin ratio, maintenance margin, aggregate unrealised PnL

**Position:**
- Asset, size, side (long/short), entry price, mark price, unrealised PnL, liquidation price

**Order:**
- Order ID, asset, side (buy/sell), price, size, order type (market/limit), status

---

## Out of Scope

- WebSocket-based real-time updates (covered by F4 and F7)
- Order management actions from the dashboard (covered by F5 and F6)
- Historical PnL or performance charts
- Multi-asset filtering or search
- Persistent storage of account snapshots
- Authentication / authorization
- Position PnL charting
- Pagination (expected row count is small for POC)

---

## Open Questions

*None at this time.*

---

## Acceptance Criteria

- [ ] **Given** the user has a configured testnet wallet (F1 complete), **When** they navigate to the dashboard, **Then** the account summary displays equity, available margin, cross-margin ratio, maintenance margin, and total unrealised PnL
- [ ] **Given** the user has open positions on testnet, **When** the dashboard loads, **Then** the positions tab shows a table with Asset, Size, Entry Price, Unrealised PnL, and Liquidation Price columns
- [ ] **Given** the user has open orders on testnet, **When** they switch to the orders tab, **Then** a table shows Asset, Side, Price, Size, Order Type, and Status columns
- [ ] **Given** the user has no open positions, **When** the dashboard loads, **Then** the positions tab displays "No open positions"
- [ ] **Given** the user has no open orders, **When** they switch to the orders tab, **Then** it displays "No open orders"
- [ ] **Given** the dashboard is loaded, **When** 2 seconds elapse, **Then** the account data is automatically refreshed from the API
- [ ] **Given** the dashboard is loaded, **When** the user clicks the manual refresh button, **Then** the data is immediately re-fetched and the "Last updated" timestamp resets
- [ ] **Given** the dashboard is displaying data, **When** the last successful fetch was more than 10 seconds ago, **Then** the data is visually dimmed to indicate staleness
- [ ] **Given** a position has positive unrealised PnL, **When** the positions table renders, **Then** the PnL is displayed in green with absolute value and percentage
- [ ] **Given** a position has negative unrealised PnL, **When** the positions table renders, **Then** the PnL is displayed in red with absolute value and percentage
- [ ] **Given** the Hyperliquid API returns an error on a poll, **When** the dashboard attempts to refresh, **Then** a toast notification is shown and the previous data remains displayed
- [ ] **Given** the Hyperliquid API is persistently unreachable, **When** multiple consecutive polls fail, **Then** an inline error banner is shown above the dashboard content

### Release Notes Information

- **Heading**: Testnet Account Dashboard
- **Release note type**: Feature
- **Release Note Summary**: View testnet account state including equity, open positions, and open orders in an Angular dashboard with auto-refresh polling and staleness indicators.
- **Release Notes Audience**: Product
- **Breaking Change**: No

---

## Related Features

- **F1** — Wallet configuration must be complete before authenticated account reads work
- **F5** — Order placement creates orders that appear in the dashboard orders table
- **F6** — Order management actions (cancel, modify) operate on orders displayed here
- **F7** — User event stream will provide reactive updates, replacing the polling mechanism as the primary update path

- **F1** — Connectivity must be established before account reads work
- **F5/F6** — Orders and positions from F5/F6 will appear in this dashboard
- **F4/F7** — WebSocket streaming will eventually supplement polling for real-time updates
