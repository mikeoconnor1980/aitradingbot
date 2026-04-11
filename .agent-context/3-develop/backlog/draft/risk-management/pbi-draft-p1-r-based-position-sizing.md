# R-Based Position Sizing

**PBI ID:** Draft
**Status:** Draft
**Priority:** P1
**Iteration:** Backlog
**Created:** 2026-04-10T00:00:00Z
**Last Updated:** 2026-04-10T23:24:13Z
**Knowledge Source:** `33-risk-management-and-trade-sizing.md`

## Summary

Implement the `RiskBased` position sizing mode where every trade's position size derives from **R** — the dollar amount risked per trade. The user specifies `riskPerTradePercent` and the system calculates position notional from R and the stop-loss distance. This is backend-only — the frontend risk card changes are covered by a separate PBI (P1 Risk Management UI).

### User Story

> As a **trader**, I want to **specify what percentage of my account I'm willing to risk per trade** so that **the system automatically calculates the correct position size based on my stop-loss distance, giving me precise dollar risk control**.

### Problem Statement

The current sizing modes (`PercentWallet`, `FixedNotional`) control notional amount but do not directly control dollar risk. The actual R depends on where the stop-loss is placed, and the user must mentally calculate whether the notional + SL combination produces an acceptable loss size. With `RiskBased` mode, the user declares the acceptable risk and the system derives everything else.

### Business Value

- Enables professional-grade risk management (risk exactly X% per trade)
- Creates an anti-martingale effect: position sizes naturally shrink after losses and grow after wins
- Foundation for all other R-based features (auto-leverage, R-multiple exits, portfolio heat)

---

## Requirements

### Functional Requirements

#### Core Sizing Calculation

- [ ] Add `RiskBased` value to `PositionSizeType` enum
- [ ] Add `RiskPerTradePercent` field to `RiskConfig` (decimal, nullable — only required when `RiskBased`)
- [ ] Add `riskPerTradePercent` field to existing `RiskConfigRequest` API DTO (nullable, ignored for other modes)
- [ ] Implement R-based calculation in `PositionSizeResolver`:
  - `R = equity × (riskPerTradePercent / 100)`
  - `positionNotional = R / (stopLossPercent / 100)`
- [ ] `PositionSizeResolver.ResolveNotional` accepts an optional `stopLossPercent` parameter (required only for `RiskBased` mode)
- [ ] Backward compatible: `PercentWallet` and `FixedNotional` modes are unaffected; existing callers pass `null` for `stopLossPercent`

#### Equity Source

- [ ] Use **realised account balance only** — do not include unrealised PnL from open positions
- [ ] For **live trading**: refresh equity before each trade entry
- [ ] For **backtesting**: use the simulated running equity (updated after each closed trade) to produce the anti-martingale effect

#### Strategy-Agnostic SL Distance Resolution

Both `GridController` and `SignalController` resolve the effective SL distance from `ExitConfig` at signal emission time:

| Exit Type | SL Distance Resolution |
|-----------|------------------------|
| `FixedPercent` | `config.Exit.StopLoss.Value / 100` — constant |
| `AtrTrailing` | `(ATR × multiplier) / entryPrice` — varies per candle |
| Grid breakdown | `config.Grid.BreakdownThreshold / 100` — grid-specific |

For grid strategies: use `Exit.StopLoss` when configured; fall back to `Grid.BreakdownThreshold` if no explicit SL is set.

- [ ] `GridController` resolves SL% from `ExitConfig` (or breakdown threshold), passes to resolver
- [ ] `SignalController` resolves SL% from `ExitConfig`, passes to resolver
- [ ] For grids: `notionalPerLevel = positionNotional / gridLevels`

#### Validation

- [ ] `riskPerTradePercent` must be > 0 and ≤ 100 when `RiskBased` is selected
- [ ] Warn the user if `riskPerTradePercent` > 5% (high risk warning, not a hard block)
- [ ] **Prevent strategy save** if `RiskBased` is selected and no stop-loss is configured in `ExitConfig`
- [ ] **Block trade entry at runtime** (safety net) if `RiskBased` mode and SL distance cannot be resolved

### Non-Functional Requirements

- [ ] Unit tests for `PositionSizeResolver` with all three modes (`PercentWallet`, `FixedNotional`, `RiskBased`)
- [ ] Unit tests for SL distance resolution per exit type (`FixedPercent`, `AtrTrailing`, `BreakdownThreshold`)
- [ ] Unit tests for validation: missing SL, riskPerTradePercent out of range
- [ ] Existing tests for `PercentWallet` and `FixedNotional` continue to pass unchanged
- [ ] Backtest sizing tests verifying anti-martingale: R shrinks after simulated losses

---

## Acceptance Criteria

- [ ] **Given** `PositionSizeType = RiskBased`, `riskPerTradePercent = 1.0`, account equity = $10,000, and `StopLoss` at 2% (fixed_percent), **When** the system calculates position size, **Then** R = $100 and positionNotional = $5,000
- [ ] **Given** `PositionSizeType = RiskBased`, `riskPerTradePercent = 1.0`, account equity = $10,000, and ATR-based SL resolving to 5%, **When** the system calculates position size, **Then** R = $100 and positionNotional = $2,000
- [ ] **Given** `PositionSizeType = RiskBased` on a grid strategy with 10 levels, **When** the grid is deployed, **Then** `notionalPerLevel = positionNotional / 10`
- [ ] **Given** `PositionSizeType = PercentWallet`, **When** the system calculates position size, **Then** existing behaviour is unchanged and `stopLossPercent` is not required
- [ ] **Given** `PositionSizeType = RiskBased` and no stop-loss configured, **When** the user saves the strategy, **Then** validation fails with an error message
- [ ] **Given** `PositionSizeType = RiskBased` and no stop-loss configured (runtime safety), **When** a trade entry signal is generated, **Then** the signal is blocked
- [ ] **Given** `riskPerTradePercent = 8`, **When** the user saves the strategy, **Then** a warning is shown (risk > 5%) but save is allowed
- [ ] **Given** a backtest with `RiskBased` mode starting at $10,000 equity, **When** the first trade loses $100, **Then** the next trade uses equity = $9,900 and R = $99

### Release Notes Information

- **Heading**: R-Based Position Sizing
- **Release note type**: Feature
- **Release Note Summary**: Traders can now select "Risk-Based" sizing mode where the system calculates position size from a declared risk percentage and stop-loss distance, ensuring precise dollar risk control per trade.
- **Release Notes Audience**: Product
- **Breaking Change**: No

## Out of Scope

- Frontend UI changes (separate PBI: P1 Risk Management UI)
- Auto-leverage calculation (separate PBI: P1 Auto-Leverage & Isolated Margin)
- Portfolio heat enforcement (separate PBI: P2 Portfolio Heat)
- R-multiple exit types (separate PBI: P2 R-Multiple Exits)
- Optimizer sweep of `riskPerTradePercent` (separate PBI: P1 Optimizer Risk Sweep)
