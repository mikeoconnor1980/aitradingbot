# Adaptive Risk (Drawdown-Adjusted)

**PBI ID:** Draft
**Status:** Draft
**Priority:** P3
**Iteration:** Backlog
**Created:** 2026-04-10T00:00:00Z
**Last Updated:** 2026-04-11T00:40:59Z
**Knowledge Source:** `33-risk-management-and-trade-sizing.md`
**Depends On:** PBI #1 (R-Based Position Sizing)

## User Story

As a **trader**, I want **the system to automatically reduce my risk percentage during drawdowns** so that **losses are contained beyond the natural anti-martingale effect, and trading halts before catastrophic loss**.

## Problem Statement

R-based sizing already shrinks position sizes as equity declines (natural anti-martingale). However, during sustained losing streaks, additional protection is needed: explicitly scaling down the risk percentage in tiers and ultimately halting all new entries when drawdown becomes severe. This provides a second layer of defence that compounds with the anti-martingale effect.

---

## Requirements

### Functional Requirements

#### High-Water Mark (HWM) Tracking
- [ ] Track per-strategy HWM from **realised equity** (same source as R calculation from PBI #1)
- [ ] HWM auto-updates whenever current equity exceeds the stored HWM (standard ratchet)
- [ ] HWM is **persisted to the database per strategy** and survives application restarts
- [ ] Drawdown percentage = `(HWM - currentEquity) / HWM * 100`

#### Drawdown Tier Configuration
- [ ] User-configurable tiers via `RiskLimitsConfig` — each tier defines a drawdown threshold (%) and a risk scaling factor (0.0–1.0)
- [ ] Default tiers (sensible out-of-the-box):

  | Drawdown Range | Risk Scaling Factor | Example (1% base) |
  |----------------|--------------------|--------------------|
  | 0–5%           | 1.0 (full risk)    | 1.0%               |
  | 5–10%          | 0.75               | 0.75%              |
  | 10–15%         | 0.50               | 0.50%              |
  | 15%+           | 0.0 (halt)         | Circuit breaker     |

- [ ] Tiers are stored as a list of `DrawdownTier` objects: `{ ThresholdPercent, ScalingFactor }` with `ScalingFactor = 0.0` meaning halt
- [ ] Validation: tiers must be in ascending threshold order, scaling factors in descending order, at least one tier required

#### Drawdown Circuit Breaker
- [ ] When the active tier has `ScalingFactor = 0.0`, all new entry signals are blocked (risk-reducing signals still pass)
- [ ] This is a **separate circuit breaker** from the existing daily-loss CB — both can be tripped independently
- [ ] **Auto-resets** when equity recovers above the halt threshold (drawdown naturally decreases)
- [ ] CB state is logged at CRITICAL level when tripped and WARNING when auto-reset

#### Risk Scaling Application
- [ ] Applies as an **overlay** on top of the base risk calculation — after `PositionSizeResolver` computes the R-based notional, multiply by the active tier's scaling factor
- [ ] The adjusted risk flows through the same `PositionSizeResolver` pipeline (no separate code path)

#### Backtest Support
- [ ] Backtest engine enforces adaptive risk and drawdown CB halt identically to live trading
- [ ] Uses the same drawdown tier logic — no special backtest bypass
- [ ] Backtest results should reflect trades skipped due to drawdown CB

#### Dashboard Display (Minimal)
- [ ] Show current drawdown % from HWM on the existing strategy dashboard
- [ ] Show the active drawdown tier name/label or scaling factor
- [ ] Show drawdown CB status (active/inactive)

### Non-Functional Requirements

- [ ] Unit tests for each drawdown tier boundary (at thresholds, between tiers)
- [ ] Unit test for CB activation and auto-reset when equity recovers
- [ ] Unit test for independent operation of daily-loss CB and drawdown CB
- [ ] Unit test for HWM persistence and recovery after restart
- [ ] Integration test: backtest run with adaptive risk produces different results than without

---

## Acceptance Criteria

- [ ] **Given** configured risk = 1% and account is in 7% drawdown from HWM, **When** a new trade signal is generated, **Then** the effective risk used for sizing is 0.75% (scaling factor 0.75)
- [ ] **Given** configured risk = 1% and account is in 12% drawdown from HWM, **When** a new trade signal is generated, **Then** the effective risk used for sizing is 0.50% (scaling factor 0.50)
- [ ] **Given** account is in 16% drawdown (above halt threshold), **When** a new entry signal is generated, **Then** the signal is blocked and dashboard shows drawdown CB active
- [ ] **Given** drawdown CB is active and equity recovers to 14% drawdown (below halt threshold), **When** the next equity refresh occurs, **Then** the drawdown CB auto-resets and trading resumes at the 0.50 scaling tier
- [ ] **Given** daily-loss CB is tripped but drawdown is only 3%, **When** a signal is generated, **Then** the signal is blocked by the daily-loss CB (drawdown CB is not active — they operate independently)
- [ ] **Given** a strategy's HWM is $10,000 and current equity is $10,500, **When** equity is refreshed, **Then** HWM updates to $10,500
- [ ] **Given** the application restarts, **When** the strategy resumes, **Then** the persisted HWM is loaded from the database and drawdown calculation continues correctly
- [ ] **Given** a backtest runs with adaptive risk enabled and the equity curve drops into the halt tier, **When** signals are generated during the halt period, **Then** those signals are skipped in the backtest results
- [ ] **Given** the user configures custom drawdown tiers, **When** tiers are not in ascending threshold order, **Then** a validation error is returned

### Release Notes Information

- **Heading**: Adaptive Risk — Automatic Drawdown-Based Risk Scaling
- **Release note type**: Feature
- **Release Note Summary**: Strategies now automatically scale down risk during drawdowns and halt new entries at a configurable severe-drawdown threshold. Compounds with R-based anti-martingale sizing for additional loss protection.
- **Release Notes Audience**: Product
- **Breaking Change**: No

## Technical Considerations

### Current State
- `LiveRiskEngine` already has a daily-loss circuit breaker (`MaxDailyLossUsd`) with cooldown-based auto-reset — drawdown CB is a new, independent breaker
- `IRiskEngine` interface has `ValidateAsync`, `RecordLoss`, `RecordOrdersPlaced`, `RecordOrdersClosed` — may need new method/property for drawdown state
- `PassThroughRiskEngine` is used in backtests — will need to be replaced or extended to enforce drawdown scaling
- `PositionSizeResolver` is a static class — the scaling factor must be passed in or applied as a multiplier on the output

### Integration Points
- Drawdown tier config lives in `RiskLimitsConfig` alongside existing limits
- HWM needs a new database column or table per strategy
- The drawdown scaling factor is applied at the same point where `PositionSizeResolver.ResolveNotional` is called (in `GridController` / `SignalController`)
- Dashboard display can reuse existing SignalR hub for real-time drawdown state

## Out of Scope

- Account-wide (cross-strategy) drawdown tracking — this PBI is per-strategy only
- Drawdown equity chart/visualisation — deferred to a future PBI
- Manual HWM reset by user — HWM only ratchets upward automatically
- Configuring different tiers per strategy — all strategies share the same tier config from `RiskLimitsConfig`
