# Plan: Derived Signal Engine — Phase 1

## TL;DR

Add 3 derived signal entry conditions (candle_pattern, liquidity_sweep, structure_shift) to the strategy engine with full backend evaluation and Angular UI. Backend-first execution order. A follow-up phase adds regime_state and range_state as strategy-level market filters.

**Key architectural change:** MarketContext must be extended to carry candle history arrays, since derived signals need 50+ bars of lookback.

---

## Phase 1A: Backend — Extend MarketContext with Candle History

**Goal:** Make candle history available to signal evaluation without breaking existing consumers.

**Problem:** MarketContext currently has only `CurrentCandle` + `PreviousCandle`. Derived signals like `liquidity_sweep` need 50+ bars of history. Both `BacktestMarketContextBuilder` and `LiveMarketContextBuilder` already maintain internal `_candles` lists — they just don't expose them.

**Steps:**

1. Add property to `MarketContext`:
   - `IReadOnlyList<Candle>? CandleHistory { get; init; }` — the trigger-timeframe candle history (newest last), nullable for backward compat
   - `IReadOnlyDictionary<string, IReadOnlyList<Candle>>? CandlesByTimeframe { get; init; }` — optional multi-timeframe history (for future use; initially just trigger TF)

2. Update `BacktestMarketContextBuilder.BuildAsync()`:
   - Pass the existing internal `_candles` list (already maintained) as `CandleHistory` on the built MarketContext
   - No new data loading needed — just expose what's already there

3. Update `LiveMarketContextBuilder.BuildAsync()`:
   - Same — pass internal `_candles` list as `CandleHistory`

4. Verify existing consumers are unaffected (property is nullable, additive)

**Relevant files:**
- Modify: `src/TradePilot.Application/Trading/Models/MarketContext.cs`
- Modify: `src/TradePilot.Application/Trading/Services/BacktestMarketContextBuilder.cs`
- Modify: `src/TradePilot.Application/Trading/Services/LiveMarketContextBuilder.cs`

---

## Phase 1B: Backend — Signal Abstractions & Implementations

**Goal:** Port the derived signal framework and implement 3 entry-condition signals.

**Steps:**

1. Create `src/TradePilot.Application/Trading/Signals/` directory structure:
   - `Abstractions/` — IDerivedSignal, ISignalContext, IDerivedSignalRegistry
   - `Models/` — SignalRequest, SignalEvaluationResult, PivotPoint, supporting enums
   - `Registry/` — DerivedSignalRegistry
   - `Implementations/` — 3 signal calculators
   - `Helpers/` — SignalParameterReader, CandleMath

2. Port core abstractions from Pack 3:
   - `IDerivedSignal` with `Name` + `Evaluate(ISignalContext, SignalRequest)` → `SignalEvaluationResult`
   - `ISignalContext` (candles by timeframe, indicator values, persisted state)
   - `IDerivedSignalRegistry` (register, get, tryGet, listNames)

3. Port models:
   - `SignalRequest(Name, Timeframe, Parameters)` record
   - `SignalEvaluationResult(IsMatch, Score, Metadata)` with `True()`/`False()` factories
   - `SweepSide` enum, `StructureShiftDirection` enum, `PivotPoint` record
   - **Candle extensions:** Add `BodySize()`, `Range()`, `IsBullish()`, `UpperWick()`, `LowerWick()` as extension methods on existing `TradePilot.Domain.Entities.Candle` (check if it has Open/High/Low/Close — if not, work with the Application-layer candle)

4. Port helpers:
   - `SignalParameterReader` (GetInt, GetDecimal, GetString, GetBool with defaults)
   - `CandleMath` (AverageRange, Slope, FindRecentPivotHigh/Low, CountBoundaryTouches)

5. Implement 3 signals (defer range_state + regime_state to follow-up):
   - `CandlePatternSignal` ("candle_pattern") — engulfing, rejection, continuation
   - `LiquiditySweepSignal` ("liquidity_sweep") — pivot sweep + close reversal
   - `StructureShiftSignal` ("structure_shift") — swing high/low break

6. Create `InMemorySignalContext` in test project for unit testing

**Relevant files:**
- New: `src/TradePilot.Application/Trading/Signals/**`
- Reference: `.agent-context/1-discover/prd/engine/tradepilot_signal_engine_pack/` (source material)
- Reference: `src/TradePilot.Domain/Entities/Candle.cs` (existing candle entity)

**Depends on:** Phase 1A (candle history on MarketContext)

---

## Phase 1C: Backend — Bridge to Condition Evaluator

**Goal:** Wire 3 derived signals into the existing IConditionHandler → ConditionEvaluator pipeline.

**Steps:**

1. Add 3 new values to `EntryConditionType` enum:
   - `CandlePattern`, `LiquiditySweep`, `StructureShift`

2. Create params interfaces in `StrategyAuthoring/Models/`:
   - `CandlePatternParams : IEntryConditionParams` — `Pattern` (string: bullish_engulfing, bearish_engulfing, bullish_rejection, bearish_rejection, bullish_continuation, bearish_continuation, bullish_rejection_or_engulfing, bearish_rejection_or_engulfing)
   - `LiquiditySweepParams : IEntryConditionParams` — `LookbackBars` (int), `PivotBars` (int), `Side` (string: upside/downside)
   - `StructureShiftParams : IEntryConditionParams` — `PivotBars` (int), `Direction` (string: bullish/bearish)

3. Update JSON deserialization:
   - `EntryConditionParamsConverter.cs` — add 3 cases for new types
   - `EntryConditionConfigConverter.cs` — add type string mappings ("candle_pattern", "liquidity_sweep", "structure_shift")

4. Create `MarketContextSignalContextAdapter : ISignalContext`:
   - Wraps `MarketContext` — uses new `CandleHistory` property for candle data
   - Exposes `IndicatorContext` values via `GetIndicatorValue()`
   - Throws clear error if `CandleHistory` is null (defensive)

5. Create `DerivedSignalConditionHandler : IConditionHandler`:
   - Single handler for all 3 types (same delegation pattern)
   - Maps `EntryConditionType` → signal name string
   - Builds `SignalRequest` from `IEntryConditionParams`
   - Wraps `MarketContext` in `MarketContextSignalContextAdapter`
   - Calls `registry.Get(name).Evaluate(context, request)`
   - Maps `SignalEvaluationResult` → `ConditionResult`

6. Register in DI:
   - `DerivedSignalRegistry` as singleton (bootstrap with 3 signals)
   - `DerivedSignalConditionHandler` as `IConditionHandler`

7. Update validators:
   - `SchemaValidator.cs` — accept 3 new condition types
   - `BusinessRuleValidator.cs` — validate params (e.g., pattern name in known set, lookbackBars 1–200, pivotBars 1–10)

8. Update `BacktestAuditCollector` / `CandleEvaluationEntry`:
   - Ensure derived signal condition results appear in backtest debug output
   - Include signal score + metadata in evaluation log (so users can debug why a signal fired/didn't)

**Relevant files:**
- Modify: `src/TradePilot.Application/StrategyAuthoring/Models/EntryConditionType.cs`
- New: `src/TradePilot.Application/StrategyAuthoring/Models/DerivedSignalParams.cs` (3 param types)
- Modify: `src/TradePilot.Application/StrategyAuthoring/Serialization/EntryConditionParamsConverter.cs`
- Modify: `src/TradePilot.Application/StrategyAuthoring/Serialization/EntryConditionConfigConverter.cs`
- New: `src/TradePilot.Application/Trading/Signals/MarketContextSignalContextAdapter.cs`
- New: `src/TradePilot.Application/StrategyAuthoring/Services/DerivedSignalConditionHandler.cs`
- Modify: `src/TradePilot.Application/StrategyAuthoring/Validation/SchemaValidator.cs`
- Modify: `src/TradePilot.Application/StrategyAuthoring/Validation/BusinessRuleValidator.cs`
- Modify: `src/TradePilot.Application/Backtesting/Models/CandleEvaluationEntry.cs` (if needed for debug)
- Modify: DI registration

**Depends on:** Phase 1B

---

## Phase 1D: Frontend — Models & Services

**Goal:** Extend TypeScript models, factory, mapper, and validation for 3 new condition types.

**Steps:**

1. Update `strategy.model.ts`:
   - Extend `EntryConditionType`: add `"candle_pattern" | "liquidity_sweep" | "structure_shift"`
   - Add types: `CandlePatternType`, `SweepSide`, `StructureShiftDirection`
   - Add params interfaces: `CandlePatternParams`, `LiquiditySweepParams`, `StructureShiftParams`
   - Extend `EntryConditionConfig.params` union

2. Create enum files in `enums/`:
   - `candle-pattern-type.enum.ts` (8 pattern label/value pairs)
   - `sweep-side.enum.ts` (upside/downside)
   - `structure-shift-direction.enum.ts` (bullish/bearish)

3. Add 3 factory methods to `condition-factory.service.ts`:
   - `createCandlePatternCondition()` — default: pattern = "bullish_engulfing"
   - `createLiquiditySweepCondition()` — defaults: lookbackBars=50, pivotBars=2, side="upside"
   - `createStructureShiftCondition()` — defaults: pivotBars=2, direction="bullish"

4. Add 3 cases in `_mapConditionParams()` in `strategy-mapper.service.ts`

5. Add validation rules in `strategy-validation.service.ts`:
   - CandlePattern: pattern must be in known set
   - LiquiditySweep: lookbackBars 1–200, pivotBars 1–10
   - StructureShift: pivotBars 1–10, direction in known set

**Relevant files:**
- Modify: `frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts`
- New: `frontend/trading-ui/src/app/features/strategy-builder/enums/candle-pattern-type.enum.ts` (+ 2 more)
- Modify: `frontend/trading-ui/src/app/features/strategy-builder/services/condition-factory.service.ts`
- Modify: `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.ts`
- Modify: `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.ts`

**Depends on:** Phase 1C complete (backend contracts finalized)

---

## Phase 1E: Frontend — 3 Condition Item Components

**Goal:** Create 3 condition-item components following the rsi-condition-item pattern.

**Steps:**

1. **`candle-pattern-condition-item`** (.ts, .html, .scss):
   - Fields: Pattern dropdown (8 options)
   - Simplest component — single mat-select
   - Info popover: explains each pattern type

2. **`liquidity-sweep-condition-item`** (.ts, .html, .scss):
   - Fields: Lookback Bars (number, 1–200), Pivot Bars (number, 1–10), Side (dropdown: upside/downside)
   - Info popover: explains stop-hunt reversal concept

3. **`structure-shift-condition-item`** (.ts, .html, .scss):
   - Fields: Pivot Bars (number, 1–10), Direction (dropdown: bullish/bearish)
   - Info popover: explains market structure breaks

4. Update `entry-conditions-card.component.ts`:
   - Import 3 new components
   - Add `onAddCandlePattern()`, `onAddLiquiditySweep()`, `onAddStructureShift()`

5. Update `entry-conditions-card.component.html`:
   - Add 3 `@if` branches in condition type rendering
   - Add 3 "Add" buttons in `mat-card-actions`

**Relevant files (all new unless noted):**
- New: `frontend/.../components/candle-pattern-condition-item/` (3 files)
- New: `frontend/.../components/liquidity-sweep-condition-item/` (3 files)
- New: `frontend/.../components/structure-shift-condition-item/` (3 files)
- Modify: `frontend/.../components/entry-conditions-card/entry-conditions-card.component.ts`
- Modify: `frontend/.../components/entry-conditions-card/entry-conditions-card.component.html`

**Depends on:** Phase 1D

---

## Phase 1F: Integration & Wizard Verification

**Goal:** End-to-end verification.

**Steps:**

1. Verify `wizard-entry-step` delegates to `entry-conditions-card` (no hardcoded types)
2. Verify `strategy-draft.service.ts` handles new condition types in draft save/restore
3. Verify `strategy-builder-page.component.ts` form initialization works with new types
4. End-to-end: create signal strategy → add candle_pattern + liquidity_sweep → save → reload → verify round-trip
5. Run backtest with derived signal conditions → verify evaluation in debug output

**Depends on:** Phase 1C + 1E

---

## Verification

### Automated Tests (Backend)
- Unit test for each signal implementation using InMemorySignalContext
- Unit test for `DerivedSignalConditionHandler` with mock registry
- Unit test for `MarketContextSignalContextAdapter`
- JSON round-trip tests for each new EntryConditionType + params
- Validator tests for new condition types (valid + invalid params)
- Integration: backtest with candle_pattern condition → verify signal evaluates and appears in audit

### Automated Tests (Frontend)
- `condition-factory.service.spec.ts` — 3 new factory methods
- `strategy-mapper.service.spec.ts` — 3 new param mappings
- Each condition-item component — render + form validation

### Manual Verification
- `http://localhost:4200/strategies/new` → Signal mode → add all 3 new conditions → configure → save → reload
- Run backtest → inspect debug output for derived signal evaluation results

---

## Execution Order (Sequential)

```
1A (MarketContext candle history)
  → 1B (signal abstractions + 3 implementations)
    → 1C (bridge to condition evaluator + validators + audit)
      → 1D (frontend models + services)
        → 1E (frontend 3 condition components)
          → 1F (integration verification)
```

Backend-first. Frontend starts only after backend contracts are finalized.

---

## Decisions

- **3 entry conditions, not 5** — candle_pattern, liquidity_sweep, structure_shift are entry triggers. regime_state + range_state → follow-up as market filters.
- **Single bridge handler** — `DerivedSignalConditionHandler` handles all 3 types via registry
- **Reuse existing Candle** with extension methods (not a new record)
- **Expose existing candle history** — both context builders already maintain it internally, just not on MarketContext
- **Backend-first** — finalize param contracts before frontend work
- **JSON retained** — no YAML

## Scope Boundaries

**Included:** 3 derived signals, signal framework, UI components, JSON serialization, backtest audit integration, unit + integration tests.

**Excluded:** regime_state, range_state (follow-up as market filters), YAML authoring, condition compilation, FluentValidation migration, strategy groups, typed strategy subtypes.
