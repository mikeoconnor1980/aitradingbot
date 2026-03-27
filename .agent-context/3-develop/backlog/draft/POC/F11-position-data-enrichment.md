# PBI Specification: F11 — Position Data Enrichment

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-26
**PRD:** [hyperliquid-poc-prd.md](../../prd-approved/hyperliquid-poc-prd.md)
**Implementation Phase:** 7
**Risk Level:** Low
**Depends On:** F2, F4

---

## Summary

Enrich the positions table and position detail view with additional data fields: mark price, position notional value (USD), margin used per position, and funding rate. These fields are critical for understanding position economics on Hyperliquid perpetuals but are currently missing from the dashboard.

### User Story

> As a **trader**, I want to **see the mark price, notional value, margin used, and funding rate for each position** so that **I can understand my true exposure, margin consumption, and position economics without switching to the Hyperliquid UI**.

### Business Value

The current positions table shows entry price and PnL but omits key context. Without mark price, the trader cannot verify PnL direction. Without notional value, they cannot assess true position size in dollar terms (especially important for low-priced assets like OP or DOGE where position sizes are large numbers). Without margin used, they cannot understand how much of their equity each position consumes. Without funding rate, they miss the continuous cost/income of holding perpetual positions — a factor that can significantly impact profitability.

---

## Problem Statement

The positions table displays: Asset, Size, Leverage, Entry Price, Unrealised PnL, Liquidation Price, and Actions. Several important data points are missing:

- **Mark Price** — the current price used for PnL calculation. Without it, users cannot confirm whether price is moving for or against them.
- **Notional Value** — the USD value of the position. A "200 OP" position means nothing without knowing it's ~$22 USD.
- **Margin Used** — how much margin is allocated to this position. Critical for understanding capital allocation.
- **Funding Rate** — the periodic rate paid/received for holding perpetual positions. On Hyperliquid, funding is continuous and can be significant.

---

## Requirements

### Functional Requirements

#### Mark Price

- [ ] Add "Mark Price" column to the positions table (between Entry Price and Unrealised PnL)
- [ ] Mark price updates on each polling cycle (inherits F2's 2-second refresh)
- [ ] Display to the same decimal precision as the entry price
- [ ] Visually highlight when mark price is above entry (green dot/arrow) or below entry (red dot/arrow) for long positions (reversed for short)

#### Position Notional Value

- [ ] Show notional value in the **Size column tooltip** (hover over size to see USD notional) — not as a separate column
- [ ] Calculated as: `abs(size) × mark price`
- [ ] Displayed in USD with appropriate formatting (e.g., "$1,975.05")
- [ ] Updates with each mark price refresh
- [ ] Also shown in position detail panel (F9) as a dedicated field

#### Margin Used

- [ ] Show margin used in the **Leverage column tooltip** (hover over leverage badge to see margin in USD) — not as a separate column
- [ ] For isolated positions: margin used is the isolated margin allocated
- [ ] For cross positions: margin used is `notional / leverage` (approximate for display purposes)
- [ ] Displayed in USD with appropriate formatting
- [ ] Tooltip also shows margin as a percentage of total equity: "$393.82 (83.8% of equity)"
- [ ] Also shown in position detail panel (F9) as a dedicated field

#### Funding Rate

- [ ] Add "Funding" indicator to each position row (compact) and full detail in the position detail panel (F9)
- [ ] Show the current funding rate as an annualised percentage or hourly rate (configurable)
- [ ] Color-coded: green if receiving funding (favorable), red if paying funding (unfavorable)
- [ ] For long positions: negative funding rate = paying; positive = receiving. Reversed for shorts.
- [ ] Tooltip shows: hourly rate, estimated daily cost/income in USD based on notional value

### Non-Functional Requirements

- [ ] Mark price and funding rate data must come from the Hyperliquid API — no local estimation
- [ ] Notional and margin calculations are client-side (derived from existing data + mark price)
- [ ] Additional API fields must not increase dashboard polling latency by more than 200ms
- [ ] All new columns must be responsive — hide or collapse on smaller viewports if needed

---

## User Flow

### Happy Path

1. User navigates to the dashboard with open positions
2. Positions table now shows: Asset, Size, Leverage, Entry Price, **Mark Price**, Unrealised PnL, Liquidation Price, **Funding**, Actions (Notional and Margin available via tooltips)
3. BTC position (Short, entry 71,464): Mark Price shows 71,200 with a green indicator (favorable for short)
4. Hovering Size shows tooltip: "Notional: $1,969.12" (0.0276 × 71,200)
5. Hovering Leverage badge shows tooltip: "Margin: $393.82 (83.8% of equity)"
6. Funding shows "-0.0042%" in green (receiving funding as a short when funding is negative)
7. Hovering Funding shows tooltip: "Hourly: -0.0042% | Est. daily: +$0.83"

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| Mark price unavailable from API | Show "—" in Mark Price column; notional shows "—" |
| Funding rate unavailable | Show "—" in Funding column |
| Calculated margin exceeds equity | Display normally (this is a valid state); margin ratio indicator (F10) handles the warning |

---

## Technical Considerations

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/positions` | **Extend response** to include `markPrice`, `marginUsed`, and `fundingRate` per position |
| GET | `/api/market-data/funding-rates` | **New endpoint** (optional) — fetch current funding rates for all assets. Alternative: include in positions response. |

### Backend Changes

The Hyperliquid API provides most of this data in the user state response:

- **Mark price**: Available in the `assetPositions` response from the `/info` endpoint (field: `position.entryPx` vs current mark from market data)
- **Margin used**: Available as `position.marginUsed` in the Hyperliquid response (may already be returned but not mapped)
- **Funding rate**: Available via the `/info` endpoint with `type: "meta"` for per-asset funding rates

Changes needed:
- Extend `PositionDto` to include `markPrice`, `marginUsed`, `fundingRate` fields
- Update the Hyperliquid client to parse and return these additional fields
- Update the positions mapping logic to populate the new fields

### Key Components

| Component | Action |
|-----------|--------|
| `PositionDto` (API) | Add `MarkPrice`, `MarginUsed`, `FundingRate` properties |
| `HyperliquidRestClient` | Parse additional fields from user state response; add funding rate fetch |
| `AccountController` | No changes if fields are added to existing positions response |
| `PositionsTableComponent` | Add new columns: Mark Price, Notional, Margin, Funding |
| `FundingIndicatorComponent` | New compact component for color-coded funding rate display |
| `PositionDetailPanelComponent` (F9) | Consume enriched data for the detail view |

### Data Model Extension

```
PositionDto (extended):
  + MarkPrice: decimal
  + MarginUsed: decimal
  + FundingRate: decimal
  + (derived in frontend) Notional: abs(Size) × MarkPrice
  + (derived in frontend) MarginPercent: MarginUsed / AccountEquity × 100
```

---

## Out of Scope

- Historical funding rate charts
- Funding rate predictions or averages
- Accumulated funding paid/received over position lifetime
- Real-time WebSocket mark price updates (uses F2 polling; F7 will add WebSocket later)
- Mark price for assets without open positions (that's market data, not position data)

---

## Open Questions

1. Does the Hyperliquid `/info` user state response already include mark price and margin used, or are separate API calls needed?
2. What is the funding rate interval on Hyperliquid — hourly or 8-hourly? This affects the display format.
3. Should funding rate be shown as annualised, hourly, or per-interval? Recommend making this configurable.

---

## Acceptance Criteria

- [ ] **Given** I have open positions, **When** the positions table loads, **Then** I see Mark Price, Notional, Margin, and Funding columns for each position
- [ ] **Given** a Long BTC position with entry 71,464 and mark 72,000, **When** the table renders, **Then** Mark Price shows 72,000 with a green indicator (price moving in favour)
- [ ] **Given** a Short BTC position with entry 71,464 and mark 72,000, **When** the table renders, **Then** Mark Price shows 72,000 with a red indicator (price moving against)
- [ ] **Given** a position with size 0.0276 and mark price 71,200, **When** the Notional column renders, **Then** it displays "$1,969.12"
- [ ] **Given** a 5× leveraged position with notional $1,969.12, **When** the Margin column renders, **Then** it shows approximately "$393.82"
- [ ] **Given** I hover over the Margin value, **When** the tooltip appears, **Then** it shows the margin as a percentage of total equity
- [ ] **Given** the current funding rate is negative and I hold a Short position, **When** the Funding column renders, **Then** it shows the rate in green (receiving funding)
- [ ] **Given** I hover over the Funding indicator, **When** the tooltip appears, **Then** it shows the hourly rate and estimated daily USD cost/income
- [ ] **Given** mark price data is unavailable from the API, **When** the table renders, **Then** Mark Price shows "—" and Notional shows "—"
- [ ] **Given** data refreshes every 2 seconds, **When** mark price changes, **Then** the Mark Price, Notional, and Margin columns update accordingly

---

## Related Features

- **F2** — Account Dashboard (provides the base positions table and polling mechanism)
- **F4** — Market Data WebSocket (future: mark price could update via WebSocket instead of polling)
- **F9** — Position Actions (detail panel consumes the enriched data from this PBI)
- **F10** — Positions Table UX (sorting/filtering operates on the enriched columns)
