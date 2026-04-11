# Kelly Criterion & Advanced Backtest Metrics

**PBI ID:** Draft
**Status:** Draft
**Priority:** P3
**Iteration:** Backlog
**Created:** 2026-04-10T00:00:00Z
**Knowledge Source:** `33-risk-management-and-trade-sizing.md`
**Depends On:** R-Multiple Exit Types & Trade Tracking

## Summary

Display Kelly criterion suggestion in backtest results and add advanced R-based portfolio metrics. These are informational/advisory — the trader decides whether to adopt the Kelly-optimal risk percentage.

### User Story

> As a **trader**, I want to **see the Kelly-optimal risk percentage after backtesting** so that **I can compare my configured risk level against the mathematically optimal one and make an informed decision**.

### Business Value

- Bridges theory and practice — shows what the math says is optimal
- Half-Kelly is a widely used professional guideline for balancing growth and variance
- SQN (System Quality Number) gives a single score for strategy quality

---

## Requirements

### Functional Requirements

- [ ] Calculate and display Kelly% after backtest completion:
  ```
  Kelly% = W - (1 - W) / R_ratio
  ```
  Where W = win probability, R_ratio = average win / average loss
- [ ] Display half-Kelly (Kelly% / 2) as the recommended conservative target
- [ ] Compare configured `riskPerTradePercent` against Kelly suggestion
- [ ] Display System Quality Number (SQN):
  ```
  SQN = (Expectancy / StdDev(R-multiples)) × sqrt(N)
  ```
- [ ] All metrics are advisory — no automatic configuration changes
- [ ] Periodic live equity refresh for accurate R calculation during extended sessions

### Non-Functional Requirements

- [ ] Unit tests for Kelly% calculation with various win rates and R-ratios
- [ ] Unit tests for SQN calculation
- [ ] Clear UI labeling: "Advisory — not automatically applied"

---

## Affected Components

| Component | File(s) | Change |
|-----------|---------|--------|
| Backtest summary | Backtest DTOs | Add Kelly%, half-Kelly, SQN |
| Backtest results display | Frontend | Show advisory metrics section |
| StrategyScheduler | `StrategyScheduler.cs` | Periodic equity refresh (optional) |

---

## Acceptance Criteria

```gherkin
Given a backtest with 60% win rate and avg win/avg loss ratio of 2.0
When the backtest completes
Then Kelly% = 0.60 - (0.40 / 2.0) = 0.40 (40%)
And half-Kelly = 20%
And these are displayed as advisory metrics in the backtest summary

Given the configured riskPerTradePercent = 1%
When viewing backtest results
Then the system shows "Configured: 1% | Kelly suggests: 20% (half-Kelly: 10%)"
```
