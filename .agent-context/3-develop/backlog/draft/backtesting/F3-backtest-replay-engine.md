# PBI Specification: F3 — Backtest Replay Engine

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-27
**PRD:** [candle-persistence-backtesting-prd.md](../../prd/candle-persistence-backtesting-prd.md)
**Implementation Phase:** 3
**Risk Level:** Medium
**Depends On:** F1 (Candle Data Persistence), StrategyEngine, GridController, RiskEngine, PositionManager (core trading pipeline — not yet implemented)

---

## Summary

Build the backtest replay engine that reads historical candle data from the local database and replays it sequentially through the live trading pipeline (CandleClock → StrategyScheduler → StrategyEngine → GridController → RiskEngine → PositionManager) using a simulated execution engine. This enables deterministic offline strategy validation using the same code as live trading.

### User Story

> As an **Operator**, I want to **run a backtest over a specified date range with a given strategy configuration** so that **I can measure how the grid strategy would have performed historically before risking capital**.

### Business Value

Proves the core architectural principle that the same pipeline works for both live and backtest modes. Enables strategy validation against ~3 years of historical data, de-risking strategy deployment and providing concrete performance metrics (PnL, drawdown, win rate) before trading live.

---

## Problem Statement

There is no mechanism to replay historical candle data through the trading pipeline. The core trading components (StrategyEngine, GridController, RiskEngine, PositionManager) are designed to be mode-agnostic, but no backtest harness exists to wire them with a replay data source and simulated execution. Without this, strategy performance can only be evaluated with live capital.

---

## Requirements

### Functional Requirements

#### CandleReplayEngine

- [ ] A `CandleReplayEngine` reads candles from the database (via `ICandleRepository`) in ascending time order for a given symbol, interval(s), and date range
- [ ] The replay engine feeds candles sequentially into the `CandleClock` to drive the strategy pipeline
- [ ] Multi-timeframe data is provided: 15m candles drive execution ticks; 1h and 4h candles provide trend/bias context via `MarketContext`
- [ ] All three timeframes (15m, 1h, 4h) must be present in the database for the requested date range; the runner fails fast with a descriptive error if any timeframe is missing
- [ ] Candle alignment across timeframes is handled — a 4h candle at time T covers 15m candles from T to T+3h45m; the replay engine provides the latest closed higher-timeframe candle at each 15m tick
- [ ] The replay engine emits candles one at a time (no lookahead bias — the strategy only sees data up to the current replay tick)

#### Indicator Warmup Period

- [ ] The replay engine supports a configurable warmup period at the start of the date range (e.g., 200 candles for EMA-200)
- [ ] During warmup, candles are fed into the `MarketContextBuilder` to populate indicator buffers, but no strategy signals are generated
- [ ] The backtest date range specified by the user is the evaluation range; the runner automatically loads additional candles preceding the start date to satisfy the warmup requirement
- [ ] If insufficient candle data exists before the start date to satisfy the warmup, the runner fails fast with a descriptive error

#### SimulatedExecutionEngine

- [ ] A `SimulatedExecutionEngine` implements the same execution interface as the live `ExecutionEngine` (e.g., `IExecutionEngine`)
- [ ] The simulated engine maintains an in-memory order book and position state
- [ ] Limit buy orders fill when the candle low ≤ order price
- [ ] Take profit (limit sell) orders fill when the candle high ≥ take profit price
- [ ] Hedge triggers activate when the candle close falls below the breakdown threshold; hedge fills at the candle close price (± slippage), matching live trigger semantics
- [ ] Configurable maker fee (default: 0.01%), taker fee (default: 0.035%) per Hyperliquid standard tier, and optional slippage (default: 0%)
- [ ] Fees are deducted from PnL on each simulated fill
- [ ] Slippage adjusts fill price away from the order price by the configured percentage
- [ ] All fills are all-or-nothing — partial fills are not supported in v1
- [ ] When multiple orders could fill on the same candle, buy orders fill first, then take-profit orders (conservative ordering to avoid phantom same-candle round-trips)
- [ ] The simulated engine tracks all fills with: fill time, fill price, side, size, fee paid
- [ ] Order cancellation is supported (e.g., when the grid is redeployed)

#### BacktestRunner

- [ ] A `BacktestRunner` (or `IBacktestRunner`) orchestrates a complete backtest run
- [ ] The runner creates fresh instances of CandleReplayEngine, SimulatedExecutionEngine, and all pipeline components for each run (stateless — no shared state between runs)
- [ ] The runner wires the pipeline: CandleReplayEngine → CandleClock → StrategyScheduler → StrategyEngine → GridController → RiskEngine → PositionManager → SimulatedExecutionEngine
- [ ] The runner accepts: symbol, interval(s), start date, end date, initial capital, and strategy configuration parameters
- [ ] The runner replays all candles in the date range and collects execution results
- [ ] The strategy can deploy, complete, and re-deploy grids as many times as conditions warrant during a single backtest run (multi-cycle)
- [ ] The `MarketContextBuilder` constructs the same `MarketContext` as in live trading, using historical candles for indicator calculation (moving averages, ATR, etc.)

#### BacktestMetricsCalculator

- [ ] A `BacktestMetricsCalculator` computes summary metrics from the list of simulated trades
- [ ] Metrics include: total trades, winning trades, losing trades, win rate (%), total PnL, max drawdown (absolute and %), average trade PnL, average hold time, number of hedges opened, total fees paid, number of grid cycles, final equity
- [ ] Max drawdown is calculated as the largest peak-to-trough equity decline during the backtest, expressed in both absolute value and percentage of peak equity
- [ ] Win rate is `winning trades / total trades * 100`

#### Equity Tracking

- [ ] The backtest tracks equity at each 15m tick (initial capital ± unrealised PnL ± realised PnL - fees)
- [ ] The equity time-series is included in the `BacktestResult` as a list of (timestamp, equity) pairs
- [ ] Initial capital is a required input in `BacktestConfig`

#### Trade Log

- [ ] The `BacktestResult` includes a complete ordered trade log
- [ ] Each `BacktestTrade` entry contains: trade ID, grid cycle ID, entry time, entry price, exit time, exit price, side, size, PnL, fees, trade type (grid fill, take profit, hedge open, hedge close)

### Non-Functional Requirements

- [ ] Backtest for 1 year of 15m data (~35K candles) completes in under 30 seconds
- [ ] The backtest engine is stateless — multiple backtests can run concurrently without interference
- [ ] The backtest engine produces identical results when run twice with the same inputs (deterministic)
- [ ] No live exchange API calls are made during a backtest — all data comes from the local database
- [ ] Memory usage remains reasonable for multi-year backtests (no unbounded allocations)

---

## User Flow

### Happy Path

1. Operator has ingested candle data via F2 (15m, 1h, 4h for BTC, covering the desired date range)
2. Backtest is triggered (via F4 API or directly via `IBacktestRunner`)
3. `BacktestRunner` loads candle data for all requested intervals from the database
4. CandleReplayEngine iterates through 15m candles in time order
5. Indicator warmup runs for the first N candles (e.g., 200) — no signals generated
6. At each tick, MarketContextBuilder provides the latest 1h and 4h context
7. StrategyEngine evaluates grid deployment signals; GridController manages grid lifecycle
8. SimulatedExecutionEngine fills orders based on candle OHLC (buy fills processed before TP fills):
   - Limit buy fills on low ≤ price
   - Take profit fills on high ≥ price
   - Hedge triggers on close < breakdown threshold
9. Grid completes and strategy re-deploys as conditions warrant (multi-cycle)
10. After all candles are replayed, BacktestMetricsCalculator computes summary metrics
11. BacktestResult is returned with metrics, equity time-series, and full trade log

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| No candle data in DB for requested range | Runner throws with descriptive error: "No candle data found for BTC/15m between {start} and {end}" |
| Missing higher-timeframe data (1h or 4h) | Runner throws with error indicating which timeframe is missing |
| Invalid strategy configuration | Runner validates config before starting replay; throws with config validation errors |
| Strategy produces no signals (no trades) | Backtest completes with zero trades; metrics show zeroes; trade log is empty |
| Date range too large for available data | Runner replays only available data within the range; metrics reflect actual replayed period |

---

## Technical Considerations

### Key Components

| Component | Layer | Action |
|-----------|-------|--------|
| `CandleReplayEngine` | `TradePilot.Application` | Reads candles from DB and emits them sequentially |
| `SimulatedExecutionEngine` | `TradePilot.Application` | In-memory order matching and fill simulation |
| `IBacktestRunner` / `BacktestRunner` | `TradePilot.Application` | Orchestrates the full backtest pipeline |
| `BacktestMetricsCalculator` | `TradePilot.Application` | Computes summary metrics from trade log |
| `BacktestResult` | `TradePilot.Application` | Result DTO containing metrics and trade log |
| `BacktestTrade` | `TradePilot.Application` | DTO for individual trade: entry/exit time, price, PnL, fees |
| `BacktestConfig` | `TradePilot.Application` | Configuration DTO for strategy parameters, fee model, slippage, initial capital |
| `EquitySnapshot` | `TradePilot.Application` | DTO for per-tick equity: timestamp + equity value |

### Pipeline Wiring (Backtest Mode)

| Component | Live Mode | Backtest Mode |
|-----------|-----------|---------------|
| Data Source | Hyperliquid WebSocket + REST | `CandleReplayEngine` (reads from DB) |
| Execution Engine | `HyperliquidExecutionEngine` | `SimulatedExecutionEngine` |
| Clock | Real-time `CandleClock` | Replay-driven `CandleClock` |
| StrategyEngine | Same | Same |
| GridController | Same | Same |
| RiskEngine | Same | Same |
| PositionManager | Same | Same |
| MarketContextBuilder | Same | Same |

### Fill Simulation Rules

| Order Type | Fill Condition | Fill Price |
|------------|---------------|------------|
| Limit Buy | Candle Low ≤ Order Price | Order Price (± slippage) |
| Take Profit (Limit Sell) | Candle High ≥ TP Price | TP Price (± slippage) |
| Hedge (Market) | Candle Close < Breakdown | Candle Close (± slippage) |

### Fee Model

```
Fill PnL = (Exit Price - Entry Price) × Size × Direction
Fee = Fill Size × Fill Price × Fee Rate
Net PnL = Fill PnL - Entry Fee - Exit Fee
```

### Multi-Timeframe Alignment

```
15m candle at T=12:00 →
  Latest closed 1h candle: 11:00 (covers 11:00-11:59)
  Latest closed 4h candle: 08:00 (covers 08:00-11:59)

15m candle at T=12:15 →
  Latest closed 1h candle: 12:00 (covers 12:00-12:59) — only if 12:00 candle has closed
  Latest closed 4h candle: 08:00 (still the latest closed)
```

---

## Out of Scope

- Backtest result persistence to database (results are returned in-memory only)
- Parameter sweep automation (future story — sequential-only for v1)
- Parallel backtest execution (future story — sequential-only for v1)
- SignalR progress reporting during long-running backtests
- Tick-level or order-book-level simulation
- Funding rate modelling in PnL
- Partial fill simulation (all-or-nothing in v1)
- Frontend visualisation of backtest results
- Multi-asset backtesting

---

## Resolved Decisions

| Decision | Resolution | Rationale |
|----------|------------|----------|
| Hedge fill price | Candle close (± slippage) | Matches live semantics where hedge triggers on "price closes below breakdown" |
| Missing HTF data | Fail fast with descriptive error | Avoids misleading backtest results from incomplete context |
| Partial fills | All-or-nothing | Simpler for v1; grid order sizes are small enough that full fills are realistic |
| Default fee tier | Hyperliquid standard: maker 0.01%, taker 0.035% | Matches most users; configurable via `BacktestConfig` |
| Indicator warmup | Required — 200+ candles loaded before evaluation range | Prevents inaccurate early signals from uninitialised indicators |
| Equity tracking | Per-tick (each 15m candle) | Enables accurate max drawdown and future equity curve charting |
| Initial capital | Required in `BacktestConfig` | Needed for position sizing, equity tracking, and percentage-based drawdown |
| Concurrency | Sequential only (v1) | Parallel runs deferred to future story |
| Grid redeployment | Multiple cycles per run | Strategy re-deploys grids as conditions warrant over long backtests |
| Intra-candle fill order | Buy fills first, then TP | Conservative ordering avoids phantom same-candle round-trips |
| Trade log | Full ordered log included | Required for debugging, analysis, and metrics calculation |

---

## Acceptance Criteria

- [ ] **Given** candle data exists for BTC 15m, 1h, and 4h in the database, **When** a backtest is triggered for a date range, **Then** candles are replayed in ascending time order through the full pipeline
- [ ] **Given** grid limit buy orders are placed by the strategy, **When** the candle low ≤ order price, **Then** the order fills in the SimulatedExecutionEngine
- [ ] **Given** take profit orders are placed, **When** the candle high ≥ TP price, **Then** the order fills in the SimulatedExecutionEngine
- [ ] **Given** a hedge trigger condition, **When** the candle close falls below the breakdown threshold, **Then** the hedge signal activates
- [ ] **Given** maker fee = 0.01% and taker fee = 0.035%, **When** a simulated fill occurs, **Then** the appropriate fee is deducted from PnL
- [ ] **Given** slippage = 0.05%, **When** a fill occurs, **Then** the fill price is adjusted away from the order price by the slippage percentage
- [ ] **Given** a backtest completes, **Then** the result includes: total trades, winning trades, losing trades, win rate, total PnL, max drawdown (absolute and %), average trade PnL, average hold time, hedges opened, total fees paid, grid cycles, final equity
- [ ] **Given** a backtest completes, **Then** the result includes a per-tick equity time-series and a complete ordered trade log
- [ ] **Given** the same inputs, **When** the backtest is run twice, **Then** the results are identical (deterministic)
- [ ] **Given** no candle data exists for the requested range, **When** the backtest is triggered, **Then** an error is returned indicating insufficient data
- [ ] **Given** 1h or 4h candle data is missing for the requested range, **When** the backtest is triggered, **Then** the runner fails fast with an error identifying which timeframe is missing
- [ ] **Given** an indicator warmup of 200 candles is required, **When** the backtest starts, **Then** the first 200 candles feed indicators only and no signals are generated
- [ ] **Given** insufficient candle data before the start date for indicator warmup, **When** the backtest is triggered, **Then** an error is returned indicating insufficient warmup data
- [ ] **Given** a grid completes (TP hit) mid-backtest, **When** conditions remain valid, **Then** the strategy re-deploys a new grid and the backtest continues
- [ ] **Given** multiple orders could fill on the same candle, **When** both a buy and TP qualify, **Then** the buy fills first
- [ ] **Given** initial capital of $10,000 in config, **When** the backtest runs, **Then** equity tracking starts at $10,000 and all metrics reference this starting capital
- [ ] **Given** 15m candles at time T, **When** the MarketContext is built, **Then** it includes the latest closed 1h and 4h candles at or before T

### Release Notes Information

- **Heading**: Backtest Replay Engine
- **Release note type**: Feature
- **Release Note Summary**: Replay historical candle data through the live trading pipeline with simulated execution, producing performance metrics including PnL, drawdown, win rate, and a full trade log.
- **Release Notes Audience**: Product
- **Breaking Change**: No

---

## Related Features

- **F1** — Candle Data Persistence provides the data source for the replay engine
- **F2** — Candle Ingestion Service populates the database with historical data
- **F4** — Backtest API exposes the replay engine via HTTP endpoints
