# Risk Management UI (Frontend)

**PBI ID:** Draft
**Status:** Draft
**Priority:** P1
**Iteration:** Backlog
**Created:** 2026-04-10T00:00:00Z
**Last Updated:** 2026-04-11T00:00:39Z
**Knowledge Source:** `33-risk-management-and-trade-sizing.md`
**Depends On:** P1 R-Based Position Sizing, P1 Auto-Leverage & Isolated Margin

## Summary

Update the frontend risk management card and backtest form to support the new `RiskBased` position sizing mode. Adds a `riskPerTradePercent` input, auto-leverage toggle, and an inline live preview showing calculated R, position size, leverage, margin, and estimated liquidation price. The preview reactively reads the stop-loss value from the exit config section.

### User Story

> As a **trader**, I want to **configure R-based risk settings through the UI and see a live preview of my risk exposure** so that **I understand exactly how much I'm risking, the derived position size, and where my liquidation price sits before deploying a strategy**.

### Problem Statement

The current risk management card only supports `PercentWallet` and `FixedNotional` modes. There is no way to configure `RiskBased` sizing or `AutoLeverage` through the UI, and no preview showing the calculated R, position size, or leverage. Users would need to edit JSON directly to use the new backend features.

### Business Value

- Makes R-based sizing accessible to all users, not just those editing JSON configs
- Live preview builds confidence by showing the math before committing
- Reduces misconfiguration errors (e.g., setting risk too high without seeing the impact)

---

## Requirements

### Functional Requirements

#### TypeScript Model Updates

- [ ] Add `"risk_based"` to `PositionSizeType` union type in `strategy.model.ts`
- [ ] Add `riskPerTradePercent?: number` and `autoLeverage?: boolean` to `RiskConfig` interface
- [ ] Update `backtest.model.ts` risk config type to include the new fields

#### Risk Management Card (Strategy Builder)

- [ ] Add `risk_based` option to position size type `<mat-select>` with label "Risk-based (R%)"
- [ ] When `risk_based` is selected:
  - Show `riskPerTradePercent` input field (number, min 0.01, max 100, step 0.1)
  - Show `autoLeverage` toggle (checkbox/slide toggle)
  - Hide `positionSizeValue` field (not applicable)
  - If `autoLeverage` is off, show manual leverage input
  - If `autoLeverage` is on, hide manual leverage input
- [ ] When `percent_wallet` or `fixed_notional` is selected, existing UI unchanged — new fields hidden
- [ ] Inline warning banner below `riskPerTradePercent` when value > 5% (non-blocking, informational)
- [ ] Validation error if `risk_based` selected and no stop-loss enabled in exit config

#### Live Calculation Preview (Inline Below Risk Fields)

- [ ] Fetch account equity from the API when the form loads (realised balance, no unrealised PnL)
- [ ] Preview panel shows (when `risk_based` is selected and SL is configured):
  - **R** = equity × riskPerTradePercent / 100 (e.g., "$100 at risk")
  - **Position Size** = R / (SL% / 100) (e.g., "$5,000 notional")
  - **Leverage** = derived from SL% + maintenance margin (e.g., "33x") — only when autoLeverage is on
  - **Margin Required** = notional / leverage (e.g., "≈$152")
  - **Est. Liquidation** = entry ± (entry × (SL% + maintenance margin rate))
- [ ] Preview reactively reads the stop-loss value from the exit config form group — updates automatically when the user changes the SL
- [ ] If no SL is configured, preview shows "Configure a stop-loss to see position sizing preview"
- [ ] Preview updates dynamically as user changes `riskPerTradePercent`, `autoLeverage`, or SL

#### Backtest Form

- [ ] Update backtest risk config section with the same `risk_based` option and fields
- [ ] Backtest form does not need the live preview (equity is the simulated starting balance)
- [ ] Ensure backtest request payload includes `riskPerTradePercent` and `autoLeverage` when `risk_based` is selected

#### Strategy Mapper

- [ ] Update `strategy-mapper.service.ts` to map `riskPerTradePercent` and `autoLeverage` between form and API models

### Non-Functional Requirements

- [ ] Responsive layout for the preview panel (fits within the existing card grid)
- [ ] Info popover tooltips for:
  - `riskPerTradePercent`: "The percentage of your account equity risked per trade. R = Equity × this value."
  - `autoLeverage`: "When enabled, leverage is calculated from your stop-loss distance so the SL fires before liquidation."
  - Preview panel: brief explanation of R-based sizing math
- [ ] Unit tests for the component: mode switching shows/hides correct fields
- [ ] Unit tests for preview calculation logic

---

## Acceptance Criteria

- [ ] **Given** the user selects `risk_based` sizing, **When** the form renders, **Then** `riskPerTradePercent` and `autoLeverage` fields appear, and `positionSizeValue` is hidden
- [ ] **Given** `risk_based` with `riskPerTradePercent = 1.0`, SL = 2%, and equity = $10,000, **When** viewing the preview, **Then** R = $100, Position Size = $5,000, Leverage = 33x (auto), Margin ≈ $152
- [ ] **Given** `risk_based` with `autoLeverage` off, **When** the form renders, **Then** the manual leverage input is shown
- [ ] **Given** `risk_based` with `autoLeverage` on, **When** the form renders, **Then** the manual leverage input is hidden
- [ ] **Given** the user changes the SL from 2% to 5% in the exit config, **When** `risk_based` is active, **Then** the preview updates automatically (Position Size shrinks, leverage drops)
- [ ] **Given** `risk_based` selected but no stop-loss enabled, **When** viewing the preview, **Then** a message says "Configure a stop-loss to see position sizing preview"
- [ ] **Given** `risk_based` selected but no stop-loss enabled, **When** the user tries to save, **Then** a validation error appears
- [ ] **Given** `riskPerTradePercent = 8`, **When** the value is entered, **Then** an inline warning banner shows (risk > 5%) but save is not blocked
- [ ] **Given** `percent_wallet` is selected, **When** the form renders, **Then** existing fields show and RiskBased fields are hidden
- [ ] **Given** `risk_based` selected in the backtest form, **When** the backtest is submitted, **Then** the payload includes `riskPerTradePercent` and `autoLeverage`

### Release Notes Information

- **Heading**: Risk-Based Sizing in Strategy Builder
- **Release note type**: Feature
- **Release Note Summary**: The strategy builder and backtest form now support Risk-Based position sizing. Enter a risk percentage and see a live preview of R, position size, leverage, and estimated liquidation price before deploying.
- **Release Notes Audience**: All
- **Breaking Change**: No

## Out of Scope

- Backend R-based calculation logic (separate PBI: P1 R-Based Position Sizing)
- Auto-leverage exchange integration (separate PBI: P1 Auto-Leverage & Isolated Margin)
- Portfolio heat display (separate PBI: P2 Portfolio Heat)
- R-multiple metrics display (separate PBI: P2 R-Multiple Exits & Tracking)
