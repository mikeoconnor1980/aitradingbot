# Partial Close at R-Levels

**PBI ID:** Draft
**Status:** Draft
**Priority:** P3
**Iteration:** Backlog
**Created:** 2026-04-10T00:00:00Z
**Knowledge Source:** `33-risk-management-and-trade-sizing.md`
**Depends On:** R-Multiple Exit Types & Trade Tracking

## Summary

Scale out of winning positions at R-multiple milestones, locking in profit in tranches while letting the remaining position run.

### User Story

> As a **trader**, I want to **automatically take partial profits at 1R and 2R milestones** so that **I lock in gains progressively while giving the remainder of the position room to capture larger moves**.

### Business Value

- Locks in profit early (1R partial makes the remaining position "risk-free")
- Balances profit-taking with trend-following
- Standard professional scaling-out technique

---

## Requirements

### Functional Requirements

- [ ] Configure partial-close tranches in `ExitConfig`:
  ```
  PartialCloses:
    - AtRMultiple: 1.0, ClosePercent: 25
    - AtRMultiple: 2.0, ClosePercent: 25
    - AtRMultiple: 3.0, ClosePercent: 50 (or trail remainder)
  ```
- [ ] Place partial TP trigger orders at each R-level after entry
- [ ] After first partial at 1R: move stop-loss to breakeven (optional, configurable)
- [ ] Track partial fills and adjust remaining position size
- [ ] Support trailing stop on the final tranche

### Non-Functional Requirements

- [ ] Unit tests for partial-close price calculations
- [ ] Backtest support for partial exits and R-multiple tracking per tranche

---

## Affected Components

| Component | File(s) | Change |
|-----------|---------|--------|
| ExitConfig | `ExitConfig.cs` | Add `PartialCloses` list |
| TriggerOrderManager | `TriggerOrderManager.cs` | Place multiple TP orders per position |
| LivePositionManager | `LivePositionManager.cs` | Handle partial fills, SL adjustment |
| BacktestPositionManager | `BacktestPositionManager.cs` | Simulate partial exits |

---

## Acceptance Criteria

```gherkin
Given a position of 100 units with R = $100
And partial close config: 25% at 1R, 25% at 2R, 50% trail at 3R+
When price reaches 1R profit
Then 25 units are closed, locking in $25 profit (0.25R)

When price reaches 2R profit
Then another 25 units are closed, locking in additional $50 profit (0.5R)

When the trailing stop fires at 3.5R for the remaining 50 units
Then the final 50 units are closed at 3.5R × 50% = 1.75R additional profit
And total trade result = 0.25R + 0.5R + 1.75R = 2.5R
```
