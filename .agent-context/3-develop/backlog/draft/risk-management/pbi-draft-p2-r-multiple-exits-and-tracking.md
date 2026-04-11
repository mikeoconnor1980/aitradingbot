# R-Multiple Exit Types & Trade Tracking

**PBI ID:** Draft
**Status:** Draft
**Priority:** P2
**Iteration:** Backlog
**Created:** 2026-04-10T00:00:00Z
**Knowledge Source:** `33-risk-management-and-trade-sizing.md`
**Depends On:** R-Based Position Sizing

## Summary

Express take-profit targets as multiples of R (1R, 2R, 3R) instead of arbitrary percentages, and record R-multiple metrics for every closed trade to enable expectancy analysis.

### User Story

> As a **trader**, I want to **set take-profit targets as multiples of my risk (R)** so that **I maintain a consistent reward-to-risk ratio and can track my system's expectancy over time**.

### Business Value

- R-multiple targets enforce minimum reward-to-risk discipline (e.g., require 2R minimum)
- R-multiple trade tracking enables expectancy, profit factor, and SQN metrics
- Foundation for evaluating whether a strategy has a statistical edge

---

## Requirements

### R-Multiple Exit Types

- [ ] Add `RMultiple` exit type to `ExitConfig` / `ExitRuleConfig`
- [ ] When configured, TP distance = `rMultipleTarget × stopLossDistance`
  - Example: 2R target with 2% SL → TP at 4% above entry
- [ ] `TriggerOrderManager` supports R-multiple TP mode
- [ ] Minimum R-multiple threshold validation (warn if < 1R, block if < 0)

### R-Multiple Trade Tracking

Every closed trade records:

| Field | Description |
|-------|-------------|
| `InitialR` | Dollar risk at trade entry |
| `RMultipleResult` | `PnL / InitialR` — the realized R-multiple |
| `MaxFavourable` | Maximum favourable excursion in R |
| `MaxAdverse` | Maximum adverse excursion in R |

### Aggregate Metrics (Backtest & Live)

| Metric | Formula |
|--------|---------|
| Expectancy | `mean(RMultipleResult)` across all trades |
| Win Rate | Trades with RMultiple > 0 / total |
| Avg Winner | Mean R-multiple of winning trades |
| Avg Loser | Mean R-multiple of losing trades (should be ≈ -1) |
| Profit Factor | Sum of positive R / abs(sum of negative R) |
| System Quality Number | `(Expectancy / StdDev(R-multiples)) × sqrt(N)` |

### Non-Functional Requirements

- [ ] Unit tests for R-multiple TP price calculation
- [ ] Unit tests for aggregate R-multiple metrics
- [ ] Backtest summary includes R-multiple distribution histogram data

---

## Affected Components

| Component | File(s) | Change |
|-----------|---------|--------|
| ExitConfig / ExitRuleConfig | `ExitConfig.cs`, `ExitRuleConfig.cs` | Add R-multiple exit type |
| TriggerOrderManager | `TriggerOrderManager.cs` | Support R-multiple TP calculation |
| Trade result entities | Domain entities | Add `InitialR`, `RMultipleResult`, `MaxFavourable`, `MaxAdverse` |
| Backtest summary | Backtest DTOs | R-multiple distribution, expectancy, SQN |
| Performance display | Frontend | Show R-multiple metrics |

---

## Acceptance Criteria

```gherkin
Given a trade with R = $100 and R-multiple TP target = 2R
And stop-loss at 2% below entry
When the TP is placed
Then the TP is set at 4% above entry ($200 profit target)

Given 10 closed trades with R-multiples: [2.1, -1.0, 1.5, -1.0, 3.0, -0.8, 2.0, -1.0, 1.8, -1.0]
When aggregate metrics are calculated
Then expectancy = 0.56R
And win rate = 50%
And profit factor = 10.4 / 4.8 ≈ 2.17
```
