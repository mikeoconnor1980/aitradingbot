# Strategy Runtime Model

This document describes the implemented runtime path from a persisted strategy record to candle-close execution. The current model is centered on a single user session running a pre-deserialized `IStrategyConfig` through a shared evaluation pipeline.

## Runtime Layers

| Layer | Responsibility |
|-------|----------------|
| Domain | `Strategy` stores config JSON, active/running flags, and `HighWaterMarkUsd` |
| Authoring | `StrategyConfig` is the concrete JSON model implementing `IStrategyConfig` |
| Scheduling | `StrategyScheduler` builds context and orchestrates evaluation |
| Strategy evaluation | `IStrategyEngine` decides whether a setup exists |
| Derived-signal evaluation | `IDerivedSignalRegistry` evaluates candle-history-based entry conditions |
| Signal emission | `IGridController` or `ISignalController` converts evaluation into `TradingSignal` payloads |
| Risk | `IRiskEngine` validates signals and tracks portfolio state |
| Execution | `IPositionManager` forwards approved signals to `IExecutionEngine` |

## Strategy Entity Runtime State

The `Strategy` entity currently stores:

| Field | Runtime Use |
|-------|-------------|
| `Id` | Identity for repositories and revisioning |
| `UserId` | Tenant scope |
| `Name` | Display name |
| `StrategyType` | High-level category label |
| `ConfigJson` | Serialized strategy configuration |
| `Version` | Revision counter |
| `IsActive` | Soft-delete and availability flag |
| `IsRunning` | Write guard for some operations such as restore |
| `HighWaterMarkUsd` | Persisted drawdown high-water mark |

`Strategy.UpdateHighWaterMark(decimal)` persists the latest high-water mark so drawdown scaling can survive worker restarts.

The earlier `StrategyRun` and `StrategyPerformance` entities described in planning docs do not exist in the current codebase.

## Strategy Config at Runtime

The scheduler takes `IStrategyConfig` directly. It does not accept raw JSON in its constructor.

Deserialization happens before scheduler creation, typically during session setup. The runtime then treats the config as a typed contract with mode-specific behavior.

See [13-strategy-config-schema.md](13-strategy-config-schema.md) for the schema and [12-strategy-customisation.md](12-strategy-customisation.md) for persistence/versioning behavior.

## Execution Flow

The implemented runtime loop is:

1. Candle close event reaches `StrategyScheduler`.
2. `IMarketContextBuilder.BuildAsync(...)` builds `MarketContext`.
3. Scheduler resolves account equity and applies drawdown state.
4. `IStrategyEngine.EvaluateAsync(...)` returns `StrategyEvaluation`.
5. Scheduler routes to `ISignalController` for signal mode or `IGridController` for grid mode.
6. `IRiskEngine.ValidateAsync(...)` filters emitted signals.
7. `IPositionManager.ExecuteSignalsAsync(...)` places orders through the execution engine.

## Core Interfaces

The runtime depends on these shared abstractions in `src/TradePilot.Application/Abstractions/Services/`.

| Interface | Key Members | Purpose |
|-----------|-------------|---------|
| `IStrategyEngine` | `EvaluateAsync(MarketContext, IStrategyConfig, CancellationToken)` | Detect setup conditions |
| `IMarketContextBuilder` | `UpdateIndicators(Candle)`, two `Build(...)` overloads, `BuildAsync(...)` | Produce market context and indicator state |
| `IDerivedSignalRegistry` | `EvaluateAsync(string signalName, SignalRequest request, ISignalContext context, CancellationToken)` | Executes derived signal implementations |
| `IGridController` | `ProcessAsync(...)` | Grid-mode signal generation and lifecycle management |
| `ISignalController` | `ProcessAsync(...)` | Signal-mode entry and exit signal generation |
| `IRiskEngine` | `ValidateAsync(...)`, `UpdatePortfolioState(...)`, `UpdateDrawdownState(...)`, `RecordPositionOpened(...)`, `RecordPositionClosed(...)` | Enforce risk, circuit breakers, and portfolio heat |
| `IPositionManager` | `ExecuteSignalsAsync(...)` | Convert approved signals into orders |
| `IExecutionEngine` | Order placement and cancellation methods | Execution boundary for live and backtest |

### `IMarketContextBuilder`

The current interface has:

- `Build(Candle triggerCandle, Candle? latestOneHourCandle, Candle? latestFourHourCandle)`
- `Build(Candle triggerCandle, Candle? latestOneHourCandle, Candle? latestFourHourCandle, IReadOnlyList<IndicatorRequirement>? requiredIndicators)`
- `BuildAsync(Candle triggerCandle, Candle? latestOneHourCandle, Candle? latestFourHourCandle, IReadOnlyList<IndicatorRequirement>? requiredIndicators, CancellationToken cancellationToken = default)`

`BuildAsync` is the important runtime entry point because it supports asynchronous enrichment such as LLM context.

### `IRiskEngine`

The drawdown-aware risk engine contract now includes more than simple validation:

| Member | Purpose |
|--------|---------|
| `ValidateAsync(...)` | Approves or blocks signals |
| `UpdatePortfolioState(decimal accountEquity)` | Keeps equity current |
| `UpdateDrawdownState(decimal scalingFactor, bool isHalted)` | Applies scheduler-computed drawdown gating |
| `DrawdownScalingFactor` | Exposes current scaling factor |
| `IsDrawdownCircuitBreakerTripped` | Indicates drawdown halt state |
| `RecordPositionOpened(string symbol, decimal riskUsd)` | Tracks portfolio heat |
| `RecordPositionClosed(string symbol)` | Removes tracked heat on close |
| `RecordLoss(decimal lossUsd)` | Rolling loss/circuit-breaker input |
| `RecordOrdersPlaced(int count)` and `RecordOrdersClosed(int count)` | Open-order tracking |
| `Reset()` | Clears all session-scoped risk state (portfolio heat, circuit-breaker counters, drawdown); called by `AgentCheckInService` before each new session to prevent prior-session state from leaking across restarts |

## Runtime Models

Important runtime state models include:

| Model | Key Runtime Fields |
|-------|--------------------|
| `MarketContext` | Trigger candle, optional 1h/4h candles, `CandleHistory`, indicators, account equity, drawdown scaling, LLM context |
| `StrategyEvaluation` | `SetupDetected`, `Reason`, optional regime/filter data, `ConditionResults` |
| `GridState` | Lifecycle, cycle id, fill counts, ATR-at-entry, trailing-stop state, protection-order state |
| `PositionState` | Symbol, size, average entry, PnL, open/closed state |
| `TradingSignal` | `SignalType`, `Symbol`, `Reason`, and parameter bag |

### Signal-Mode Condition Flow

For signal mode, the runtime path is now:

1. `StrategyScheduler` builds `MarketContext`, including recent trigger-timeframe candle history.
2. `CompositeStrategyEngine` evaluates any enabled trend filter.
3. `ConditionEvaluator` dispatches each configured condition to a matching handler.
4. Simple indicator conditions evaluate directly against `MarketContext.Indicators`.
5. Derived conditions are handled by `DerivedSignalConditionHandler`, which adapts `MarketContext` into `ISignalContext` and calls `IDerivedSignalRegistry`.
6. `ConditionResult` entries are carried into `StrategyEvaluation.ConditionResults` for audit/debug use.
7. `SignalController` emits `OpenPosition` or exit signals if the aggregated evaluation passes and position state allows it.

## Strategy Scheduler Runtime Model

`StrategyScheduler` is the concrete runtime coordinator. Its constructor currently accepts:

- `IMarketContextBuilder`
- `IStrategyEngine`
- `IGridController`
- `IRiskEngine`
- `IPositionManager`
- `IStrategyConfig`
- optional trigger timeframe
- optional audit collector
- optional `ISignalController`
- `initialCapital`
- optional `BacktestExecutionContextAccessor`
- optional shared `GridState`
- optional drawdown tiers
- optional `Strategy`
- optional `IStrategyRepository`

The important distinction is that it receives a resolved config object and optional supporting runtime state, not a raw config JSON string.

## Drawdown and High-Water Mark Flow

Drawdown handling now sits directly in the scheduler:

1. Resolve account equity from live position state or simulated backtest equity.
2. Compare it against the stored or inferred high-water mark.
3. Call `DrawdownEvaluator.Evaluate(...)` with configured tiers.
4. Write the resulting scaling factor onto `MarketContext`.
5. Call `IRiskEngine.UpdateDrawdownState(...)`.
6. Persist `Strategy.HighWaterMarkUsd` through `IStrategyRepository` when a new high-water mark is reached.

This is one of the major differences from older docs that treated the risk layer as a passive validator.

## Fan-Out Model

The current worker is not a multi-subscriber fan-out engine in one scheduler instance. The runtime model is effectively one `StrategyScheduler` per user session in the worker/control-plane architecture.

That matters for planning because shared state such as `GridState`, `PositionState`, and `HighWaterMarkUsd` is session-local, not globally multiplexed inside a single scheduler.

## Future Recommendations

- Add explicit runtime session persistence if the product needs durable execution-session history.
- Introduce stronger typed signal contracts instead of string `SignalType` values.
- Add first-class interfaces for scheduler orchestration if external implementations are needed.
- Expand scheduler telemetry around high-water mark updates and drawdown transitions.