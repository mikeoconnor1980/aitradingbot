# Strategy Configuration Schema

The strategy configuration schema is defined by `StrategyConfig` in `src/TradingApp.Application/StrategyAuthoring/Models/StrategyConfig.cs`.
It implements `IStrategyConfig` (Domain marker) and is the concrete representation stored as JSON in the `StrategyConfig.ConfigJson` database column.
The schema is versioned (`SchemaVersion`) and extensible via the `StrategyMode` discriminator.

---

## Top-Level Structure

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `schemaVersion` | `int` | yes | Always `1` for v1 schemas |
| `strategyMode` | `StrategyMode` enum | yes | Discriminator: `grid` or `signal` |
| `strategyName` | `string` | yes | Human-readable name |
| `exchange` | `string` | yes | Default `"Hyperliquid"` |
| `market` | `string` | yes | Symbol (e.g. `"BTC"`) |
| `timeframe` | `string` | yes | Trigger timeframe (default `"15m"`) |
| `direction` | `Direction` enum | yes | `long` or `short` |
| `enabled` | `bool` | yes | Whether strategy is active |
| `templateId` | `string?` | no | Optional template link |
| `grid` | `GridConfig?` | mode-dependent | Required when `strategyMode = grid` |
| `trendFilter` | `TrendFilterConfig?` | no | Optional macro filter (not evaluated in v1) |
| `entryLogic` | `EntryLogic` enum | no | `all` or `any` — applied to entry conditions |
| `entryConditions` | `EntryConditionConfig[]?` | mode-dependent | Required when `strategyMode = signal` |
| `exit` | `ExitConfig` | yes | Take profit and stop loss rules |
| `risk` | `RiskConfig` | yes | Leverage, sizing, cooldown |
| `metadata` | `StrategyMetadata?` | no | Authoring metadata |
| `source` | `SourceMetadata?` | no | Import/template provenance; includes original NL input if created via interpretation |

All enums serialize as `snake_case_lower` strings.

---

## Grid Mode Example

```json
{
  "schemaVersion": 1,
  "strategyMode": "grid",
  "strategyName": "BTC Pullback Grid",
  "exchange": "Hyperliquid",
  "market": "BTC",
  "timeframe": "15m",
  "direction": "long",
  "enabled": true,
  "grid": {
    "levels": 4,
    "spacing": 0.35,
    "entryMode": "auto_from_signal_candle",
    "anchorPrice": null,
    "breakdownThreshold": 0.02
  },
  "exit": {
    "takeProfit": { "type": "percent_from_entry", "value": 0.8 },
    "stopLoss": { "type": "percent_from_entry", "value": 2.0 },
    "exitOnOppositeSignal": false
  },
  "risk": {
    "positionSizeType": "percent_of_equity",
    "positionSizeValue": 10,
    "leverage": 3,
    "maxOpenTrades": 1,
    "cooldownValue": 30,
    "cooldownUnit": "minutes",
    "allowSameCandleReentry": false
  }
}
```

---

## Signal Mode Example

```json
{
  "schemaVersion": 1,
  "strategyMode": "signal",
  "strategyName": "BTC RSI Signal",
  "exchange": "Hyperliquid",
  "market": "BTC",
  "timeframe": "15m",
  "direction": "long",
  "enabled": true,
  "entryLogic": "all",
  "entryConditions": [
    {
      "id": "cond-1",
      "enabled": true,
      "type": "rsi",
      "label": "RSI Oversold",
      "params": {
        "period": 14,
        "operator": "lt",
        "value": 40
      }
    }
  ],
  "exit": {
    "takeProfit": { "type": "percent_from_entry", "value": 0.8 },
    "stopLoss": { "type": "percent_from_entry", "value": 2.0 },
    "exitOnOppositeSignal": false
  },
  "risk": {
    "positionSizeType": "percent_of_equity",
    "positionSizeValue": 10,
    "leverage": 3,
    "maxOpenTrades": 1,
    "cooldownValue": 30,
    "cooldownUnit": "minutes",
    "allowSameCandleReentry": false
  }
}
```

---

## Sub-Model Reference

### GridConfig

`src/TradingApp.Application/StrategyAuthoring/Models/GridConfig.cs`

| Field | Type | Description |
|-------|------|-------------|
| `levels` | `int` | Number of grid levels (≥ 1) |
| `spacing` | `decimal` | Percent spacing between levels |
| `entryMode` | `string` | Schema value (e.g. `"auto_from_signal_candle"`); normalized to domain `EntryModes` at API boundary |
| `anchorPrice` | `decimal?` | Used when `entryMode = "wait_for_limit_price"` |
| `breakdownThreshold` | `decimal` | Price drop that triggers hedge |

### ExitConfig / ExitRuleConfig

`src/TradingApp.Application/StrategyAuthoring/Models/ExitConfig.cs`

Contains `TakeProfit` and `StopLoss`, each an `ExitRuleConfig`:

| Field | Type | Description |
|-------|------|-------------|
| `Type` | `ExitRuleType` enum | Rule type discriminator |
| `Value` | `decimal?` | Primary value (percentage, R-multiple, or fallback percent depending on type) |
| `Enabled` | `bool` | Whether this exit rule is active |
| `AtrMultiplier` | `decimal?` | ATR multiplier (required for `AtrTrailing` and `AtrInitial`) |
| `AtrPeriod` | `int?` | ATR period override (default 14; currently reserved, ATR period is hardcoded in context builders) |
| `Lookback` | `int?` | Lookback period (used by `SwingLow`) |
| `TrailingStopWarmup` | `int?` | Candles to skip before trailing stop activates (`AtrTrailing` only) |

`ExitRuleType` enum (`src/TradingApp.Application/StrategyAuthoring/Models/ExitRuleType.cs`):

| Value | Behaviour |
|-------|----------|
| `FixedPercent` | Static stop at `Value`% from entry |
| `AtrTrailing` | Trailing stop: HWM − (ATR × multiplier), recalculated every candle |
| `AtrInitial` | Locked stop: ATR captured at entry time, stop = entry ± (lockedATR × multiplier). Does not trail. Falls back to `Value`% if ATR unavailable at entry |
| `SwingLow` | Stop at recent swing low (lookback-based) |
| `RMultiple` | Take-profit at `Value` × R from entry |

### RiskConfig

`src/TradingApp.Application/StrategyAuthoring/Models/RiskConfig.cs`

| Field | Type | Description |
|-------|------|-------------|
| `positionSizeType` | `PositionSizeType` enum | How size is calculated: `percent_wallet`, `fixed_notional`, or `risk_based` |
| `positionSizeValue` | `decimal` | Size value (e.g. 10 = 10% of equity); unused for `risk_based` |
| `riskPerTradePercent` | `decimal?` | Percent of equity to risk per trade (required for `risk_based`; e.g. 1.0 = 1%) |
| `leverage` | `decimal` | Leverage multiplier (≥ 1, default 1); ignored when `autoLeverage = true` |
| `autoLeverage` | `bool` | When true, leverage auto-derived from SL distance; only effective with `risk_based` |
| `maxOpenTrades` | `int` | Max concurrent positions |
| `cooldownValue` / `cooldownUnit` | `int` / `CooldownUnit` enum | Post-trade cooldown |
| `allowSameCandleReentry` | `bool` | Whether same-candle re-entry is permitted |

`RiskBased` sizing: `R = equity × riskPerTradePercent / 100`; `notional = R / (stopLossPercent / 100)`. For grids, total notional is divided by grid levels. Requires stop-loss to be configured. See [33-risk-management-and-trade-sizing.md](33-risk-management-and-trade-sizing.md) for full details.

### TrendFilterConfig

`src/TradingApp.Application/StrategyAuthoring/Models/TrendFilterConfig.cs`

Optional macro trend filter. In v1, populated but **not evaluated** (info message emitted by validator). Fields: `enabled`, `type` (`TrendFilterType` enum), `fastPeriod`, `slowPeriod`, `operator` (`TrendOperator` enum), `appliesTo` (`Direction` enum).

### EntryConditionConfig (Signal Mode)

`src/TradingApp.Application/StrategyAuthoring/Models/EntryConditionConfig.cs`

Each condition has: `id`, `enabled`, `type` (`EntryConditionType` enum), `label`, and a polymorphic `params` object.

Supported param types:

| `type` | Params class | Key fields |
|--------|-------------|------------|
| `rsi` | `RsiParams` | `period` (2–200), `operator` (`lt`, `lte`, `gt`, `gte`, `cross_above`, `cross_below`), `value` |
| `price_vs_ema` | `PriceVsEmaParams` | `emaPeriod`, `operator` (`above`, `below`, `cross_above`, `cross_below`, `near`, `touch`) |
| `macd` | `MacdParams` | `fastPeriod` (2–50), `slowPeriod` (5–200), `signalPeriod` (2–50), `operator` (see below); max 1 per strategy |

MACD operators:

| Operator | Condition |
|----------|-----------|
| `cross_above_signal` | MACD line crossed above signal line on this candle |
| `cross_below_signal` | MACD line crossed below signal line on this candle |
| `above_zero` | MACD line is above zero |
| `below_zero` | MACD line is below zero |
| `histogram_rising` | Histogram is rising vs. previous candle |
| `histogram_falling` | Histogram is falling vs. previous candle |

`BusinessRuleValidator` enforces: `fastPeriod < slowPeriod`; all periods within their ranges; at most one `macd` condition per strategy config.

Custom `EntryConditionConfigConverter` + `EntryConditionParamsConverter` handle polymorphic JSON deserialization using the `type` field as discriminator. Files: `src/TradingApp.Application/StrategyAuthoring/Serialization/`.

---

## JSON Serialization

All strategy JSON must be serialized/deserialized using `StrategyJsonOptions.Default` (`src/TradingApp.Application/StrategyAuthoring/Serialization/StrategyJsonOptions.cs`):
- `camelCase` property names
- Enums as `snake_case_lower` strings
- Polymorphic entry condition params via custom converters

---

## Validation Pipeline

`POST /api/strategies/validate` runs `CompositeStrategyValidator` which chains three levels:

| Level | Class | Checks |
|-------|-------|--------|
| 1 — Schema | `SchemaValidator` | Required fields present (e.g. `strategyName`, `market`) |
| 2 — Business Rules | `BusinessRuleValidator` | Range constraints (e.g. `grid.levels ≥ 1`, `risk.leverage ≥ 1`) |
| 3 — Cross-Field | `CrossFieldValidator` | Mode consistency (e.g. `grid` required for `strategyMode = grid`; at least one entry condition for `signal` mode) |

`ValidationResult` includes errors, warnings, and info messages grouped by severity (`ValidationSeverity` enum). Info-level messages are non-blocking (e.g. trend filter not yet evaluated).

Files: `src/TradingApp.Application/StrategyAuthoring/Validation/`

---

## Adding New Entry Condition Types

**Backend**
1. Add enum value to `EntryConditionType` (`src/TradingApp.Application/StrategyAuthoring/Models/`)
2. Create `{Name}Params` record implementing `IEntryConditionParams`
3. Register type in `EntryConditionParamsConverter` switch
4. Add `BusinessRuleValidator` checks (range constraints, cross-field rules)
5. Create `{Name}ConditionHandler : IConditionHandler` in `src/TradingApp.Application/StrategyAuthoring/Services/`
6. Register handler in `Program.cs` (`builder.Services.AddScoped<IConditionHandler, {Name}ConditionHandler>()`)
7. Update `StrategyInterpreterPrompt.cs` with correct operator strings for the new type

**Frontend**
8. Add `{name}-condition-item` component under `strategy-builder/components/`
9. Register the type in `ConditionFactoryService` to create its typed `FormGroup`
10. Add the condition item to `EntryConditionsCardComponent` switch/dispatch
11. Add a `STRATEGY_TEMPLATES` entry (if applicable) and update `_isSignalTemplate()` in all 4 locations

The schema should remain backward compatible.