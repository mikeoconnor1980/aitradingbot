---
applyTo: ".agent-context/3-develop/build/changes/20260403-ui-rsi-condition-signal-mode-changes.md"
currentAgent: "None"
agentStartedAt: "2026-04-03T14:43:54Z"
status: "complete"
lastUpdated: "2026-04-03T14:43:54Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: F6 — UI: RSI Condition Card + Signal Mode

## Overview

Enable signal mode in the Strategy Builder UI by unlocking the entry conditions card, delivering the RSI condition item component, adding a "Custom Signal" template, and wiring form/mapper/validation/preview for signal-mode strategies.

## PBI Details

**PBI:** F6 — UI: RSI Condition Card + Signal Mode
**Status:** Draft | **Phase:** 1b (First Signal Condition)
**Depends On:** F2 (Builder UI — Grid), F5 (Condition Evaluator + RSI Handler)

### User Story

> As a **trader**, I want to **add RSI conditions to a strategy using the visual builder** so that **I can create signal-based entry rules without writing JSON**.

### Acceptance Criteria

- [ ] **Given** "Custom Signal" template, **When** selected, **Then** grid card hidden, entry conditions card enabled
- [ ] **Given** "Add RSI" clicked, **When** condition added, **Then** RSI fields (period, operator, value) displayed with defaults
- [ ] **Given** RSI value = 150, **When** form validates, **Then** error: "RSI value must be between 0 and 100"
- [ ] **Given** signal strategy with one RSI condition, **When** saved, **Then** canonical JSON has `strategyMode = "signal"`, `grid = null`, `entryConditions` with RSI entry
- [ ] **Given** preview card, **When** RSI condition configured, **Then** shows "when RSI(14) is below 40"
- [ ] **Given** signal mode with no conditions, **When** validated, **Then** error: "At least one entry condition required"

## Objectives

- Add "Custom Signal" template to the strategy template selector
- Transform the entry conditions card stub into a functional card with FormArray orchestration
- Create an RSI condition item component with period, operator, value fields and common shell (enabled, label, duplicate, remove)
- Create a condition factory service for creating typed condition FormGroups
- Update the page component to manage signal mode (form building, card visibility, template switching)
- Update the mapper to produce signal-mode canonical JSON
- Update the client validation service for signal-mode rules
- Update the preview summary card for signal-mode text generation
- Support loading saved signal-mode strategies back into the form

### Discovery References

- Backend models fully implemented (F5): `StrategyMode`, `EntryConditionConfig`, `RsiParams`, `EntryConditionType`, `EntryLogic` in `src/TradePilot.Application/StrategyAuthoring/Models/`
- Server-side validation already enforces: signal mode requires non-empty `entryConditions` + `entryLogic`; RSI period > 0, value 0–100
- RSI operators are string literals (not enum): `lt`, `lte`, `gt`, `gte`, `cross_above`, `cross_below`
- `EntryLogic` enum: `All` | `Any` — required for signal mode
- Frontend `StrategyMode` type already exists as `"grid" | "signal"` but `entryConditions` and `entryLogic` are typed as `null` only
- Knowledge doc 13 lists RsiParams field as `threshold` — this is outdated; the actual backend field is `Value` (camelCase `value` in JSON)

### Project Patterns

- `frontend/trading-ui/src/app/features/strategy-builder/components/grid-config-card/grid-config-card.component.ts` — Card with `@Input() group: FormGroup` + `hasError()` pattern
- `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` — Page orchestrating form, cards, validation stream, mapper, save/load
- `frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts` — All TypeScript types and `STRATEGY_TEMPLATES` constant
- `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.ts` — Form → config mapping (currently grid-only)
- `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.ts` — Client-side validation (currently grid-only)
- `frontend/trading-ui/src/app/features/strategy-builder/components/preview-summary-card/preview-summary-card.component.ts` — Preview text generation (currently grid-only)
- `frontend/trading-ui/src/app/features/strategy-builder/components/entry-conditions-card/entry-conditions-card.component.ts` — Empty stub to transform
- `src/TradePilot.Application/StrategyAuthoring/Models/RsiParams.cs` — Backend RSI params model (source of truth for field names/ranges)

### [x] Phase 1: Foundation — Models, Enums & Condition Factory

**Complexity**: Low | **Risk**: Low

- [x] Task 1.1: Update strategy model types for signal mode
  - Details: .agent-context/3-develop/build/plans/details/20260403-ui-rsi-condition-signal-mode-phase-01-details.md#task-11-update-strategy-model-types

- [x] Task 1.2: Create RSI operator display-name helper
  - Details: .agent-context/3-develop/build/plans/details/20260403-ui-rsi-condition-signal-mode-phase-01-details.md#task-12-create-rsi-operator-display-name-helper

- [x] Task 1.3: Create condition factory service
  - Details: .agent-context/3-develop/build/plans/details/20260403-ui-rsi-condition-signal-mode-phase-01-details.md#task-13-create-condition-factory-service

- [x] Task 1.4: Add "Custom Signal" template
  - Details: .agent-context/3-develop/build/plans/details/20260403-ui-rsi-condition-signal-mode-phase-01-details.md#task-14-add-custom-signal-template

- [x] Task 1.5: Frontend build and lint verification
  - Details: .agent-context/3-develop/build/plans/details/20260403-ui-rsi-condition-signal-mode-phase-01-details.md#task-15-frontend-build-and-lint

### [x] Phase 2: UI Components — RSI Condition Item & Entry Conditions Card

**Complexity**: Medium | **Risk**: Low

- [x] Task 2.1: Create RSI condition item component
  - Details: .agent-context/3-develop/build/plans/details/20260403-ui-rsi-condition-signal-mode-phase-02-details.md#task-21-create-rsi-condition-item-component

- [x] Task 2.2: Transform entry conditions card from stub to functional
  - Details: .agent-context/3-develop/build/plans/details/20260403-ui-rsi-condition-signal-mode-phase-02-details.md#task-22-transform-entry-conditions-card

- [x] Task 2.3: Frontend build and lint verification
  - Details: .agent-context/3-develop/build/plans/details/20260403-ui-rsi-condition-signal-mode-phase-02-details.md#task-23-frontend-build-and-lint

### [x] Phase 3: Page Integration — Wiring, Mapper, Validation, Preview & Tests

**Complexity**: High | **Risk**: Medium

- [x] Task 3.1: Update page component for signal mode support
  - Details: .agent-context/3-develop/build/plans/details/20260403-ui-rsi-condition-signal-mode-phase-03-details.md#task-31-update-page-component-for-signal-mode

- [x] Task 3.2: Update page template for conditional card rendering
  - Details: .agent-context/3-develop/build/plans/details/20260403-ui-rsi-condition-signal-mode-phase-03-details.md#task-32-update-page-template

- [x] Task 3.3: Update mapper service for signal mode
  - Details: .agent-context/3-develop/build/plans/details/20260403-ui-rsi-condition-signal-mode-phase-03-details.md#task-33-update-mapper-for-signal-mode

- [x] Task 3.4: Update client validation service for signal mode
  - Details: .agent-context/3-develop/build/plans/details/20260403-ui-rsi-condition-signal-mode-phase-03-details.md#task-34-update-validation-for-signal-mode

- [x] Task 3.5: Update preview summary card for signal mode
  - Details: .agent-context/3-develop/build/plans/details/20260403-ui-rsi-condition-signal-mode-phase-03-details.md#task-35-update-preview-for-signal-mode

- [x] Task 3.6: Add unit tests for condition factory
  - Details: .agent-context/3-develop/build/plans/details/20260403-ui-rsi-condition-signal-mode-phase-03-details.md#task-36-unit-tests-condition-factory

- [x] Task 3.7: Add unit tests for mapper signal-mode branch
  - Details: .agent-context/3-develop/build/plans/details/20260403-ui-rsi-condition-signal-mode-phase-03-details.md#task-37-unit-tests-mapper

- [x] Task 3.8: Add unit tests for validation signal-mode branch
  - Details: .agent-context/3-develop/build/plans/details/20260403-ui-rsi-condition-signal-mode-phase-03-details.md#task-38-unit-tests-validation

- [x] Task 3.9: Frontend build, lint, and test verification
  - Details: .agent-context/3-develop/build/plans/details/20260403-ui-rsi-condition-signal-mode-phase-03-details.md#task-39-frontend-build-lint-test

## Scoping Summary

| Phase | Complexity | Risk |
|-------|-----------|------|
| Phase 1: Foundation — Models, Enums & Condition Factory | Low | Low |
| Phase 2: UI Components — RSI Condition Item & Entry Conditions Card | Medium | Low |
| Phase 3: Page Integration — Wiring, Mapper, Validation, Preview & Tests | High | Medium |
| **Total** | **Medium** | **Low** |

### Scoping Notes

- This is a purely frontend feature — no backend code changes required (F5 already delivered all backend support)
- The backend API already accepts signal-mode payloads with RSI conditions; this PBI wires the UI to produce them
- `EntryLogic` (All/Any) is included in the mapper output but the UI dropdown for it is deferred — hardcoded to `"all"` for now since F6 only delivers RSI as first condition type
- Adding future condition types (EMA, MACD) requires: new item component + factory entry + "Add" button — no changes to card orchestration
- Server-side JSON serialization of `EntryLogic` uses snake_case_lower strings (`"all"`/`"any"`) — the mapper will send lowercase strings matching this convention

## Dependencies

- Angular Material (mat-card, mat-form-field, mat-input, mat-select, mat-icon, mat-button, mat-checkbox, mat-slide-toggle)
- Angular Reactive Forms (FormBuilder, FormGroup, FormArray, Validators)
- Existing strategy builder infrastructure (StrategyApiService, StrategyMapperService, StrategyValidationService)

## Success Criteria

- "Custom Signal" template selection hides grid card and shows entry conditions card
- RSI conditions can be added, configured, duplicated, and removed
- Signal-mode strategies produce correct canonical JSON via mapper
- Client validation enforces: at least one condition, RSI period > 0, RSI value 0–100
- Preview card shows signal-mode text: "Enter a long trade on BTC-USD 15m when RSI(14) is below 40."
- Saved signal-mode strategies load correctly back into the form
- All new/modified services have unit tests
- Frontend builds and passes lint + tests

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|----------|
| Implementation Planner | planned | 2026-04-03T12:19:26Z | 2026-04-03T12:30:41Z |
| Plan Reviewer | plan-reviewed | 2026-04-03T13:00:00Z | 2026-04-03T13:05:00Z |
| Plan Implementer | implemented | 2026-04-03T12:51:47Z | 2026-04-03T13:00:48Z |
| Implementation Reviewer | complete | 2026-04-03T14:43:54Z | 2026-04-03T14:50:00Z |
