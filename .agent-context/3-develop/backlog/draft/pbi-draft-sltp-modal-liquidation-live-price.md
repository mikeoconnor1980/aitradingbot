# SL/TP Modal: Display Liquidation Price and Live Asset Price

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-30T16:22:12Z

## User Story

As a **trader**, I want **the Stop Loss / Take Profit modal to show the liquidation price and the live asset price** so that **I can set SL/TP levels with full awareness of my risk boundaries and current market conditions**.

## Problem Statement

When setting SL/TP on a position, the user currently has to mentally track the liquidation price and live price from other parts of the UI. Showing these reference values directly in the modal reduces errors and speeds up decision-making.

## Requirements

### Functional Requirements

- [ ] Display the position's **liquidation price** as a read-only reference field in the SL/TP modal
- [ ] Display the **current live price** of the asset as a read-only reference field, updating in real-time
- [ ] Visually distinguish reference fields from editable SL/TP input fields (e.g. greyed label, smaller text)
- [ ] Warn the user if they set a Stop Loss beyond the liquidation price (it would never trigger)

### Non-Functional Requirements

- [ ] Live price should update at the same frequency as the existing market data stream
- [ ] No additional API endpoints needed — data is already available from position and market data streams

## Acceptance Criteria

- [ ] **Given** a user opens the SL/TP modal for a position, **When** the modal renders, **Then** it shows the liquidation price and live asset price alongside the SL/TP inputs
- [ ] **Given** the live price changes while the modal is open, **When** the data refreshes, **Then** the displayed live price updates
- [ ] **Given** a user sets a SL price beyond the liquidation price, **When** they attempt to confirm, **Then** a warning is shown

## Out of Scope

- Auto-suggesting SL/TP values based on liquidation price
- Showing liquidation price on the order entry form (separate PBI)
