# Volatility-Scaled Initial Stop Loss

**PBI ID:** Draft
**Status:** Draft
**Priority:** P3
**Iteration:** Backlog
**Created:** 2026-04-10T00:00:00Z
**Last Updated:** 2026-04-11T00:49:25Z
**Knowledge Source:** `33-risk-management-and-trade-sizing.md`
**Depends On:** PBI #1 (R-Based Position Sizing)

## User Story

As a **trader**, I want **my initial stop-loss distance to adapt to current market volatility using ATR** so that **I'm not stopped out prematurely in volatile conditions and my position sizes adjust automatically to maintain consistent dollar risk**.

## Problem Statement

A fixed-percentage stop loss ignores market conditions. In volatile markets, a tight SL causes excessive stop-outs. In calm markets, a wide SL underutilises capital. Using ATR for the initial stop distance keeps the dollar risk (R) constant while letting the SL breathe with volatility — the position naturally sizes smaller when volatility is high and larger when it's low.

---

## Requirements

### Functional Requirements

#### New ExitRuleType: AtrInitial
- [ ] Add `AtrInitial` to the `ExitRuleType` enum — separate from existing `AtrTrailing`
- [ ] `AtrInitial` sets the **initial** stop-loss distance at entry; `AtrTrailing` remains for trailing behaviour
- [ ] Configuration reuses existing `ExitRuleConfig` fields: `AtrMultiplier` (default 2.0) + new `AtrPeriod` field (default 14)
- [ ] SL distance = `ATR(AtrPeriod) × AtrMultiplier`
- [ ] SL price is calculated from the entry price (long: entry − distance, short: entry + distance)

#### ATR Snapshot at Entry
- [ ] The ATR value used for initial SL is **locked at the entry candle close** — not recalculated after entry
- [ ] The locked ATR value is captured from `context.Indicators.Atr` at the time the entry signal fires

#### Integration with R-Based Position Sizing
- [ ] When `PositionSizeType = RiskBased` AND `StopLoss.Type = AtrInitial`:
  - SL distance (as %) = `(ATR × multiplier) / entryPrice × 100`
  - Position size = `R / SL distance` (the ATR-derived SL feeds into `PositionSizeResolver`)
- [ ] This is the key value prop: volatile market → wider ATR SL → smaller position, keeping R constant

#### Behaviour in Non-RiskBased Modes
- [ ] When `PositionSizeType ≠ RiskBased`, `AtrInitial` just sets the SL price — no effect on position sizing (informational SL only)

#### Combo with Trailing Stop
- [ ] `AtrInitial` can be combined with a trailing stop — initial SL set by ATR, then a separate trailing stop can tighten it over time
- [ ] This requires `ExitConfig` to support separate initial SL type and trailing SL type (or the trailing stop overrides the initial when it tightens)

#### Fallback When ATR Unavailable
- [ ] If ATR data is unavailable (insufficient candle history for the period), fall back to `FixedPercent` SL using `ExitRuleConfig.Value` and log a warning
- [ ] Validation: `AtrInitial` config should warn if `Value` (fallback percent) is not set

#### Optimizer Support
- [ ] `AtrMultiplier` and `AtrPeriod` should be sweepable by the optimizer in `ParameterBounds`
- [ ] Add `AtrMultiplierRange` (min/max/step) and `AtrPeriodRange` (min/max/step) to `ParameterBounds`
- [ ] `StrategyConfigGenerator` generates `AtrInitial` exit configs when these ranges are present

### Non-Functional Requirements

- [ ] Unit tests for ATR-based initial SL distance calculation at various ATR values
- [ ] Unit tests showing position size varies inversely with volatility (high ATR → smaller position)
- [ ] Unit test for fallback to FixedPercent when ATR is unavailable
- [ ] Unit test for ATR snapshot locked at entry (doesn't change on subsequent candles)
- [ ] Unit test for combo: AtrInitial + trailing stop tightening

---

## Acceptance Criteria

- [ ] **Given** `StopLoss.Type = AtrInitial`, ATR(14) = $500, entry price = $50,000, multiplier = 2.0, **When** a long entry is placed, **Then** initial SL price = $49,000 (entry − $1,000)
- [ ] **Given** `PositionSizeType = RiskBased`, risk = 1% of $10,000 equity (R = $100), and ATR-derived SL distance = 2% of entry, **When** position size is calculated, **Then** notional = $100 / 0.02 = $5,000
- [ ] **Given** ATR doubles from $500 to $1,000 (high volatility period), **When** a new entry is calculated, **Then** SL distance doubles to $2,000 and position size halves to $2,500
- [ ] **Given** `PositionSizeType = PercentWallet` and `StopLoss.Type = AtrInitial`, **When** an entry is placed, **Then** the SL price is set by ATR but position size uses PercentWallet logic (no R-based sizing)
- [ ] **Given** insufficient candle history for ATR(14), **When** an entry signal fires, **Then** SL falls back to `FixedPercent` using `Value` and a warning is logged
- [ ] **Given** `StopLoss.Type = AtrInitial` and a trailing stop is also configured, **When** the trailing stop tightens past the initial ATR SL, **Then** the tighter trailing SL is used
- [ ] **Given** ATR = $500 at the entry candle, **When** ATR changes to $800 on the next candle, **Then** the initial SL remains at the original ATR-derived price (locked at entry)
- [ ] **Given** the optimizer has `AtrMultiplierRange: { Min: 1.0, Max: 3.0, Step: 0.5 }`, **When** a sweep runs, **Then** strategy configs are generated with AtrMultiplier values 1.0, 1.5, 2.0, 2.5, 3.0

### Release Notes Information

- **Heading**: Volatility-Scaled Initial Stop Loss (ATR-Based)
- **Release note type**: Feature
- **Release Note Summary**: Initial stop-loss distance can now be set using ATR, automatically adapting to market volatility. When combined with R-based position sizing, this keeps dollar risk constant while sizing positions smaller in volatile conditions and larger in calm ones.
- **Release Notes Audience**: Product
- **Breaking Change**: No

## Technical Considerations

### Current State
- `ExitRuleType` enum has `FixedPercent`, `SwingLow`, `AtrTrailing` — new `AtrInitial` variant needed
- `ExitRuleConfig` already has `AtrMultiplier` (used by `AtrTrailing`) — can be reused; needs new `AtrPeriod` field
- `TriggerOrderManager.CalculateStopLossPrice` handles `AtrTrailing` branch using `context.Indicators.Atr` — new `AtrInitial` branch needed (uses entry price instead of candle high as reference)
- `AtrCalculator` already exists in `TradingApp.Indicators` and calculates ATR series with Wilder smoothing
- `GridController` line 137 calls `PositionSizeResolver.ResolveNotional(config.Risk, context.AccountEquity)` — needs to also pass SL distance when `AtrInitial` + `RiskBased`
- `PositionSizeResolver` currently has no concept of SL distance — the `RiskBased` branch (from PBI #1) will need an optional `stopLossPercent` parameter that this PBI populates with the ATR-derived value

### Integration Points
- ATR value comes from `context.Indicators.Atr` (already populated in the candle processing pipeline)
- The SL distance feeds into `PositionSizeResolver` at the same callsite in `GridController`/`SignalController`
- Optimizer adds `AtrMultiplierRange` and `AtrPeriodRange` to `ParameterBounds`

## Out of Scope

- SwingLow-based initial SL — remains a separate `ExitRuleType`
- ATR-based take-profit distance — TP remains percentage/R-multiple based
- Dynamic ATR recalculation after entry for the initial SL (it's locked at entry)
- Separate ATR config fields for initial vs trailing — both reuse `AtrMultiplier` and `AtrPeriod` on `ExitRuleConfig`
