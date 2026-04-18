# Useful Direction for TradePilot Strategy Engine

## Big architectural recommendation

Use:

- **YAML** for strategy authoring
- **C# models** for runtime
- **validation + compilation** before activation

Do **not** evaluate raw YAML in the live engine.

---

## Why this matters

Trading systems fail in expensive ways when config is:
- ambiguous
- weakly validated
- interpreted differently in different parts of the stack

The safer approach is:

```text
YAML -> DTO -> Validation -> Domain Model -> Compiled Runtime Strategy
```

---

## Core engine boundary ideas

## Candle / market data layer
Responsible for:
- candle retrieval
- timeframe aggregation
- latest price snapshots
- volume / session awareness

## Indicator layer
Responsible for:
- EMA
- RSI
- ATR
- VWAP
- custom calculations

Avoid recomputing the same indicator separately per strategy.  
Use a shared registry keyed by:

- symbol
- timeframe
- indicator kind
- parameters

---

## Derived signal layer
Responsible for higher-level concepts such as:
- candle patterns
- slope state
- market structure
- liquidity sweep
- structure shift
- range state
- regime state

This becomes the key abstraction layer that keeps strategy YAML clean.

---

## Strategy compiler
Turn YAML-defined conditions into runtime evaluators.

Example:

```yaml
- lhs: price.close
  operator: ">"
  rhs: { type: indicator, id: ema50_trend }
  timeframe: 1h
```

should become something like:

```csharp
(ctx) => ctx.Price("1h").Close > ctx.Indicator("ema50_trend").Value
```

Compile once, not on every tick.

---

## Orchestrator
Responsible for:
- loading active strategies
- subscribing to required feeds
- evaluating strategies in correct sequence
- handling conflicts between strategies
- applying capital / exposure limits

This will matter a lot once signal + DCA + grid coexist.

---

## State stores
You should probably have separate state models per strategy type:

### Signal
- current position state
- pending order state
- last signal timestamp

### DCA
- activation state
- ladder levels
- filled count
- average entry
- total cost
- remaining budget

### Grid
- live range
- open buy levels
- open sell levels
- inventory
- realised grid profit
- break-even / hold price
- grid fill history

---

## Important conflict rule

A grid strategy and a directional signal strategy on the same symbol can easily fight each other.

Examples:
- signal strategy wants breakout long
- neutral grid is still selling upper ladder levels
- DCA is adding while short strategy is active

You need one of:
- account partitioning
- strategy priority
- strategy compatibility rules
- exposure coordinator

Without this, behaviour gets messy fast.

---

## Useful model idea: strategy capabilities

You may want each strategy subtype to expose capabilities like:

- directional
- inventory_based
- multi_order
- requires_state
- supports_backtest
- supports_tick_execution
- supports_candle_execution

That helps with orchestration and UI.

---

## Backtesting note

Backtesting requirements differ:

### Signal
Mostly event-based backtesting.

### DCA
Needs average cost and staged fill simulation.

### Grid
Needs accurate intra-range fill simulation, fee modeling, and inventory tracking.

A naive candle-close-only backtest can badly misrepresent grid behaviour.

---

## Practical MVP order

1. Signal strategies
2. DCA
3. Fixed-range grid
4. Auto-recentering or trend-biased grid later

That is the most realistic path.

---

## One final product thought

Users often think they want:
- “all strategies in one place”

What they actually need is:
- clear categories
- understandable risk
- state visibility
- strong guardrails

Especially for grid and DCA, surface these clearly:
- average entry
- total capital deployed
- next order level
- invalidation or pause condition
- fee-adjusted profitability