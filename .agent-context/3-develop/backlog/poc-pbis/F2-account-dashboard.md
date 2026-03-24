# PBI Specification: F2 — Account Dashboard

**Date:** 2026-03-24  
**Author:** PRD Agent  
**Status:** Draft  
**PRD:** [hyperliquid-poc-prd.md](../../prd-approved/hyperliquid-poc-prd.md)  
**Implementation Phase:** 3  
**Risk Level:** Medium  
**Depends On:** F1, F3

---

## Summary

Display the testnet account state — balance, open positions, and open orders — in an Angular dashboard that auto-refreshes on a polling interval.

### User Story

> As a **developer**, I want to **see my testnet account state at a glance** so that **I can confirm authenticated reads from Hyperliquid work correctly**.

### Business Value

Proves the backend can make authenticated read requests and the Angular–.NET API contract works for account data. This is a prerequisite for order placement (F5) and order management (F6).

---

## Requirements

### Functional Requirements

- [ ] Fetch account balance from Hyperliquid (equity, available margin, cross-margin details)
- [ ] Display open positions (asset, size, entry price, unrealised PnL, liquidation price)
- [ ] Display open orders (asset, side, price, size, order type, status)
- [ ] Auto-refresh on a polling interval (e.g. 5 seconds)
- [ ] Angular UI renders account summary card, positions table, and orders table

### Non-Functional Requirements

- [ ] Polling interval is configurable (default 5s)
- [ ] Dashboard handles empty state gracefully (no positions, no orders)

---

## User Flow

### Happy Path

1. Developer navigates to Dashboard tab in Angular UI
2. Account summary card shows equity, available margin, cross-margin details
3. Positions table shows any open BTC-PERP positions with size, entry price, unrealised PnL, liquidation price
4. Orders table shows any open orders with asset, side, price, size, type, status
5. Data refreshes automatically every 5 seconds

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| Hyperliquid API returns error on account fetch | Error message displayed in dashboard; previous data retained |
| No open positions | Positions table shows empty state message |
| No open orders | Orders table shows empty state message |
| Backend unreachable | Dashboard shows connection error; retries on next poll interval |

---

## Technical Considerations

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/account` | Returns account balance (equity, margin, cross-margin) |
| GET | `/api/account/positions` | Returns open positions for the configured wallet |
| GET | `/api/account/orders` | Returns open orders for the configured wallet |

### Key Components

| Component | Action |
|-----------|--------|
| `AccountController` | Exposes account, positions, and orders endpoints |
| `HyperliquidRestClient` | Calls Hyperliquid REST API for account state |
| `hyperliquid-api.service.ts` | Angular service making HTTP calls to account endpoints |
| Dashboard feature component | Renders account summary, positions table, orders table with polling |

### Data Models

**Account Summary:**
- Equity, available margin, cross-margin ratio

**Position:**
- Asset, size, side (long/short), entry price, mark price, unrealised PnL, liquidation price

**Order:**
- Order ID, asset, side (buy/sell), price, size, order type (market/limit), status

---

## Out of Scope

- Historical positions or orders
- Position PnL charting
- Order filtering or sorting
- Pagination (expected row count is small for POC)

---

## Open Questions

*None at this time.*

---

## Acceptance Criteria

- [ ] Account balance (equity, available margin) renders in summary card
- [ ] Positions table displays all open positions with correct fields
- [ ] Orders table displays all open orders with correct fields
- [ ] Data auto-refreshes on polling interval without manual reload
- [ ] Empty states handled gracefully for positions and orders
- [ ] API errors surfaced to the UI with a meaningful message

---

## Related Features

- **F1** — Connectivity must be established before account reads work
- **F3** — Market data REST proves the REST client before this feature adds authenticated reads
- **F5/F6** — Orders and positions from F5/F6 will appear in this dashboard
