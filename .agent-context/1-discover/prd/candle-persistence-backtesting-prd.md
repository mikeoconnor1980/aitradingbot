# PRD: Historical Candle Data Persistence and Offline Backtesting

**Status:** Draft  
**Author:** AI PRD Writer  
**Created:** 2026-03-26  
**Last Updated:** 2026-03-26  
**Version:** 0.1  

---

## 1. Executive Summary

This PRD defines the requirements for two foundational capabilities of the AI Grid Trading System: **historical candle data persistence** and **offline backtesting**. Together, these features enable the platform to store OHLCV market data locally in a SQLite database and replay that data through the existing trading pipeline (StrategyEngine → GridController → RiskEngine → PositionManager) using a simulated execution engine. This eliminates dependency on live exchange data for strategy validation, enables deterministic performance measurement, and provides the foundation for strategy optimisation and parameter sweeps.

The scope covers four features:
1. **Candle Data Persistence** — Domain entity, EF Core DbContext, SQLite storage
2. **Candle Ingestion Service** — Batch fetch from Hyperliquid, upsert to DB
3. **Backtest Replay Engine** — Sequential candle replay through the live trading pipeline with simulated execution
4. **Backtest API** — HTTP endpoint to trigger and retrieve backtest results

---

## 2. Background & Context

### Problem Statement

The platform currently fetches candle data from Hyperliquid's REST API on demand (`candleSnapshot` endpoint) but does not persist it. Every candle request hits the exchange. There is no way to run strategies against historical data to measure performance before risking capital.

### Current State

| Capability | Status |
|---|---|
| Hyperliquid REST client (`HyperliquidRestClient.GetCandlesAsync`) | Implemented — fetches up to 500 candles per request |
| Infrastructure models (`HyperliquidCandle`, `HyperliquidCandleSnapshotRequest`, `CandleSnapshotPayload`) | Implemented |
| Application DTO (`CandleDto` — Timestamp, OHLCV) | Implemented |
| `HyperliquidAssetMapper` (asset name mapping, timeframe → interval ms) | Implemented |
| Persistence layer (`TradePilot.Persistence`) | Empty shell — no DbContext, no entities, no migrations |
| Domain layer (`TradePilot.Domain`) | Empty shell — no entities |
| Backtesting engine | Not started |
| StrategyEngine, GridController, RiskEngine, PositionManager | Not yet implemented (defined in architecture docs) |
| Signal contracts (DeployGrid, TakeProfit, OpenHedge, etc.) | Defined in architecture — not yet implemented |

### Opportunity

- **De-risk strategy deployment**: Validate grid strategy performance against ~3 years of BTC data before trading live
- **Reduce API calls**: Cached candle data eliminates redundant exchange requests
- **Enable iteration**: Parameter sweeps and A/B strategy comparison become possible
- **Prove architecture**: Demonstrates that the same pipeline works for both live and backtest modes — a core architectural principle

### Data Volume Estimate

| Interval | Candles/year | ~3 years | Row size (est.) |
|---|---|---|---|
| 15m | 35,040 | ~105,120 | ~120 bytes |
| 1h | 8,760 | ~26,280 | ~120 bytes |
| 4h | 2,190 | ~6,570 | ~120 bytes |
| **Total** | | **~137,970** | **~16 MB** |

This is trivially small for SQLite. No partitioning, sharding, or archival strategy is needed.

---

## 3. Goals & Objectives

### Business Goals

| ID | Goal | Success Metric |
|---|---|---|
| BG-1 | Validate grid strategy performance before risking capital | At least one complete backtest run across 1+ year of BTC data producing a meaningful result set |
| BG-2 | Reduce reliance on live exchange data for development | Candle data available locally for all three timeframes (15m, 1h, 4h) covering available history |
| BG-3 | Enable rapid strategy iteration | Ability to run a backtest and receive results in under 30 seconds for a 1-year date range |

### User Goals

| ID | Goal | Success Metric |
|---|---|---|
| UG-1 | As a trader, I can ingest all available BTC candle history from Hyperliquid in a single operation | Ingestion completes successfully for 15m, 1h, and 4h intervals |
| UG-2 | As a trader, I can run a backtest against a specified date range and strategy config | API returns a complete backtest result with trades, PnL, drawdown, win rate |
| UG-3 | As a trader, I can compare strategy parameter variations | Multiple backtests can run over the same data range with different configs |

### Non-Goals

| ID | Non-Goal | Rationale |
|---|---|---|
| NG-1 | Tick-level or order book data persistence | Candle-level simulation is sufficient for the grid strategy; tick data would be 100x+ more storage |
| NG-2 | Real-time candle persistence (auto-sync on each candle close) | Out of scope for this feature; can be added later as a Worker enhancement |
| NG-3 | Multi-asset support beyond BTC | The platform trades BTC perpetuals only in the current phase |
| NG-4 | Frontend UI for backtesting | API-only for this PRD; UI can be built in a subsequent feature |
| NG-5 | Parameter sweep automation | The backtest API supports single runs; automated sweep tooling is a future enhancement |
| NG-6 | Multi-user/tenant scoping for candle data | Candle data is market data (shared, not tenant-scoped). Backtest results may be tenant-scoped in future |
| NG-7 | Funding rate, order book, or sentiment data persistence | Future enhancement as defined in the backtesting architecture doc |

---

## 4. Scope

### In Scope

| Area | Details |
|---|---|
| Domain entity | `Candle` entity in `TradePilot.Domain` |
| Database setup | EF Core DbContext with SQLite provider in `TradePilot.Persistence` |
| Candle ingestion | Batch fetch service with pagination, upsert, rate-limit handling |
| Backtest engine | `CandleReplayEngine`, `SimulatedExecutionEngine`, `BacktestRunner`, `BacktestMetricsCalculator` |
| Backtest API | `POST /api/backtests` endpoint with result model |
| Fill simulation | Limit buy fills on candle low ≤ price; take profit fills on candle high ≥ price |
| Fee modelling | Configurable maker/taker fees and optional slippage |

### Out of Scope

- Frontend backtest UI (Angular components, charts, result visualisation)
- Automated candle sync (Worker-driven continuous ingestion)
- Parameter sweep automation
- Multi-asset candle support
- Backtest result persistence (results returned synchronously; DB storage is a future enhancement)
- SignalR progress reporting for long-running backtests (optional/future)

### Future Considerations

- Persist backtest results to DB for historical comparison
- Worker-based continuous candle sync on each confirmed candle close
- Funding rate data ingestion for more accurate PnL modelling
- Angular backtest UI with chart overlay of entry/exit points
- Parameter sweep runner with comparison dashboard

---

## 5. Requirements

### 5.1 Functional Requirements

#### Feature 1: Candle Data Persistence

| ID | Requirement | Priority |
|---|---|---|
| F1-01 | A `Candle` domain entity exists in `TradePilot.Domain` with properties: `Id` (long, auto-increment), `Symbol` (string), `Interval` (string), `Timestamp` (long, unix ms — candle open time), `Open` (decimal), `High` (decimal), `Low` (decimal), `Close` (decimal), `Volume` (decimal), `NumTrades` (int) | Must |
| F1-02 | A `TradePilotDbContext` exists in `TradePilot.Persistence` using the `Microsoft.EntityFrameworkCore.Sqlite` provider | Must |
| F1-03 | A composite unique index exists on (`Symbol`, `Interval`, `Timestamp`) to prevent duplicate candle entries | Must |
| F1-04 | EF Core migrations are used to create and evolve the database schema | Must |
| F1-05 | The SQLite database file path is configurable via `appsettings.json` (default: `Data/TradePilot.db`) | Must |
| F1-06 | An `ICandleRepository` interface is defined in `TradePilot.Application` with methods for querying and bulk-inserting candles | Must |
| F1-07 | A `CandleRepository` implementation exists in `TradePilot.Persistence` | Must |

#### Feature 2: Candle Ingestion Service

| ID | Requirement | Priority |
|---|---|---|
| F2-01 | An `ICandleIngestionService` interface is defined in `TradePilot.Application` | Must |
| F2-02 | The service fetches candles from Hyperliquid in batches (up to 500 candles per request, paginating forward by time) | Must |
| F2-03 | The service supports ingestion for BTC across 15m, 1h, and 4h intervals | Must |
| F2-04 | Duplicate candles are skipped (upsert semantics using the composite unique index) | Must |
| F2-05 | Rate limiting is handled gracefully — the service pauses between batch requests (configurable delay, default 200ms) | Must |
| F2-06 | The service reports progress: total candles fetched, total inserted, total skipped, per-interval counts | Should |
| F2-07 | An API endpoint `POST /api/candles/ingest` triggers ingestion with parameters: `symbol`, `intervals[]`, `startTime` (optional), `endTime` (optional) | Must |
| F2-08 | The ingestion endpoint returns a summary of the ingestion result | Must |
| F2-09 | If `startTime` is omitted, ingestion starts from the latest candle timestamp in the DB for that symbol/interval (or from the earliest available date on Hyperliquid if the DB is empty) | Should |
| F2-10 | Ingestion is idempotent — running it multiple times for the same range produces no duplicates | Must |

#### Feature 3: Backtest Replay Engine

| ID | Requirement | Priority |
|---|---|---|
| F3-01 | A `CandleReplayEngine` reads candles from the database in ascending time order for a given symbol, interval(s), and date range | Must |
| F3-02 | The replay engine feeds candles sequentially into the `CandleClock` → `StrategyScheduler` → `StrategyEngine` → `GridController` → `RiskEngine` → `PositionManager` pipeline | Must |
| F3-03 | A `SimulatedExecutionEngine` implements the same execution interface as the live `ExecutionEngine`, but simulates order placement and fills in-memory | Must |
| F3-04 | Limit buy orders fill when the candle low ≤ order price | Must |
| F3-05 | Take profit sell orders fill when the candle high ≥ take profit price | Must |
| F3-06 | Hedge triggers activate when the candle close falls below the breakdown threshold | Must |
| F3-07 | Configurable maker fee (default: 0.01%), taker fee (default: 0.035%), and optional slippage (default: 0) | Must |
| F3-08 | The `MarketContextBuilder` constructs the same `MarketContext` as in live trading, using historical candles for indicator calculation | Must |
| F3-09 | Multi-timeframe data is provided to the strategy: 15m candles drive execution; 1h and 4h candles provide trend/bias context | Must |
| F3-10 | The replay engine handles candle alignment across timeframes — a 4h candle at T covers 15m candles from T to T+3h45m | Should |

#### Feature 4: Backtest API & Results

| ID | Requirement | Priority |
|---|---|---|
| F4-01 | `POST /api/backtests` accepts: `symbol`, `intervals[]`, `startDate`, `endDate`, `strategyConfig` (JSON) | Must |
| F4-02 | The endpoint validates inputs: date range must have candle data in DB, strategy config must be valid | Must |
| F4-03 | The endpoint returns a `BacktestResult` containing: total trades, winning trades, losing trades, win rate, total PnL, max drawdown, average trade PnL, average hold time, number of hedges opened, total fees paid | Must |
| F4-04 | The result includes a list of individual trades with: entry time, exit time, entry price, exit price, side, PnL, fees | Should |
| F4-05 | The backtest runs synchronously and returns the result in the HTTP response | Must |
| F4-06 | Request validation returns 400 for invalid parameters (missing date range, unknown symbol, invalid config) | Must |
| F4-07 | If no candle data exists for the requested range, the endpoint returns 404 with a descriptive error | Must |

### 5.2 Non-Functional Requirements

| ID | Requirement | Priority |
|---|---|---|
| NF-01 | Backtest for 1 year of 15m data (~35K candles) completes in under 30 seconds | Should |
| NF-02 | Candle ingestion for all 3 intervals (~137K candles total) completes in under 10 minutes | Should |
| NF-03 | SQLite DB file remains under 50 MB for the full dataset | Should |
| NF-04 | Backtest engine is stateless — multiple backtests can run concurrently without interference | Must |
| NF-05 | All database operations use async EF Core APIs | Must |
| NF-06 | The backtest engine must produce identical results when run twice with the same inputs (deterministic) | Must |

---

## 6. Technical Considerations

### Architecture

The implementation follows the existing clean architecture:

| Layer | Additions |
|---|---|
| `TradePilot.Domain` | `Candle` entity |
| `TradePilot.Application` | `ICandleRepository`, `ICandleIngestionService`, `IBacktestRunner`, query/command handlers, DTOs (`BacktestRequest`, `BacktestResult`, `BacktestTrade`, `IngestionResult`) |
| `TradePilot.Infrastructure` | `CandleIngestionService` (uses existing `IHyperliquidRestClient`) |
| `TradePilot.Persistence` | `TradePilotDbContext`, `CandleRepository`, EF Core migrations |
| `TradePilot.Api` | `CandleIngestionController`, `BacktestController` |

### Database

- **Provider**: SQLite via `Microsoft.EntityFrameworkCore.Sqlite`
- **Connection string**: Configured in `appsettings.json` under `ConnectionStrings:DefaultConnection`
- **Migrations**: Code-first via `dotnet ef migrations`
- **Migration path**: SQLite → Azure SQL requires only provider swap and connection string change (per ADR 3)

### Entity Design

```
Candle
├── Id (long, PK, auto-increment)
├── Symbol (string, max 20)
├── Interval (string, max 10)
├── Timestamp (long, unix ms — candle open time)
├── Open (decimal)
├── High (decimal)
├── Low (decimal)
├── Close (decimal)
├── Volume (decimal)
└── NumTrades (int)

Index: IX_Candle_Symbol_Interval_Timestamp (unique)
```

### Candle Ingestion Flow

```
API Request (symbol, intervals, startTime?, endTime?)
  → CandleIngestionService
    → For each interval:
      → Determine start time (DB latest or earliest available)
      → Loop: fetch 500 candles from Hyperliquid
        → Upsert batch to DB (skip duplicates via unique index)
        → Advance cursor by last candle timestamp
        → Pause for rate limiting (configurable delay)
      → Until endTime reached or no more data
    → Return IngestionResult (counts per interval)
```

### Backtest Pipeline Integration

The backtest reuses the same components as live trading with two substitutions:

| Component | Live | Backtest |
|---|---|---|
| Data source | Hyperliquid WebSocket + REST | `CandleReplayEngine` reading from DB |
| Execution engine | `HyperliquidExecutionEngine` | `SimulatedExecutionEngine` |
| Clock | Real-time `CandleClock` | Replay-driven `CandleClock` |

All other components (StrategyEngine, GridController, GridPlanner, RiskEngine, PositionManager, signal contracts) are identical.

```
BacktestRunner
  → Create CandleReplayEngine (reads from DB)
  → Create SimulatedExecutionEngine
  → Wire pipeline: ReplayEngine → CandleClock → StrategyScheduler
      → StrategyEngine → GridController → RiskEngine
      → PositionManager → SimulatedExecutionEngine
  → Run replay
  → Collect metrics via BacktestMetricsCalculator
  → Return BacktestResult
```

### Integration Points

| Integration | Details |
|---|---|
| `IHyperliquidRestClient.GetCandlesAsync` | Already implemented — used by ingestion service for batch fetching |
| `HyperliquidAssetMapper` | Already implemented — maps asset names and resolves timeframe intervals |
| `CandleClock` / `StrategyScheduler` | Defined in architecture docs — backtest feeds candles into the same clock |
| Signal contracts | Defined in architecture docs — backtest produces and processes the same signals |

### Constraints

- **Hyperliquid rate limits**: The ingestion service must throttle requests. Hyperliquid does not publish formal rate limits for the info API, but conservative pacing (200ms between requests) is recommended.
- **Candle data availability**: Hyperliquid BTC data starts from late 2022/early 2023. The ingestion service should handle empty responses gracefully when requesting data before the earliest available date.
- **Decimal precision**: Candle prices should use `decimal` (not `double`) to avoid floating-point precision issues in PnL calculations.
- **Clock alignment**: Multi-timeframe replay must ensure that 1h and 4h candle data is available at the correct timestamps when the 15m execution loop needs it.

### Dependencies

The backtest engine depends on components that are **defined in architecture docs but not yet implemented**:

- `StrategyEngine` / `ITradingStrategy` / `GridStrategy`
- `GridController` / `GridPlanner`
- `RiskEngine`
- `PositionManager`
- `CandleClock` / `StrategyScheduler`
- `MarketContextBuilder`
- Signal contracts (`DeployGrid`, `TakeProfit`, `OpenHedge`, etc.)

**Feature 1 (Candle Persistence) and Feature 2 (Candle Ingestion) have no upstream dependencies** and can be implemented immediately.

**Feature 3 (Replay Engine) and Feature 4 (Backtest API)** depend on the core trading pipeline. The `SimulatedExecutionEngine` and `CandleReplayEngine` can be scaffolded, but full end-to-end backtesting requires the strategy pipeline to be in place.

---

## 7. Use Cases

### Personas

| Persona | Description |
|---|---|
| **Operator** | The platform developer/owner running the system in POC phase. Ingests data, runs backtests, validates strategy before deploying live. Single user for now. |

### Feature 1 & 2: Candle Data Persistence and Ingestion

#### US-1.1: Ingest Full BTC Candle History

**As an** Operator  
**I want to** ingest all available BTC candle data from Hyperliquid for 15m, 1h, and 4h intervals  
**So that** I have a complete local dataset for backtesting

**Acceptance Criteria:**
- Given the database is empty, when I call `POST /api/candles/ingest` with `symbol=BTC` and `intervals=[15m, 1h, 4h]`, then candles are fetched from the earliest available date to now
- Given the ingestion completes, then the response includes counts: total fetched, total inserted, total skipped per interval
- Given Hyperliquid returns no data for a time range, then the service stops pagination for that interval gracefully
- Given the database already contains candles, then duplicates are skipped (no errors, no duplicate rows)

#### US-1.2: Incremental Candle Sync

**As an** Operator  
**I want to** re-run ingestion to pick up only new candles since the last sync  
**So that** I can keep my local dataset current without re-downloading everything

**Acceptance Criteria:**
- Given the database has candles up to timestamp T for a given symbol/interval, when I call ingest without specifying `startTime`, then fetching begins from T+1
- Given no new candles exist on Hyperliquid, then the response shows 0 inserted

#### US-1.3: Query Candles from Database

**As the** backtest engine (internal consumer)  
**I want to** query candles by symbol, interval, and date range in ascending time order  
**So that** the replay engine can feed them sequentially into the trading pipeline

**Acceptance Criteria:**
- Given candles exist in the database, when queried with symbol=BTC, interval=15m, start=T1, end=T2, then candles are returned ordered by Timestamp ascending
- Given no candles exist for the requested range, then an empty collection is returned

### Feature 3: Backtest Replay Engine

#### US-3.1: Run a Single-Timeframe Backtest

**As an** Operator  
**I want to** run a backtest over a specified date range with a given strategy configuration  
**So that** I can measure how the grid strategy would have performed historically

**Acceptance Criteria:**
- Given candle data exists for the requested range, when a backtest is triggered, then candles are replayed in time order through the full pipeline
- Given grid limit buy orders are placed, then they fill when the candle low ≤ order price
- Given take profit orders are placed, then they fill when the candle high ≥ TP price
- Given the backtest completes, then a result is returned with total trades, win rate, PnL, max drawdown

#### US-3.2: Simulated Execution with Fees

**As an** Operator  
**I want** fees and optional slippage applied to all simulated fills  
**So that** backtest results reflect realistic trading conditions

**Acceptance Criteria:**
- Given maker fee = 0.01% and taker fee = 0.035%, then all simulated fills include the appropriate fee deduction
- Given slippage = 0.05%, then fill prices are adjusted away from the order price by the slippage percentage
- Given no fees/slippage are specified, then defaults are applied

#### US-3.3: Multi-Timeframe Context in Backtest

**As an** Operator  
**I want** the backtest to provide 4h trend data and 1h bias data alongside 15m execution candles  
**So that** the strategy has the same multi-timeframe context as in live trading

**Acceptance Criteria:**
- Given 15m, 1h, and 4h candle data exists, when the replay reaches a 15m candle at time T, then the MarketContext includes the latest closed 1h and 4h candles at or before T
- Given 4h data is missing, then the backtest returns an error indicating insufficient data

### Feature 4: Backtest API

#### US-4.1: Trigger Backtest via API

**As an** Operator  
**I want to** trigger a backtest via a REST API call with symbol, date range, and strategy config  
**So that** I can run backtests programmatically

**Acceptance Criteria:**
- Given valid parameters, when I call `POST /api/backtests`, then a backtest runs and the result is returned in the response body
- Given an invalid date range (end before start), then a 400 error is returned
- Given no candle data exists for the requested range, then a 404 error is returned
- Given an invalid strategy config, then a 400 error is returned with validation details

#### US-4.2: View Detailed Trade Log

**As an** Operator  
**I want** the backtest result to include a trade-by-trade log  
**So that** I can inspect individual entries and exits to understand strategy behaviour

**Acceptance Criteria:**
- Given a backtest completes with trades, then the result includes an array of trades, each with: entry time, exit time, entry price, exit price, side, PnL, fees paid
- Given a backtest completes with no trades (strategy never triggered), then the trades array is empty and summary metrics are zero

---

## 8. Open Questions

| ID | Question | Status | Answer |
|---|---|---|---|
| OQ-1 | What is the earliest BTC candle available on Hyperliquid? Should the ingestion service auto-discover this or use a hardcoded start date? | Open | — |
| OQ-2 | Should backtest results be persisted to the database for later comparison, or is synchronous return sufficient for POC? | Open | — |
| OQ-3 | The core trading pipeline components (StrategyEngine, GridController, RiskEngine, PositionManager) are not yet implemented. Should Features 1 & 2 (persistence + ingestion) be delivered as a standalone milestone first, with Features 3 & 4 (backtest engine + API) delivered after the pipeline is built? | Open | — |
| OQ-4 | Should the ingestion endpoint run synchronously (blocking until complete) or return immediately and process in the background via the Worker? For ~137K candles at 200ms throttle, ingestion takes ~5-8 minutes. | Open | — |
| OQ-5 | What Hyperliquid fee tier should be used as the default for backtesting? The PRD assumes maker = 0.01%, taker = 0.035%. Are these the correct current rates? | Open | — |
| OQ-6 | Should the hedge fill simulation use candle close (as described in the architecture doc: "price closes below breakdown threshold") or candle low (more aggressive assumption)? | Open | — |
| OQ-7 | For multi-timeframe backtesting, should the replay engine require all three timeframes (15m, 1h, 4h) to be present in the DB, or should it degrade gracefully if higher timeframes are missing? | Open | — |

---

*This document is in draft status and stored in the PRD draft directory for review.*
