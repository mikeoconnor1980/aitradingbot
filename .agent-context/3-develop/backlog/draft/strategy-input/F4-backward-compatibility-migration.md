# PBI Specification: F4 — Backward Compatibility & Grid Migration

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-04-02
**PRD:** [02-strategy-input-pipeline.md](../../../prd-draft/02-strategy-input-pipeline.md)
**Implementation Phase:** 1a (Post-UI)
**Risk Level:** High
**Depends On:** F1 (Schema), F2 (Strategy Builder UI)

---

## Summary

Migrate existing `GridStrategyConfig` entries to the new canonical schema. Grid fields map into the `grid` section; TP/SL into `exit`; source is marked as `migration`. Migrated strategies are editable in the Strategy Builder and runable in the engine.

### User Story

> As a **trader**, I want my **existing grid strategies to continue working after the upgrade** so that **I don't lose configured strategies**.

---

## Requirements

### Functional Requirements

- [ ] `IStrategyMigrator` maps old flat `GridStrategyConfig` → new `StrategyConfig` with `strategyMode = "grid"`
  - `GridLevels` → `grid.levels`, `GridSpacing` → `grid.spacing`, `EntryMode` → `grid.entryMode`, `ManualAnchorPrice` → `grid.anchorPrice`, `BreakdownThreshold` → `grid.breakdownThreshold`
  - `TakeProfitPercent` → `exit.takeProfit` (type: fixed_percent)
  - `StopLossPercent` → `exit.stopLoss` (type: fixed_percent)
  - `PositionSize` → `risk.positionSizeValue` (type: fixed_usd), `Leverage` → `risk.leverage`
- [ ] Migrated configs get `schemaVersion = 1`, `source.entryPoint = "migration"`, `source.summary = "Migrated from legacy grid config"`
- [ ] Lazy migration on first load (no `schemaVersion` detected)
- [ ] Batch migration script for operators
- [ ] Migration creates revision 1
- [ ] Failures logged; original JSON preserved

---

## Acceptance Criteria

- [ ] **Given** old grid config without `schemaVersion`, **When** loaded, **Then** auto-migrated to canonical schema
- [ ] **Given** migrated strategy, **When** validated, **Then** all levels pass
- [ ] **Given** migrated strategy, **When** opened in Builder UI, **Then** grid params correctly populated
- [ ] **Given** malformed original JSON, **When** migration fails, **Then** error logged, original preserved

### Release Notes Information

- **Heading**: Strategy Schema Migration
- **Release Note Summary**: Existing grid strategies auto-migrate to the new schema.
- **Breaking Change**: Yes — schema superseded, migration automatic.
