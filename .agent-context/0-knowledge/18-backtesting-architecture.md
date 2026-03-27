# Backtesting Architecture

This document describes how backtesting works for the trading platform.

The purpose of backtesting is to run the same strategy logic used in live trading
against historical market data so that performance can be measured before risking capital.

The backtesting engine must reuse the same core components as the live system wherever possible.

---

# Core Principle

Live trading and backtesting should share:

- StrategyEngine
- GridStrategy
- GridController
- GridPlanner
- RiskEngine
- PositionManager
- Signal contracts

The main difference is the source of market data and the execution engine.

Live mode uses Hyperliquid.
Backtest mode uses historical candles and a simulated execution engine.

---

# Practical Runtime Flow

Historical data
→ Candle replay engine
→ MarketContextBuilder
→ StrategyEngine
→ Signals
→ RiskEngine
→ PositionManager
→ SimulatedExecutionEngine
→ Backtest results

This should mirror the live pipeline as closely as possible.

---

# Historical Inputs

The backtest engine should consume:

- OHLCV candles
- optional funding data
- optional order book snapshots
- optional sentiment snapshots

Minimum viable backtesting input:

- 4H candles
- 1H candles
- 15m candles

The GridStrategy can run on these timeframes without needing tick-level data.

OHLCV candles are persisted to the database via `ICandleRepository` (interface: `src/TradingApp.Application/Abstractions/Repositories/ICandleRepository.cs`, implementation: `src/TradingApp.Persistence/Repositories/CandleRepository.cs`). The `HistoricalDataProvider` component should query `ICandleRepository.GetCandlesAsync(symbol, interval, startTime, endTime)` to supply the replay engine with ordered candle data.

---

# Replay Model

Backtesting should process historical candles sequentially.

Example:

1. Load candle history
2. Step forward candle by candle
3. Build MarketContext for each step
4. Run strategy
5. Process signals
6. Simulate fills and exits
7. Record PnL and state transitions

The replay must preserve time order.

---

# Execution Simulation

Backtesting does not place real orders.

Instead, a SimulatedExecutionEngine must:

- interpret signals
- simulate limit order placement
- simulate fills when price reaches levels
- simulate hedge opening and closing
- simulate take profit
- record fees and slippage assumptions

---

# Fill Logic

At minimum, a limit buy order fills when:

candle low <= order price

A take profit sell fills when:

candle high >= take profit price

A hedge trigger can be activated when:

price closes below the breakdown threshold

This is a simplification, but is sufficient for v1.

---

# Slippage and Fees

The backtest engine should model:

- maker fees
- taker fees
- optional slippage per fill

These should be configurable.

Without fees and slippage, results will look unrealistically strong.

---

# Backtest Result Model

The engine should record:

- total trades
- win rate
- total PnL
- max drawdown
- average trade
- average hold time
- number of hedges opened
- strategy mode usage

---

# Strategy Version Testing

Because strategies are stored as JSON configs, multiple strategy versions can be tested easily.

Example:

BTC Pullback Grid v1
BTC Pullback Grid v2
BTC Pullback Grid v3

Each version can be replayed over the same data range.

This allows comparison of configuration changes.

---

# Parameter Sweeps

The system can later support parameter sweeps.

Examples:

- EMA lengths
- grid spacing
- take profit percent
- hedge percent
- max exposure

A sweep runs many backtests automatically and compares performance.

---

# Suggested Components

BacktestRunner
HistoricalDataProvider
ReplayClock
BacktestContextBuilder
SimulatedExecutionEngine
BacktestMetricsCalculator
BacktestReportBuilder

---

# API and UI

The API can expose endpoints such as:

POST /backtests
GET /backtests/{id}
GET /backtests/{id}/results

The Angular UI can allow the user to:

- choose a strategy
- choose a date range
- run a backtest
- view performance metrics
- compare strategy versions

---

# Safety Principle

Backtesting must never call live exchange execution code.

Use a separate execution implementation:

IExecutionEngine
├ LiveExecutionEngine
└ SimulatedExecutionEngine

This prevents accidental live orders during testing.

---

# Recommended v1 Scope

For v1, keep it simple:

- candle-based replay
- one active strategy at a time
- simple fill logic
- configurable fees
- summary metrics
- equity curve

That is enough to validate the GridStrategy before live trading.

---

# Future Enhancements

Possible future improvements:

- tick-level replay
- order book simulation
- funding-aware PnL
- Monte Carlo runs
- walk-forward testing
- paper trading bridge