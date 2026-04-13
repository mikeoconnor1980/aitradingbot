# Strategy Runtime Model

This document defines how strategies run inside the trading system.

The runtime model separates:

Strategy definition  
Strategy configuration  
Strategy execution  
Strategy performance tracking

---

# Key Concepts

Strategy Plugin

Implemented in C# as a class that implements:

ITradingStrategy

Example:

GridStrategy

Future strategies may include:

TrendBreakoutStrategy  
MeanReversionStrategy

---

# Strategy

Represents a user-created strategy instance.

Fields:

Id  
Name  
StrategyType  
CreatedAt  
IsActive  
IsRunning (tracks live execution state; default false; prevents destructive operations like restore while true)

Example:

BTC Pullback Grid

Multiple strategies may exist but typically only one is active.

`Strategy.SetRunningState(bool)` updates the running flag; throws if setting `true` on an inactive strategy. Restore operations throw `ConflictException` (HTTP 409) when `IsRunning` is true.

Note: `IsRunning` is a stub in the POC phase — the worker does not yet update this property.

---

# StrategyConfig

JSON configuration stored in the database column `StrategyConfig.ConfigJson`.
At runtime, the JSON is deserialized into `TradingApp.Application.StrategyAuthoring.Models.StrategyConfig`
using `StrategyJsonOptions.Default` and passed through the pipeline as `IStrategyConfig`.

Key top-level fields used at runtime:
- `strategyMode` — discriminator (`grid` or `signal`)
- `market` — trading symbol
- `grid` — grid parameters (GridConfig)
- `exit` — take profit and stop loss rules (ExitConfig)
- `risk` — leverage, sizing, cooldown (RiskConfig)
- `entryConditions` — typed entry conditions for signal mode

See [Strategy Config Schema](13-strategy-config-schema.md) for full schema and sub-model reference.

---

# ActiveStrategy

At runtime the worker loads active strategies for all active subscribers.

Execution flow:

Worker Start  
↓  
Load all active subscribers  
↓  
For each subscriber:  
  Load ActiveStrategy  
  ↓  
  Load StrategyConfig JSON  
  ↓  
  Instantiate Strategy Plugin  
  ↓  
  Execute Strategy (using subscriber's keys)

---

# StrategyRun

Represents one runtime execution period for a strategy.

Fields:

UserId  
StrategyId  
StartTime  
EndTime  
Status

Example status:

Running  
Stopped  
Error

---

# StrategyPerformance

Stores performance metrics for each strategy.

Fields:

UserId  
StrategyId  
TotalTrades  
WinRate  
TotalPnL  
MaxDrawdown  
AverageTrade

These metrics allow comparison between strategy versions.

---

# Strategy Versioning

Users may create multiple versions of the same strategy.

Example:

BTC Pullback Grid v1  
BTC Pullback Grid v2  
BTC Pullback Grid v3

Only one version is active at a time.

This allows experimentation without losing historical results.

---

# Runtime Execution Loop

Worker execution loop (runs per subscriber on candle close):

Update Market Data (shared)  
↓  
Calculate Indicators (shared)  
↓  
For each active subscriber:  
  Load StrategyConfig  
  ↓  
  Execute Strategy Plugin  
  ↓  
  Generate Signals  
  ↓  
  Risk Engine Validation  
  ↓  
  Order Execution (using subscriber's keys)

---

# Strategy Safety

The strategy plugin never bypasses the risk engine.

All orders must pass:

Max exposure limits  
Daily loss limits  
Leverage limits

Risk enforcement happens after signals are generated.

Per-user risk limits are applied.
Platform-level risk limits are applied on top of per-user limits.

---

# Pipeline Interfaces

The execution pipeline is defined by thin interfaces in `src/TradingApp.Application/Abstractions/Services/`.
All of these are shared between live trading and backtesting:

| Interface | Key Method(s) | Purpose |
|-----------|--------------|----------|
| `IStrategyEngine` | `EvaluateAsync(MarketContext, IStrategyConfig) → StrategyEvaluation` | Detects valid setups |
| `IMarketContextBuilder` | `UpdateIndicators(Candle)` + `Build(trigger, 1h?, 4h?) → MarketContext` | Builds shared market context |
| `IGridController` | `ProcessAsync(evaluation, context, gridState, positionState, IStrategyConfig) → IReadOnlyList<TradingSignal>` | Grid lifecycle + signal emission |
| `IRiskEngine` | `ValidateAsync(signals)`; `UpdatePortfolioState(equity)`; `RecordPositionClosed(symbol)` | Filters signals against risk limits; tracks portfolio equity and per-symbol risk for portfolio heat enforcement |
| `IPositionManager` | `ExecuteSignalsAsync(approvedSignals)` | Routes approved signals to `IExecutionEngine` |
| `IExecutionEngine` | `PlaceOrderAsync`, `CancelOrderAsync`, `CancelAllOrdersAsync` | Execution boundary (live vs. simulated) |

Key model types in `src/TradingApp.Application/Trading/Models/`:

| Model | Key Properties |
|-------|----------------|
| `MarketContext` | `Symbol`, `TimestampUtc`, `CurrentCandle`, `LatestOneHourCandle?`, `LatestFourHourCandle?`, `Indicators` |
| `StrategyEvaluation` | `SetupDetected` (bool), `Reason` (string?) |
| `IndicatorSnapshot` | `EmaFast`, `EmaSlow`, `EmaTrend`, `Rsi`, `Atr` |
| `GridState` | `Lifecycle` (GridLifecycle), `GridCycleId?`, `FilledLevels`, `TotalLevels` |
| `PositionState` | `Symbol`, `Size`, `AverageEntryPrice`, `UnrealisedPnL`, `IsOpen` |
| `OrderRequest` | `Symbol`, `Side` (OrderSide), `OrderType`, `Price`, `Size`, `TradeType`, `ClientOrderId?` |

---

# Future Extensions

Possible future improvements:

Multi-strategy execution  
Strategy portfolio management  
A/B strategy testing  
Automated strategy optimisation