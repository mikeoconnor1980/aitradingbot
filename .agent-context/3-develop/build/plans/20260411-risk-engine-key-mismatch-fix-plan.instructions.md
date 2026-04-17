applyTo: ".agent-context/3-develop/build/changes/20260411-risk-engine-key-mismatch-fix-changes.md"
currentAgent: ""
agentStartedAt: ""
status: "complete"
lastUpdated: "2026-04-12T09:02:40Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: Risk Engine — Signal Parameter Key Mismatch Fix

## Overview

Fix two signal parameter key mismatches that cause `LiveRiskEngine.CheckOrderSize` and `CheckOpenOrderLimit` to silently pass all signals, rendering max order size and max open orders enforcement completely inoperative.

## PBI Details

### Summary

Fix the signal parameter key mismatch that causes `LiveRiskEngine.CheckOrderSize` to silently pass all signals. Three different keys are used across the pipeline — `"notionalUsd"` (risk engine), `"notionalPerLevel"` (GridController), and `"notional"` (SignalController) — so the max order size check never finds the value and never blocks oversized orders.

Additionally, `CheckOpenOrderLimit` uses `"levels"` but `GridController` emits `"gridLevels"`, causing the same silent-pass issue for max open orders enforcement.

### User Story

> As a **trader**, I want **the max order size check to actually block oversized orders** so that **my configured risk limits protect my account as expected**.

### Acceptance Criteria

- [ ] **Given** `MaxOrderSizeUsd = 1000` and a `DeployGrid` signal with `notionalUsd = 1500`, **When** the risk engine validates the signal, **Then** the signal is blocked
- [ ] **Given** `MaxOrderSizeUsd = 1000` and a `DeployGrid` signal with `notionalUsd = 800`, **When** the risk engine validates the signal, **Then** the signal is approved
- [ ] **Given** `MaxOrderSizeUsd = 1000` and an `OpenPosition` signal with `notionalUsd = 1500`, **When** the risk engine validates the signal, **Then** the signal is blocked
- [ ] **Given** a signal with no `notionalUsd` key in parameters, **When** the risk engine validates the signal, **Then** the signal passes (backward compatible)

## Objectives

- Standardise on `"notionalUsd"` as the canonical signal parameter key for USD notional values
- Fix `"levels"` → `"gridLevels"` mismatch in `CheckOpenOrderLimit`
- Ensure all unit tests use the corrected keys and verify the risk checks actually block oversized signals

### Discovery References

- `LiveRiskEngine.CheckOrderSize` (line 176) looks up `"notionalUsd"` — neither `GridController` nor `SignalController` emit this key, so `TryGetValue` always returns `false` and the check silently passes
- `LiveRiskEngine.CheckOpenOrderLimit` (line 192) looks up `"levels"` — `GridController` emits `"gridLevels"`, so same silent-pass issue
- Existing `LiveRiskEngineTests` use `"notionalUsd"` and `"levels"` in test dictionaries, so tests pass but don't test the production code path
- `LivePositionManager` and `BacktestPositionManager` both read `"notionalPerLevel"` and `"gridLevels"` — these consumers must be updated when keys change

### Project Patterns

- `src/TradePilot.Application/Trading/Services/LiveRiskEngine.cs` — Risk engine with `CheckOrderSize` and `CheckOpenOrderLimit`
- `src/TradePilot.Application/Trading/Services/GridController.cs` — Emits `DeployGrid` signals with `"notionalPerLevel"` and `"gridLevels"`
- `src/TradePilot.Application/Trading/Services/SignalController.cs` — Emits `OpenPosition` signals with `"notional"`
- `src/TradePilot.Application/Trading/Services/LivePositionManager.cs` — Reads `"notionalPerLevel"` and `"gridLevels"` from signals
- `src/TradePilot.Application/Trading/Services/BacktestPositionManager.cs` — Reads `"notionalPerLevel"` and `"gridLevels"` from signals
- `tests/TradePilot.Application.Tests/Trading/Services/LiveRiskEngineTests.cs` — Existing risk engine tests
- `tests/TradePilot.Application.Tests/Trading/Services/GridControllerTests.cs` — Asserts `"notionalPerLevel"` on DeployGrid output
- `tests/TradePilot.Application.Tests/Trading/Services/SignalControllerTests.cs` — Asserts `"notional"` on OpenPosition output
- `tests/TradePilot.Application.Tests/Trading/Services/LivePositionManagerTests.cs` — Uses `"notionalPerLevel"` in signal construction

### [x] Phase 1: Fix Key Mismatches Across Pipeline and Tests

**Complexity**: Low | **Risk**: Low

- [x] Task 1.1: Rename "notionalPerLevel" → "notionalUsd" in GridController signal emission
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-engine-key-mismatch-fix-phase-01-details.md#task-11-rename-notionalperlevel-in-gridcontroller

- [x] Task 1.2: Rename "notional" → "notionalUsd" in SignalController signal emission
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-engine-key-mismatch-fix-phase-01-details.md#task-12-rename-notional-in-signalcontroller

- [x] Task 1.3: Update LivePositionManager to read "notionalUsd" instead of "notionalPerLevel"
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-engine-key-mismatch-fix-phase-01-details.md#task-13-update-livepositionmanager

- [x] Task 1.4: Update BacktestPositionManager to read "notionalUsd" instead of "notionalPerLevel"
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-engine-key-mismatch-fix-phase-01-details.md#task-14-update-backtestpositionmanager

- [x] Task 1.5: Fix "levels" → "gridLevels" in LiveRiskEngine.CheckOpenOrderLimit
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-engine-key-mismatch-fix-phase-01-details.md#task-15-fix-levels-key-in-checkopenorderlimit

- [x] Task 1.6: Update GridControllerTests key assertions
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-engine-key-mismatch-fix-phase-01-details.md#task-16-update-gridcontrollertests

- [x] Task 1.7: Update SignalControllerTests key assertions
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-engine-key-mismatch-fix-phase-01-details.md#task-17-update-signalcontrollertests

- [x] Task 1.8: Update LivePositionManagerTests key usage
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-engine-key-mismatch-fix-phase-01-details.md#task-18-update-livepositionmanagertests

- [x] Task 1.9: Update LiveRiskEngineTests — fix keys and add acceptance criteria tests
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-engine-key-mismatch-fix-phase-01-details.md#task-19-update-liveriskengine-tests

- [x] Task 1.10: Run all tests and verify
  - Details: .agent-context/3-develop/build/plans/details/20260411-risk-engine-key-mismatch-fix-phase-01-details.md#task-110-run-all-tests

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: Fix Key Mismatches Across Pipeline and Tests | Low | Low |
| **Total** | **Low** | **Low** |

### Scoping Notes

- All changes are string literal renames with no logic changes (except `CheckOpenOrderLimit` key fix)
- Every affected file has been fully read and verified during discovery
- No DI, configuration, or infrastructure changes required
- No new files need to be created — all changes are modifications to existing files
- The `"notional"` key emitted by `SignalController` was dead code (never consumed) but will now be read by `LiveRiskEngine.CheckOrderSize` after the rename to `"notionalUsd"`

## Dependencies

- No external dependencies
- No infrastructure changes
- No database changes

## Success Criteria

- All existing tests pass with corrected key names
- New acceptance criteria tests verify `CheckOrderSize` blocks oversized signals end-to-end
- `CheckOpenOrderLimit` uses the correct `"gridLevels"` key
- `dotnet build TradePilot.sln` succeeds
- `dotnet test` passes all tests

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|----------|
| Implementation Planner | planned | 2026-04-11T22:20:58Z | 2026-04-11T22:27:47Z |
| Plan Reviewer | reviewed | 2026-04-11T22:28:18Z | 2026-04-11T22:33:11Z |
| 3-Develop: 2 Implementer | implemented | 2026-04-11T23:06:16Z | 2026-04-12T08:04:27Z |
| 3-Develop: 3 Reviewer | complete | 2026-04-12T08:06:10Z | 2026-04-12T09:02:40Z |
