# Grid Controller

`GridController` owns the grid-mode lifecycle after `GridStrategyEngine` reports a valid setup. It converts a setup into `DeployGrid` or `TakeProfit` signals, maintains in-memory grid state, and applies candle-close exit logic for open positions.

## Responsibilities

The current controller is responsible for:

- validating whether a new grid can be deployed in the current lifecycle state;
- calculating grid deployment parameters inline;
- estimating signal risk for portfolio-heat enforcement;
- evaluating take-profit and stop-loss conditions for open positions;
- updating `GridState` as the cycle moves toward close.

There is no separate `GridPlanner` class in the current implementation.

## Interface

`IGridController.ProcessAsync(...)` receives:

| Parameter | Purpose |
|-----------|---------|
| `StrategyEvaluation evaluation` | Result from the strategy engine |
| `MarketContext context` | Trigger candle, indicators, equity, and regime context |
| `GridState gridState` | Shared in-memory lifecycle state |
| `PositionState positionState` | Current open-position view |
| `IStrategyConfig strategyConfig` | Typed strategy config |

It returns `IReadOnlyList<TradingSignal>`.

## Implemented Output Signals

The controller currently emits only two signal types:

| Signal | When emitted |
|--------|--------------|
| `DeployGrid` | When a new grid should be opened |
| `TakeProfit` | For normal take-profit exits and stop-loss-triggered exits |

The following are not emitted by `GridController`:

- `CancelGrid`
- `OpenHedge`
- `AdjustHedge`
- `CloseHedge`
- `FlattenPosition`
- `Cooldown`

Some of those names still appear elsewhere as reserved or risk-reducing signal types, but they are not produced by this controller.

## Lifecycle Model

`GridLifecycle` contains these states:

- `Inactive`
- `Planning`
- `Deploying`
- `Active`
- `PartiallyFilled`
- `FullyFilled`
- `Closing`
- `Closed`

### Actual Transition Pattern

The important runtime nuance is that `Planning` exists in the enum but is currently unused. The controller transitions directly from `Inactive` or `Closed` to `Deploying` when it emits a fresh `DeployGrid` signal.

Typical flow:

1. `Inactive` or `Closed` -> `Deploying` on a new setup.
2. Execution/fill processing moves the grid toward `PartiallyFilled` or `FullyFilled`.
3. Candle-close exit logic or final target placement moves the cycle to `Closing`.
4. Fill handling or session recovery logic eventually returns the cycle to `Closed`.

## Grid State Owned by the Controller

`GridState` contains more than lifecycle fields.

| Field | Purpose |
|-------|---------|
| `GridCycleId` | Correlates orders and exits for the active grid |
| `FilledLevels` / `TotalLevels` | Tracks ladder progress |
| `InitialRDollars` | Stores 1R for risk-based sizing |
| `AtrAtEntry` | Stores ATR captured at entry for `AtrInitial` stops |
| `TrailingStopHighWatermark` | High watermark used by ATR-trailing exits |
| `CandlesSinceEntry` | Warmup counter for ATR trailing stops |
| `ProtectionOrders` | Exchange-native TP/SL trigger order state |

### Protection Orders State

`GridState.ProtectionOrders` is a `ProtectionOrderState` object that tracks:

- stop-loss trigger order id and price;
- take-profit trigger order id and price;
- last update timestamp;
- whether any protection orders are active.

This is in-memory only and rebuilt from exchange state on worker recovery.

## Deployment Logic

When a new setup is detected and the lifecycle allows entry, the controller:

1. Resolves the effective entry mode and anchor price.
2. Computes stop-loss distance for risk-based sizing when needed.
3. Resolves notional sizing and applies drawdown scaling.
4. Computes leverage for risk-based auto-leverage flows.
5. Generates a new `GridCycleId` and resets state.
6. Emits one `DeployGrid` signal carrying the ladder metadata.

Important payload fields include:

- `anchorPrice`
- `gridLevels`
- `gridSpacingPercent`
- `notionalUsd`
- `gridCycleId`
- `entryMode`
- `leverage`
- `isIsolated`
- `estimatedRiskUsd`

## Risk-Based Sizing

For `RiskBased` strategies, the controller resolves stop distance through `StopLossDistanceResolver` and then sizes notional through `PositionSizeResolver`.

Supported stop-distance inputs:

| Stop type | Behaviour |
|-----------|-----------|
| `FixedPercent` | Uses configured percentage |
| `AtrTrailing` | Uses current ATR and trailing multiplier |
| `AtrInitial` | Uses ATR captured at entry; defaults to `2m` multiplier when not explicitly set |
| Fallback | May use `GridConfig.BreakdownThreshold` |

The current default multipliers are:

| Rule | Default multiplier |
|------|--------------------|
| `AtrInitial` | `2m` |
| `AtrTrailing` | `3m` |

Earlier documentation that claimed `AtrInitial` defaulted to `3m` is incorrect.

### Estimated Signal Risk

`GridController` includes a private `EstimateSignalRisk(...)` helper that calculates `estimatedRiskUsd` for the emitted `DeployGrid` signal. The risk engine uses that value for portfolio-heat checks.

## Exit Logic

When a position is open, the controller does not emit a new grid deployment. It instead evaluates exits.

Implemented exit paths:

| Exit path | Signal emitted | Cancellation reason |
|-----------|----------------|---------------------|
| Standard take profit | `TakeProfit` | `TakeProfitTriggered` |
| ATR trailing stop | `TakeProfit` | `TrailingStopTriggered` |
| Fixed stop loss | `TakeProfit` | `StopLossTriggered` |
| ATR initial stop | `TakeProfit` | `StopLossTriggered` |

For partially filled grids, the controller keeps remaining buy levels working and checks candle-close profit conditions against the latest average entry. Once fully filled, it can emit a limit-style full-position take-profit signal.

## Position Manager Interaction

The controller does not place or cancel exchange orders directly. It emits signals and leaves execution to the position manager.

In the current architecture:

- `BacktestPositionManager` interprets `DeployGrid`, `TakeProfit`, and `CancelGrid`.
- `LivePositionManager` interprets `DeployGrid`, `TakeProfit`, and `CancelGrid`, and may also coordinate trigger-order cleanup.

That said, sizing and grid-lifecycle decisions are owned by `GridController`, not by a separate position-sizing or planning component.

## Future Recommendations

- Remove or activate the unused `Planning` lifecycle state.
- Promote `DeployGrid` and `TakeProfit` payloads to typed signal contracts.
- Add first-class hedge or flatten behavior only if those flows are actually implemented end to end.
- Consider extracting a reusable planner if grid-shape calculation becomes more complex.