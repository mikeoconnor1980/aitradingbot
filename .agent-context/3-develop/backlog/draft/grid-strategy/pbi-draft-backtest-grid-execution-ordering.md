# Backtest Grid Execution Ordering Accuracy

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-30T22:18:43Z

## User Story

As a strategy developer, I want the backtest to represent grid fills and stop-loss behavior in a way that matches the intended execution model so that I can trust the reported fill counts, average entry, and cycle PnL when evaluating the strategy.

## Problem Statement

The current backtest engine processes fills from candle OHLC data and then evaluates the strategy on candle close. This is deterministic and simple, but for a grid strategy it can produce outcomes that are difficult to trust around fast moves and stop-loss events.

In particular, users can observe scenarios where only the first grid level is recorded as filled and the cycle exits at a loss, even though a ladder of additional grid orders was placed below. This is logically consistent with the current engine because only filled orders affect average entry, but it raises an unresolved product and engineering question: does the current candle-level execution model represent the intended strategy behavior accurately enough for backtesting decisions?

This PBI is to capture the problem, document the current execution assumptions, analyze where those assumptions break down for grid trading, and recommend a forward path. It is not yet committing the team to a specific implementation approach.

## Requirements

### Functional Requirements

1. **Document current execution model** — Clearly describe the existing backtest execution order, including:
   - open orders are processed against each 15m candle's OHLC range
   - buy limits fill when candle low reaches the order price
   - sell limits fill when candle high reaches the order price
   - strategy evaluation occurs on confirmed candle close
   - stop-loss/take-profit signals are generated after candle-close evaluation
   - remaining grid orders can be cancelled as part of the closing flow
2. **Capture the user-facing problem** — Record representative scenarios where the current model creates confusing or potentially misleading outcomes, including:
   - only 1 of N levels filled before exit
   - expected average-entry improvement not appearing because lower levels never filled
   - stop-loss outcomes that appear to happen "before" lower resting orders participate
3. **Analyze impact on backtest trustworthiness** — Assess which reported values are most sensitive to the current execution model, including:
   - levels filled per cycle
   - average entry price evolution
   - stop-loss and take-profit sequencing
   - cycle PnL and win/loss classification
4. **Compare candidate future approaches** — Document the pros, cons, and implementation implications of at least these options:
   - keep the current candle-based model and document limitations clearly
   - add a deterministic intrabar path model within each candle
   - simulate fills using a lower timeframe while keeping strategy evaluation on confirmed 15m closes
5. **Produce a recommendation** — Recommend one preferred direction for future implementation, with rationale tied to strategy realism, complexity, performance, and determinism.
6. **Define follow-on backlog** — If the recommendation is accepted, identify the follow-on implementation work items needed to deliver it.

### Non-Functional Requirements

- **Clarity**: The analysis must be understandable to both product and engineering stakeholders reviewing backtest credibility.
- **Architecture alignment**: Any recommended path must preserve the core principle that live trading and backtesting reuse the same strategy pipeline wherever practical.
- **Determinism**: Candidate approaches must explicitly state whether repeated runs over the same data produce the same result.
- **Performance awareness**: The recommendation must note the likely effect on backtest runtime and data requirements.

## Acceptance Criteria

- [ ] **Given** the current backtest engine, **When** the PBI is reviewed, **Then** it clearly explains the existing execution order and why unfilled grid levels do not affect average entry.
- [ ] **Given** a reviewer assessing backtest credibility, **When** they read the PBI, **Then** they can understand why stop-loss scenarios with only 1 filled level can occur under the current model.
- [ ] **Given** the identified execution-order issue, **When** the PBI is reviewed, **Then** it includes at least three candidate improvement approaches with explicit tradeoffs.
- [ ] **Given** the candidate approaches, **When** the PBI is reviewed, **Then** it records a recommended next direction or explicitly states what analysis is still needed before a direction can be chosen.
- [ ] **Given** the recommendation, **When** the PBI is reviewed, **Then** it identifies any follow-on implementation PBIs or spikes required.

### Release Notes Information

- **Heading**: Backtest Grid Execution Ordering Accuracy
- **Release note type**: Feature
- **Release Note Summary**: Analysis and design work to improve how backtests model grid fills, average entry changes, and stop-loss sequencing so that results are more trustworthy.
- **Release Notes Audience**: Product
- **Breaking Change**: No

## Investigation Notes

- Current architecture intentionally reuses `StrategyEngine`, `GridController`, `RiskEngine`, and `PositionManager`, with `SimulatedExecutionEngine` as the backtest-specific execution layer.
- The current replay loop processes fills before candle-close strategy evaluation.
- The current behavior is internally consistent, but it may not be realistic enough for grid strategies because it does not model true intrabar sequence.
- This is most likely to matter in fast moves, stop-loss events, and any scenario where multiple grid levels and exit conditions compete within a small time window.
- My current view is that this is a meaningful backtesting accuracy issue, not just a UI explanation issue.
- My current view is that the strongest long-term option is to evaluate strategy signals on 15m confirmed closes while simulating execution from a lower timeframe. A deterministic intrabar path model is a viable intermediate option if the team wants lower implementation cost.

## Technical Considerations

### Relevant Components

- `src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs`
- `src/TradingApp.Application/Backtesting/Services/SimulatedExecutionEngine.cs`
- `src/TradingApp.Application/Trading/Services/GridController.cs`
- `src/TradingApp.Application/Trading/Services/BacktestPositionManager.cs`
- `src/TradingApp.Application/Backtesting/GetBacktestDebugQuery.cs`

### Constraints

- Strategy decisions are intentionally evaluated on confirmed candle closes only.
- Backtesting currently operates from persisted candle data, not tick data.
- Any redesign should keep deterministic replay and avoid introducing look-ahead bias.

## Out of Scope

- Implementing a new backtest execution model in this item
- Reworking live trading execution behavior
- Changing the user-facing backtest UI beyond what is needed to explain current limitations
- Tick-level simulation or exchange microstructure modeling
