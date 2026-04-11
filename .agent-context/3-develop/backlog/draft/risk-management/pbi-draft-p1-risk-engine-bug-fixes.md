# Risk Engine Bug Fix — Notional Key Mismatch

**PBI ID:** Draft
**Status:** Draft
**Priority:** P1
**Iteration:** Backlog
**Created:** 2026-04-10T00:00:00Z
**Last Updated:** 2026-04-10T23:43:37Z
**Knowledge Source:** `33-risk-management-and-trade-sizing.md`

## Summary

Fix the signal parameter key mismatch that causes `LiveRiskEngine.CheckOrderSize` to silently pass all signals. Three different keys are used across the pipeline — `"notionalUsd"` (risk engine), `"notionalPerLevel"` (GridController), and `"notional"` (SignalController) — so the max order size check never finds the value and never blocks oversized orders.

### User Story

> As a **trader**, I want **the max order size check to actually block oversized orders** so that **my configured risk limits protect my account as expected**.

### Problem Statement

`LiveRiskEngine.CheckOrderSize` looks up the key `"notionalUsd"` in signal parameters to enforce `MaxOrderSizeUsd`. However:
- `GridController` emits the key `"notionalPerLevel"`
- `SignalController` emits the key `"notional"`

Since neither matches `"notionalUsd"`, `TryGetValue` returns `false` and the check silently passes — max order size enforcement is effectively disabled.

### Business Value

- Risk controls that silently pass give a false sense of safety
- A single key mismatch means _no_ oversized order will ever be blocked
- Prerequisite to trusting the risk engine for all new R-based features

---

## Requirements

### Functional Requirements

- [ ] Standardise on the canonical key `"notionalUsd"` for the USD notional value in signal parameters
- [ ] Update `GridController` to emit `"notionalUsd"` (rename from `"notionalPerLevel"`)
- [ ] Update `SignalController` to emit `"notionalUsd"` (rename from `"notional"`)
- [ ] Verify `LiveRiskEngine.CheckOrderSize` already uses `"notionalUsd"` (no change needed)
- [ ] Audit any other consumers of `"notionalPerLevel"` or `"notional"` keys and update them

### Non-Functional Requirements

- [ ] Unit test in `LiveRiskEngineTests`: signal with notional > MaxOrderSizeUsd is blocked
- [ ] Unit test in `LiveRiskEngineTests`: signal with notional ≤ MaxOrderSizeUsd is approved
- [ ] Update any existing tests that reference the old key names
- [ ] Verify downstream consumers (LivePositionManager, BacktestPositionManager) still read the correct key

---

## Note: Bug 2 (RecordLoss) Already Fixed

The knowledge doc listed `RecordLoss` not being wired as a bug. Investigation confirmed `FillProcessor` already calls `RecordLoss()` on negative PnL fills (lines 63 and 107). No action needed.

---

## Acceptance Criteria

- [ ] **Given** `MaxOrderSizeUsd = 1000` and a `DeployGrid` signal with `notionalUsd = 1500`, **When** the risk engine validates the signal, **Then** the signal is blocked
- [ ] **Given** `MaxOrderSizeUsd = 1000` and a `DeployGrid` signal with `notionalUsd = 800`, **When** the risk engine validates the signal, **Then** the signal is approved
- [ ] **Given** `MaxOrderSizeUsd = 1000` and a `SignalEntry` signal with `notionalUsd = 1500`, **When** the risk engine validates the signal, **Then** the signal is blocked
- [ ] **Given** a signal with no `notionalUsd` key in parameters, **When** the risk engine validates the signal, **Then** the signal passes (backward compatible)

### Release Notes Information

- **Heading**: Risk Engine — Max Order Size Fix
- **Release note type**: Bug Fix
- **Release Note Summary**: Fixed an issue where the max order size limit was not enforced due to a signal parameter key mismatch between controllers and the risk engine.
- **Release Notes Audience**: Product
- **Breaking Change**: No

## Out of Scope

- Portfolio heat enforcement (separate PBI: P2)
- Consecutive loss circuit breaker changes (already working via FillProcessor)
