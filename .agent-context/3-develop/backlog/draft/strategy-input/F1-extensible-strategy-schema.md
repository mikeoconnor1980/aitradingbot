# PBI Specification: F1 — Extensible Strategy Schema (v1 — Grid)

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-04-02
**Last Updated:** 2026-04-02
**PRD:** [02-strategy-input-pipeline.md](../../../prd-draft/02-strategy-input-pipeline.md)
**Reference:** [strategy-builder-ui-detailed.md](../../1-discover/prd/strategy-builder-ui-detailed.md)
**Implementation Phase:** 1a (Foundation)
**Risk Level:** Medium
**Depends On:** F0 (Typed Config Separation)

---

## Summary

Define the versioned, extensible strategy JSON schema and C# model that supports the **full composable structure** from the Strategy Builder UI spec but only **requires grid fields for v1**. The schema uses a `strategyMode` discriminator (`grid` vs `signal`) to honestly reflect the two different execution models. Entry conditions, trend filters, and advanced exit types are present as optional schema sections — they don't do anything in v1 but they're structurally in place so the schema doesn't need breaking changes when RSI/EMA/MACD handlers ship.

Also implement a three-level server-side validation pipeline.

### User Story

> As a **platform developer**, I want an **extensible strategy schema with validation** so that **I can add new condition types and exit rules later without schema migrations**.

### Business Value

Designing the schema wide from day one means: no schema breaking changes when new condition types ship; frontend and backend models stay aligned; `schemaVersion` enables graceful evolution; the Strategy Builder UI (F2) can show the full layout and progressively enable sections.

---

## Problem Statement

The transitional `StrategyConfig` from F0 carries only grid fields. The UI spec describes a much richer model with composable conditions, trend filters, and multiple exit types. We need a schema that bridges both: grid works now, composable conditions work later, no rework in between.

---

## Requirements

### Functional Requirements

#### Schema Design — Build Wide

- [ ] `schemaVersion` (integer, starting at 1) at root level
- [ ] `strategyMode` discriminator: `grid` or `signal`
  - `grid` mode: `GridController` handles execution; grid-specific params required
  - `signal` mode: future condition-based strategies; entry conditions required
- [ ] **Strategy details** (required for all modes): `strategyName`, `exchange`, `market`, `timeframe`, `direction` (long/short/both), `enabled`, `templateId` (optional)
- [ ] **Grid section** (required when `strategyMode = grid`): `levels`, `spacing`, `entryMode`, `anchorPrice`, `breakdownThreshold`
- [ ] **Trend filter section** (optional, all modes): `enabled`, `type`, `fastPeriod`, `slowPeriod`, `operator`, `appliesTo` — structurally present but **not evaluated in v1**
- [ ] **Entry conditions section** (required when `strategyMode = signal`, ignored for grid): `entryLogic` (all/any), `entryConditions` array with `id`, `enabled`, `type`, `label`, `params` — structurally present but **not evaluated in v1**
- [ ] **Exit rules** (required for all modes): `takeProfit` (enabled, type, value), `stopLoss` (enabled, type, value, lookback). Flat structure — `value` and `lookback` are nullable; `lookback` is only used for `swing_low` type. v1 supports `fixed_percent` and `swing_low` types only; other types are valid enum values but the engine falls back to defaults
- [ ] **Risk management** (required for all modes): `positionSizeType`, `positionSizeValue`, `leverage`, `maxOpenTrades`, `cooldownValue`, `cooldownUnit`, `allowSameCandleReentry`
- [ ] **Metadata** (optional): `tags`, `notes`
- [ ] **Source metadata**: `entryPoint` (ui_builder/natural_language/pine_import/migration), `summary`

#### C# Models

- [ ] `StrategyConfig` class in `TradePilot.Application/StrategyAuthoring/Models/` implementing `IStrategyConfig` (marker interface from F0 in Domain) — full schema, replaces F0's `GridStrategyConfig` record entirely
- [ ] Sub-models: `GridConfig`, `TrendFilterConfig`, `EntryConditionConfig`, `ExitConfig`, `ExitRuleConfig`, `RiskConfig`, `StrategyMetadata`, `SourceMetadata`
- [ ] Enums: `StrategyMode`, `Direction`, `TrendFilterType`, `TrendOperator`, `EntryConditionType`, `EntryLogic`, `ExitRuleType`, `PositionSizeType`, `CooldownUnit`, `StrategyEntryPoint`
- [ ] Entry condition `params` — typed per condition type (e.g., `RsiParams`, `PriceVsEmaParams`, `MacdParams`) with a base type for unknown/future condition types. Custom `JsonConverter` using the `type` discriminator handles polymorphic deserialization.
- [ ] `ExitRuleConfig` — flat structure with nullable `value` and `lookback` fields. `lookback` is null/ignored unless `type = swing_low`. No typed params for exits in v1.
- [ ] F0's `GridStrategyConfig` record in Domain is removed; all consumers now use the full `StrategyConfig`
- [ ] Pipeline interfaces (`IStrategyEngine`, `IGridController`, `StrategyScheduler`, `BacktestRunner`) continue to accept `IStrategyConfig`; `StrategyConfig` flows through as the concrete implementation
- [ ] `GridStrategyEngine` reads grid fields from `config.Grid.*`; TP/SL from `config.Exit.*`
- [ ] `GridController` reads grid fields from `config.Grid.*`; TP/SL from `config.Exit.*`

#### ExecutionConfig / Risk Ownership

- [ ] `StrategyConfig.Risk` is the single source of truth for user-facing risk parameters: `positionSizeType`, `positionSizeValue`, `leverage`, `maxOpenTrades`, `cooldownValue`, `cooldownUnit`, `allowSameCandleReentry`
- [ ] F0's `ExecutionConfig` shrinks to contain only `FeeModel` (makerFee, takerFee, slippage) — exchange/simulation execution context with no duplication
- [ ] `BacktestConfig` references `StrategyConfig` (as `IStrategyConfig`) for strategy + risk params, and `ExecutionConfig` for fees only

#### Validation Pipeline

- [ ] `IStrategyValidator` with `Validate(StrategyConfig) → ValidationResult` in `TradePilot.Application/StrategyAuthoring/Validation/`
- [ ] **Level 1 — Schema validation**: Required fields present, correct types, enum values valid, `schemaVersion` present
- [ ] **Level 2 — Business rules**: `strategyName` required (max 100), grid levels > 0, spacing > 0, TP/SL values > 0 when enabled, position size > 0, leverage ≥ 1, RSI 0–100, periods > 0
- [ ] **Level 3 — Cross-field consistency**: `strategyMode = grid` → grid section required; `strategyMode = signal` → entry conditions required; direction vs trend filter `appliesTo`
- [ ] `ValidationError` with `severity` (error/warning/info), `fieldPath`, `code`, `message`
- [ ] Validation rules from the UI spec's validation table are implemented
- [ ] FluentValidation at the API boundary coexists with `IStrategyValidator` — FluentValidation handles request-level validation, `IStrategyValidator` handles domain-level business and cross-field rules

#### API Endpoint

- [ ] `POST /api/strategies/validate` — accepts a `StrategyConfig` JSON body, runs the full 3-level validation pipeline, returns `ValidationResult` with all errors/warnings/info messages
- [ ] `RunBacktestRequest` updated to send the full `StrategyConfig` shape + `ExecutionConfig` (fees only)

#### Serialization

- [ ] C# model ↔ JSON round-trip produces identical output
- [ ] `System.Text.Json` with camelCase naming, string enum serialization
- [ ] Optional sections serialize as `null` when not configured (not omitted)
- [ ] Custom `JsonConverter` for `EntryConditionConfig.Params` polymorphic deserialization using the `type` discriminator field

#### Database Changes

- [ ] `BacktestRun.StrategyConfigJson` column updated to store the new full `StrategyConfig` JSON shape
- [ ] Old backtest records cleaned out — clean break, no migration of F0-shaped data
- [ ] `BacktestRunResponseMapper` updated to deserialize from the new schema shape

#### Angular (Deferred)

- Angular TypeScript model and `BacktestService` updates are deferred to F2 (Strategy Builder UI)
- Temporary breakage of the existing backtest UI between F1 and F2 is accepted

### Non-Functional Requirements

- [ ] Validation completes in < 50ms
- [ ] Adding a new entry condition type requires: new enum value, new params type, new validation rules — no changes to schema structure or serialization

---

## Technical Considerations

### Schema Structure (v1)

```json
{
  "schemaVersion": 1,
  "strategyMode": "grid",
  "strategyName": "BTC Grid Long",
  "exchange": "Hyperliquid",
  "market": "BTC-USD",
  "timeframe": "15m",
  "direction": "long",
  "enabled": true,
  "templateId": null,

  "grid": {
    "levels": 10,
    "spacing": 0.5,
    "entryMode": "auto_from_signal_candle",
    "anchorPrice": null,
    "breakdownThreshold": 1.5
  },

  "trendFilter": null,
  "entryLogic": null,
  "entryConditions": null,

  "exit": {
    "takeProfit": { "enabled": true, "type": "fixed_percent", "value": 2 },
    "stopLoss": { "enabled": true, "type": "fixed_percent", "value": 6 },
    "exitOnOppositeSignal": false
  },

  "risk": {
    "positionSizeType": "percent_wallet",
    "positionSizeValue": 5,
    "leverage": 1,
    "maxOpenTrades": 1,
    "cooldownValue": 0,
    "cooldownUnit": "candles",
    "allowSameCandleReentry": false
  },

  "metadata": { "tags": ["grid"], "notes": "" },
  "source": { "entryPoint": "ui_builder", "summary": "Grid long 10 levels 0.5% spacing" }
}
```

### Future Signal Mode Strategy (not implemented, but valid schema)

```json
{
  "schemaVersion": 1,
  "strategyMode": "signal",
  "strategyName": "EMA Pullback BTC 15m",
  "exchange": "Hyperliquid",
  "market": "BTC-USD",
  "timeframe": "15m",
  "direction": "long",
  "enabled": true,
  "templateId": "ema-pullback",

  "grid": null,

  "trendFilter": {
    "enabled": true,
    "type": "ema_cross",
    "fastPeriod": 50,
    "slowPeriod": 200,
    "operator": "gt",
    "appliesTo": "long"
  },

  "entryLogic": "all",
  "entryConditions": [
    {
      "id": "cond-1", "enabled": true, "type": "rsi",
      "label": "RSI Pullback",
      "params": { "period": 14, "operator": "lt", "value": 40 }
    }
  ],

  "exit": {
    "takeProfit": { "enabled": true, "type": "fixed_percent", "value": 3 },
    "stopLoss": { "enabled": true, "type": "swing_low", "lookback": 5 },
    "exitOnOppositeSignal": false
  },

  "risk": { "positionSizeType": "percent_wallet", "positionSizeValue": 5, "leverage": 1, "maxOpenTrades": 1 },
  "metadata": { "tags": ["trend", "pullback", "ema", "rsi"], "notes": "" },
  "source": { "entryPoint": "ui_builder", "summary": "EMA pullback with RSI confirmation" }
}
```

### Extensibility Contract

Adding a new condition type (e.g., Bollinger) requires:
1. Add `bollinger` to `EntryConditionType` enum
2. Add `BollingerParams` record
3. Add validation rules in `BusinessRuleValidator`
4. Add condition handler in the evaluator (F5)
5. Add UI card in the Strategy Builder (F6)

**No schema structure changes. No serialization changes. No interface changes.**

---

## Out of Scope

- Condition evaluation (F5)
- Strategy Builder UI (F2)
- Trend filter evaluation (F5)
- Advanced exit types engine support (future)
- Strategy versioning (F3)
- Angular TypeScript model updates (deferred to F2)
- Typed params for exit rules (flat nullable fields sufficient for v1)

---

## Decisions Log

| # | Question | Decision |
|---|----------|----------|
| 1 | Should `entryConditions[].params` be typed per condition or a generic dictionary? | **Typed records.** `RsiParams`, `PriceVsEmaParams`, `MacdParams` etc. with a `Dictionary<string, object>` fallback for unknown types. Custom `JsonConverter` handles polymorphic deserialization via the `type` discriminator. |
| 2 | Should the validator warn about unsupported features in v1 (e.g., signal mode, trend filter)? | **Yes — info-level messages.** "Trend filter configured but not yet evaluated by the engine" and "Signal mode strategies are not yet supported for execution." These are informational, not blocking. |
| 3 | `IStrategyConfig` relationship | **StrategyConfig implements IStrategyConfig.** The marker interface from F0 (in Domain) is preserved. Pipeline interfaces continue to accept `IStrategyConfig`. The concrete `StrategyConfig` lives in `TradePilot.Application`. |
| 4 | ExecutionConfig / Risk ownership | **StrategyConfig.Risk owns user-facing risk params** (leverage, positionSize, maxOpenTrades, cooldown). **ExecutionConfig shrinks to FeeModel only** (makerFee, takerFee, slippage). No duplication. |
| 5 | F0's GridStrategyConfig fate | **Replaced entirely.** F0's `GridStrategyConfig` record in Domain is removed. All consumers use the full `StrategyConfig` with `strategyMode = grid`. |
| 6 | Exit rule params approach | **Flat structure.** `ExitRuleConfig` uses nullable `value` and `lookback` fields. `lookback` is null/ignored unless `type = swing_low`. No typed params for exits in v1. |
| 7 | DB impact for old backtest records | **Clean break.** Old backtest records cleaned out. No migration of F0-shaped data. |
| 8 | Polymorphic deserialization for entry condition params | **Custom `JsonConverter`** using the entry condition `type` field as discriminator. |
| 9 | Validation architecture | **FluentValidation + IStrategyValidator coexist.** FluentValidation at API boundary for request validation; `IStrategyValidator` in Application layer for 3-level business/cross-field validation. |
| 10 | Validation API endpoint | **New `POST /api/strategies/validate` endpoint** for the Strategy Builder UI to call during authoring. |
| 11 | Angular updates | **Deferred to F2.** Temporary breakage of backtest UI between F1 and F2 is accepted. |

---

## Acceptance Criteria

- [ ] **Given** a grid strategy JSON matching the v1 schema, **When** deserialized, **Then** `StrategyConfig` has `StrategyMode = Grid` and `Grid` section populated
- [ ] **Given** `strategyMode = "grid"` and `grid = null`, **When** Level 3 validation runs, **Then** error: "Grid configuration required for grid mode"
- [ ] **Given** `strategyMode = "signal"` and no entry conditions, **When** Level 3 validation runs, **Then** error: "At least one entry condition required for signal mode"
- [ ] **Given** a strategy with `trendFilter` populated, **When** validated in v1, **Then** info message: "Trend filter not yet evaluated" (not blocking)
- [ ] **Given** `grid.levels = 0`, **When** Level 2 validation runs, **Then** error on `grid.levels`
- [ ] **Given** `strategyName = ""`, **When** Level 1 validation runs, **Then** error: "Strategy name is required"
- [ ] **Given** the new `StrategyConfig`, **When** used in `IStrategyEngine.EvaluateAsync`, **Then** `GridStrategyEngine` reads from `config.Grid.*` correctly
- [ ] **Given** the new `StrategyConfig`, **When** used in `IGridController.ProcessAsync`, **Then** `GridController` reads TP/SL from `config.Exit.*` correctly
- [ ] **Given** a `StrategyConfig` with an RSI entry condition, **When** serialized to JSON and back, **Then** the `RsiParams` are correctly round-tripped
- [ ] **Given** all existing tests, **When** run after updating to the new type, **Then** all pass

- [ ] **Given** a `POST /api/strategies/validate` request with an invalid config, **When** the endpoint runs, **Then** it returns all validation errors/warnings/info grouped by level
- [ ] **Given** `ExecutionConfig` after F1, **When** inspected, **Then** it contains only `FeeModel` (no leverage or positionSize)
- [ ] **Given** `StrategyConfig.Risk` with `leverage = 0`, **When** Level 2 validation runs, **Then** error: leverage must be ≥ 1

### Release Notes Information

- **Heading**: Extensible Strategy Schema
- **Release Note Summary**: New versioned strategy schema supporting grid strategies now and composable conditions (RSI, EMA, MACD) in the future.
- **Breaking Change**: Yes — `StrategyConfig` type changes from transitional to full schema. `ExecutionConfig` trimmed to FeeModel only. All consumers updated.
