# Follow-Up: Market Filters — regime_state + range_state

## Overview

This follow-up adds `regime_state` and `range_state` as strategy-level **market filters** (alongside the existing `TrendFilter`), not as entry conditions. These are gates that block/allow entries based on market structure, rather than triggers that detect trade setups.

**Depends on:** Derived Signal Engine Phase 1 (the signal framework + registry must be in place).

---

## Rationale

- `regime_state` answers "what kind of market are we in?" (ranging, trending, breakout, high volatility, parabolic)
- `range_state` answers "is price consolidating in a defined range?"
- These are **pre-conditions** for trading, not entry signals. Putting them in `EntryConditions[]` alongside RSI/MACD muddies semantics.
- The existing `TrendFilter` is the same concept — a strategy-level gate. Adding a `MarketFilter` section alongside it is natural.

---

## Architecture

### New Concept: MarketFilter

```
StrategyConfig
├── TrendFilter       (existing — EMA cross, SMA cross, price above EMA)
├── MarketFilter      (new — regime_state, range_state)
│   ├── enabled: bool
│   ├── type: "regime_state" | "range_state"
│   └── params: RegimeStateFilterParams | RangeStateFilterParams
├── EntryConditions[] (existing + 3 new derived signals from Phase 1)
└── ...
```

### Evaluation Flow

```
Candle Close
  → Build MarketContext (includes candle history from Phase 1A)
  → Check TrendFilter           ← existing gate
  → Check MarketFilter          ← NEW gate (blocks if filter fails)
  → Evaluate EntryConditions[]  ← only reached if filters pass
  → GridController / SignalController / DcaController
  → RiskEngine
  → Execution
```

---

## Backend Changes

### 1. MarketFilter Model

```csharp
// New in StrategyAuthoring/Models/
public sealed record MarketFilterConfig
{
    public bool Enabled { get; init; }
    public MarketFilterType Type { get; init; }
    public IMarketFilterParams? Params { get; init; }
}

public enum MarketFilterType
{
    Unknown,
    RegimeState,
    RangeState
}

public interface IMarketFilterParams { }

public sealed record RegimeStateFilterParams : IMarketFilterParams
{
    public int LookbackBars { get; init; } = 30;
    public decimal BreakoutThresholdPercent { get; init; } = 2.5m;
    public decimal HighVolatilityAtrPercent { get; init; } = 2.0m;
    public decimal ParabolicThresholdPercent { get; init; } = 8.0m;
    public string AllowedRegime { get; init; } = "ranging"; // or comma-separated list
}

public sealed record RangeStateFilterParams : IMarketFilterParams
{
    public int LookbackBars { get; init; } = 30;
    public int MinTouches { get; init; } = 4;
    public decimal MaxSlopeAbsPercent { get; init; } = 1.0m;
    public decimal BoundaryTolerancePercent { get; init; } = 0.003m;
}
```

### 2. StrategyConfig Extension

Add `MarketFilter` property to `StrategyConfig`:
```csharp
public MarketFilterConfig? MarketFilter { get; init; }
```

### 3. MarketFilterEvaluator

New service (follows `TrendFilterEvaluator` pattern):
```csharp
public interface IMarketFilterEvaluator
{
    FilterResult Evaluate(MarketFilterConfig config, MarketContext context);
}
```

Uses the `IDerivedSignalRegistry` from Phase 1 to evaluate `RegimeStateSignal` and `RangeStateSignal`.

### 4. Strategy Engine Integration

Update `CompositeStrategyEngine` (or `ConditionEvaluator`) to check MarketFilter before evaluating entry conditions, same as TrendFilter is checked.

### 5. Signal Implementations

Implement `RegimeStateSignal` and `RangeStateSignal` using the Phase 1 `IDerivedSignal` framework. These are the same implementations from Pack 3 — just deferred from Phase 1.

---

## Frontend Changes

### 1. Strategy Model

Add to `StrategyConfig`:
```typescript
marketFilter?: MarketFilterConfig | null;
```

### 2. UI: Market Filter Card

**Not a repeatable condition card** — it's a configuration panel (like TrendFilter):
- Enable/disable toggle
- Filter type dropdown (Regime State / Range State)
- Dynamic parameter fields based on type:
  - **Regime State**: Expected regime dropdown (ranging, trending_up, trending_down, breakout, high_volatility) + optional advanced thresholds
  - **Range State**: Lookback bars, min touches, max slope %, tolerance %

### 3. Wizard Integration

Add market filter step/section to the strategy wizard (or include in the existing entry step alongside trend filter).

---

## Estimated Scope

| Area | New Files | Modified Files |
|------|-----------|----------------|
| Backend models | 2-3 | StrategyConfig |
| Backend evaluator | 1 | CompositeStrategyEngine |
| Backend signals | 2 | DerivedSignalRegistry bootstrap |
| Backend validation | 0 | SchemaValidator, BusinessRuleValidator |
| Backend serialization | 0 | JSON converters (for MarketFilterConfig) |
| Frontend models | 0 | strategy.model.ts |
| Frontend components | 3 (card + 2 param panels) | strategy-builder-page, wizard |
| Frontend services | 0 | mapper, validation |
| Tests | 4-6 | - |

---

## Notes

- Reuses the entire derived signal framework from Phase 1 (IDerivedSignal, ISignalContext, DerivedSignalRegistry, CandleMath, etc.)
- The signal implementations (RegimeStateSignal, RangeStateSignal) are already designed in Pack 3 — just need to be ported
- Simpler UI than entry conditions (config panel, not repeatable cards)
- Could potentially support multiple market filters in future (e.g., both regime AND range), but start with single filter
