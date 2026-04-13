<!-- markdownlint-disable-file -->

# Task Details: Knowledge Base Audit & Refresh

## Phase 3: Strategy & Execution (01, 12, 13, 14, 15, 16, 19)

## Standards and Knowledge References

- `.github/instructions/agent-knowledge.instructions.md` — documentation standards
- Cross-reference related knowledge docs where relevant

### Task 3.1: Update `01-trading-strategy.md` {#task-31-update-trading-strategy}

Realign the trading strategy document with actual implementation.

- **Complexity**: High
- **Risk Factors**: Strategy documents are critical for planning — must accurately distinguish implemented vs future
- **Files**:
  - `.agent-context/0-knowledge/01-trading-strategy.md` — update
- **Success**:
  - Interface name corrected to `IStrategyEngine`
  - Unimplemented filters (EMA trend, bias, pullback, hedge) clearly labeled as "NOT IMPLEMENTED"
  - Actual regime gating documented (MarketRegime enum, SyntheticRegimeProvider)
  - Signal mode documented alongside Grid mode
  - DrawdownEvaluator + DrawdownTier system documented
  - Portfolio heat enforcement documented

#### Changes Required

1. **Fix interface name**: `ITradingStrategy` → `IStrategyEngine` (implemented by `GridStrategyEngine`, `CompositeStrategyEngine`)

2. **Fix/remove EMA trend filter section**: `GridStrategyEngine.EvaluateAsync` does NOT check EMA(200), EMA(20) > EMA(50). It only checks: (1) grid config completeness, (2) 1H/4H candle availability, (3) LLM regime not `RiskOff`. EMA-based trend filtering only exists for Signal mode via `CompositeStrategyEngine` + `ConditionEvaluator`.

3. **Fix/remove bias filter section**: No VWAP, no RSI>50 check, no Price>VWAP in grid path.

4. **Fix/remove entry trigger section**: No explicit pullback detection or candlestick pattern matching in `GridStrategyEngine`.

5. **Remove hedge logic**: `OpenHedge`/`CloseHedge` are only referenced as "risk-reducing" in `LiveRiskEngine.IsRiskReducing()` but no code generates them.

6. **Fix deployment conditions**: Grid deployments are allowed in `Normal`, `Aggressive`, and `Defensive` regimes; only blocked in `RiskOff`.

7. **Add `MarketRegime` enum**: `Aggressive`, `Normal`, `Defensive`, `RiskOff` — drives strategy gating.

8. **Add Signal mode**: `StrategyMode.Signal` + `SignalController` + `CompositeStrategyEngine` as an alternative execution mode alongside Grid.

9. **Add DrawdownEvaluator**: `DrawdownTier` system for adaptive risk scaling during drawdowns.

10. **Add portfolio heat enforcement**: `RiskLimitsConfig.MaxPortfolioHeatPercent`.

11. **Add Future Recommendations**:
    - Implement EMA trend filter for grid mode
    - VWAP indicator integration
    - Hedge logic implementation
    - Additional strategy types (TrendBreakout, MeanReversion, FundingArbitrage)
    - Candlestick pattern recognition

---

### Task 3.2: Update `12-strategy-customisation.md` {#task-32-update-strategy-customisation}

Fix enum names and add missing features.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `.agent-context/0-knowledge/12-strategy-customisation.md` — update
- **Success**:
  - `RevisionSource` → `StrategyEntryPoint` enum values corrected
  - `PositionSizeType` naming corrected
  - StrategyRun/StrategyPerformance references removed
  - StrategyReview feature documented
  - HighWaterMarkUsd documented

#### Changes Required

1. **Fix `RevisionSource`**: Rename to `StrategyEntryPoint` with correct values: `UiBuilder`, `UiWizard`, `NaturalLanguage`, `PineImport`, `Migration`, `Optimizer`

2. **Fix `PositionSizeType`**: `percent_of_equity` → `PercentWallet` (serializes as `percent_wallet`)

3. **Remove `StrategyRun` and `StrategyPerformance` references**: These entities were never created

4. **Add StrategyReview feature**: `StrategyReviewDto` + `RequestStrategyReviewCommand` — AI-powered strategy review (separate from interpretation). `StrategyReview` domain entity at `src/TradingApp.Domain/Entities/StrategyReview.cs`

5. **Add `Strategy.HighWaterMarkUsd`**: Drawdown HWM persisted per strategy, updated via `Strategy.UpdateHighWaterMark()`

6. **Add Future Recommendations**: `StrategyRun` entity for live session tracking

---

### Task 3.3: Update `13-strategy-config-schema.md` {#task-33-update-strategy-config-schema}

Fix field names and add missing condition types.

- **Complexity**: Medium
- **Risk Factors**: Casing inconsistency bug exists in codebase — document clearly
- **Files**:
  - `.agent-context/0-knowledge/13-strategy-config-schema.md` — update
- **Success**:
  - `PriceVsEmaParams` field corrected (`Period` not `emaPeriod`, + `DistanceType`/`DistanceValue`)
  - `SupportResistance` condition type added
  - `TrendFilterConfig.Period` field added
  - EntryMode casing documented with bug note
  - `SwingLow` exit rule noted as enum-only (no evaluator logic)

#### Changes Required

1. **Fix `PriceVsEmaParams`**: `emaPeriod` → `Period`. Add undocumented fields: `DistanceType` and `DistanceValue`.

2. **Add `SupportResistance` condition type**: `EntryConditionType` enum has 4th value `SupportResistance`. `SupportResistanceParams` has fields: `Lookback`, `Strength`, `Operator`, `Tolerance`. Supported operators: `near_support`, `near_resistance`, `above_support`, `below_resistance`, `bounce_support`, `bounce_resistance`. Implemented by `SupportResistanceConditionHandler`.

3. **Add `TrendFilterConfig.Period`**: Additional `int?` field not in original doc.

4. **Fix EntryMode casing**: Domain constants in `EntryModes.cs` are PascalCase: `"AutoFromSignalCandle"`, `"WaitForLimitPrice"`, `"InitialMarketThenGrid"`. Add a **⚠️ KNOWN BUG** note: JSON default in `GridConfig.EntryMode` uses snake_case but `GridController` compares with PascalCase `EntryModes.WaitForLimitPrice` — potential runtime mismatch if config uses snake_case.

5. **Note `SwingLow` exit rule**: Enum value exists but no evaluator logic — falls through to fixed stop loss path.

6. **Add `IndicatorRequirement` model**: Used by `IndicatorExtractor` + `StrategyScheduler` for signal mode — determines which indicators need computing.

---

### Task 3.4: Update `14-strategy-runtime-model.md` {#task-34-update-strategy-runtime-model}

Expand pipeline interfaces and fix constructor details.

- **Complexity**: Medium
- **Risk Factors**: Interface signatures must match actual code exactly
- **Files**:
  - `.agent-context/0-knowledge/14-strategy-runtime-model.md` — update
- **Success**:
  - `IRiskEngine` expanded with all new methods/properties
  - `IMarketContextBuilder` signature updated (`BuildAsync` with `requiredIndicators` + `CancellationToken`)
  - Constructor takes `IStrategyConfig` (not JSON string)
  - StrategyRun/StrategyPerformance references removed
  - ISignalController added to pipeline
  - HighWaterMarkUsd documented

#### Changes Required

1. **Expand `IRiskEngine`**: Add `RecordPositionOpened(string symbol, decimal riskUsd)`, `UpdateDrawdownState(decimal, bool)`, `DrawdownScalingFactor` property, `IsDrawdownCircuitBreakerTripped` property.

2. **Fix `IMarketContextBuilder`**: Two `Build` overloads plus `BuildAsync` (with `requiredIndicators` and `CancellationToken`).

3. **Fix constructor**: `StrategyScheduler` takes a pre-resolved `IStrategyConfig` (not JSON string). Config is deserialized at session setup layer.

4. **Remove `StrategyRun`/`StrategyPerformance`**: These entities don't exist.

5. **Add `ISignalController`/`SignalController`**: Signal mode counterpart to `IGridController`, injected into `StrategyScheduler`.

6. **Add `Strategy.HighWaterMarkUsd`** and `UpdateHighWaterMark()`: Drawdown HWM persisted to DB via `IStrategyRepository`.

7. **Note fan-out**: Current Worker uses one `StrategyScheduler` per user session (single-tenant agent model) — not multi-user fan-out.

---

### Task 3.5: Update `15-grid-controller.md` {#task-35-update-grid-controller}

Fix signal outputs and state management.

- **Complexity**: Medium
- **Risk Factors**: Must clearly distinguish implemented vs aspirational signals
- **Files**:
  - `.agent-context/0-knowledge/15-grid-controller.md` — update
- **Success**:
  - `GridPlanner` reference removed (doesn't exist)
  - `Planning` state marked as unused
  - Output signals corrected (only `DeployGrid` + `TakeProfit`)
  - ProtectionOrders state documented
  - AtrInitial default multiplier fixed (2m not 3m)

#### Changes Required

1. **Remove `GridPlanner`**: Does not exist as a separate class. `GridController` calculates levels inline.

2. **Mark `Planning` state as unused**: Code goes directly `Inactive/Closed → Deploying`, skipping `Planning`.

3. **Fix output signals**: `GridController` only emits `DeployGrid` and `TakeProfit` (which also covers stop-loss triggers via `cancellationReason`). Remove: `CancelGrid` (only in BacktestPositionManager), `OpenHedge`, `AdjustHedge`, `CloseHedge`, `FlattenPosition`, `Cooldown` — none emitted by GridController.

4. **Add `ProtectionOrders` state**: `GridState.ProtectionOrders` (`ProtectionOrderState`) tracks exchange-native TP/SL trigger order IDs; cleared on position close.

5. **Add `TrailingStopHighWatermark` and `CandlesSinceEntry`**: Tracked for ATR trailing stop.

6. **Fix ATR multiplier defaults**: `AtrInitial` default = `2m` (not `3m`); `AtrTrailing` default = `3m`.

7. **Add `EstimateSignalRisk()`**: Private method for `estimatedRiskUsd` calculation.

8. **Fix PositionManager note**: Position sizing and grid lifecycle are handled by `GridController` itself, not separately by PositionManager.

---

### Task 3.6: Update `16-signal-contracts.md` {#task-36-update-signal-contracts}

Realign signal documentation with actual emissions.

- **Complexity**: Medium
- **Risk Factors**: Must clearly distinguish emitted vs dead signals
- **Files**:
  - `.agent-context/0-knowledge/16-signal-contracts.md` — update
- **Success**:
  - Hedge/Pause/Cooldown/Flatten signals marked as NOT IMPLEMENTED
  - `OpenPosition` signal added
  - `TakeProfit` payload expanded
  - Signal persistence table removed
  - `CancellationReason` enum documented

#### Changes Required

1. **Mark as NOT IMPLEMENTED**: `OpenHedge`, `AdjustHedge`, `CloseHedge` (listed in risk engine as risk-reducing but never emitted), `PauseStrategy` (never emitted), `Cooldown` (never emitted), `FlattenPosition` (referenced but never emitted).

2. **Add `OpenPosition` signal**: Emitted by `SignalController` for signal-mode strategies; handled by `LivePositionManager` and `BacktestPositionManager`.

3. **Expand `TakeProfit` payload**: Add `size`, `orderType`, `gridCycleId`, `cancellationReason` parameters. Note: stop-loss triggers emit `TakeProfit` signal type with `cancellationReason = StopLossTriggered`.

4. **Remove signal persistence table**: Signal lifecycle states (Generated → Validated → Approved → Executed) and storage table schema are NOT IMPLEMENTED. Signals are in-memory only.

5. **Add `CancellationReason` enum**: `GridRedeployed`, `TakeProfitTriggered`, `StopLossTriggered`, `LiquidationTriggered`, `TrailingStopTriggered`, `ManualCancel`.

6. **Add Future Recommendations**:
    - Implement signal persistence for audit trail
    - Implement hedge signals
    - Add signal analytics and history
    - Consider typed signal classes (currently string-based)

---

### Task 3.7: Update `19-scheduling-architecture.md` {#task-37-update-scheduling-architecture}

Fix constructor signature and add new pipeline features.

- **Complexity**: Medium
- **Risk Factors**: None
- **Files**:
  - `.agent-context/0-knowledge/19-scheduling-architecture.md` — update
- **Success**:
  - Constructor signature updated (IStrategyConfig, 11 params)
  - BuildAsync documented (not Build)
  - Drawdown state management documented
  - ISignalController dispatch documented
  - ResolveAccountEquity documented

#### Changes Required

1. **Fix constructor**: Takes `IStrategyConfig` (pre-deserialized), not `string strategyConfigJson`. Full constructor has 11 parameters including: `ISignalController?`, `decimal initialCapital`, `BacktestExecutionContextAccessor?`, `GridState?`, `IReadOnlyList<DrawdownTier>?`, `Strategy?`, `IStrategyRepository?`.

2. **Fix Build → BuildAsync**: `IMarketContextBuilder.BuildAsync` (async with `requiredIndicators` + `CancellationToken`).

3. **Add drawdown state**: `ApplyDrawdownStateAsync()` called every candle; uses `DrawdownEvaluator.Evaluate()`. HWM persisted to DB.

4. **Add `ISignalController` dispatch**: When `StrategyMode.Signal`, dispatches to `SignalController.ProcessAsync`.

5. **Add `ResolveAccountEquity()`**: Uses simulated engine equity in backtest mode, live account equity otherwise.

6. **Add `LastContext` property**: Exposes most recent `MarketContext` built.

7. **Note**: `ICandleClock` and `IStrategyScheduler` interfaces still not created. `StrategyExecutionCheckpoint` still not implemented.

## Phase Success Criteria

- All 7 strategy/execution knowledge files accurately describe implemented behavior
- Unimplemented features clearly labeled as "NOT IMPLEMENTED" or in "Future Recommendations"
- Interface signatures match actual code
- Signal emission status is accurately documented
