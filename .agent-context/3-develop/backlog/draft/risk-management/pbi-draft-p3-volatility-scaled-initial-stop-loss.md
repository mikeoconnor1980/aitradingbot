# Volatility-Scaled Initial Stop Loss

**PBI ID:** Draft
**Status:** Draft
**Priority:** P3
**Iteration:** Backlog
**Created:** 2026-04-10T00:00:00Z
**Knowledge Source:** `33-risk-management-and-trade-sizing.md`
**Depends On:** R-Based Position Sizing

## Summary

Use ATR (Average True Range) to set the initial stop-loss distance for position sizing, instead of a fixed percentage. This naturally sizes positions smaller in volatile markets and larger in calm markets, keeping dollar risk constant at R.

### User Story

> As a **trader**, I want **my stop-loss distance to adapt to current market volatility** so that **I'm not stopped out prematurely in volatile conditions and my position sizes adjust automatically to maintain consistent dollar risk**.

### Business Value

- Tight fixed SL in volatile markets causes excessive stop-outs
- Wide fixed SL in calm markets wastes capital (position too small)
- ATR-based SL adapts to the current regime while keeping R constant
- Extends the existing ATR trailing stop capability to initial sizing

---

## Requirements

### Functional Requirements

- [ ] Add `AtrBased` stop-loss type for initial SL distance (not just trailing)
- [ ] Configuration:
  ```
  StopLoss:
    Type: AtrBased
    AtrPeriod: 14
    AtrMultiplier: 2.0
  ```
- [ ] SL distance = `ATR(period) × multiplier`
- [ ] Position size = `R / stopLossDistance` (volatile market → wider SL → smaller position)
- [ ] ATR is recalculated at each candle close (same as existing trailing stop)
- [ ] Works with both `RiskBased` mode (R-driven sizing) and existing modes (informational)

### Non-Functional Requirements

- [ ] Unit tests for ATR-based SL distance calculation
- [ ] Unit tests showing position size varies inversely with volatility
- [ ] Backtest comparison: fixed SL vs ATR-based SL on same strategy

---

## Affected Components

| Component | File(s) | Change |
|-----------|---------|--------|
| ExitConfig | `ExitConfig.cs` | Add `AtrBased` SL type for initial distance |
| SL distance resolvers | GridController, SignalController | Resolve ATR-based SL distance |
| PositionSizeResolver | `PositionSizeResolver.cs` | Accepts ATR-derived SL distance (no change needed if SL resolution is upstream) |
| ATR calculation | Existing `AtrCalculator` | Reuse for initial SL (already exists for trailing) |

---

## Acceptance Criteria

```gherkin
Given ATR(14) = $500 on BTC at $50,000 and multiplier = 2.0
And R = $100 (1% of $10,000)
When stop-loss distance is calculated
Then SL distance = $1,000 (2% of entry)
And position size = $100 / ($1,000 / $50,000) = $5,000

Given ATR(14) increases to $1,000 (high volatility)
When stop-loss distance is recalculated
Then SL distance = $2,000 (4% of entry)
And position size = $100 / ($2,000 / $50,000) = $2,500
```
