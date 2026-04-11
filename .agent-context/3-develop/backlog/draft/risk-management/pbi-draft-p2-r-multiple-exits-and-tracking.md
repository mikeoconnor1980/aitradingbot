# R-Multiple Exit Types & Trade Tracking

**PBI ID:** Draft
**Status:** Draft
**Priority:** P2
**Iteration:** Backlog
**Created:** 2026-04-10T00:00:00Z
**Last Updated:** 2026-04-11T00:24:55Z
**Knowledge Source:** `33-risk-management-and-trade-sizing.md`
**Depends On:** P1 R-Based Position Sizing

## Summary

Express take-profit targets as multiples of R (1R, 2R, 3R) instead of arbitrary percentages, and record R-multiple metrics for every closed trade (when using `RiskBased` mode) to enable expectancy analysis. Includes MFE/MAE tracking per trade and aggregate metrics displayed in backtest results.

### User Story

> As a **trader**, I want to **set take-profit targets as multiples of my risk (R) and track R-multiple results for every trade** so that **I maintain a consistent reward-to-risk ratio and can evaluate my system's statistical edge over time**.

### Problem Statement

Take-profit is currently a fixed percentage or ATR-based distance, with no relationship to the dollar risk (R). A 2% TP with a 3% SL is a sub-1R trade — the trader must manually calculate whether the risk-reward makes sense. R-multiple targets enforce this discipline automatically. Additionally, there is no trade-level R tracking, so metrics like expectancy, profit factor, and SQN cannot be calculated.

### Business Value

- R-multiple targets enforce minimum reward-to-risk discipline (e.g., 2R minimum)
- R-multiple trade tracking enables expectancy, profit factor, and SQN metrics
- MFE/MAE data reveals trade management quality (did you exit too early? did you hold losers too long?)
- Foundation for evaluating whether a strategy has a statistical edge

---

## Requirements

### Functional Requirements

#### R-Multiple Exit Type (TP only)

- [ ] Add `RMultiple` value to `ExitRuleType` enum
- [ ] When `ExitRuleType = RMultiple`, the `Value` field on `ExitRuleConfig` represents the R-multiple target (e.g., `2.0` = 2R)
- [ ] TP price calculation: `tpPrice = entryPrice ± (stopLossDistance × rMultipleTarget)`
  - Example: 2R target with 2% SL → TP at 4% above entry (long) or below entry (short)
- [ ] `TriggerOrderManager.CalculateTakeProfitPrice` extended to handle `RMultiple` type
  - Requires stop-loss distance as additional input (read from `ExitConfig.StopLoss`)
- [ ] Validation: warn if R-multiple < 1 (sub-1R trade), block if < 0
- [ ] Stop-loss exit types remain unchanged (`FixedPercent`, `SwingLow`, `AtrTrailing`)

#### R-Multiple Trade Tracking

Only tracked when `PositionSizeType = RiskBased`:

- [ ] Record `InitialR` (dollar risk) at trade entry time on the trade result entity
- [ ] Record `RMultipleResult = PnL / InitialR` when the trade closes
- [ ] Track `MaxFavourableExcursion` (MFE) in R: highest unrealised profit in R during the trade
- [ ] Track `MaxAdverseExcursion` (MAE) in R: deepest unrealised loss in R during the trade
- [ ] MFE/MAE updated on each candle close during the trade's lifetime (backtest) or on each price update (live)
- [ ] Non-RiskBased trades: R-multiple fields are null (not tracked)

#### Aggregate Metrics (Backtest & Live)

| Metric | Formula |
|--------|---------|
| Expectancy | `mean(RMultipleResult)` across R-tracked trades |
| Win Rate | Trades with RMultiple > 0 / total R-tracked trades |
| Avg Winner | Mean R-multiple of winning trades |
| Avg Loser | Mean R-multiple of losing trades (should be ≈ -1) |
| Profit Factor | Sum of positive R / abs(sum of negative R) |
| SQN | `(Expectancy / StdDev(R-multiples)) × sqrt(N)` |

- [ ] Calculate aggregate metrics in backtest summary
- [ ] Include R-multiple distribution histogram data (e.g., buckets: <-1R, -1R to 0, 0 to 1R, 1R to 2R, 2R to 3R, >3R)

#### Frontend — Backtest Results

- [ ] Display R-multiple aggregate metrics section in backtest results:
  - Expectancy, Win Rate, Avg Winner, Avg Loser, Profit Factor, SQN
- [ ] Display R-multiple distribution as a histogram/bar chart
- [ ] Per-trade table includes `InitialR`, `RMultipleResult`, `MFE`, `MAE` columns (when available)
- [ ] Metrics section only shown when the backtest used `RiskBased` mode

### Non-Functional Requirements

- [ ] Unit tests for R-multiple TP price calculation (long and short, various R targets)
- [ ] Unit tests for `CalculateTakeProfitPrice` with `RMultiple` type needing SL distance
- [ ] Unit tests for aggregate R-multiple metric calculations
- [ ] Unit tests for MFE/MAE tracking during a trade lifecycle
- [ ] Backtest integration test: R-multiple exits fire at correct prices
- [ ] Existing `FixedPercent` and `AtrTrailing` TP tests unaffected

---

## Acceptance Criteria

- [ ] **Given** a long trade with R = $100, entry at $50,000, SL at 2% ($49,000), and R-multiple TP = 2R, **When** the TP trigger is placed, **Then** TP price = $52,000 (4% above entry)
- [ ] **Given** a short trade with R = $100, entry at $50,000, SL at 2% ($51,000), and R-multiple TP = 3R, **When** the TP trigger is placed, **Then** TP price = $47,000 (6% below entry)
- [ ] **Given** a trade closes with PnL = $250 and InitialR = $100, **When** metrics are recorded, **Then** RMultipleResult = 2.5
- [ ] **Given** a trade closes with PnL = -$100 and InitialR = $100, **When** metrics are recorded, **Then** RMultipleResult = -1.0
- [ ] **Given** a trade where price reached +3R before closing at +1.5R, **When** MFE/MAE are checked, **Then** MFE = 3.0R and MAE = 0 (never went negative)
- [ ] **Given** 10 closed R-tracked trades with R-multiples [2.1, -1.0, 1.5, -1.0, 3.0, -0.8, 2.0, -1.0, 1.8, -1.0], **When** aggregate metrics are calculated, **Then** expectancy ≈ 0.56R, win rate = 50%, profit factor ≈ 2.17
- [ ] **Given** a `PercentWallet` backtest, **When** results are displayed, **Then** R-multiple metrics section is not shown
- [ ] **Given** a `RiskBased` backtest, **When** results are displayed, **Then** R-multiple histogram and metrics are shown
- [ ] **Given** an R-multiple TP target < 1.0, **When** the strategy is saved, **Then** a warning is shown ("Sub-1R trade — relies on high win rate")
- [ ] **Given** an R-multiple TP target < 0, **When** the strategy is validated, **Then** it is rejected

### Release Notes Information

- **Heading**: R-Multiple Take-Profit & Trade Tracking
- **Release note type**: Feature
- **Release Note Summary**: Take-profit can now be expressed as a multiple of R (e.g., 2R = twice the risk). Backtest results show R-multiple metrics including expectancy, profit factor, SQN, and per-trade MFE/MAE when using Risk-Based sizing.
- **Release Notes Audience**: Product
- **Breaking Change**: No

## Out of Scope

- R-multiple stop-loss types (SL remains fixed_percent, atr_trailing, etc.)
- Partial close at R-levels (separate PBI: P3 Partial Close)
- Kelly criterion suggestion (separate PBI: P3 Kelly Criterion)
- R-multiple tracking for non-RiskBased sizing modes
