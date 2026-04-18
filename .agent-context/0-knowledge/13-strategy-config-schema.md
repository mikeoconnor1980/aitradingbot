# Strategy Configuration Schema

`StrategyConfig` is the concrete JSON model stored in `Strategy.ConfigJson`. It implements `IStrategyConfig` and supports both grid-mode and signal-mode strategies through the `strategyMode` discriminator.

## Top-Level Structure

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `schemaVersion` | `int` | yes | Currently `1` |
| `strategyMode` | `StrategyMode` | yes | `grid` or `signal` |
| `strategyName` | `string` | yes | Display name |
| `exchange` | `string` | yes | Default is Hyperliquid |
| `market` | `string` | yes | Symbol code such as `BTC` |
| `timeframe` | `string` | yes | Trigger timeframe, typically `15m` |
| `direction` | `Direction` | yes | `long`, `short`, or `both` where applicable |
| `enabled` | `bool` | yes | Active flag |
| `templateId` | `string?` | no | Optional template reference |
| `grid` | `GridConfig?` | mode-dependent | Required for grid mode |
| `trendFilter` | `TrendFilterConfig?` | no | Used in signal mode when enabled |
| `entryLogic` | `EntryLogic` | signal mode | `all` or `any` |
| `entryConditions` | `EntryConditionConfig[]?` | signal mode | Required for signal mode |
| `exit` | `ExitConfig` | yes | Take-profit and stop-loss configuration |
| `risk` | `RiskConfig` | yes | Sizing, leverage, and cooldown |
| `metadata` | `StrategyMetadata?` | no | Authoring metadata |
| `source` | `SourceMetadata?` | no | Provenance including `StrategyEntryPoint` |

All strategy JSON uses `StrategyJsonOptions.Default`: camelCase property names and snake_case enum values.

## Grid Mode Notes

`GridConfig` contains:

| Field | Type | Description |
|-------|------|-------------|
| `levels` | `int` | Number of ladder levels |
| `spacing` | `decimal` | Percent spacing between levels |
| `entryMode` | `string` | Authoring value stored in config |
| `anchorPrice` | `decimal?` | Used for wait-for-limit flows |
| `breakdownThreshold` | `decimal` | Fallback stop-loss distance input for some sizing flows |

### Entry Mode Casing

`EntryModes` in the domain uses PascalCase constants:

- `AutoFromSignalCandle`
- `WaitForLimitPrice`
- `InitialMarketThenGrid`

The JSON schema and frontend examples typically use snake_case values such as `auto_from_signal_candle`.

Warning: known bug

`GridController` compares directly against PascalCase `EntryModes.WaitForLimitPrice`, while config defaults are typically authored in snake_case. The API normalizes some request paths, but a raw config payload using snake_case can still be a runtime mismatch risk if it bypasses normalization.

## Exit Configuration

`ExitConfig` contains `takeProfit`, `stopLoss`, and `exitOnOppositeSignal`.

`ExitRuleConfig` fields:

| Field | Type | Description |
|-------|------|-------------|
| `type` | `ExitRuleType` | Exit rule discriminator |
| `value` | `decimal?` | Primary percentage or R-multiple value |
| `enabled` | `bool` | Whether the rule is active |
| `atrMultiplier` | `decimal?` | Used by `AtrTrailing` and `AtrInitial` |
| `atrPeriod` | `int?` | Present in schema; ATR period is not yet dynamically applied in runtime builders |
| `lookback` | `int?` | Used by `SwingLow` |
| `trailingStopWarmup` | `int?` | Candle warmup before ATR trailing activates |

`ExitRuleType` values:

| Value | Runtime Behaviour |
|-------|-------------------|
| `FixedPercent` | Static stop or TP based on percentage |
| `AtrTrailing` | Uses moving ATR-based trailing stop |
| `AtrInitial` | Locks ATR at entry and keeps a fixed volatility-based stop |
| `SwingLow` | Enum exists, but there is no dedicated evaluator logic yet |
| `RMultiple` | Uses stop distance multiplied by `value` |

`SwingLow` is currently an enum-level option without dedicated exit evaluation logic. In practice the runtime falls back to other supported stop-loss handling.

## Risk Configuration

`RiskConfig` fields include:

| Field | Type | Notes |
|-------|------|-------|
| `positionSizeType` | `PositionSizeType` | `percent_wallet`, `fixed_notional`, or `risk_based` |
| `positionSizeValue` | `decimal` | Primary value for non-risk-based sizing |
| `riskPerTradePercent` | `decimal?` | Required for `risk_based` sizing |
| `leverage` | `decimal` | Manual leverage when auto-leverage is off |
| `autoLeverage` | `bool` | Derives leverage from stop distance in risk-based mode |
| `maxOpenTrades` | `int` | Max simultaneous positions |
| `cooldownValue` | `int` | Cooldown amount |
| `cooldownUnit` | `CooldownUnit` | Minutes, candles, and related units |
| `allowSameCandleReentry` | `bool` | Re-entry guard |

## Trend Filter Configuration

`TrendFilterConfig` fields are:

| Field | Type |
|-------|------|
| `enabled` | `bool` |
| `type` | `TrendFilterType` |
| `period` | `int?` |
| `fastPeriod` | `int` |
| `slowPeriod` | `int` |
| `operator` | `TrendOperator` |
| `appliesTo` | `Direction` |

Unlike earlier docs, `period` is part of the current model.

## Signal Entry Conditions

Each `EntryConditionConfig` has `id`, `enabled`, `type`, `label`, and polymorphic `params`.

Supported `EntryConditionType` values:

| Type | Params model | Key fields |
|------|--------------|------------|
| `rsi` | `RsiParams` | `period`, `operator`, `value` |
| `price_vs_ema` | `PriceVsEmaParams` | `period`, `operator`, `distanceType`, `distanceValue` |
| `macd` | `MacdParams` | `fastPeriod`, `slowPeriod`, `signalPeriod`, `operator` |
| `support_resistance` | `SupportResistanceParams` | `lookback`, `strength`, `operator`, `tolerance` |
| `candle_pattern` | `CandlePatternParams` | `pattern` |
| `liquidity_sweep` | `LiquiditySweepParams` | `lookbackBars`, `pivotBars`, `side` |
| `structure_shift` | `StructureShiftParams` | `lookbackBars`, `direction` |

### Derived Signal Condition Params

These condition types are evaluated through the derived-signal engine rather than a direct indicator comparison.

#### `CandlePatternParams`

| Field | Type |
|-------|------|
| `pattern` | `string` |

Implemented pattern values are validated in `BusinessRuleValidator` and currently include the candle-pattern set supported by `CandlePatternSignal`.

#### `LiquiditySweepParams`

| Field | Type |
|-------|------|
| `lookbackBars` | `int` |
| `pivotBars` | `int` |
| `side` | `string` |

`side` selects which sweep direction is valid for the condition. The frontend and backend use snake_case enum-style values.

#### `StructureShiftParams`

| Field | Type |
|-------|------|
| `lookbackBars` | `int` |
| `direction` | `string` |

`direction` selects bullish or bearish structure-shift detection.

### `PriceVsEmaParams`

The current model uses:

| Field | Type |
|-------|------|
| `period` | `int` |
| `operator` | `string` |
| `distanceType` | `string` |
| `distanceValue` | `decimal?` |

The older `emaPeriod` field name is stale.

### `SupportResistanceParams`

Implemented by `SupportResistanceConditionHandler`.

Supported operators:

- `near_support`
- `near_resistance`
- `above_support`
- `below_resistance`
- `bounce_support`
- `bounce_resistance`

## Indicator Requirements

Signal mode uses `IndicatorExtractor` to produce `IndicatorRequirement` records for the scheduler and context builder.

| Field | Type |
|-------|------|
| `type` | `string` |
| `period` | `int` |
| `fastPeriod` | `int?` |
| `slowPeriod` | `int?` |
| `signalPeriod` | `int?` |
| `lookback` | `int?` |
| `strength` | `int?` |

This model determines which indicators the scheduler must compute before evaluating signal-mode conditions.

Derived signal conditions may also require recent trigger-timeframe candle history. `MarketContext.CandleHistory` now carries that history into the runtime so the derived-signal engine can evaluate structure and sweep logic consistently in both live and backtest execution.

## Validation Pipeline

`POST /api/strategies/validate` runs `CompositeStrategyValidator`:

| Layer | Class | Purpose |
|-------|-------|---------|
| Schema | `SchemaValidator` | Required fields and basic presence checks |
| Business rules | `BusinessRuleValidator` | Ranges, operator constraints, and per-type validation |
| Cross-field | `CrossFieldValidator` | Mode consistency and related-field validation |

Validation returns errors, warnings, and info messages. Trend-filter behavior and other partial implementations are surfaced as non-blocking messages rather than silently ignored.

## Extending the Schema

1. Add or update the model under `StrategyAuthoring/Models`.
2. Update polymorphic serialization converters if the change affects entry-condition params.
3. Extend `BusinessRuleValidator` and `CrossFieldValidator`.
4. Add or update runtime consumers such as `IndicatorExtractor`, `CompositeStrategyEngine`, the relevant condition handler, and the derived-signal registry if the condition is price-structure-based.
5. Update the frontend form factories and template helpers.

## Future Recommendations

- Fix the entry-mode casing mismatch so config values and runtime comparisons use one canonical form.
- Either implement `SwingLow` evaluation or remove it from the public schema.
- Add stricter typing for `distanceType` and other string-based operator fields.
- Consider schema version `2` when adding any breaking changes to serialized strategy configs.