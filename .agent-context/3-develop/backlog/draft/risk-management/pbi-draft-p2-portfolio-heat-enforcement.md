# Portfolio Heat Enforcement

**PBI ID:** Draft
**Status:** Draft
**Priority:** P2
**Iteration:** Backlog
**Created:** 2026-04-10T00:00:00Z
**Last Updated:** 2026-04-11T00:17:24Z
**Knowledge Source:** `33-risk-management-and-trade-sizing.md`
**Depends On:** P1 R-Based Position Sizing

## Summary

Enforce a maximum portfolio-wide risk exposure (portfolio heat) to prevent catastrophic correlated drawdowns across simultaneous open positions. Heat is the sum of R across ALL open positions on the account, regardless of which strategy opened them. Includes a new API endpoint and dashboard display.

### User Story

> As a **trader**, I want **the system to block new entries when my total open risk exceeds a configured threshold** so that **correlated positions can't cause a catastrophic account drawdown if they all fail simultaneously**.

### Problem Statement

Currently, `maxOpenTrades` limits positions per strategy but does not cap the total dollar risk across the account. A trader running multiple strategies or one strategy with multiple open trades has no aggregate risk ceiling. If all positions are correlated (e.g., multiple long crypto perps), they can all fail simultaneously, causing a drawdown far larger than any single-trade risk percentage.

### Business Value

- Prevents unbounded aggregate risk when running multiple simultaneous positions
- Protects against correlated drawdowns (e.g., all long crypto perps dropping together)
- Standard professional risk management practice (typical cap: 5–6% of equity)

---

## Requirements

### Functional Requirements

#### Configuration

- [ ] Add `MaxPortfolioHeatPercent` to `RiskLimitsConfig` (default: 6, 0 = disabled)
- [ ] Configurable via `appsettings.json` under _RiskLimits_ section

#### Heat Calculation

- [ ] Portfolio heat = sum of R for each open position across all strategies on the account
- [ ] For `RiskBased` positions: R = `riskPerTradePercent / 100 × equity` (recorded at entry time)
- [ ] For `PercentWallet`/`FixedNotional` positions (no explicit R): estimate R from `positionNotional × (stopLossPercent / 100)` using the configured SL distance
- [ ] If a position has no configured SL: use the position's margin as a conservative proxy for R
- [ ] Heat recalculates when a position opens or closes

#### Enforcement

- [ ] `LiveRiskEngine` blocks new entries when `currentHeat + newTradeR > equity × MaxPortfolioHeatPercent / 100`
- [ ] Risk-reducing signals (TakeProfit, CancelGrid, FlattenPosition) always pass regardless of heat
- [ ] When a position closes, heat drops and may unblock new entries
- [ ] At 1% risk per trade with 6% cap, allows up to 6 simultaneous positions

#### Backtest Support

- [ ] Enforce portfolio heat in backtest risk engine (blocks entries when heat exceeds limit)
- [ ] Backtest results should report how many signals were blocked by heat limits

#### API Endpoint

- [ ] New `GET /api/risk/portfolio-heat` endpoint returning:
  - `heatPercent`: current portfolio heat as % of equity
  - `maxHeatPercent`: configured limit
  - `positions`: array of `{ symbol, strategyName, rDollars, rPercent }`
  - `equity`: current account equity used for calculation
- [ ] Endpoint reads from the same heat tracker used by the risk engine

#### Dashboard Display

- [ ] Display current portfolio heat percentage on the dashboard
- [ ] Visual indicator: green (< 50% of max), amber (50–80% of max), red (> 80% of max)
- [ ] Show breakdown by position (which positions contribute how much R)
- [ ] Update when positions open or close (poll or refresh on navigation)

### Non-Functional Requirements

- [ ] Unit tests for heat calculation with mixed `RiskBased` and `PercentWallet` positions
- [ ] Unit tests for enforcement: allowed at limit, blocked above limit
- [ ] Unit test: closing a position reduces heat and re-enables entry
- [ ] Unit tests for R estimation from non-RiskBased positions
- [ ] Integration test for the API endpoint

---

## Acceptance Criteria

- [ ] **Given** `MaxPortfolioHeatPercent = 6` and equity = $10,000, and 5 open `RiskBased` positions each with R = $100 (heat = 5%), **When** a new entry arrives with R = $100, **Then** the entry is allowed (5% + 1% = 6% ≤ 6%)
- [ ] **Given** the same scenario with 6 positions already open (heat = 6%), **When** a new entry arrives with R = $100, **Then** the entry is blocked (6% + 1% = 7% > 6%)
- [ ] **Given** a `PercentWallet` position with $2,000 notional and 3% SL, **When** heat is calculated, **Then** its estimated R = $2,000 × 0.03 = $60
- [ ] **Given** heat = 6% and one position closes, **When** a new entry arrives, **Then** the entry is allowed (heat dropped below limit)
- [ ] **Given** `MaxPortfolioHeatPercent = 0` (disabled), **When** any entry arrives, **Then** the heat check is skipped
- [ ] **Given** a TakeProfit signal, **When** heat is at the limit, **Then** the signal passes (risk-reducing)
- [ ] **Given** the dashboard loads, **When** there are 3 open positions with R = $100 each and equity = $10,000, **Then** the heat gauge shows 3% with a green indicator
- [ ] **Given** a backtest with heat enforcement enabled, **When** a 7th position would exceed the 6% limit, **Then** the entry is blocked and reported in results

### Release Notes Information

- **Heading**: Portfolio Heat — Aggregate Risk Limit
- **Release note type**: Feature
- **Release Note Summary**: A new portfolio heat limit caps total open risk across all positions. The dashboard shows current heat percentage with a breakdown by position, and the risk engine blocks new entries when the limit is reached.
- **Release Notes Audience**: Product
- **Breaking Change**: No

## Out of Scope

- Correlation analysis between positions (heat treats all positions independently)
- Per-strategy heat caps (heat is account-wide only)
- Adaptive risk / drawdown-adjusted risk (separate PBI: P3)
