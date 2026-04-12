---
applyTo: ".agent-context/3-develop/build/changes/20260411-risk-management-ui-changes.md"
currentAgent: "None"
agentStartedAt: "2026-04-12T09:28:27Z"
status: "implemented"
lastUpdated: "2026-04-12T13:35:00Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: Risk Management UI — R-Based Position Sizing

## Overview

Add `RiskBased` position sizing mode, auto-leverage toggle, and live R-based preview to the strategy builder risk management card and backtest form.

## PBI Details

**PBI ID:** Draft — P1 Risk Management UI
**Depends On:** P1 R-Based Position Sizing (backend), P1 Auto-Leverage & Isolated Margin (backend)
**Knowledge Source:** `33-risk-management-and-trade-sizing.md`

### User Story

> As a **trader**, I want to **configure R-based risk settings through the UI and see a live preview of my risk exposure** so that **I understand exactly how much I'm risking, the derived position size, and where my liquidation price sits before deploying a strategy**.

### Acceptance Criteria

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

## Objectives

- Add `"risk_based"` to `PositionSizeType` union and new optional fields to `RiskConfig`
- Update risk management card with conditional fields for R-based sizing mode
- Add live calculation preview panel showing R, position size, leverage, margin, and est. liquidation
- Update backtest form and preview summary for the new sizing mode
- Create comprehensive unit tests for the risk card component

### Discovery References

- `.agent-context/0-knowledge/33-risk-management-and-trade-sizing.md` — R-based sizing formulas, auto-leverage derivation, maintenance margin rate
- `.agent-context/0-knowledge/04-domain-model.md` — Core entities, RiskConfig definition
- `.agent-context/0-knowledge/13-strategy-config-schema.md` — Full StrategyConfig JSON schema

### Project Patterns

- `frontend/trading-ui/src/app/features/strategy-builder/components/exit-rules-card/exit-rules-card.component.ts` — Reactive enable/disable pattern via `valueChanges` + `takeUntilDestroyed`
- `frontend/trading-ui/src/app/features/strategy-builder/components/grid-config-card/grid-config-card.component.ts` — Conditional field visibility via getter
- `frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.ts` — Target component (currently thin wrapper)
- `frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts` — `PositionSizeType`, `RiskConfig` interface
- `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.ts` — Form → API model mapping
- `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.ts` — Client-side validation
- `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` — `_buildForm()` risk FormGroup definition
- `frontend/trading-ui/src/app/core/services/hyperliquid-api.service.ts` — `getAccountSummary()` for equity
- `frontend/trading-ui/src/app/core/models/backtest.model.ts` — `BacktestRiskConfig` mirror type
- `frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.ts` — `positionSizeLabel` getter
- `frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.ts` — `positionSizeLabel` getter
- `frontend/trading-ui/src/app/features/strategy-builder/components/preview-summary-card/preview-summary-card.component.ts` — Risk text display

### [x] Phase 1: TypeScript Models & Form Infrastructure

**Complexity**: Medium | **Risk**: Low

- [x] Task 1.1: Update `PositionSizeType` and `RiskConfig` in `strategy.model.ts`
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-management-ui-phase-01-details.md#task-11-update-positionsizetype-and-riskconfig

- [x] Task 1.2: Update `BacktestRiskConfig` in `backtest.model.ts`
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-management-ui-phase-01-details.md#task-12-update-backtestriskconfig

- [x] Task 1.3: Add new form controls in `strategy-builder-page._buildForm()`
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-management-ui-phase-01-details.md#task-13-add-new-form-controls

- [x] Task 1.4: Pass `exitGroup` to risk card from parent template
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-management-ui-phase-01-details.md#task-14-pass-exitgroup-to-risk-card

- [x] Task 1.5: Update `strategy-mapper.service.ts` risk mapping
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-management-ui-phase-01-details.md#task-15-update-strategy-mapper

- [x] Task 1.6: Update `strategy-validation.service.ts` for mode-conditional validation
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-management-ui-phase-01-details.md#task-16-update-strategy-validation

- [x] Task 1.7: Build verification
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-management-ui-phase-01-details.md#task-17-build-verification

### [x] Phase 2: Risk Management Card UI & Unit Tests

**Complexity**: High | **Risk**: Medium

- [x] Task 2.1: Add imports, inputs, and reactive lifecycle to `risk-management-card.component.ts`
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-management-ui-phase-02-details.md#task-21-add-imports-inputs-and-reactive-lifecycle

- [x] Task 2.2: Update `risk-management-card.component.html` with new fields and conditional visibility
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-management-ui-phase-02-details.md#task-22-update-template-with-new-fields

- [x] Task 2.3: Update `risk-management-card.component.scss` for new layout sections
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-management-ui-phase-02-details.md#task-23-update-styles

- [x] Task 2.4: Create `risk-management-card.component.spec.ts`
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-management-ui-phase-02-details.md#task-24-create-unit-tests

- [x] Task 2.5: Build + lint + test verification
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-management-ui-phase-02-details.md#task-25-build-lint-test

### [x] Phase 3: Live Calculation Preview

**Complexity**: High | **Risk**: Medium

- [x] Task 3.1: Add equity fetching and preview calculation logic to `risk-management-card.component.ts`
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-management-ui-phase-03-details.md#task-31-add-equity-fetching-and-preview-logic

- [x] Task 3.2: Add preview panel template to `risk-management-card.component.html`
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-management-ui-phase-03-details.md#task-32-add-preview-panel-template

- [x] Task 3.3: Add preview panel styles to `risk-management-card.component.scss`
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-management-ui-phase-03-details.md#task-33-add-preview-panel-styles

- [x] Task 3.4: Add unit tests for preview calculations
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-management-ui-phase-03-details.md#task-34-add-preview-calculation-tests

- [x] Task 3.5: Build + test verification
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-management-ui-phase-03-details.md#task-35-build-test-verification

### [x] Phase 4: Backtest & Preview Summary Updates

**Complexity**: Low | **Risk**: Low

- [x] Task 4.1: Update `backtest-form.component.ts` positionSizeLabel for `risk_based`
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-management-ui-phase-04-details.md#task-41-update-backtest-form-label

- [x] Task 4.2: Update `backtest-result.component.ts` positionSizeLabel for `risk_based`
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-management-ui-phase-04-details.md#task-42-update-backtest-result-label

- [x] Task 4.3: Update `preview-summary-card.component.ts` risk display
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-management-ui-phase-04-details.md#task-43-update-preview-summary-card

- [x] Task 4.4: Update existing test specs for new `risk_based` mode
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-management-ui-phase-04-details.md#task-44-update-existing-test-specs

- [x] Task 4.5: Full build + lint + all tests
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-management-ui-phase-04-details.md#task-45-full-build-lint-all-tests

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: TypeScript Models & Form Infrastructure | Medium | Low |
| Phase 2: Risk Management Card UI & Unit Tests | High | Medium |
| Phase 3: Live Calculation Preview | High | Medium |
| Phase 4: Backtest & Preview Summary Updates | Low | Low |
| **Total** | **High** | **Medium** |

### Scoping Notes

- Backend PBIs (P1 R-Based Position Sizing, P1 Auto-Leverage & Isolated Margin) are prerequisites — this plan assumes backend `PositionSizeType.RiskBased` enum, `RiskPerTradePercent`, and `AutoLeverage` fields exist in the backend models when this is implemented
- If backend PBIs are not yet complete, the frontend can be built and tested independently but the save/load round-trip will fail until the backend accepts `risk_based` as a valid `positionSizeType`
- `MatSlideToggleModule` is a new Material import not currently used anywhere in the codebase
- Preview calculations are frontend-only (formulas from `33-risk-management-and-trade-sizing.md`) — no backend API call for the calculation
- Equity is fetched from `GET /api/account` → `AccountSummaryDto.Equity` (existing endpoint)
- Stop-loss preview only works with `fixed_percent` SL type — non-percentage SL types (swing_low, atr_trailing) show "Configure a fixed-percent stop-loss to see position sizing preview"
- Maintenance margin rate hardcoded to `0.5 / maxLeverage` per knowledge file (0.01 for BTC@50x) — future enhancement may fetch this per-asset

## Dependencies

- Angular Material (`MatSlideToggleModule`, `MatTooltipModule`)
- `HyperliquidApiService.getAccountSummary()` — existing service
- Backend `RiskBased` enum value and new fields (from prereq PBIs)

## Success Criteria

- All acceptance criteria from the PBI pass
- Risk management card shows/hides fields correctly for all three sizing modes
- Live preview updates reactively when riskPerTradePercent, autoLeverage, or SL changes
- Warning banner appears when riskPerTradePercent > 5%
- Validation error when risk_based selected with no stop-loss
- All existing and new unit tests pass
- Frontend builds and lints without errors

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-04-11T12:00:00Z | 2026-04-11T12:30:00Z |
| Plan Reviewer | plan-reviewed | 2026-04-11T00:50:38Z | 2026-04-11T00:58:33Z |
| 3-Develop: 2 Implementer | implemented | 2026-04-12T09:28:27Z | 2026-04-12T13:35:00Z |
