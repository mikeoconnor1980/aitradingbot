applyTo: ".agent-context/3-develop/build/changes/20260411-auto-leverage-isolated-margin-changes.md"
currentAgent: "3-Develop: 3 Reviewer"
agentStartedAt: "2026-04-12T12:00:00Z"
status: "complete"
lastUpdated: "2026-04-12T12:30:00Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: Auto-Leverage & Isolated Margin Enforcement

## Overview

Derive leverage automatically from R (risk amount) and stop-loss distance, enforce isolated margin mode on Hyperliquid. When `AutoLeverage = true` and `PositionSizeType = RiskBased`, leverage is calculated as `floor(1 / (SL%/100 + maintenanceMarginRate))`, clamped to `[1, maxLeverage]`. Isolated margin is always enforced for RiskBased mode. The backtest engine simulates liquidation as a fallback beyond stop-loss.

## PBI Details

### Summary

When using Risk-Based sizing, leverage is automatically derived from stop-loss distance and asset margin tiers. Instead of the user choosing leverage arbitrarily, the system calculates it so that margin posted ≈ R and the stop-loss fires before the liquidation price. Isolated margin is enforced to contain risk per position.

### User Story

> As a **trader**, I want **leverage to be automatically calculated from my risk settings** so that **my stop-loss always fires before liquidation, and I never risk more than R on a single trade**.

### Acceptance Criteria

- **Given** `RiskBased` config with 1% risk, 2% SL, and a 50x-max asset (1% maintenance margin), **When** auto-leverage is enabled, **Then** leverage = floor(1 / (0.02 + 0.01)) = 33, and `SetLeverage(33, isolated)` is called on the exchange before the trade entry
- **Given** the same scenario, **When** the position is open, **Then** the liquidation price sits beyond the stop-loss price (SL fires first)
- **Given** `AutoLeverage = false` and manual leverage = 10, **When** a trade is placed, **Then** `SetLeverage(10, ...)` is called with the manual value
- **Given** `RiskBased` mode, **When** `SetLeverage` is called, **Then** `isIsolated = true` always (cross margin is not permitted)
- **Given** `PercentWallet` mode with `AutoLeverage = true`, **When** the config is validated, **Then** `AutoLeverage` is ignored (only works with `RiskBased`)
- **Given** auto-leverage calculates 60x but the asset's max is 50x, **When** leverage is applied, **Then** it is clamped to 50x and a warning is logged
- **Given** a backtest with `RiskBased` mode and auto-leverage, **When** price gaps through the stop-loss to the liquidation price, **Then** the position is force-closed at the liquidation price
- **Given** margin tier data is unavailable for an asset, **When** leverage is calculated, **Then** a conservative fallback (20x max, 2.5% maintenance) is used

## Objectives

- Implement leverage calculation utility: `leverage = floor(1 / (SL%/100 + 0.5/maxLeverage))`, clamped to `[1, maxLeverage]`
- Add `AutoLeverage` boolean to `RiskConfig` (default: `false`)
- Add `SetLeverageAsync` to `IExecutionEngine` and implement in live/backtest engines
- Enforce isolated margin (`isIsolated = true`) for all RiskBased mode trades
- Wire leverage setting into the grid pipeline (GridController → signal → LivePositionManager → execution engine)
- Simulate liquidation in backtests as fallback beyond stop-loss

### Discovery References

- `.agent-context/0-knowledge/33-risk-management-and-trade-sizing.md` — R-based sizing, auto-leverage formula, isolated margin reasoning
- `.agent-context/0-knowledge/04-domain-model.md` — RiskConfig schema, PositionDto (Leverage, MarginMode)
- `.agent-context/0-knowledge/02-hyperliquid-integration.md` — SetLeveragePayload, exchange actions
- `.agent-context/0-knowledge/15-grid-controller.md` — grid lifecycle, DeployGrid signal
- `.agent-context/0-knowledge/18-backtesting-architecture.md` — SimulatedExecutionEngine, backtest/live code reuse
- `.agent-context/0-knowledge/30-worker-execution-pipeline.md` — Worker pipeline, SetLeverage stub

### Project Patterns

- `src/TradePilot.Application/Trading/Services/PositionSizeResolver.cs` — static utility pattern for leverage calculator
- `src/TradePilot.Application/Trading/Services/GridController.cs` — DeployGrid signal emission with Parameters dict
- `src/TradePilot.Application/Trading/Services/LivePositionManager.cs` — signal consumption and execution engine calls
- `src/TradePilot.Infrastructure/Services/LiveExecutionEngine.cs` — EIP-712 signed exchange actions, asset index caching
- `src/TradePilot.Application/Backtesting/Services/SimulatedExecutionEngine.cs` — backtest execution stubs
- `src/TradePilot.Api/Services/HyperliquidAssetMetadataCache.cs` — asset metadata caching with MaxLeverage
- `src/TradePilot.Api/Services/HyperliquidOrderService.cs` — UpdateLeverageAsync (working API implementation)
- `src/TradePilot.Application/StrategyAuthoring/Validation/BusinessRuleValidator.cs` — risk config validation
- `src/TradePilot.Application/Agent/Models/OrderCommandPayload.cs` — SetLeveragePayload model

### [x] Phase 1: Domain Model, Leverage Calculator & Defaults

**Complexity**: Low-Medium | **Risk**: Low

- [x] Task 1.1: Add `AutoLeverage` boolean to `RiskConfig` record
  - Details: .agent-context/3-develop/build/plans/details/20260411-auto-leverage-isolated-margin-phase-01-details.md#task-11-add-autoleverage-to-riskconfig

- [x] Task 1.2: Create `LeverageCalculator` static utility
  - Details: .agent-context/3-develop/build/plans/details/20260411-auto-leverage-isolated-margin-phase-01-details.md#task-12-create-leveragecalculator-static-utility

- [x] Task 1.3: Flip `IsCross` defaults to `false` (isolated margin)
  - Details: .agent-context/3-develop/build/plans/details/20260411-auto-leverage-isolated-margin-phase-01-details.md#task-13-flip-iscross-defaults

- [x] Task 1.4: Update `BusinessRuleValidator.ValidateRisk`
  - Details: .agent-context/3-develop/build/plans/details/20260411-auto-leverage-isolated-margin-phase-01-details.md#task-14-update-businessrulevalidator

- [x] Task 1.5: Update `RiskConfigRequest` DTO and `BacktestsController` mapping
  - Details: .agent-context/3-develop/build/plans/details/20260411-auto-leverage-isolated-margin-phase-01-details.md#task-15-update-riskconfigrequest-and-backtestscontroller

- [x] Task 1.6: Unit tests for LeverageCalculator and validation
  - Details: .agent-context/3-develop/build/plans/details/20260411-auto-leverage-isolated-margin-phase-01-details.md#task-16-unit-tests

- [x] Task 1.7: Build solution and run architecture tests
  - Details: .agent-context/3-develop/build/plans/details/20260411-auto-leverage-isolated-margin-phase-01-details.md#task-17-build-and-architecture-tests

### [x] Phase 2: Execution Engine SetLeverage

**Complexity**: Medium | **Risk**: Medium

- [x] Task 2.1: Add `SetLeverageAsync` to `IExecutionEngine` interface
  - Details: .agent-context/3-develop/build/plans/details/20260411-auto-leverage-isolated-margin-phase-02-details.md#task-21-add-setleverageasync-to-iexecutionengine

- [x] Task 2.2: Implement `SetLeverageAsync` in `LiveExecutionEngine`
  - Details: .agent-context/3-develop/build/plans/details/20260411-auto-leverage-isolated-margin-phase-02-details.md#task-22-implement-in-liveexecutionengine

- [x] Task 2.3: Implement `SetLeverageAsync` in `HyperliquidExecutionEngine` (API layer)
  - Details: .agent-context/3-develop/build/plans/details/20260411-auto-leverage-isolated-margin-phase-02-details.md#task-23-implement-in-hyperliquidexecutionengine

- [x] Task 2.4: Implement `SetLeverageAsync` in `SimulatedExecutionEngine` (backtest)
  - Details: .agent-context/3-develop/build/plans/details/20260411-auto-leverage-isolated-margin-phase-02-details.md#task-24-implement-in-simulatedexecutionengine

- [x] Task 2.5: Wire `HandleSetLeverageAsync` in `AgentCheckInService`
  - Details: .agent-context/3-develop/build/plans/details/20260411-auto-leverage-isolated-margin-phase-02-details.md#task-25-wire-handlesetleverageasync

- [x] Task 2.6: Unit tests for execution engine implementations
  - Details: .agent-context/3-develop/build/plans/details/20260411-auto-leverage-isolated-margin-phase-02-details.md#task-26-unit-tests

- [x] Task 2.7: Build solution and run architecture tests
  - Details: .agent-context/3-develop/build/plans/details/20260411-auto-leverage-isolated-margin-phase-02-details.md#task-27-build-and-architecture-tests

### [x] Phase 3: Grid Pipeline Integration

**Complexity**: Medium-High | **Risk**: Medium

- [x] Task 3.1: Add `MaxLeverage` to `MarketContext` and populate from exchange
  - Details: .agent-context/3-develop/build/plans/details/20260411-auto-leverage-isolated-margin-phase-03-details.md#task-31-add-maxleverage-to-marketcontext

- [x] Task 3.2: Compute leverage in `GridController` and include in DeployGrid signal
  - Details: .agent-context/3-develop/build/plans/details/20260411-auto-leverage-isolated-margin-phase-03-details.md#task-32-compute-leverage-in-gridcontroller

- [x] Task 3.3: Call `SetLeverageAsync` in `LivePositionManager.DeployGridAsync`
  - Details: .agent-context/3-develop/build/plans/details/20260411-auto-leverage-isolated-margin-phase-03-details.md#task-33-call-setleverageasync-in-livepositionmanager

- [x] Task 3.4: Update `IHyperliquidOrderService.UpdateLeverageAsync` default parameter
  - Details: .agent-context/3-develop/build/plans/details/20260411-auto-leverage-isolated-margin-phase-03-details.md#task-34-update-updateLeverageasync-default

- [x] Task 3.5: Unit and integration tests for pipeline wiring
  - Details: .agent-context/3-develop/build/plans/details/20260411-auto-leverage-isolated-margin-phase-03-details.md#task-35-unit-and-integration-tests

- [x] Task 3.6: Build solution and run architecture tests
  - Details: .agent-context/3-develop/build/plans/details/20260411-auto-leverage-isolated-margin-phase-03-details.md#task-36-build-and-architecture-tests

### [x] Phase 4: Backtest Liquidation Simulation

**Complexity**: Medium | **Risk**: Medium

- [x] Task 4.1: Track leverage and compute margin in `SimulatedExecutionEngine`
  - Details: .agent-context/3-develop/build/plans/details/20260411-auto-leverage-isolated-margin-phase-04-details.md#task-41-track-leverage-and-margin

- [x] Task 4.2: Compute liquidation price and simulate force-close
  - Details: .agent-context/3-develop/build/plans/details/20260411-auto-leverage-isolated-margin-phase-04-details.md#task-42-compute-liquidation-and-force-close

- [x] Task 4.3: Unit tests for backtest liquidation simulation
  - Details: .agent-context/3-develop/build/plans/details/20260411-auto-leverage-isolated-margin-phase-04-details.md#task-43-unit-tests

- [x] Task 4.4: Build solution and run architecture tests
  - Details: .agent-context/3-develop/build/plans/details/20260411-auto-leverage-isolated-margin-phase-04-details.md#task-44-build-and-architecture-tests

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: Domain Model, Leverage Calculator & Defaults | Low-Medium | Low |
| Phase 2: Execution Engine SetLeverage | Medium | Medium |
| Phase 3: Grid Pipeline Integration | Medium-High | Medium |
| Phase 4: Backtest Liquidation Simulation | Medium | Medium |
| **Total** | **Medium** | **Medium** |

### Scoping Notes

- `RiskConfig` is a JSON-serialized record, not a DB entity — adding `AutoLeverage` requires no database migration
- `HyperliquidAssetMetadataCache` already fetches `MaxLeverage` from the `meta` API — no new exchange API calls needed
- `LiveExecutionEngine.ResolveAssetIndexAsync` already calls `meta` — extending to cache `maxLeverage` is minimal
- `SetLeveragePayload.IsCross` default flip from `true` to `false` is a **breaking change** for the manual `SetLeverage` command path (dashboard → agent) — existing callers currently pass `isCross` explicitly via the API, so impact is limited
- Backtest liquidation simulation is additive — no changes to existing backtest PnL/fee calculations
- Frontend is explicitly out of scope (separate PBI: P1 Risk Management UI)

## Dependencies

- P1 R-Based Position Sizing must be implemented (confirmed: `PositionSizeResolver.CalculateRiskBased` already works)
- `HyperliquidAssetMetadataCache` with `MaxLeverage` per asset (confirmed: already exists)

## Success Criteria

- All acceptance criteria from PBI are met
- Auto-leverage formula produces correct values for various SL%/maintenance margin rate combinations
- `SetLeverage(leverage, isolated)` is called on the exchange before every trade entry
- Isolated margin is always enforced for RiskBased mode
- Backtest simulates liquidation as fallback beyond stop-loss
- Existing tests pass when `AutoLeverage = false`
- Solution builds clean, all new and existing tests pass

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-04-11T22:19:57Z | 2026-04-11T22:35:00Z |
| Plan Reviewer | plan-reviewed | 2026-04-11T22:38:03Z | 2026-04-11T22:44:32Z |
| 3-Develop: 2 Implementer | implemented | 2026-04-11T22:49:33Z | 2026-04-12T07:13:42Z |
| 3-Develop: 3 Reviewer | complete | 2026-04-12T12:00:00Z | 2026-04-12T12:30:00Z |
