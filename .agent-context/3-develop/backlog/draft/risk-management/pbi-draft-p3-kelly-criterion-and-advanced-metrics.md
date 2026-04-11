# Kelly Criterion & Advanced Backtest Metrics

**PBI ID:** Draft
**Status:** Draft
**Priority:** P3
**Iteration:** Backlog
**Created:** 2026-04-10T00:00:00Z
**Last Updated:** 2026-04-11T00:54:51Z
**Knowledge Source:** `33-risk-management-and-trade-sizing.md`
**Depends On:** PBI #7 (R-Multiple Exits & Trade Tracking)

## User Story

As a **trader**, I want to **see Kelly-optimal risk percentage, SQN, expectancy, and other advanced metrics after backtesting** so that **I can evaluate my strategy's statistical quality and compare my configured risk level against what the math recommends**.

## Problem Statement

Basic backtest metrics (win rate, total PnL, max drawdown) don't tell the full story about a strategy's statistical edge. Professional traders rely on Kelly criterion for optimal risk sizing, SQN for strategy quality scoring, and expectancy for per-trade edge assessment. These metrics require R-multiple data per trade (from PBI #7) and are only meaningful with sufficient sample size.

---

## Requirements

### Functional Requirements

#### Metrics Calculated (Backtest-Only)
- [ ] **Kelly%** — full Kelly optimal risk percentage: `Kelly% = W - (1 - W) / R_ratio`, where W = win probability, R_ratio = average winning R-multiple / average losing R-multiple
- [ ] **Half-Kelly** — conservative recommendation: `Kelly% / 2`
- [ ] **SQN** (System Quality Number): `(Expectancy / StdDev(R-multiples)) × sqrt(N)`, where N = total trades
- [ ] **Expectancy** — average R-multiple per trade: `sum(R-multiples) / N`
- [ ] **Average Win/Loss R-Ratio** — average winning R-multiple / average losing R-multiple (absolute values)
- [ ] **Profit Factor** — gross profit / gross loss (from raw PnL, not R-multiples)

#### RiskBased Mode Requirement
- [ ] Kelly%, SQN, Expectancy, and Win/Loss R-Ratio are **only available when the backtest used `RiskBased` position sizing** (R-multiples must be recorded per trade via PBI #7)
- [ ] Profit Factor can be calculated from raw PnL regardless of sizing mode
- [ ] When metrics are unavailable (non-RiskBased), the API returns `null` for those fields and the UI shows "Requires R-Based sizing" placeholder

#### Minimum Trade Count Threshold
- [ ] **30 trades minimum** for Kelly%, SQN, and Expectancy to be considered statistically meaningful
- [ ] If fewer than 30 trades, metrics are still calculated but the UI shows a warning: "Low sample size (N trades) — metrics may be unreliable"

#### Persistence
- [ ] All metrics are persisted to the `BacktestRun` entity alongside existing summary fields (WinRate, TotalPnl, MaxDrawdown, etc.)
- [ ] New nullable columns: `KellyPercent`, `HalfKellyPercent`, `Sqn`, `Expectancy`, `WinLossRRatio`, `ProfitFactor`
- [ ] Calculated at backtest completion as part of the summary computation step

#### API Response
- [ ] Add the new metrics to `BacktestRunResponse` as nullable decimal fields
- [ ] Add new metrics to `BacktestSummaryDto` for the list view (at minimum `ProfitFactor` and `Sqn`)

#### Frontend Display
- [ ] New **"Advanced Metrics"** card/section below existing summary cards on backtest result page
- [ ] Show comparison: "Your risk: X% | Kelly suggests: Y% | Half-Kelly: Z%"
- [ ] SQN with quality label: < 1.6 "Poor", 1.6–1.9 "Below Average", 2.0–2.4 "Average", 2.5–2.9 "Good", 3.0–5.0 "Excellent", 5.1–6.9 "Superb", 7.0+ "Holy Grail"
- [ ] All metrics labelled as **"Advisory — not automatically applied"**
- [ ] Low sample size warning displayed prominently when < 30 trades

### Non-Functional Requirements

- [ ] Unit tests for Kelly% calculation with various win rates and R-ratios
- [ ] Unit tests for SQN calculation including edge cases (zero trades, all wins, all losses)
- [ ] Unit test for profit factor (including zero gross loss → infinity handling)
- [ ] Unit test confirming metrics return null when non-RiskBased sizing used
- [ ] Unit test for low sample size flag

---

## Acceptance Criteria

- [ ] **Given** a backtest with 60% win rate, avg winning R-multiple = 2.0, avg losing R-multiple = 1.0, **When** the backtest completes, **Then** Kelly% = 0.60 - (0.40 / 2.0) = 0.40 (40%) and Half-Kelly = 20%
- [ ] **Given** a backtest with RiskBased sizing and 50 trades, **When** viewing results, **Then** Kelly%, Half-Kelly, SQN, Expectancy, Win/Loss R-Ratio, and Profit Factor are all displayed in the Advanced Metrics section
- [ ] **Given** the configured `riskPerTradePercent` = 1%, **When** viewing results with Kelly% = 20%, **Then** the UI shows "Your risk: 1% | Kelly suggests: 20% | Half-Kelly: 10%"
- [ ] **Given** a backtest with `PercentWallet` sizing, **When** viewing results, **Then** Kelly%, SQN, Expectancy, and Win/Loss R-Ratio show "Requires R-Based sizing", and only Profit Factor is displayed
- [ ] **Given** a backtest with only 15 trades, **When** advanced metrics are displayed, **Then** a warning "Low sample size (15 trades) — metrics may be unreliable" is shown
- [ ] **Given** a backtest with SQN = 3.2, **When** viewing results, **Then** SQN is displayed as "3.2 — Excellent"
- [ ] **Given** a backtest with all losing trades (no wins), **When** Kelly% is calculated, **Then** Kelly% = negative (indicating no edge) and is displayed with a warning
- [ ] **Given** a backtest where gross loss = 0 (all winning trades), **When** Profit Factor is calculated, **Then** it displays as "∞" or "N/A — no losing trades"
- [ ] **Given** the backtest completes, **When** the BacktestRun entity is saved, **Then** KellyPercent, HalfKellyPercent, Sqn, Expectancy, WinLossRRatio, and ProfitFactor are persisted to the database

### Release Notes Information

- **Heading**: Kelly Criterion & Advanced Backtest Metrics
- **Release note type**: Feature
- **Release Note Summary**: Backtest results now include Kelly-optimal risk percentage, SQN (System Quality Number), expectancy, profit factor, and win/loss R-ratio. These advisory metrics help traders evaluate strategy quality and compare their configured risk against mathematically optimal levels.
- **Release Notes Audience**: Product
- **Breaking Change**: No

## Technical Considerations

### Current State
- `BacktestRun` entity has WinRate, TotalPnl, MaxDrawdown, AverageTradePnl, etc. — new nullable columns needed
- `BacktestRunResponse` maps entity to API response — needs new fields
- `BacktestSummaryDto` used for list views — add key metrics (ProfitFactor, Sqn)
- `BacktestRunResponseMapper.ToResponse()` maps all fields — extend for new metrics
- R-multiple per trade is added by PBI #7 (`BacktestTrade` will have `RMultiple` field)
- The metrics calculation should be a self-contained service/static class (e.g., `AdvancedMetricsCalculator`) that takes a list of trades and returns the metrics

### Database Migration
- New nullable columns on `BacktestRuns` table: `KellyPercent REAL`, `HalfKellyPercent REAL`, `Sqn REAL`, `Expectancy REAL`, `WinLossRRatio REAL`, `ProfitFactor REAL`
- Nullable so existing runs don't break

### Integration Points
- Metrics calculated at backtest completion, after trade history is finalized
- Frontend `backtest.model.ts` `BacktestResult` interface needs new optional fields
- Backtest result component gets a new "Advanced Metrics" section

## Out of Scope

- Live trading Kelly/SQN calculation — this PBI is backtest-only
- Optimizer result table integration — optimizer may show these metrics in a future PBI
- Automatic risk adjustment based on Kelly — all metrics are advisory only
- Rolling/windowed Kelly (Kelly over last N trades) — full-history only
- Quarter-Kelly or other fractions beyond half-Kelly
