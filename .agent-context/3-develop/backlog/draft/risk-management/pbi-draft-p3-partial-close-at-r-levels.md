# Partial Close at R-Levels

**PBI ID:** Draft
**Status:** Draft
**Priority:** P3
**Iteration:** Backlog
**Created:** 2026-04-10T00:00:00Z
**Last Updated:** 2026-04-11T00:32:00Z
**Knowledge Source:** `33-risk-management-and-trade-sizing.md`
**Depends On:** P2 R-Multiple Exit Types & Trade Tracking

## Summary

Scale out of winning signal-based positions at configurable R-multiple milestones, locking in profit in tranches while letting the remaining position run. Each tranche is placed as a separate TP trigger order on Hyperliquid with a partial size. The existing stop-loss/trailing stop config continues to manage the remaining position — no automatic breakeven move.

### User Story

> As a **trader**, I want to **automatically take partial profits at configurable R-level milestones** so that **I lock in gains progressively while giving the remainder of the position room to capture larger moves**.

### Problem Statement

Currently, take-profit is all-or-nothing — the entire position closes at a single TP level. A winning trade that retraces after hitting TP gives back all unrealised profit if TP wasn't reached, or exits entirely if it was. Partial closes at R-milestones let the trader secure profit incrementally while still benefiting from extended moves.

### Business Value

- Locks in profit early (1R partial makes the remaining position "risk-free" when combined with a breakeven SL)
- Balances profit-taking with trend-following
- Standard professional scaling-out technique
- Reduces regret from exiting winners too early or too late

---

## Requirements

### Functional Requirements

#### Configuration

- [ ] Add `PartialCloses` list to `ExitConfig`:
  ```
  partialCloses:
    - atRMultiple: 1.0
      closePercent: 25
    - atRMultiple: 2.0
      closePercent: 25
    - atRMultiple: 3.0
      closePercent: 50
  ```
- [ ] Each tranche has `atRMultiple` (decimal > 0) and `closePercent` (1–100)
- [ ] `PartialCloses` is optional — when empty or null, the entire position is managed by the existing TP/SL/trailing stop config (user can choose not to use partial closes at all)
- [ ] `closePercent` values across all tranches must sum to ≤ 100
- [ ] If tranches sum to < 100, the remaining percentage is managed by the existing SL/trailing stop
- [ ] Partial closes only apply to signal-based strategies (not grid — grid has its own TP per level)
- [ ] Partial closes only apply when `PositionSizeType = RiskBased` (R must be known to calculate R-levels)

#### Optimizer Integration

- [ ] Add `IncludePartialCloses` boolean to `ParameterBounds` (default: `false`)
- [ ] When `IncludePartialCloses = true` and `PositionSizeMode = RiskBased`, the optimizer randomly generates candidates with and without partial closes enabled
- [ ] When partial closes are included, generate tranche configs from a predefined set of common profiles (e.g., "25/25/50 at 1R/2R/3R", "50/50 at 1R/2R", "none")
- [ ] This lets the optimizer discover whether partial closes improve risk-adjusted returns for the strategy

#### Exchange Execution

- [ ] After a signal entry fills, place one TP trigger order per tranche on Hyperliquid
  - Each trigger order has: `size = totalPositionSize × (closePercent / 100)`, `triggerPrice = entryPrice ± (SL distance × atRMultiple)`
- [ ] `TriggerOrderManager` extended to place multiple TP triggers per position
- [ ] Track which tranches have filled via `ProtectionOrderState` (or a new partial-close state)
- [ ] When a tranche fills, update the remaining position size for SL/trailing stop orders
- [ ] Existing SL/trailing stop continues to protect the remaining un-closed portion — no automatic breakeven move (SL behaviour stays as configured in `ExitConfig.StopLoss`)

#### Position Tracking

- [ ] Adjust remaining position size after each partial close
- [ ] Update SL trigger order size to match remaining position (Hyperliquid requires size on trigger orders)
- [ ] Track per-tranche R-multiple result for detailed reporting

#### Backtest Simulation

- [ ] Simulate partial closes at each R-level during backtest
- [ ] Backtest checks candle high/low against tranche trigger prices
- [ ] Report per-tranche fills in backtest trade detail
- [ ] Final remaining position closed by SL/trailing stop as normal

#### Frontend Configuration

- [ ] Add partial close tranche editor in exit config section (strategy builder)
- [ ] UI allows adding/removing tranches with `atRMultiple` and `closePercent` fields
- [ ] Validation: sum of `closePercent` ≤ 100, `atRMultiple` > 0
- [ ] Show warning if no trailing stop configured on final tranche ("remaining position has no trailing exit")
- [ ] Only shown when `PositionSizeType = RiskBased`

### Non-Functional Requirements

- [ ] Unit tests for partial TP trigger price calculations (long and short)
- [ ] Unit tests for position size adjustment after each tranche fill
- [ ] Unit tests for SL trigger order size update after partial close
- [ ] Backtest test: multi-tranche exits fire at correct R-levels
- [ ] Existing single-TP tests unaffected when `PartialCloses` is empty/null

---

## Acceptance Criteria

- [ ] **Given** a long position of 100 units with R = $100, entry at $50,000, SL at 2%, and partial closes [25% at 1R, 25% at 2R, 50% at 3R], **When** trigger orders are placed, **Then** 3 TP triggers: 25 units at $51,000 (1R), 25 units at $52,000 (2R), 50 units at $53,000 (3R)
- [ ] **Given** the 1R tranche fills (25 units closed at $51,000), **When** the position is updated, **Then** remaining position = 75 units and SL trigger order size is updated to 75 units
- [ ] **Given** the 2R tranche fills (25 more units closed), **When** the position is updated, **Then** remaining position = 50 units
- [ ] **Given** the final tranche fills at 3R, **When** the trade closes completely, **Then** total R-multiple = (25×1R + 25×2R + 50×3R) / 100 = 2.25R
- [ ] **Given** price reverses after the 1R partial and SL fires at the original SL level, **When** the remaining 75 units close at -1R, **Then** total result = +0.25R (1R partial) - 0.75R (SL on remainder) = -0.50R (better than -1.0R without partials)
- [ ] **Given** `PositionSizeType = PercentWallet`, **When** exit config is loaded, **Then** partial close UI is hidden
- [ ] **Given** partial close tranches summing to 110%, **When** the user tries to save, **Then** validation error: "Partial close percentages must not exceed 100%"
- [ ] **Given** a backtest with partial closes config, **When** candle high crosses the 1R level, **Then** the 1R tranche is simulated as filled
- [ ] **Given** `IncludePartialCloses = true` in the optimizer, **When** candidates are generated, **Then** some use partial close tranches and some use no partial closes (full TP/trailing only)

### Release Notes Information

- **Heading**: Partial Close at R-Levels
- **Release note type**: Feature
- **Release Note Summary**: Signal strategies using Risk-Based sizing can now configure partial profit-taking at R-multiple milestones. Each tranche is placed as a separate trigger order, locking in profits progressively while letting the remainder of the position run.
- **Release Notes Audience**: Product
- **Breaking Change**: No

## Out of Scope

- Grid strategy partial closes (grid already has per-level TP)
- Automatic breakeven SL move after first partial (SL stays as configured)
- Per-tranche independent trailing stop config (all tranches share the strategy's exit config)
- Partial closes for non-RiskBased sizing modes
