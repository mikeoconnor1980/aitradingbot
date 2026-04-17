applyTo: ".agent-context/3-develop/build/changes/20260331-grid-ladder-partial-fill-changes.md"
currentAgent: "None"
agentStartedAt: "2026-03-31T10:32:14Z"
status: "implemented"
lastUpdated: "2026-03-31T15:56:45Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: Grid Ladder Remains Active After Partial Fill

## Overview

Correct the grid lifecycle in `GridController` so that remaining buy ladder levels stay active after partial fills, allowing additional fills to improve average entry before the cycle closes via take-profit, stop-loss, or full fill.

## PBI Details

### User Story

As a strategy developer, I want the grid ladder to remain active after the first fills occur so that additional lower levels can continue filling, average entry can improve as intended, and the backtest/live behavior matches the intended pullback grid strategy.

### Problem Statement

The current `GridController.ProcessAsync` transitions the cycle to `Closing` as soon as `positionState.IsOpen` is true (any non-zero position). This immediately emits a `TakeProfit` signal, and `BacktestPositionManager.PlaceTakeProfitAsync` cancels ALL remaining open buy orders before placing the exit sell. The ladder never stays active beyond the first fill.

The `GridLifecycle` enum already defines `PartiallyFilled` and `FullyFilled` states, and `BacktestRunner.ApplyGridFillState` correctly sets them — but `GridController` ignores these states and overwrites them with `Closing` unconditionally.

### Acceptance Criteria

- [ ] **Given** a deployed grid with multiple buy levels, **When** the first level fills, **Then** the remaining lower levels stay open and available to fill.
- [ ] **Given** a partially filled grid, **When** price continues lower into additional grid levels, **Then** those lower levels fill and update position size and average entry.
- [ ] **Given** a partially filled grid, **When** the cycle has not yet reached an explicit exit condition, **Then** the controller does not cancel the remaining open buy ladder solely because a position exists.
- [ ] **Given** a grid cycle that reaches take profit or stop loss, **When** the exit is triggered, **Then** the remaining unfilled grid levels are cancelled as part of the closing flow with the correct cancellation reason.
- [ ] **Given** a completed cycle after partial and additional fills, **When** the debug data and cycle summary are viewed, **Then** the reported levels filled and average-entry-driven outcome match the corrected lifecycle behavior.
- [ ] **Given** the documented strategy knowledge, **When** the implementation is reviewed, **Then** the runtime behavior matches the intended multi-level accumulating grid design.

## Objectives

- Fix `GridController.ProcessAsync` to respect `GridLifecycle` intermediate states (`PartiallyFilled`, `FullyFilled`) instead of transitioning directly to `Closing` on first position open
- Implement controller-checked take-profit for partially filled grids (check candle close against TP level each candle; emit market close only when TP is reached)
- Ensure stop-loss remains reachable from all filled states
- Rename `CancellationReason.PositionOpened` to `CancellationReason.TakeProfitTriggered` for clarity
- Create comprehensive `GridControllerTests` (currently no unit tests exist for the controller)
- Add multi-level integration tests to `RealBacktestRunnerTests`
- Update knowledge documentation to reflect corrected lifecycle behavior

### Design Decision: TP During Partial Fill

**Selected: Option A — Controller-Checked TP**

During `PartiallyFilled` state, no persistent TP sell order is placed in the engine. Instead, the controller checks each candle close against the TP level (computed from current average entry). If the TP level is reached → emit market close. Otherwise → stay active. This requires no changes to `BacktestPositionManager` or `SimulatedExecutionEngine`.

### Discovery References

**Root Cause (2 co-dependent bugs)**:

1. **`GridController.ProcessAsync` (line 36)**: `if (positionState.IsOpen)` → `gridState.Lifecycle = GridLifecycle.Closing` — unconditionally overwrites `PartiallyFilled`/`FullyFilled` states set by `BacktestRunner.ApplyGridFillState`
2. **`BacktestPositionManager.PlaceTakeProfitAsync`**: Cancels ALL open orders (including remaining buy ladder) every time a `TakeProfit` signal is received — correct for genuine exits, but triggered prematurely by Bug #1

**Execution ordering per candle** (BacktestRunner main loop):
1. `executionEngine.ProcessCandle(candle)` → fills occur
2. `RecordFill` → `ApplyGridFillState` → sets `PartiallyFilled`/`FullyFilled` ✓
3. `scheduler.UpdateState(gridState, positionState)` → `IsOpen = true`
4. `candleClock.ProcessCandleAsync` → `GridController.ProcessAsync` → **Bug: overwrites lifecycle to `Closing`**
5. `BacktestPositionManager` → **Bug: cancels all remaining buy orders**

### Project Patterns

- `src/TradePilot.Application/Trading/Services/GridController.cs` - Primary bug location; lifecycle-blind position-open branch
- `src/TradePilot.Application/Trading/Services/BacktestPositionManager.cs` - Signal execution; cancellation logic
- `src/TradePilot.Application/Trading/Models/GridState.cs` - Mutable state bag: `Lifecycle`, `FilledLevels`, `TotalLevels`, `GridCycleId`
- `src/TradePilot.Application/Trading/Models/GridLifecycle.cs` - Enum with 8 values including `PartiallyFilled`, `FullyFilled`
- `src/TradePilot.Application/Trading/Models/PositionState.cs` - `IsOpen => Size != 0`
- `src/TradePilot.Application/Backtesting/Services/BacktestRunner.cs` - `ApplyGridFillState` correctly sets intermediate lifecycle states
- `src/TradePilot.Application/Backtesting/Models/CancellationReason.cs` - Enum: `GridRedeployed`, `PositionOpened`, `StopLossTriggered`, `ManualCancel`
- `tests/TradePilot.Application.Tests/Backtesting/Services/RealBacktestRunnerTests.cs` - Integration tests with real controller (gridLevels=1 only)
- `.agent-context/0-knowledge/15-grid-controller.md` - Grid controller knowledge (lifecycle states)
- `.agent-context/0-knowledge/24-backtesting-grid-engine-explained.md` - Grid engine docs (documents current buggy behavior)

### [x] Phase 1: GridController Lifecycle Fix + Unit Tests

**Complexity**: Medium | **Risk**: Medium

- [x] Task 1.1: Refactor `GridController.ProcessAsync` to be lifecycle-aware
  - Details: .agent-context/3-develop/build/plans/details/20260331-grid-ladder-partial-fill-phase-01-details.md#task-11-refactor-gridcontroller-processasync-to-be-lifecycle-aware

- [x] Task 1.2: Rename `CancellationReason.PositionOpened` to `TakeProfitTriggered` (C#, TypeScript, and test usages including `BacktestsControllerTests.cs`)
  - Details: .agent-context/3-develop/build/plans/details/20260331-grid-ladder-partial-fill-phase-01-details.md#task-12-rename-cancellationreason-positionopened

- [x] Task 1.3: Create `GridControllerTests.cs` with comprehensive unit tests
  - Details: .agent-context/3-develop/build/plans/details/20260331-grid-ladder-partial-fill-phase-01-details.md#task-13-create-gridcontrollertests

- [x] Task 1.4: Run tests and verify
  - Details: .agent-context/3-develop/build/plans/details/20260331-grid-ladder-partial-fill-phase-01-details.md#task-14-run-tests-and-verify

### [x] Phase 2: Integration Tests + Knowledge Documentation

**Complexity**: Medium | **Risk**: Low

- [x] Task 2.1: Add multi-level grid integration tests to `RealBacktestRunnerTests`
  - Details: .agent-context/3-develop/build/plans/details/20260331-grid-ladder-partial-fill-phase-02-details.md#task-21-add-multi-level-grid-integration-tests

- [x] Task 2.2: Update existing integration tests for corrected behavior
  - Details: .agent-context/3-develop/build/plans/details/20260331-grid-ladder-partial-fill-phase-02-details.md#task-22-update-existing-integration-tests

- [x] Task 2.3: Update knowledge documentation
  - Details: .agent-context/3-develop/build/plans/details/20260331-grid-ladder-partial-fill-phase-02-details.md#task-23-update-knowledge-documentation

- [x] Task 2.4: Run all tests and verify
  - Details: .agent-context/3-develop/build/plans/details/20260331-grid-ladder-partial-fill-phase-02-details.md#task-24-run-all-tests-and-verify

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: GridController Lifecycle Fix + Unit Tests | Medium | Medium |
| Phase 2: Integration Tests + Knowledge Documentation | Medium | Low |
| **Total** | **Medium** | **Medium** |

### Scoping Notes

- The fix is contained entirely within `TradePilot.Application` — no Domain, Infrastructure, or API layer changes needed
- `BacktestPositionManager` and `SimulatedExecutionEngine` do NOT need functional changes — the fix upstream in `GridController` prevents premature signal emission
- The `CancellationReason` rename is cosmetic but improves auditability
- The controller-checked TP for partial fills uses candle close (consistent with "strategies execute on confirmed candle closes only" principle)
- A future PBI may add persistent TP sell orders during partial fill (Option B) if more precise exit timing is needed
- Frontend `CancellationReason` TypeScript enum rename is included in Task 1.2

## Dependencies

- No external dependencies — all libraries and frameworks are already in the project
- `GridLifecycle.PartiallyFilled` and `GridLifecycle.FullyFilled` already exist in the enum

## Success Criteria

- All 6 PBI acceptance criteria pass
- `GridController` unit tests cover all lifecycle transition paths
- Multi-level integration tests demonstrate ladder staying active after partial fills
- Existing integration tests continue to pass (gridLevels=1 scenario unchanged)
- Knowledge documentation reflects corrected lifecycle behavior
- `dotnet test` passes for all affected test projects

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-03-31T09:57:47Z | 2026-03-31T10:09:12Z |
| Plan Reviewer | plan-reviewed | 2026-03-31T10:15:00Z | 2026-03-31T10:20:00Z |
| Plan Implementer | implemented | 2026-03-31T10:32:14Z | 2026-03-31T15:56:45Z |
