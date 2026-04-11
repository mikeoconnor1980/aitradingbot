# Optimizer Risk Parameter Sweep

**PBI ID:** Draft
**Status:** Draft
**Priority:** P1
**Iteration:** Backlog
**Created:** 2026-04-10T00:00:00Z
**Last Updated:** 2026-04-10T23:51:09Z
**Knowledge Source:** `33-risk-management-and-trade-sizing.md`
**Depends On:** P1 R-Based Position Sizing, P1 Auto-Leverage & Isolated Margin

## Summary

Extend the strategy optimizer to sweep `riskPerTradePercent` when the user selects `RiskBased` sizing mode. The optimizer currently only generates `PercentWallet` candidates — this PBI adds `RiskBased` as a discrete mode the user can select, with its own parameter set. Signal-only (the optimizer does not generate grid strategies).

### User Story

> As a **trader**, I want **the optimizer to test different risk percentages per trade** so that **I find the risk level that maximizes risk-adjusted returns for my strategy**.

### Problem Statement

The current optimizer sweeps `PositionSizeOptions` (percent of wallet) and `LeverageMin`/`LeverageMax` independently. With `RiskBased` sizing, position size and leverage are both derived from `riskPerTradePercent` and stop-loss distance, so the existing sweep dimensions don't apply. The optimizer needs a new parameter axis for risk percentage, and the leverage sweep must be conditional on whether auto-leverage is enabled.

### Business Value

- Discovers optimal R per trade for the strategy
- Tests different risk/SL combinations that produce different position sizes and leverage
- When auto-leverage is enabled, eliminates leverage as an independent sweep dimension (reducing search space)

---

## Requirements

### Functional Requirements

#### ParameterBounds Additions

- [ ] Add `PositionSizeMode` enum field to `ParameterBounds` (values: `PercentWallet`, `RiskBased`; default: `PercentWallet`)
- [ ] Add `RiskPerTradePercentOptions` array to `ParameterBounds` (default: `[0.25, 0.5, 1.0, 1.5, 2.0, 3.0]`)
- [ ] Add `IncludeAutoLeverage` boolean to `ParameterBounds` (default: `true`)
- [ ] When `PositionSizeMode = PercentWallet`: existing behaviour — sweep `PositionSizeOptions` and `LeverageMin`/`LeverageMax`
- [ ] When `PositionSizeMode = RiskBased`: sweep `RiskPerTradePercentOptions` instead of `PositionSizeOptions`

#### StrategyConfigGenerator Changes

- [ ] `GenerateRiskConfig` checks `bounds.PositionSizeMode` to determine which fields to populate
- [ ] When `RiskBased`:
  - Set `PositionSizeType = RiskBased`
  - Pick `RiskPerTradePercent` from `RiskPerTradePercentOptions`
  - Set `AutoLeverage` randomly between `true`/`false` (sweep both variants)
  - When `AutoLeverage = true`: skip leverage sweep (leverage is derived from SL distance)
  - When `AutoLeverage = false`: sweep leverage from `LeverageMin`/`LeverageMax` as before
- [ ] When `PercentWallet`: existing behaviour unchanged
- [ ] Every `RiskBased` candidate must have a stop-loss in `ExitConfig` (guaranteed by existing exit generation)

#### Fitness Function

- [ ] No changes needed — existing fitness function evaluates total PnL, max drawdown, and win rate

### Non-Functional Requirements

- [ ] Unit tests for `StrategyConfigGenerator` generating `RiskBased` configs with correct fields
- [ ] Unit test: `RiskBased` + `AutoLeverage = true` candidates do not have independently swept leverage
- [ ] Unit test: `RiskBased` + `AutoLeverage = false` candidates DO sweep leverage
- [ ] Existing `PercentWallet` optimization tests continue to pass unchanged
- [ ] Validation: `RiskPerTradePercentOptions` must contain at least one value when `RiskBased` mode is selected

---

## Acceptance Criteria

- [ ] **Given** `PositionSizeMode = RiskBased` and `RiskPerTradePercentOptions = [0.5, 1.0, 1.5, 2.0]`, **When** the optimizer generates candidates, **Then** all candidates use `PositionSizeType = RiskBased` with `RiskPerTradePercent` drawn from the options
- [ ] **Given** `PositionSizeMode = PercentWallet`, **When** the optimizer generates candidates, **Then** all candidates use `PercentWallet` with `PositionSizeValue` from `PositionSizeOptions` (existing behaviour)
- [ ] **Given** a `RiskBased` candidate with `AutoLeverage = true`, **When** the config is generated, **Then** leverage is NOT independently swept (derived from SL distance at runtime)
- [ ] **Given** a `RiskBased` candidate with `AutoLeverage = false`, **When** the config is generated, **Then** leverage IS swept from `LeverageMin`/`LeverageMax`
- [ ] **Given** `PositionSizeMode = RiskBased` and `IncludeAutoLeverage = true`, **When** candidates are generated, **Then** some have `AutoLeverage = true` and some have `AutoLeverage = false`
- [ ] **Given** `IncludeAutoLeverage = false`, **When** candidates are generated, **Then** all have `AutoLeverage = false` and leverage is swept normally

### Release Notes Information

- **Heading**: Optimizer — Risk-Based Sizing Sweep
- **Release note type**: Feature
- **Release Note Summary**: The strategy optimizer can now sweep risk-per-trade percentages when using Risk-Based sizing mode, discovering the optimal risk level alongside SL/TP and entry parameters.
- **Release Notes Audience**: Product
- **Breaking Change**: No

## Out of Scope

- Grid strategy optimization (optimizer currently signal-only)
- Kelly criterion display in results (separate PBI: P3 Kelly Criterion)
- Frontend optimizer UI changes for RiskBased mode selection (can be added later)
