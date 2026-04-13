# Scheduling Architecture (CandleClock + StrategyScheduler)

The scheduler layer ensures strategies run on confirmed candle closes and that the same high-level pipeline can be used in both live trading and backtesting. The current implementation is centered on `CandleClock` producing `CandleClosedEvent` and `StrategyScheduler` orchestrating one user session at a time.

## Design Goals

The implemented design aims to:

- run strategy logic only on closed candles;
- keep candle detection separate from strategy logic;
- share market-context construction between live and backtest paths;
- support grid mode and signal mode from the same scheduler;
- apply drawdown and equity state before risk validation.

## Core Components

| Component | Purpose |
|-----------|---------|
| `CandleClock` | Deduplicates candle closes and emits `CandleClosedEvent` |
| `CandleClosedEvent` | Canonical scheduling trigger payload |
| `StrategyScheduler` | Builds context, applies drawdown state, evaluates strategy, and dispatches signals |

## CandleClock

`CandleClock` tracks the latest closed candle per symbol and timeframe and emits `CandleClosedEvent` once per close.

Important model notes:

- `Candle.Interval` is the timeframe string.
- `Candle.Timestamp` is the candle open time in Unix milliseconds.
- `CloseTimeUtc` on the event is derived, not read directly from the candle entity.

`CandleClock` is a concrete class. `ICandleClock` has not been introduced yet.

## CandleClosedEvent

The event carries:

| Field | Meaning |
|-------|---------|
| `Symbol` | Instrument symbol |
| `Timeframe` | Closed interval |
| `OpenTimeUtc` | Candle open timestamp |
| `CloseTimeUtc` | Derived close timestamp |
| `Candle` | The closed candle payload |

## StrategyScheduler Responsibilities

`StrategyScheduler` listens to candle-close events and drives the rest of the trading pipeline.

Current responsibilities:

1. Filter events to the configured trigger timeframe.
2. Derive required indicators for signal mode.
3. Call `IMarketContextBuilder.BuildAsync(...)`.
4. Resolve current account equity.
5. Apply drawdown state and persist high-water mark updates.
6. Call `IStrategyEngine.EvaluateAsync(...)`.
7. Route to `IGridController` or `ISignalController`.
8. Pass emitted signals through `IRiskEngine.ValidateAsync(...)`.
9. Execute approved signals through `IPositionManager`.

## Constructor Shape

The scheduler constructor no longer takes raw JSON. It takes a pre-resolved `IStrategyConfig` plus pipeline services and optional runtime collaborators.

Key constructor inputs include:

- `IMarketContextBuilder`
- `IStrategyEngine`
- `IGridController`
- `IRiskEngine`
- `IPositionManager`
- `IStrategyConfig`
- optional `ISignalController`
- `decimal initialCapital`
- optional `BacktestExecutionContextAccessor`
- optional `GridState`
- optional drawdown tiers
- optional `Strategy`
- optional `IStrategyRepository`

This is effectively an 11-plus-parameter constructor depending on which optional runtime services are supplied.

## Trigger Timeframe

The trigger timeframe defaults to `15m`, but the scheduler only cares about the configured `triggerTimeframe` string. It filters on `evt.Timeframe` and returns early for all other candle closes.

The higher timeframes are contextual inputs, not independent triggers.

## Market Context Construction

The runtime path uses `IMarketContextBuilder.BuildAsync(...)`, not the older synchronous-only `Build(...)` description.

For signal mode, the scheduler extracts `IndicatorRequirement` objects from the strategy config before calling the context builder. That allows the context builder to compute only the indicators needed by the configured conditions.

## Drawdown State Management

One of the major responsibilities now handled inside the scheduler is drawdown control.

### `ResolveAccountEquity()`

The scheduler resolves equity differently by execution mode:

| Mode | Equity source |
|------|---------------|
| Backtest | Simulated execution engine equity and PnL via `BacktestExecutionContextAccessor` |
| Live | Initial capital plus current `PositionState.UnrealisedPnL` |

### `ApplyDrawdownStateAsync()`

This method runs on every trigger candle and:

1. Loads the current or persisted high-water mark.
2. Calls `DrawdownEvaluator.Evaluate(...)`.
3. Writes `DrawdownScalingFactor` onto the market context.
4. Calls `IRiskEngine.UpdateDrawdownState(...)`.
5. Persists `Strategy.HighWaterMarkUsd` through `IStrategyRepository` when a new HWM is set.

## Mode Dispatch

The scheduler contains the runtime dispatch point between strategy modes.

| Condition | Controller used |
|-----------|-----------------|
| `StrategyMode.Signal` and `ISignalController` supplied | `SignalController.ProcessAsync(...)` |
| Otherwise | `GridController.ProcessAsync(...)` |

This means signal mode is a first-class scheduling path, not an external or future extension.

## State Accessors

The scheduler exposes:

| Member | Purpose |
|--------|---------|
| `UpdateState(GridState, PositionState)` | Refreshes scheduler-owned state before processing a candle |
| `GetGridState()` | Returns the shared grid state |
| `LastContext` | Exposes the most recent `MarketContext` built |

`LastContext` is useful for diagnostics and downstream integrations that need the latest indicator snapshot.

## Live and Backtest Flow

### Live Trading

WebSocket updates feed candle construction and close detection. On each relevant close, the scheduler builds context, evaluates the active strategy for that user session, validates signals, and forwards them to the live position manager.

### Backtesting

Replay feeds historical candles into the same scheduling pattern. The key difference is that equity and execution state come from the simulated engine rather than a live account service.

## Duplicate Execution Protection

`CandleClock` deduplication is implemented. However, some of the larger persistence ideas from the original design are still not in place:

- `StrategyExecutionCheckpoint` is not implemented.
- `IStrategyScheduler` is not implemented.

Those remain design ideas rather than shipped runtime components.

## Future Recommendations

- Add durable execution checkpoints if restart-safe once-per-candle guarantees become mandatory.
- Introduce `ICandleClock` and `IStrategyScheduler` if multiple implementations or testing seams are needed.
- Expand scheduler diagnostics around `LastContext`, equity changes, and drawdown transitions.
- Consider a clearer separation between per-session orchestration and any future multi-session fan-out coordinator.