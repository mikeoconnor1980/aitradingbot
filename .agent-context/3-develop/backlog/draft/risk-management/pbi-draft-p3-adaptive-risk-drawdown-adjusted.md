# Adaptive Risk (Drawdown-Adjusted)

**PBI ID:** Draft
**Status:** Draft
**Priority:** P3
**Iteration:** Backlog
**Created:** 2026-04-10T00:00:00Z
**Knowledge Source:** `33-risk-management-and-trade-sizing.md`
**Depends On:** R-Based Position Sizing

## Summary

Dynamically reduce the risk percentage when the account is in a drawdown, providing an additional layer of protection on top of the natural anti-martingale effect. Includes a circuit breaker that halts trading at a severe drawdown threshold.

### User Story

> As a **trader**, I want **the system to automatically reduce my risk percentage during losing streaks** so that **drawdowns are limited even beyond the natural anti-martingale effect, and trading halts before catastrophic loss**.

### Business Value

- Additional protection layer during sustained losing periods
- Compounds with anti-martingale effect for stronger drawdown containment
- Circuit breaker prevents emotional overtrading during bad periods

---

## Requirements

### Functional Requirements

- [ ] Track account high-water mark and current drawdown percentage
- [ ] Apply drawdown-based risk scaling:

  | Account Drawdown | Risk Adjustment |
  |------------------|-----------------|
  | 0–5% | Full risk (e.g., 1%) |
  | 5–10% | 75% of configured risk |
  | 10–15% | 50% of configured risk |
  | 15%+ | Circuit breaker — halt all new entries |

- [ ] Configuration: drawdown tiers and scaling factors in `RiskLimitsConfig`
- [ ] Optional: configurable drawdown tiers (not hardcoded)
- [ ] Circuit breaker halt is logged and displayed in dashboard
- [ ] Circuit breaker can be manually reset by the user
- [ ] Applies as an overlay on top of the base R calculation

### Non-Functional Requirements

- [ ] Unit tests for each drawdown tier
- [ ] Unit test for circuit breaker activation and manual reset
- [ ] Backtest support for adaptive risk to evaluate impact on drawdown curves

---

## Affected Components

| Component | File(s) | Change |
|-----------|---------|--------|
| RiskLimitsConfig | `RiskLimitsConfig.cs` | Add drawdown tier configuration |
| LiveRiskEngine | `LiveRiskEngine.cs` | Track HWM, apply drawdown scaling |
| BacktestRiskEngine | If applicable | Same logic for backtest |
| PositionSizeResolver | `PositionSizeResolver.cs` | Accept adjusted risk% from engine |
| Dashboard | Frontend | Show drawdown status and circuit breaker state |

---

## Acceptance Criteria

```gherkin
Given configured risk = 1% and account is in 7% drawdown from high-water mark
When a new trade signal is generated
Then the effective risk used for sizing is 0.75% (75% of 1%)

Given account is in 16% drawdown
When a new trade signal is generated
Then the entry is blocked (circuit breaker active)
And the dashboard shows "Trading halted — drawdown circuit breaker active"
```
