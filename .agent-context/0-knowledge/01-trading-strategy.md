# Trading Strategy

This project currently supports two execution styles behind a common strategy engine contract: grid mode and signal mode. Both paths run on confirmed candle closes, emit in-memory trading signals, and must pass through the risk engine before orders are placed.

## Architecture Overview

The runtime strategy path is selected from `StrategyConfig.StrategyMode` and evaluated through `IStrategyEngine`.

| Component | Purpose |
|-----------|---------|
| `IStrategyEngine` | Shared strategy contract used by the scheduler |
| `GridStrategyEngine` | Grid-mode setup detector |
| `CompositeStrategyEngine` | Routes between grid mode and signal mode |
| `SignalController` | Signal-mode entry and exit signal emission |
| `GridController` | Grid lifecycle and exit management |
| `IRiskEngine` | Portfolio heat, order count, drawdown, and circuit-breaker enforcement |
| `SyntheticRegimeProvider` | Backtest/live fallback that derives regime from indicators when LLM context is absent |

Execution flow:

Market data
-> `IMarketContextBuilder`
-> `IStrategyEngine`
-> `IGridController` or `ISignalController`
-> `IRiskEngine`
-> `IPositionManager`
-> `IExecutionEngine`

## Implemented Strategy Modes

| Mode | Engine | Controller | Entry Style |
|------|--------|------------|-------------|
| `Grid` | `GridStrategyEngine` via `CompositeStrategyEngine` | `GridController` | Deploys a ladder of buy orders around an anchor price |
| `Signal` | `CompositeStrategyEngine` + condition evaluation | `SignalController` | Opens a single position when all/any configured conditions pass |

## Grid Mode

Grid mode is much simpler than the original planning docs suggested. `GridStrategyEngine.EvaluateAsync` currently checks only:

1. Grid configuration completeness.
2. Availability of the latest 1h and 4h candles.
3. Market regime not equal to `RiskOff`.

If those conditions pass, the engine returns `SetupDetected = true` and the `GridController` decides whether to deploy a new grid or manage an open cycle.

### Regime Gating

The strategy uses `MarketRegime` to gate new entries.

| Regime | Behaviour |
|--------|-----------|
| `Aggressive` | Grid entries allowed |
| `Normal` | Grid entries allowed |
| `Defensive` | Grid entries allowed |
| `RiskOff` | New grid entries blocked |

`MarketRegime` is sourced from `MarketContext.LlmContext.DerivedRegime` when available. In backtests and fallback flows, `SyntheticRegimeProvider` derives it from EMA alignment, ATR percentile, and RSI.

### What Grid Mode Does Not Currently Implement

The following concepts were described in early documentation but are not part of the live grid path today:

| Concept | Status | Notes |
|---------|--------|-------|
| EMA trend filter for grid entries | NOT IMPLEMENTED | Grid mode does not check EMA(200) or EMA(20) > EMA(50) before entry |
| 1h bias filter | NOT IMPLEMENTED | No VWAP, RSI>50, or price-vs-VWAP checks in `GridStrategyEngine` |
| Pullback or candlestick trigger logic | NOT IMPLEMENTED | No reclaim/pattern detector in the grid path |
| Hedge entry/exit logic | NOT IMPLEMENTED | Hedge signals are referenced as risk-reducing types only; nothing emits them |

## Signal Mode

Signal mode is implemented and is the main place where indicator-driven entry conditions live.

`CompositeStrategyEngine` switches to signal evaluation when `StrategyMode.Signal` is selected. In that path it:

1. Optionally evaluates `TrendFilterConfig` when the filter is enabled and applies to the current direction.
2. Uses `IConditionEvaluator` to evaluate configured entry conditions.
3. Returns a `StrategyEvaluation` that is consumed by `SignalController`.

`SignalController` then:

1. Emits `OpenPosition` when a setup is detected and no position is open.
2. Emits `TakeProfit` for take-profit, fixed stop-loss, ATR-initial stop, or ATR-trailing stop exits.
3. Reuses the same risk-engine and position-manager pipeline as grid mode.

See [13-strategy-config-schema.md](13-strategy-config-schema.md) for the supported signal condition types.

## Timeframe Model

The runtime still uses a trigger candle plus higher-timeframe context, but the interpretation is different from the original plan.

| Timeframe | Runtime Use |
|-----------|-------------|
| Trigger timeframe, usually `15m` | Drives strategy evaluation on candle close |
| `1h` | Passed into market context as supporting context |
| `4h` | Passed into market context as supporting context |

Higher-timeframe candles are required for grid-mode evaluation, but they are not used as explicit EMA or bias filters in the current grid implementation.

## Risk Overlay

Strategy code never bypasses risk controls. The scheduler updates account equity, computes drawdown state, and then the risk engine validates emitted signals.

### Drawdown Evaluation

`DrawdownEvaluator` applies drawdown tiers against strategy equity versus a persisted high-water mark.

Default tiers from `RiskLimitsConfig`:

| Drawdown Threshold | Scaling Factor |
|--------------------|----------------|
| `5%` | `0.75` |
| `10%` | `0.50` |
| `15%` | `0.00` |

The resulting scaling factor is written onto `MarketContext.DrawdownScalingFactor`, consumed by sizing logic, and pushed into `IRiskEngine.UpdateDrawdownState()`.

### Portfolio Heat

Portfolio heat is implemented via `RiskLimitsConfig.MaxPortfolioHeatPercent`. New entry signals carrying `estimatedRiskUsd` are blocked if current tracked risk plus the new signal would exceed the configured share of account equity.

## Creating or Extending a Strategy Path

1. Decide whether the new behavior belongs in grid mode, signal mode, or a new `StrategyMode`.
2. Extend `IStrategyEngine` routing inside `CompositeStrategyEngine` if a new mode is needed.
3. Add or extend the controller that converts evaluations into `TradingSignal` payloads.
4. Ensure the signal payload carries sizing and risk metadata needed by `IRiskEngine` and `IPositionManager`.
5. Update [13-strategy-config-schema.md](13-strategy-config-schema.md), [14-strategy-runtime-model.md](14-strategy-runtime-model.md), and [16-signal-contracts.md](16-signal-contracts.md) together.

## Future Recommendations

- Implement a real EMA trend filter for grid mode instead of regime-only gating.
- Add VWAP-aware conditions and indicator support.
- Implement hedge signal generation if hedging remains a product goal.
- Add additional strategy families such as trend breakout, mean reversion, or funding-arbitrage strategies.
- Add explicit candlestick-pattern conditions if they are needed for signal mode or future grid entry refinement.