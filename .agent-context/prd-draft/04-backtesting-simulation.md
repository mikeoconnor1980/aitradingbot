# PRD: Backtesting & Simulation

**Status:** Draft  
**Priority:** High (proves the trading engine works before risking capital)  
**Date:** 2026-04-01  
**Depends on:** PRD-03 (Trading Engine) provides the pipeline this PRD exercises  
**Depended on by:** Live trading (future PRD) requires paper-trading burn-in first  

---

## 1. Background & Context

### Problem Statement

The trading engine (PRD-03) must be validated before any capital is at risk. Two simulation modes are needed:
1. **Backtesting** — replay historical candles through the engine to measure strategy performance
2. **Paper trading** — run the engine on live market data with simulated execution to prove real-time behaviour

Both modes reuse the exact same pipeline (`GridStrategy` → `GridController` → `RiskEngine` → `PositionManager`). The only difference is the data source and execution engine.

### Current State

- Historical candle ingestion exists (Binance and Hyperliquid endpoints in POC)
- `CandleClock` and `StrategyScheduler` are designed to work identically in backtest and live modes
- `SimulatedExecutionEngine` concept is defined in the backtesting architecture but not yet implemented
- No paper trading mode exists

### Why Both in One PRD

Backtesting and paper trading share:
- The same trading pipeline (PRD-03)
- The same `SimulatedExecutionEngine` (paper trading uses live prices but simulated fills)
- The same metrics calculation (`BacktestMetricsCalculator`)
- The same result model (`BacktestResult`)

Paper trading is essentially "backtesting on live data." Separating them would create artificial boundaries.

---

## 2. Goals & Objectives

### Business Goals

| ID | Goal | Success Metric |
|----|------|---------------|
| BG-1 | Prove strategy logic before risking capital | Backtests run end-to-end and produce reproducible PnL, drawdown, and trade logs |
| BG-2 | Validate real-time behaviour before going live | Paper trading runs continuously for a defined burn-in period without errors |
| BG-3 | Enable iterative strategy parameter tuning | Users can run multiple backtests with different parameters and compare results |

### User Goals

| ID | Goal | Description |
|----|------|-------------|
| UG-1 | Run a backtest on historical data | Configure a strategy, select a date range, and get performance results |
| UG-2 | Compare backtest results across parameter sets | View PnL, drawdown, win rate, trade count side-by-side |
| UG-3 | Run paper trading on live data | Start a paper-trading session and observe strategy behaviour in real time |
| UG-4 | Trust that backtest results are realistic | Fees, slippage, and fill simulation produce results close to real execution |

### Non-Goals

| ID | Non-Goal | Rationale |
|----|----------|-----------|
| NG-1 | Live order placement | Paper trading simulates only; live trading is a future PRD |
| NG-2 | Strategy optimisation / parameter search | Manual comparison only; automated parameter sweeps are future work |
| NG-3 | Multi-symbol backtesting | BTC perpetual only |
| NG-4 | Tick-level simulation | Candle-based (OHLCV) simulation only — sufficient for grid strategy |

---

## 3. Scope

### Phase 1 — Deterministic Backtesting

#### Historical Data

- Candle storage for 15m, 1H, 4H BTC data (persisted via `ICandleRepository`)
- Ingestion pipeline from Binance (primary, longer history) and Hyperliquid
- Candles stored with `Source` field to distinguish providers
- `HistoricalDataProvider` queries `ICandleRepository.GetCandlesAsync(symbol, interval, startTime, endTime)`

#### Replay Engine

- `CandleReplayEngine` loads 15m/1h/4h candles in parallel, aligns higher-timeframe starts, determines warmup boundary
- Sequential candle-by-candle replay preserving time order
- `CandleClock` (same as live) emits `CandleClosedEvent` per candle
- Warmup period (default: 200 candles) seeds indicator state before evaluation starts
- Higher-timeframe alignment prevents look-ahead bias

#### Simulated Execution Engine

- `SimulatedExecutionEngine` — Application layer, no I/O, fresh instance per run
- Limit buy fills when `candle.Low <= orderPrice`
- Take profit sell fills when `candle.High >= takeProfitPrice`
- Hedge trigger on price close below breakdown threshold
- Fill priority: buys processed before sells within each candle
- FIFO trade pairing: `GridFill` → `TakeProfit`, `HedgeOpen` → `HedgeClose`
- Fee model: maker rate (default 0.01%), taker rate (default 0.035%), configurable slippage
- Fees deducted at fill time into `SimulatedPosition.RealisedPnL`

#### Backtest Orchestration

`BacktestRunner` orchestrates five phases:
1. **Validation** — guards on `BacktestConfig` (symbol, dates, capital, intervals)
2. **Data load** — fetch candles in parallel, align timeframes
3. **Warmup** — feed candles to `MarketContextBuilder.UpdateIndicators()` to seed state
4. **Evaluation loop** — per 15m candle: process fills → update indicators → strategy evaluation → risk check → execute signals → record equity
5. **Metrics** — `BacktestMetricsCalculator.Calculate()` → `BacktestResult`

#### BacktestConfig

| Field | Type | Description |
|-------|------|-------------|
| `Symbol` | string | Trading symbol (e.g., `BTC`) |
| `Intervals` | list | Must include `15m`, `1h`, `4h` |
| `StartDateUtc` | long | Unix ms — start of evaluation period |
| `EndDateUtc` | long | Unix ms — end of evaluation period |
| `InitialCapital` | decimal | Starting equity |
| `FeeModel` | FeeModel | Maker/taker rates, slippage |
| `WarmupPeriod` | int | Default 200 (15m candles) |
| `StrategyConfigJson` | string | Canonical JSON from PRD-02 |
| `EnableAuditLog` | bool | Default true — per-candle audit |

#### BacktestResult

| Field | Type |
|-------|------|
| `TotalTrades` | int |
| `WinningTrades` / `LosingTrades` | int |
| `WinRate` | decimal (0–100) |
| `TotalPnL` | decimal |
| `MaxDrawdownAbsolute` / `MaxDrawdownPercent` | decimal |
| `AverageTradePnL` | decimal |
| `AverageHoldTime` | TimeSpan |
| `HedgesOpened` | int |
| `TotalFeesPaid` | decimal |
| `GridCycles` | int |
| `FinalEquity` | decimal |
| `EquityTimeSeries` | list of `(TimestampUtc, Equity)` |
| `TradeLog` | list of `BacktestTrade` |

#### Persistence

- Completed runs persisted as `BacktestRun` entities
- CQRS commands/queries: `RunBacktestCommand`, `GetBacktestResultQuery`, `GetBacktestListQuery`, `GetCandleCoverageQuery`
- Audit log (per-candle, order events, grid cycles) stored as JSON blob columns

### Phase 2 — Paper Trading

#### Live Market Data Connection

- `CandleClock` triggers on confirmed candle closes from Hyperliquid WebSocket
- `StrategyScheduler` builds shared `MarketContext` from live data, fans out to subscribers
- Live `MarketStateStore` updates continuously from WebSocket

#### Paper Trading Mode

- Same pipeline: `GridStrategy` → `GridController` → `RiskEngine` → `SimulatedExecutionEngine`
- `SimulatedExecutionEngine` uses live market prices for fills (not historical candle OHLC)
- Per-user execution checkpoints persisted to database
- Run history and paper-trade metrics recorded using same `BacktestResult` model

#### Worker Service

- `TradePilot.Worker` background service orchestrates strategy execution
- Restart recovery: checkpoint persistence ensures no duplicate signal generation
- Paper mode flag: provably distinct from live — no accidental real orders

#### UI

- Basic backtest UI (Phase 1): configure strategy params, select date range, run backtest, view results
- Paper trading dashboard (Phase 2): active strategy state, simulated positions, signal log, start/stop
- View paper trade performance alongside backtest results for comparison

---

## 4. Technical Considerations

### Architecture Position

```
Historical Candles (backtest) / Live WebSocket (paper)
        ↓
   CandleReplayEngine / MarketStateStore
        ↓
   CandleClock (same component)
        ↓
   StrategyScheduler (same component)
        ↓
   Trading Engine Pipeline (PRD-03)
        ↓
   SimulatedExecutionEngine (backtest & paper)
        ↓
   BacktestMetricsCalculator → BacktestResult
```

### What Changes Between Modes

| Component | Backtest | Paper Trading | Live (future) |
|-----------|----------|--------------|----------------|
| Data source | Historical candles | Hyperliquid WebSocket | Hyperliquid WebSocket |
| CandleClock trigger | Replay engine | Real-time candle close | Real-time candle close |
| Execution engine | `SimulatedExecutionEngine` (candle OHLC) | `SimulatedExecutionEngine` (live prices) | `HyperliquidExecutionEngine` |
| Persistence | `BacktestRun` entity | Paper trade run entity | Live trade records |

### Constraints

| Constraint | Detail |
|-----------|--------|
| **One symbol** | BTC perpetual only |
| **One strategy** | `GridStrategy` only |
| **One user** | Single hardcoded identity |
| **Candle-based** | OHLCV — no tick-level simulation |
| **No live orders** | Paper mode must be provably incapable of placing real orders |

---

## 5. Acceptance Criteria

### Backtesting

- [ ] Historical BTC candles can be ingested from Binance and stored reliably
- [ ] `CandleReplayEngine` loads and aligns 15m/1h/4h candles correctly
- [ ] Warmup period seeds indicator state without generating signals
- [ ] Backtests run end-to-end without touching live exchange code
- [ ] Repeated runs over the same data produce identical outputs (deterministic)
- [ ] Results include PnL, drawdown, win rate, trade log, equity time series
- [ ] Fee model affects results (not ignored)
- [ ] Higher-timeframe alignment prevents look-ahead bias
- [ ] Audit log captures per-candle state and grid cycle events
- [ ] Basic UI allows configuring, running, and viewing backtest results

### Paper Trading

- [ ] Strategy executes exactly once per confirmed candle close in paper mode
- [ ] Worker restarts do not cause duplicate signal generation
- [ ] Execution checkpoints persist to database and survive worker restart
- [ ] Simulated fills use live market prices
- [ ] Paper trade results (PnL, drawdown, signals) are recorded and viewable
- [ ] Clear separation: paper mode cannot accidentally place live orders
- [ ] Paper trading can run continuously for a defined burn-in period

---

## 6. References

| Document | Path |
|----------|------|
| Backtesting Architecture | [18-backtesting-architecture.md](../../0-knowledge/18-backtesting-architecture.md) |
| Scheduling Architecture | [19-scheduling-architecture.md](../../0-knowledge/19-scheduling-architecture.md) |
| Development Plan | [08-development-plan.md](../../0-knowledge/08-development-plan.md) |
| Binance Integration | [23-binance-integration.md](../../0-knowledge/23-binance-integration.md) |
| Grid Engine Explained | [24-backtesting-grid-engine-explained.md](../../0-knowledge/24-backtesting-grid-engine-explained.md) |
| Trading Engine (pipeline this PRD exercises) | [03-trading-engine.md](03-trading-engine.md) |
| Strategy Input Pipeline (provides strategy configs) | [02-strategy-input-pipeline.md](02-strategy-input-pipeline.md) |
