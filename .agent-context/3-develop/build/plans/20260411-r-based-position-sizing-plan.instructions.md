---
applyTo: ".agent-context/3-develop/build/changes/20260411-r-based-position-sizing-changes.md"
currentAgent: "None"
agentStartedAt: "2026-04-12T06:40:18Z"
status: "complete"
lastUpdated: "2026-04-12T07:16:17Z"
---
<!-- markdownlint-disable-file -->

Implement the `RiskBased` position sizing mode where every trade's position size derives from R — the dollar amount risked per trade. The user specifies `riskPerTradePercent` and the system calculates position notional from R and the stop-loss distance.

## PBI Details

### User Story

> As a **trader**, I want to **specify what percentage of my account I'm willing to risk per trade** so that **the system automatically calculates the correct position size based on my stop-loss distance, giving me precise dollar risk control**.

### Problem Statement

The current sizing modes (`PercentWallet`, `FixedNotional`) control notional amount but do not directly control dollar risk. The actual R depends on where the stop-loss is placed, and the user must mentally calculate whether the notional + SL combination produces an acceptable loss size. With `RiskBased` mode, the user declares the acceptable risk and the system derives everything else.

### Business Value

- Enables professional-grade risk management (risk exactly X% per trade)
- Creates an anti-martingale effect: position sizes naturally shrink after losses and grow after wins
- Foundation for all other R-based features (auto-leverage, R-multiple exits, portfolio heat)

### Acceptance Criteria

- [ ] **Given** `PositionSizeType = RiskBased`, `riskPerTradePercent = 1.0`, account equity = $10,000, and `StopLoss` at 2% (fixed_percent), **When** the system calculates position size, **Then** R = $100 and positionNotional = $5,000
- [ ] **Given** `PositionSizeType = RiskBased`, `riskPerTradePercent = 1.0`, account equity = $10,000, and ATR-based SL resolving to 5%, **When** the system calculates position size, **Then** R = $100 and positionNotional = $2,000
- [ ] **Given** `PositionSizeType = RiskBased` on a grid strategy with 10 levels, **When** the grid is deployed, **Then** `notionalPerLevel = positionNotional / 10`
- [ ] **Given** `PositionSizeType = PercentWallet`, **When** the system calculates position size, **Then** existing behaviour is unchanged and `stopLossPercent` is not required
- [ ] **Given** `PositionSizeType = RiskBased` and no stop-loss configured, **When** the user saves the strategy, **Then** validation fails with an error message
- [ ] **Given** `PositionSizeType = RiskBased` and no stop-loss configured (runtime safety), **When** a trade entry signal is generated, **Then** the signal is blocked
- [ ] **Given** `riskPerTradePercent = 8`, **When** the user saves the strategy, **Then** a warning is shown (risk > 5%) but save is allowed
- [ ] **Given** a backtest with `RiskBased` mode starting at $10,000 equity, **When** the first trade loses $100, **Then** the next trade uses equity = $9,900 and R = $99

## Objectives

- Add `RiskBased` value to `PositionSizeType` enum
- Add `RiskPerTradePercent` property to `RiskConfig` record
- Extend `PositionSizeResolver.ResolveNotional` to compute R-based sizing from stop-loss distance
- Create `StopLossDistanceResolver` helper for strategy-agnostic SL% extraction
- Update `GridController` and `SignalController` to resolve SL distance and pass it to the resolver
- Add validation requiring SL configuration when `RiskBased` is selected
- Add API DTO field `riskPerTradePercent` to `RiskConfigRequest` with mapping
- Add runtime safety net to block entries when SL distance cannot be resolved
- Create comprehensive unit tests for all three sizing modes

### Discovery References

- `33-risk-management-and-trade-sizing.md` — R = equity × riskPerTradePercent / 100; positionNotional = R / (stopLossPercent / 100)
- `PositionSizeResolver` is `internal static class` — no DI registration needed, keep static
- `RiskConfig` is JSON-serialized inside `StrategyConfig.ConfigJson` — no database migration required for new fields
- Equity available via `MarketContext.AccountEquity` (live: Hyperliquid clearinghouse; backtest: SimulatedExecutionEngine running equity)
- ATR available at entry time via `context.Indicators.Atr` on `IndicatorSnapshot`
- `PositionSizeValue` remains used for `PercentWallet`/`FixedNotional`; for `RiskBased`, `RiskPerTradePercent` is the primary input
- Existing `GridController` treats resolver output as `notionalPerLevel`; for `RiskBased`, resolver returns total notional — controller must divide by `gridLevels`
- `BusinessRuleValidator.ValidateRisk()` validates risk fields; `CrossFieldValidator` validates cross-section rules — both need updating

### Project Patterns

- `src/TradingApp.Application/Trading/Services/PositionSizeResolver.cs` — static resolver, switch on PositionSizeType
- `src/TradingApp.Application/StrategyAuthoring/Models/RiskConfig.cs` — sealed record, JSON-serialized
- `src/TradingApp.Application/StrategyAuthoring/Models/PositionSizeType.cs` — enum {PercentWallet, FixedNotional}
- `src/TradingApp.Application/StrategyAuthoring/Models/ExitConfig.cs` — contains StopLoss as ExitRuleConfig
- `src/TradingApp.Application/StrategyAuthoring/Models/ExitRuleConfig.cs` — SL config with Type/Value/AtrMultiplier
- `src/TradingApp.Application/StrategyAuthoring/Models/ExitRuleType.cs` — {FixedPercent, SwingLow, AtrTrailing}
- `src/TradingApp.Application/StrategyAuthoring/Models/GridConfig.cs` — BreakdownThreshold for grid SL fallback
- `src/TradingApp.Application/Trading/Services/GridController.cs` — calls ResolveNotional, sets notionalPerLevel
- `src/TradingApp.Application/Trading/Services/SignalController.cs` — calls ResolveNotional in EmitOpenPosition
- `src/TradingApp.Application/Trading/Models/MarketContext.cs` — AccountEquity + Indicators.Atr
- `src/TradingApp.Application/StrategyAuthoring/Validation/BusinessRuleValidator.cs` — ValidateRisk field checks
- `src/TradingApp.Application/StrategyAuthoring/Validation/CrossFieldValidator.cs` — cross-section consistency
- `src/TradingApp.Api/Models/RunBacktestRequest.cs` — RiskConfigRequest DTO with Data Annotations
- `src/TradingApp.Api/Controllers/BacktestsController.cs` — MapStrategyConfig manual mapping
- `tests/TradingApp.Application.Tests/Trading/Services/GridControllerTests.cs` — grid sizing tests, Given/When/Then
- `tests/TradingApp.Application.Tests/StrategyAuthoring/Validation/BusinessRuleValidatorTests.cs` — validation tests
- `tests/TradingApp.Application.Tests/StrategyAuthoring/Validation/CrossFieldValidatorTests.cs` — cross-field tests

### [x] Phase 1: Domain Model & Core Calculation

**Complexity**: Medium | **Risk**: Low

- [x] Task 1.1: Add `RiskBased` enum value to `PositionSizeType`
  - Details: .agent-context/3-develop/build/plans/details/20260411-r-based-position-sizing-phase-01-details.md#task-11-add-riskbased-enum-value

- [x] Task 1.2: Add `RiskPerTradePercent` property to `RiskConfig` record
  - Details: .agent-context/3-develop/build/plans/details/20260411-r-based-position-sizing-phase-01-details.md#task-12-add-riskpertradepercent-to-riskconfig

- [x] Task 1.3: Extend `PositionSizeResolver.ResolveNotional` with optional `stopLossPercent` and `RiskBased` arm
  - Details: .agent-context/3-develop/build/plans/details/20260411-r-based-position-sizing-phase-01-details.md#task-13-extend-positionsizeresolver

- [x] Task 1.4: Create `PositionSizeResolverTests` with all three sizing modes
  - Details: .agent-context/3-develop/build/plans/details/20260411-r-based-position-sizing-phase-01-details.md#task-14-create-positionsizeresolvertests

- [x] Task 1.5: Build and run tests to verify backward compatibility
  - Details: .agent-context/3-develop/build/plans/details/20260411-r-based-position-sizing-phase-01-details.md#task-15-build-and-run-tests

### [x] Phase 2: SL Distance Resolution & Controller Integration

**Complexity**: Medium | **Risk**: Medium

- [x] Task 2.1: Create `StopLossDistanceResolver` static helper
  - Details: .agent-context/3-develop/build/plans/details/20260411-r-based-position-sizing-phase-02-details.md#task-21-create-stoploss-distance-resolver

- [x] Task 2.2: Update `GridController` to resolve SL%, pass to resolver, divide by grid levels
  - Details: .agent-context/3-develop/build/plans/details/20260411-r-based-position-sizing-phase-02-details.md#task-22-update-gridcontroller

- [x] Task 2.3: Update `SignalController` to resolve SL%, pass to resolver
  - Details: .agent-context/3-develop/build/plans/details/20260411-r-based-position-sizing-phase-02-details.md#task-23-update-signalcontroller

- [x] Task 2.4: Add runtime safety net — block entry when `RiskBased` and SL% unresolvable
  - Details: .agent-context/3-develop/build/plans/details/20260411-r-based-position-sizing-phase-02-details.md#task-24-add-runtime-safety-net

- [x] Task 2.5: Create `StopLossDistanceResolverTests`
  - Details: .agent-context/3-develop/build/plans/details/20260411-r-based-position-sizing-phase-02-details.md#task-25-create-stoploss-distance-resolver-tests

- [x] Task 2.6: Add/update `GridControllerTests` and `SignalControllerTests` for `RiskBased` mode
  - Details: .agent-context/3-develop/build/plans/details/20260411-r-based-position-sizing-phase-02-details.md#task-26-update-controller-tests

- [x] Task 2.7: Build and run all tests
  - Details: .agent-context/3-develop/build/plans/details/20260411-r-based-position-sizing-phase-02-details.md#task-27-build-and-run-all-tests

### [x] Phase 3: Validation, API DTO & Backtest Verification

**Complexity**: Medium | **Risk**: Low

- [x] Task 3.1: Add `RiskPerTradePercent` validation in `BusinessRuleValidator`
  - Details: .agent-context/3-develop/build/plans/details/20260411-r-based-position-sizing-phase-03-details.md#task-31-add-riskpertradepercent-validation

- [x] Task 3.2: Add `RiskBased` requires SL cross-field validation in `CrossFieldValidator`
  - Details: .agent-context/3-develop/build/plans/details/20260411-r-based-position-sizing-phase-03-details.md#task-32-add-riskbased-sl-cross-field-validation

- [x] Task 3.3: Add `RiskPerTradePercent` to `RiskConfigRequest` DTO and update `BacktestsController.MapStrategyConfig`
  - Details: .agent-context/3-develop/build/plans/details/20260411-r-based-position-sizing-phase-03-details.md#task-33-update-api-dto-and-mapping

- [x] Task 3.4: Add validation and API mapping tests
  - Details: .agent-context/3-develop/build/plans/details/20260411-r-based-position-sizing-phase-03-details.md#task-34-add-validation-and-mapping-tests

- [x] Task 3.5: Add backtest anti-martingale sizing test
  - Details: .agent-context/3-develop/build/plans/details/20260411-r-based-position-sizing-phase-03-details.md#task-35-add-backtest-anti-martingale-test

- [x] Task 3.6: Build and run full test suite
  - Details: .agent-context/3-develop/build/plans/details/20260411-r-based-position-sizing-phase-03-details.md#task-36-build-and-run-full-test-suite

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: Domain Model & Core Calculation | Medium | Low |
| Phase 2: SL Distance Resolution & Controller Integration | Medium | Medium |
| Phase 3: Validation, API DTO & Backtest Verification | Medium | Low |
| **Total** | **Medium** | **Low** |

### Scoping Notes

- No database migration required — `RiskConfig` is JSON-serialized inside `ConfigJson`; new fields deserialize with defaults from old data
- `PositionSizeResolver` remains `internal static` — no DI changes needed
- Frontend is explicitly out of scope (separate PBI: P1 Risk Management UI)
- Auto-leverage calculation is out of scope (separate PBI: P1 Auto-Leverage & Isolated Margin)
- `PositionSizeValue` remains for `PercentWallet`/`FixedNotional`; for `RiskBased`, `RiskPerTradePercent` is the controlling input
- Existing modes treat resolver output as per-level notional; `RiskBased` returns total notional — `GridController` divides by `gridLevels`, `SignalController` uses directly
- Equity at sizing time typically has no unrealised PnL (no open position at deploy/entry time); anti-martingale effect works via realised P&L updating equity between trades

## Dependencies

- No new NuGet packages needed
- No new infrastructure or cloud resources
- No deployment or pipeline changes
- Existing libraries: MSTest, FluentAssertions, Moq

## Success Criteria

- All three sizing modes (`PercentWallet`, `FixedNotional`, `RiskBased`) produce correct position notionals
- `RiskBased` mode computes R from equity × riskPerTradePercent, and derives notional from R / SL distance
- Grid mode divides total notional by grid levels for `RiskBased`
- SL distance resolution works for `FixedPercent`, `AtrTrailing`, and grid breakdown threshold
- Validation prevents saving a strategy with `RiskBased` and no stop-loss
- Warning emitted when `riskPerTradePercent > 5%`
- Runtime safety net blocks trade entry when SL distance cannot be resolved
- Backtest demonstrates anti-martingale: R shrinks after losses, grows after wins
- All existing tests pass unchanged

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|----------|
| Implementation Planner | planned | 2026-04-11T20:50:21Z | 2026-04-11T21:07:13Z |
| Plan Reviewer | plan-reviewed | 2026-04-11T21:08:20Z | 2026-04-11T21:15:34Z |
| Plan Implementer | implemented | 2026-04-11T21:34:26Z | 2026-04-12T06:37:08Z |
| Plan Reviewer | plan-reviewed | 2026-04-11T21:02:48Z | 2026-04-11T21:33:33Z |
| Implementation Reviewer | complete | 2026-04-12T06:40:18Z | 2026-04-12T07:16:17Z |
