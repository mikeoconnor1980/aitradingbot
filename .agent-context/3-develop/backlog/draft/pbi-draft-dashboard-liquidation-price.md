# Dashboard: Show Liquidation Price in Grid

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-30T16:22:12Z

## User Story

As a **trader**, I want **the dashboard grid to show the liquidation price for each open position** so that **I can quickly see how close my positions are to liquidation without navigating elsewhere**.

## Problem Statement

The dashboard grid currently does not display the liquidation price. Traders need this critical risk metric visible at a glance to manage their positions effectively.

## Requirements

### Functional Requirements

- [ ] Add a "Liq. Price" column to the positions grid on the dashboard
- [ ] Liquidation price is sourced from the position data returned by the Hyperliquid API
- [ ] The value should update in real-time alongside other position data
- [ ] Format the price consistently with other price columns (appropriate decimal places)

### Non-Functional Requirements

- [ ] No additional API calls required — liquidation price is already available in position data

## Acceptance Criteria

- [ ] **Given** a user has open positions, **When** they view the dashboard grid, **Then** each position row shows the liquidation price
- [ ] **Given** a position's liquidation price changes (e.g. after adding margin), **When** the data refreshes, **Then** the displayed liquidation price updates accordingly

## Out of Scope

- Liquidation price warnings or alerts
- Liquidation price on the order entry form
