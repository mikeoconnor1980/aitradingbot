applyTo: ".agent-context/3-develop/build/changes/20260330-sltp-modal-liquidation-live-price-changes.md"
currentAgent: "None"
agentStartedAt: "2026-03-30T20:02:05Z"
status: "implemented"
lastUpdated: "2026-03-30T20:05:30Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: SL/TP Modal — Liquidation Price, Live Price & Distance to Liquidation

## Overview

Enhance the Set SL/TP modal to display the position's liquidation price, live asset price (colour-coded for profit/loss), percentage distance to liquidation, and block submission when stop loss is set beyond liquidation price.

## PBI Details

### User Story

As a **trader**, I want **the Stop Loss / Take Profit modal to show the liquidation price, live asset price, and distance to liquidation** so that **I can set SL/TP levels with full awareness of my risk boundaries and current market conditions**.

### Acceptance Criteria

- [ ] **Given** a user opens the SL/TP modal for a position, **When** the modal renders, **Then** it shows the liquidation price, live asset price, and % distance to liquidation in the header section below Side/Entry/Size
- [ ] **Given** the live price changes while the modal is open, **When** the WebSocket delivers a new price, **Then** the displayed live price and % to liquidation update in real-time
- [ ] **Given** the live price is above entry for a long position, **When** the modal renders, **Then** the live price is displayed in green
- [ ] **Given** the live price is below entry for a long position, **When** the modal renders, **Then** the live price is displayed in red
- [ ] **Given** a user enters a Stop Loss price beyond the liquidation price, **When** the validation runs, **Then** the Confirm button is disabled and an inline error message is shown
- [ ] **Given** a user corrects the Stop Loss to a valid value, **When** the validation re-runs, **Then** the Confirm button is re-enabled and the error message is removed

## Objectives

- Display liquidation price, live price, and % distance in the modal header section
- Subscribe to SignalR `priceUpdate$` for real-time live price updates
- Colour-code live price green/red based on profit/loss relative to entry
- Convert SL-beyond-liquidation check from a display-only warning to a form-level validator that blocks submission
- Add `[disabled]` binding to the Confirm button when form is invalid
- Create a comprehensive spec file for the modal component

### Discovery References

- **Live price pattern**: `OrderEntryComponent._subscribeToPriceUpdates()` — subscribes to `SignalRService.priceUpdate$` with `takeUntilDestroyed`, normalises asset name by stripping `-PERP`
- **Dialog test pattern**: `CloseAllDialogComponent` spec — `jasmine.createSpyObj("MatDialogRef", ["close"])`, `MAT_DIALOG_DATA` injection, `NoopAnimationsModule`
- **Existing SL validation**: `isSlBeyondLiquidation()` method already checks direction-aware SL vs liquidation price — currently warning-only, to be converted to form validator
- **Colour variables**: `--colour-profit` (#4ade80) and `--colour-loss` (#f87171) already defined globally in `styles.scss`
- **Asset format mismatch**: `Position.asset = "BTC"`, `PriceUpdate.asset = "BTC-PERP"` — must normalise when filtering

### Project Patterns

- `frontend/trading-ui/src/app/features/dashboard/positions-table/set-sltp-modal/set-sltp.modal.component.ts` — Target modal component (full source read)
- `frontend/trading-ui/src/app/features/dashboard/positions-table/set-sltp-modal/set-sltp.modal.component.html` — Target modal template (full source read)
- `frontend/trading-ui/src/app/features/dashboard/positions-table/set-sltp-modal/set-sltp.modal.component.scss` — Target modal styles (full source read)
- `frontend/trading-ui/src/app/core/models/position.model.ts` — Position interface with `liquidationPrice`, `markPrice`, `entryPrice`
- `frontend/trading-ui/src/app/core/models/price-update.model.ts` — PriceUpdate interface with `asset`, `lastPrice`
- `frontend/trading-ui/src/app/core/services/signalr.service.ts` — `priceUpdate$` Observable for live price stream
- `frontend/trading-ui/src/app/features/order-entry/order-entry.component.ts` — Live price subscription pattern with `takeUntilDestroyed`
- `frontend/trading-ui/src/app/features/dashboard/positions-table/close-all-dialog/close-all-dialog.component.spec.ts` — Dialog test pattern

### [x] Phase 1: Modal Logic — Live Price, Liquidation Display & Validation

**Complexity**: Medium | **Risk**: Low

- [x] Task 1.1: Inject SignalRService and subscribe to live price updates
  - Details: .agent-context/3-develop/build/plans/details/20260330-sltp-modal-liquidation-live-price-phase-01-details.md#task-11-inject-signalrservice-and-subscribe-to-live-price-updates

- [x] Task 1.2: Add computed properties for liquidation distance and live price colour
  - Details: .agent-context/3-develop/build/plans/details/20260330-sltp-modal-liquidation-live-price-phase-01-details.md#task-12-add-computed-properties-for-liquidation-distance-and-live-price-colour

- [x] Task 1.3: Convert SL-beyond-liquidation check to a form validator
  - Details: .agent-context/3-develop/build/plans/details/20260330-sltp-modal-liquidation-live-price-phase-01-details.md#task-13-convert-sl-beyond-liquidation-check-to-a-form-validator

- [x] Task 1.4: Update template to show reference data and disable Confirm button
  - Details: .agent-context/3-develop/build/plans/details/20260330-sltp-modal-liquidation-live-price-phase-01-details.md#task-14-update-template-to-show-reference-data-and-disable-confirm-button

- [x] Task 1.5: Add SCSS styles for new reference data rows
  - Details: .agent-context/3-develop/build/plans/details/20260330-sltp-modal-liquidation-live-price-phase-01-details.md#task-15-add-scss-styles-for-new-reference-data-rows

- [x] Task 1.6: Create comprehensive spec file for SetSlTpModalComponent
  - Details: .agent-context/3-develop/build/plans/details/20260330-sltp-modal-liquidation-live-price-phase-01-details.md#task-16-create-comprehensive-spec-file-for-setSltpModalComponent

- [x] Task 1.7: Run frontend build and lint
  - Details: .agent-context/3-develop/build/plans/details/20260330-sltp-modal-liquidation-live-price-phase-01-details.md#task-17-run-frontend-build-and-lint

## Scoping Summary

| Phase | Complexity | Risk |
|-------|----------|------|
| Phase 1: Modal Logic — Live Price, Liquidation Display & Validation | Medium | Low |
| **Total** | **Medium** | **Low** |

### Scoping Notes

- This is a single-phase, frontend-only feature — no backend changes required
- Liquidation price and mark price are already available on the `Position` interface passed to the modal
- Live price stream already exists via `SignalRService.priceUpdate$` — no new WebSocket connections needed
- The `isSlBeyondLiquidation()` logic already exists — it needs to be converted from a display-only check to a form validator
- Currently BTC-only live price stream (hardcoded in backend) — sufficient for POC scope
- Seed `livePrice` from `position.markPrice` until first SignalR update arrives

## Dependencies

- `SignalRService` (existing) — live price stream
- Angular Material (existing) — dialog, form fields, buttons
- Position model (existing) — `liquidationPrice`, `markPrice`, `entryPrice`, `asset`

## Success Criteria

- Modal displays liquidation price, live price, and % distance to liquidation in the header section
- Live price updates in real-time when WebSocket delivers new prices
- Live price is green when in profit, red when in loss (relative to entry price and position side)
- Setting a stop loss beyond liquidation price disables the Confirm button and shows an inline error
- Correcting or clearing the stop loss re-enables the Confirm button
- All new tests pass
- Frontend builds and lints without errors

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|----------|
| Implementation Planner | planned | 2026-03-30T19:36:07Z | 2026-03-30T19:45:16Z |
| Plan Reviewer | plan-reviewed | 2026-03-30T19:45:48Z | 2026-03-30T19:49:00Z |
| Plan Implementer | implemented | 2026-03-30T20:02:05Z | 2026-03-30T20:05:30Z |
