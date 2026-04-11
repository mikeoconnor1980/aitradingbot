# Auto-Leverage & Isolated Margin Enforcement

**PBI ID:** Draft
**Status:** Draft
**Priority:** P1
**Iteration:** Backlog
**Created:** 2026-04-10T00:00:00Z
**Last Updated:** 2026-04-10T23:37:14Z
**Knowledge Source:** `33-risk-management-and-trade-sizing.md`
**Depends On:** P1 R-Based Position Sizing

## Summary

Derive leverage automatically from R and stop-loss distance, and enforce isolated margin mode on Hyperliquid. Instead of the user choosing leverage arbitrarily, the system calculates it so that margin posted ≈ R and the stop-loss fires before the liquidation price. This is backend-only — the frontend leverage preview is covered by the separate P1 Risk Management UI PBI.

### User Story

> As a **trader**, I want **leverage to be automatically calculated from my risk settings** so that **my stop-loss always fires before liquidation, and I never risk more than R on a single trade**.

### Problem Statement

The current `Leverage` field in `RiskConfig` is stored but never applied to sizing math or the exchange. The `SetLeverage` agent command exists as a stub. Users can set leverage arbitrarily, creating a mismatch between SL distance and liquidation price. The `SetLeveragePayload` defaults to cross margin (`IsCross = true`), but R-based risk containment requires isolated margin so that liquidation only affects a single position.

### Business Value

- Eliminates the common mistake of setting leverage too high relative to stop-loss distance
- Stop-loss always fires before liquidation (SL is the exit, liquidation is the backstop)
- Isolated margin prevents cascading liquidation across positions
- Maximum capital efficiency — margin ≈ R plus a small safety buffer

---

## Requirements

### Functional Requirements

#### Configuration

- [ ] Add `AutoLeverage` boolean field to `RiskConfig` (default: `false`)
- [ ] `AutoLeverage` is only available when `PositionSizeType = RiskBased`; ignored for `PercentWallet`/`FixedNotional`
- [ ] Add `autoLeverage` field to existing `RiskConfigRequest` API DTO (nullable)
- [ ] Change `SetLeveragePayload.IsCross` default from `true` to `false` (isolated by default)
- [ ] When `RiskBased` mode is used, **always enforce isolated margin** — cross margin is not permitted

#### Leverage Calculation

- [ ] Implement leverage calculation utility:
  ```
  leverage = 1 / (stopLossPercent / 100 + maintenanceMarginRate)
  ```
  Clamped to `[1, maxLeverage]` for the asset (floor, not round)
- [ ] When `AutoLeverage = true`, the calculated leverage replaces the manual `Leverage` field
- [ ] When `AutoLeverage = false`, manual `Leverage` value is used as before

#### Margin Tiers (Asset Metadata)

- [ ] Fetch margin tier data from Hyperliquid API (max leverage and maintenance margin rate per asset)
- [ ] Cache margin tiers at startup and refresh periodically (e.g., daily or on demand)
- [ ] Derive `maintenanceMarginRate = 0.5 / maxLeverage` per asset (e.g., BTC 50x → 1%, alt 20x → 2.5%)
- [ ] Fallback: if tier data unavailable, use a conservative default (e.g., 20x max, 2.5% maintenance)

#### Exchange Integration

- [ ] Implement `SetLeverage` in the agent's `LiveExecutionEngine` (currently a stub in `AgentCheckInService`)
- [ ] Add `SetLeverageAsync(asset, leverage, isIsolated)` to `IExecutionEngine` interface
- [ ] Live implementation uses `HyperliquidUpdateLeverageAction` (model already exists)
- [ ] Call `SetLeverage` on the exchange **before every trade entry** (each grid cycle start, each signal entry)
- [ ] Must be called per-asset (Hyperliquid sets leverage per-asset, not per-order)
- [ ] Handle error if leverage exceeds asset's max: clamp to max leverage and log a warning

#### Backtest Simulation

- [ ] Backtest `IExecutionEngine` implementation records the leverage per trade
- [ ] Simulate margin per position: `margin = positionNotional / leverage`
- [ ] Simulate liquidation price from entry, leverage, and maintenance margin
- [ ] If price hits liquidation price during backtest: force-close at liquidation price as a worst-case outcome
- [ ] Normal outcome: stop-loss fires first (liquidation is the fallback for extreme scenarios only)

### Non-Functional Requirements

- [ ] Unit tests for leverage calculation with various SL% and maintenance margin rates
- [ ] Unit tests verifying clamp to `[1, maxLeverage]`
- [ ] Unit test verifying isolated margin is always enforced for `RiskBased` mode
- [ ] Integration test verifying `SetLeverage` is called before trade entry
- [ ] Backtest test verifying liquidation acts as fallback beyond stop-loss
- [ ] Existing tests unaffected when `AutoLeverage = false`

---

## Acceptance Criteria

- [ ] **Given** `RiskBased` config with 1% risk, 2% SL, and a 50x-max asset (1% maintenance margin), **When** auto-leverage is enabled, **Then** leverage = floor(1 / (0.02 + 0.01)) = 33, and `SetLeverage(33, isolated)` is called on the exchange before the trade entry
- [ ] **Given** the same scenario, **When** the position is open, **Then** the liquidation price sits beyond the stop-loss price (SL fires first)
- [ ] **Given** `AutoLeverage = false` and manual leverage = 10, **When** a trade is placed, **Then** `SetLeverage(10, ...)` is called with the manual value
- [ ] **Given** `RiskBased` mode, **When** `SetLeverage` is called, **Then** `isIsolated = true` always (cross margin is not permitted)
- [ ] **Given** `PercentWallet` mode with `AutoLeverage = true`, **When** the config is validated, **Then** `AutoLeverage` is ignored (only works with `RiskBased`)
- [ ] **Given** auto-leverage calculates 60x but the asset's max is 50x, **When** leverage is applied, **Then** it is clamped to 50x and a warning is logged
- [ ] **Given** a backtest with `RiskBased` mode and auto-leverage, **When** price gaps through the stop-loss to the liquidation price, **Then** the position is force-closed at the liquidation price
- [ ] **Given** margin tier data is unavailable for an asset, **When** leverage is calculated, **Then** a conservative fallback (20x max, 2.5% maintenance) is used

### Release Notes Information

- **Heading**: Auto-Leverage & Isolated Margin
- **Release note type**: Feature
- **Release Note Summary**: When using Risk-Based sizing, leverage is now automatically derived from stop-loss distance and asset margin tiers, ensuring the stop-loss always fires before liquidation. Isolated margin is enforced to contain risk per position.
- **Release Notes Audience**: Product
- **Breaking Change**: No (existing configs with `AutoLeverage = false` or non-RiskBased modes are unaffected). Note: `SetLeveragePayload.IsCross` default changes from `true` to `false`.

## Reference

### Stop-Loss vs Liquidation Ordering

```
Entry Price
  ↓ price moves against
Stop-Loss Price        ← SL fires here, lose ≈ R (normal outcome)
  ↓ if SL fails (gap, no liquidity, extreme volatility)
Liquidation Price      ← market order to book, lose R + buffer (rare)
  ↓ if still not filled
Backstop Liq Price     ← position seized, ALL margin lost (extreme)
```

### Existing Infrastructure

| Component | Status |
|-----------|--------|
| `SetLeveragePayload` | Exists (needs `IsCross` default flip) |
| `HyperliquidUpdateLeverageAction` | Model exists (asset index, isCross, leverage) |
| `AgentCheckInService.HandleSetLeverageAsync` | Stub — logs warning, does nothing |
| `IExecutionEngine` | Missing `SetLeverageAsync` method |
| `HyperliquidAccountService.ExtractLeverage` | Already parses leverage + margin mode from positions |

## Out of Scope

- Frontend leverage preview / isolated margin indicator (separate PBI: P1 Risk Management UI)
- Portfolio heat enforcement (separate PBI: P2 Portfolio Heat)
- Drawdown-adjusted risk scaling (separate PBI: P3 Adaptive Risk)
