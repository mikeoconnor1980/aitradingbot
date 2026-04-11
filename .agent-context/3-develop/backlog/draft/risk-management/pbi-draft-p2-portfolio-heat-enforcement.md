# Portfolio Heat Enforcement

**PBI ID:** Draft
**Status:** Draft
**Priority:** P2
**Iteration:** Backlog
**Created:** 2026-04-10T00:00:00Z
**Knowledge Source:** `33-risk-management-and-trade-sizing.md`
**Depends On:** R-Based Position Sizing

## Summary

Enforce a maximum portfolio-wide risk exposure (portfolio heat) to prevent catastrophic correlated drawdowns across simultaneous open positions.

### User Story

> As a **trader**, I want **the system to block new entries when my total open risk exceeds a configured threshold** so that **correlated positions can't cause a catastrophic account drawdown if they all fail simultaneously**.

### Business Value

- Prevents unbounded aggregate risk when running multiple simultaneous positions
- Protects against correlated drawdowns (e.g., all long crypto perps dropping together)
- Standard professional risk management practice

---

## Requirements

### Functional Requirements

- [ ] Add `MaxPortfolioHeatPercent` to `RiskLimitsConfig` (default: 6, 0 = disabled)
- [ ] Track total R-exposure across all open positions:
  ```
  PortfolioHeat = sum of R for each open position
  ```
- [ ] RiskEngine blocks new entries when `PortfolioHeat + newTradeR > equity × MaxPortfolioHeatPercent / 100`
- [ ] At 1% risk per trade, this allows 5–6 simultaneous positions before blocking
- [ ] Display current portfolio heat percentage in dashboard
- [ ] When a position closes, portfolio heat recalculates and may unblock new entries

### Non-Functional Requirements

- [ ] Unit tests for portfolio heat calculation and enforcement
- [ ] Test that closing a position reduces heat and re-enables entry

---

## Affected Components

| Component | File(s) | Change |
|-----------|---------|--------|
| RiskLimitsConfig | `RiskLimitsConfig.cs` | Add `MaxPortfolioHeatPercent` |
| LiveRiskEngine | `LiveRiskEngine.cs` | Track and enforce portfolio heat |
| BacktestRiskEngine | If applicable | Same enforcement in backtest |
| Dashboard | Frontend | Display portfolio heat gauge |

---

## Acceptance Criteria

```gherkin
Given MaxPortfolioHeatPercent = 6 and account equity = $10,000
And 5 open positions each risking 1% ($100 each = $500 total heat = 5%)
When a new entry signal arrives risking 1% ($100)
Then the entry is allowed (5% + 1% = 6% ≤ 6%)

Given the same scenario but 6 positions already open (heat = 6%)
When a new entry signal arrives risking 1%
Then the entry is blocked (6% + 1% = 7% > 6%)
```
