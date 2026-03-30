# SL/TP Modal: Display Liquidation Price and Live Asset Price

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-30T16:22:12Z

## User Story

As a **trader**, I want **the Stop Loss / Take Profit modal to show the liquidation price, live asset price, and distance to liquidation** so that **I can set SL/TP levels with full awareness of my risk boundaries and current market conditions**.

## Problem Statement

When setting SL/TP on a position, the user currently has to look behind the modal at the expanded position row to see the liquidation price, and check the market data section for the live price. Showing these reference values directly in the modal reduces errors and speeds up decision-making. The screenshot below shows the current state — the liquidation price (1,866.29) is visible in the position row but not in the modal itself.

## Requirements

### Functional Requirements

#### Reference Data in Modal Header

- [ ] Display the position's **liquidation price** as a read-only field in the modal header section (below the existing Side / Entry / Size summary)
- [ ] Display the **current live price** of the asset as a read-only field in the same header section, updating in real-time via the existing WebSocket market data stream
- [ ] Display the **% distance to liquidation** from the live price (e.g. "12.4% away") next to or below the liquidation price
- [ ] Visually distinguish reference fields from the editable SL/TP input fields (e.g. muted/greyed label, smaller text)

#### Live Price Colour Coding

- [ ] Colour the live price **green** when it is above the entry price (for longs) or below (for shorts) — indicating profit
- [ ] Colour the live price **red** when it is below the entry price (for longs) or above (for shorts) — indicating loss

#### Stop Loss Validation

- [ ] If the user sets a Stop Loss **beyond the liquidation price** (i.e. below liq. price for longs, above for shorts), **disable the Confirm button**
- [ ] Show a clear inline error message explaining why submission is blocked (e.g. "Stop Loss is beyond liquidation price — it would never trigger")
- [ ] Re-enable the Confirm button as soon as the SL is corrected or cleared

### Non-Functional Requirements

- [ ] Live price must update at the same frequency as the existing market data WebSocket stream (no additional API calls)
- [ ] No additional backend endpoints needed — liquidation price comes from position data, live price from the market data stream
- [ ] Modal should not noticeably re-render or flicker when the live price updates

## Acceptance Criteria

- [ ] **Given** a user opens the SL/TP modal for a position, **When** the modal renders, **Then** it shows the liquidation price, live asset price, and % distance to liquidation in the header section below Side/Entry/Size
- [ ] **Given** the live price changes while the modal is open, **When** the WebSocket delivers a new price, **Then** the displayed live price and % to liquidation update in real-time
- [ ] **Given** the live price is above entry for a long position, **When** the modal renders, **Then** the live price is displayed in green
- [ ] **Given** the live price is below entry for a long position, **When** the modal renders, **Then** the live price is displayed in red
- [ ] **Given** a user enters a Stop Loss price beyond the liquidation price, **When** the validation runs, **Then** the Confirm button is disabled and an inline error message is shown
- [ ] **Given** a user corrects the Stop Loss to a valid value, **When** the validation re-runs, **Then** the Confirm button is re-enabled and the error message is removed

### Release Notes Information

- **Heading**: SL/TP Modal Now Shows Liquidation Price, Live Price and Distance to Liquidation
- **Release note type**: Enhancement
- **Release Note Summary**: The Set SL/TP modal now displays the position's liquidation price, live asset price (colour-coded for profit/loss), and percentage distance to liquidation. Setting a stop loss beyond the liquidation price is blocked with a clear error message.
- **Release Notes Audience**: Product
- **Breaking Change**: No

## Out of Scope

- Auto-suggesting SL/TP values based on liquidation price
- Showing liquidation price on the order entry form (separate PBI)
- Audible or push-notification alerts when price nears liquidation
