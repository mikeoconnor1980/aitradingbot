# Grid Ladder Remains Active After Partial Fill

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-30T22:18:43Z

## User Story

As a strategy developer, I want the grid ladder to remain active after the first fills occur so that additional lower levels can continue filling, average entry can improve as intended, and the backtest/live behavior matches the intended pullback grid strategy.

## Problem Statement

The intended strategy behavior is a multi-level pullback grid where lower levels can continue filling as price moves deeper into the ladder. This allows the position to average into the move before either recovering to take profit or breaking down into stop-loss or hedge logic.

The current implementation appears to behave differently. Once a position is open, the grid controller switches the cycle into closing mode and emits an exit signal. The position manager then cancels the remaining open buy orders before placing the take-profit or stop-loss exit order. This means the ladder no longer remains active after partial entry.

As a result, the strategy behaves more like “enter on the first filled levels, then stop adding and manage only the exit” rather than a true accumulating grid. This can materially distort:

- levels filled per cycle
- average entry evolution
- stop-loss outcomes
- grid-cycle PnL
- alignment between intended strategy behavior and actual implementation

## Requirements

### Functional Requirements

1. **Keep the ladder active after partial fills** — When one or more grid buy levels fill, the remaining unfilled grid levels must stay open unless an explicit lifecycle event requires them to be cancelled.
2. **Do not cancel remaining buys just because a position is open** — Opening a position from partial grid fills must not, by itself, transition the cycle into an exit-only state.
3. **Support continued averaging while the cycle is active** — Additional lower grid levels must be allowed to fill while the grid cycle remains active, and each new fill must contribute to position size and average entry.
4. **Exit behavior must be explicit** — Remaining grid levels may only be cancelled when one of the intended terminal or protective conditions occurs, such as:
   - take profit exit is being executed for the cycle
   - stop loss exit is being executed for the cycle
   - hedge/breakdown logic explicitly requires cancellation
   - the grid is redeployed or manually cancelled
5. **Lifecycle must reflect partial-fill states** — Grid lifecycle transitions should use the intended active states such as `Active`, `PartiallyFilled`, `FullyFilled`, `Closing`, and `Closed`, rather than moving directly to `Closing` on the first open position.
6. **Debug/audit outputs must remain understandable** — Backtest debug views and grid-cycle summaries must correctly report placed levels, filled levels, cancellations, and exit reason after the lifecycle behavior is corrected.

### Non-Functional Requirements

- **Strategy alignment**: The implementation must align with the documented pullback grid strategy and grid lifecycle model.
- **Determinism**: Backtest behavior must remain deterministic over the same historical data.
- **Safety**: Risk controls must still apply, and the change must not allow position accumulation beyond configured limits.
- **Shared pipeline**: Any solution should preserve the architecture principle that live trading and backtesting share the same grid-control pipeline wherever practical.

## Acceptance Criteria

- [ ] **Given** a deployed grid with multiple buy levels, **When** the first level fills, **Then** the remaining lower levels stay open and available to fill.
- [ ] **Given** a partially filled grid, **When** price continues lower into additional grid levels, **Then** those lower levels fill and update position size and average entry.
- [ ] **Given** a partially filled grid, **When** the cycle has not yet reached an explicit exit condition, **Then** the controller does not cancel the remaining open buy ladder solely because a position exists.
- [ ] **Given** a grid cycle that reaches take profit or stop loss, **When** the exit is triggered, **Then** the remaining unfilled grid levels are cancelled as part of the closing flow with the correct cancellation reason.
- [ ] **Given** a completed cycle after partial and additional fills, **When** the debug data and cycle summary are viewed, **Then** the reported levels filled and average-entry-driven outcome match the corrected lifecycle behavior.
- [ ] **Given** the documented strategy knowledge, **When** the implementation is reviewed, **Then** the runtime behavior matches the intended multi-level accumulating grid design.

### Release Notes Information

- **Heading**: Grid Ladder Remains Active After Partial Fill
- **Release note type**: Feature
- **Release Note Summary**: Corrects grid lifecycle behavior so remaining ladder levels stay active after partial fills, allowing additional fills to improve average entry before the cycle closes.
- **Release Notes Audience**: Product
- **Breaking Change**: No

## Investigation Notes

- The strategy documentation describes a multi-level grid where average entry improves as more lower levels fill.
- The grid controller knowledge describes lifecycle states including `Active`, `PartiallyFilled`, and `FullyFilled`, which implies the ladder is expected to stay active beyond the first fill.
- The current implementation appears to set the grid lifecycle to `Closing` as soon as `positionState.IsOpen` is true.
- The current backtest position manager cancels remaining open orders when placing the exit signal, which likely causes the ladder to stop participating after partial entry.
- This issue is separate from, but related to, the broader backtest execution-order/intrabar modeling problem.

## Technical Considerations

### Relevant Components

- `src/TradePilot.Application/Trading/Services/GridController.cs`
- `src/TradePilot.Application/Trading/Services/BacktestPositionManager.cs`
- `src/TradePilot.Application/Backtesting/Services/BacktestRunner.cs`
- `src/TradePilot.Application/Trading/Models/GridState.cs`
- `src/TradePilot.Application/Trading/Models/GridLifecycle.cs`

### Design Questions To Refine Later

- At what exact lifecycle point should the ladder stop accepting additional fills?
- Should take profit be placed only after full fill, after first fill, or be adjusted dynamically as average entry changes?
- How should hedge logic interact with still-open grid levels?
- Do live trading and backtesting already share enough infrastructure to apply the same fix in both modes?

## Out of Scope

- Resolving the separate intrabar/candle-path backtesting accuracy problem
- Designing a full lower-timeframe execution model
- Reworking the UI beyond any updates needed to reflect corrected lifecycle behavior
- Adding new strategy types or changing non-grid strategies
