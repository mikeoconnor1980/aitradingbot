# Backtesting Architecture

Backtesting reuses the same strategy, scheduling, controller, and risk pipeline used in trading, but swaps live exchange execution for historical candle replay plus an in-memory execution engine. The current implementation is asynchronous: the API persists a queued `BacktestRun`, enqueues work, processes it in a hosted service, and streams progress back to the UI.

## Overview

The implemented backtest path shares these components with the live pipeline:

- `IMarketContextBuilder`
- `IStrategyEngine`
- `IGridController`
- `ISignalController`
- `StrategyScheduler`
- `IRiskEngine`
- `IPositionManager`
- trading signal contracts and most strategy-domain models

The main differences are execution and orchestration.

| Concern | Live mode | Backtest mode |
|---|---|---|
| Market data | Hyperliquid streams and live context builders | Historical candles loaded from persistence |
| Execution | `LiveExecutionEngine` and exchange services | `SimulatedExecutionEngine` |
| Risk engine | `LiveRiskEngine` | `BacktestRiskEngine` |
| Run lifecycle | Long-lived trading session | Queued API job processed by `BacktestProcessorService` |

`GridPlanner` is not part of the current runtime path.

## Async Job Architecture

Backtests are queued and processed in the API host rather than executed inline in the controller action.

| Component | Location | Purpose |
|---|---|---|
| `RunBacktestCommand` | `src/TradingApp.Application/Backtesting/RunBacktestCommand.cs` | Creates a queued `BacktestRun`, persists it, and enqueues a `BacktestJob` |
| `BacktestJobQueue` | `src/TradingApp.Application/Backtesting/BacktestJobQueue.cs` | Bounded channel used for queued replay work |
| `BacktestCancellationManager` | `src/TradingApp.Application/Backtesting/BacktestCancellationManager.cs` | Registers per-job cancellation tokens and powers `/cancel` |
| `BacktestProcessorService` | `src/TradingApp.Api/Services/BacktestProcessorService.cs` | Hosted service that dequeues jobs, runs `IBacktestRunner`, updates persistence, and broadcasts progress |
| `BacktestRun` | `src/TradingApp.Domain/Entities/BacktestRun.cs` | Persistent status + metrics record for `Queued`, `Running`, `Completed`, `Failed`, and `Cancelled` states |

The API returns `202 Accepted` for new runs. Progress is pushed through SignalR using the `ReceiveBacktestProgress` event.

## Practical Runtime Flow

`BacktestRunner` performs the replay after a queued job is dequeued:

1. `ValidateConfig` checks symbol, date range, capital, intervals, warmup, and execution settings.
2. `CandleReplayEngine.LoadAsync` loads the required candles and returns `ReplayData`.
3. Warmup candles seed indicator state through `IMarketContextBuilder.UpdateIndicators(...)`.
4. For each trigger candle after warmup:
   - `SimulatedExecutionEngine.ProcessCandle(...)` fills open orders and updates position state.
   - `IMarketContextBuilder.UpdateIndicators(...)` advances indicators.
   - the latest closed 1h and 4h candles are resolved for higher-timeframe context.
   - `StrategyScheduler` executes `Build -> EvaluateAsync -> GridController/SignalController -> IRiskEngine.ValidateAsync -> IPositionManager.ExecuteSignalsAsync`.
   - equity snapshots and optional audit events are recorded.
   - an `onProgress(candlesProcessed, totalCandles, timestamp)` callback is invoked periodically.
5. `BacktestMetricsCalculator` computes summary metrics.
6. `BacktestProcessorService` maps the result into `BacktestRun.MarkCompleted(...)`, persists it, and broadcasts completion.

The `CandleClock` and `StrategyScheduler` are the same core orchestration types used elsewhere in the application.

## Historical Inputs

The current implementation consumes persisted candle data via `ICandleRepository`.

| Input | Status | Notes |
|---|---|---|
| OHLCV candles | Implemented | Required for every run |
| Funding-rate enrichment for review summaries | Implemented outside the replay loop | Used by `BacktestSummaryForReview`, not the candle loop itself |
| Order-book replay | Not implemented | Future work |
| Sentiment snapshot replay | Not implemented | Regime is derived synthetically during replay |

The replay engine still loads `15m`, `1h`, and `4h` candles for context even when the trigger timeframe is configurable.

## Replay Model

Backtesting processes candles sequentially and preserves time order. The replay loop is deterministic: given the same `BacktestConfig`, same candle set, and same persisted strategy config, the engine should produce the same result.

## BacktestConfig

`BacktestConfig` lives in `src/TradingApp.Application/Backtesting/Models/BacktestConfig.cs`.

| Field | Type | Default | Description |
|---|---|---|---|
| `Symbol` | `string` | — | Trading symbol |
| `Intervals` | `IReadOnlyList<string>` | — | Context intervals loaded for the replay |
| `StartDateUtc` | `long` | — | Unix ms start of the evaluation window |
| `EndDateUtc` | `long` | — | Unix ms end of the evaluation window |
| `InitialCapital` | `decimal` | — | Starting equity |
| `Strategy` | `IStrategyConfig` | — | Typed strategy configuration |
| `Execution` | `ExecutionConfig` | — | Fee and slippage model |
| `TriggerTimeframe` | `string` | `15m` | Trigger candle stream used to drive the scheduler |
| `WarmupPeriod` | `int` | `200` | Warmup candles used to seed indicators |
| `EnableAuditLog` | `bool` | `true` | Enables candle, order-event, and grid-cycle audit capture |

## Execution Simulation

Backtesting never places real exchange orders. `SimulatedExecutionEngine` lives in the Application layer and is instantiated per run so each replay gets a fresh in-memory order book.

The engine is responsible for:

- placing in-memory orders from strategy signals
- simulating fills from candle OHLC data
- applying maker/taker fees and optional slippage
- calculating realized and unrealized PnL
- simulating liquidation fallback behavior when protection orders fail first

## Execution Behavior

Important implemented behaviors:

- **Fill priority**: buys are processed before sells within a candle.
- **Portfolio heat enforcement**: `BacktestRiskEngine` blocks new entries when total estimated risk would exceed `MaxPortfolioHeatPercent` and counts them in `HeatBlockedSignalCount`.
- **Drawdown-tier gating**: `BacktestRiskEngine` also applies drawdown scaling and hard halts. Signals blocked by drawdown halting are counted in `DrawdownBlockedSignalCount`.
- **Audit null-object pattern**: `NullBacktestAuditCollector.Instance` is used when `EnableAuditLog = false`.
- **Execution context bridge**: `BacktestExecutionContextAccessor` uses `AsyncLocal` to expose the current `SimulatedExecutionEngine` and `CurrentTimestampUtc` to shared services during replay.

## BacktestResult

`BacktestResult` lives in `src/TradingApp.Application/Backtesting/Models/BacktestResult.cs`.

Key fields include:

| Field | Type | Description |
|---|---|---|
| `TotalTrades` | `int` | Completed trade count |
| `WinningTrades` / `LosingTrades` | `int` | Win/loss counts |
| `WinRate` | `decimal` | Win percentage |
| `TotalPnL` | `decimal` | Realized PnL |
| `MaxDrawdownAbsolute` / `MaxDrawdownPercent` | `decimal` | Worst drawdown in absolute and percent terms |
| `AverageTradePnL` | `decimal` | Mean trade PnL |
| `AverageHoldTime` | `TimeSpan` | Mean trade duration |
| `HedgesOpened` | `int` | Hedge open count |
| `TotalFeesPaid` | `decimal` | Total fees across all fills |
| `GridCycles` | `int` | Completed grid cycles |
| `CandlesReplayed` | `int` | Trigger candles processed after warmup |
| `FinalEquity` | `decimal` | Final simulated equity |
| `HeatBlockedSignalCount` | `int` | Signals blocked by portfolio heat |
| `DrawdownBlockedSignalCount` | `int` | Signals blocked by drawdown halts |
| `EquityTimeSeries` | `IReadOnlyList<EquitySnapshot>` | Equity curve |
| `TradeLog` | `IReadOnlyList<BacktestTrade>` | Full trade log |

The result model also carries audit logs and R-oriented review metrics such as `Expectancy`, `ProfitFactor`, `Sqn`, `KellyPercent`, `HalfKellyPercent`, and `WinLossRRatio`.

## BacktestRun Persistence

`BacktestRun` in `src/TradingApp.Domain/Entities/BacktestRun.cs` is the persisted representation of a queued or completed run.

Important entity fields include:

- queue/status fields: `Status`, `Progress`, `TotalCandles`, `CandlesReplayed`, `ElapsedMs`, `ErrorMessage`
- summary metrics: `TotalTrades`, `WinRate`, `TotalPnl`, `MaxDrawdown`, `AverageTradePnl`, `TotalFeesPaid`
- JSON payloads: `TradesJson`, `EquityTimeSeriesJson`, `CandleLogJson`, `OrderEventLogJson`, `GridCycleLogJson`
- review-oriented metrics: `Expectancy`, `ProfitFactor`, `Sqn`, `KellyPercent`, `HalfKellyPercent`, `WinLossRRatio`

Runs are created through `BacktestRun.CreateQueued(...)` for the async path and updated to completed or failed states by `BacktestProcessorService`.

## Review-Oriented Summary Models

The strategy-review system consumes backtest summaries through dedicated projection models rather than the raw replay result.

| Model | Location | Purpose |
|---|---|---|
| `BacktestSummaryForReview` | `src/TradingApp.Application/Backtesting/Models/BacktestSummaryForReview.cs` | Converts `BacktestRun` persistence data into a review-friendly summary |
| `RegimeSegmentationSummary` | `src/TradingApp.Application/Backtesting/Models/RegimeSegmentationSummary.cs` | Segments completed cycles by trend, volatility, funding, and session buckets |

These models are used by `StrategyReviewer` to add historical performance context to AI-generated reviews.

## Key Components

| Component | Purpose | File |
|---|---|---|
| `BacktestRunner` | Orchestrates validation, replay, and metric calculation | `src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs` |
| `CandleReplayEngine` | Loads and aligns historical candles | `src/TradingApp.Application/Backtesting/Services/CandleReplayEngine.cs` |
| `SimulatedExecutionEngine` | In-memory execution engine for replay | `src/TradingApp.Application/Backtesting/Services/SimulatedExecutionEngine.cs` |
| `BacktestExecutionContextAccessor` | Async-local bridge exposing current execution engine and timestamp | `src/TradingApp.Application/Backtesting/BacktestExecutionContextAccessor.cs` |
| `BacktestMetricsCalculator` | Computes summary statistics | `src/TradingApp.Application/Backtesting/Services/BacktestMetricsCalculator.cs` |
| `BacktestJobQueue` | Queued work channel | `src/TradingApp.Application/Backtesting/BacktestJobQueue.cs` |
| `BacktestCancellationManager` | Per-run cancellation registry | `src/TradingApp.Application/Backtesting/BacktestCancellationManager.cs` |
| `BacktestProcessorService` | Hosted service executing queued runs | `src/TradingApp.Api/Services/BacktestProcessorService.cs` |
| `IBacktestAuditCollector` / `NullBacktestAuditCollector` | Audit collection boundary and no-op fallback | `src/TradingApp.Application/Backtesting/Services/` |

## API And UI

Implemented API endpoints in `src/TradingApp.Api/Controllers/BacktestsController.cs`:

| Method | Route | Description |
|---|---|---|
| `POST` | `/api/backtests` | Queue a backtest and return `202 Accepted` |
| `GET` | `/api/backtests/{id}` | Retrieve a persisted result |
| `GET` | `/api/backtests` | List backtest summaries |
| `GET` | `/api/backtests/validate` | Validate candle coverage |
| `GET` | `/api/backtests/{id}/debug` | Load per-cycle debug data |
| `POST` | `/api/backtests/{id}/cancel` | Cancel a queued or running backtest |

The Angular UI in `frontend/trading-ui/src/app/features/backtesting/` renders the run form, results, debug views, comparisons, and progress updates. `BacktestService` also exposes `cancelBacktest(id)`.

## Safety Principle

Backtesting must never call live exchange execution code. The separate `SimulatedExecutionEngine` implementation is the isolation boundary that prevents accidental live order placement during replay.

## Future Recommendations

- Add tick-level or intrabar replay when candle-level fills become too optimistic for newer strategies.
- Add order-book and latency simulation instead of exact-limit-price fills.
- Surface blocked-signal diagnostics such as `DrawdownBlockedSignalCount` more prominently in the UI.
- Expand `ReplayData` reuse patterns for optimizer batches and walk-forward validation.
- Add richer funding-aware and session-aware attribution alongside the existing reviewer summaries.# Backtesting Architecture

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

BacktestRunner orchestrates five phases:

1. **Validation** — guards on BacktestConfig (symbol, dates, capital, intervals)
2. **Data load** — `CandleReplayEngine.LoadAsync` fetches 15m/1h/4h candles in parallel, aligns higher-timeframe starts, determines warmup boundary
3. **Warmup** — feeds the first `WarmupPeriod` (default: 200) candles to `IMarketContextBuilder.UpdateIndicators()` to seed indicator state; no signals generated
4. **Evaluation loop** — for each post-warmup 15m candle:
   - `SimulatedExecutionEngine.ProcessCandle` — fills open orders against candle OHLC
   - `IMarketContextBuilder.UpdateIndicators` — update indicator state
   - `CandleReplayEngine.GetLatestClosedCandle` — resolve latest closed 1h/4h candle
   - `CandleClock.ProcessCandleAsync` → fires `StrategyScheduler.HandleCandleClosedAsync`
   - StrategyScheduler: `IMarketContextBuilder.Build` → `IStrategyEngine.EvaluateAsync` → `IGridController.ProcessAsync` → `IRiskEngine.ValidateAsync` → `IPositionManager.ExecuteSignalsAsync`
   - Record equity snapshot
5. **Metrics** — `BacktestMetricsCalculator.Calculate(tradeLog, equityTimeSeries, initialCapital, gridCycles)` → `BacktestResult`

The `CandleClock` and `StrategyScheduler` are the exact same classes used in live trading. `SimulatedExecutionEngine` is the only backtest-specific pipeline component.

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

# BacktestConfig

`BacktestConfig` (`src/TradingApp.Application/Backtesting/Models/BacktestConfig.cs`):

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Symbol` | `string` | — | Trading symbol (e.g. `BTC`) |
| `Intervals` | `IReadOnlyList<string>` | — | Must include `15m`, `1h`, `4h` |
| `StartDateUtc` | `long` | — | Unix ms — start of evaluation period (after warmup) |
| `EndDateUtc` | `long` | — | Unix ms — end of evaluation period |
| `InitialCapital` | `decimal` | — | Starting equity for PnL simulation |
| `Strategy` | `IStrategyConfig` | — | Typed strategy config passed to the pipeline. In v1, always a `StrategyConfig` (`src/TradingApp.Application/StrategyAuthoring/Models/StrategyConfig.cs`). |
| `Execution` | `ExecutionConfig` | — | Fee model for this run (see `FeeModel.Default`). Leverage is in `StrategyConfig.Risk.Leverage`. |
| `WarmupPeriod` | `int` | `200` | 15m candles fed to indicator state before evaluation starts |
| `EnableAuditLog` | `bool` | `true` | When `true`, per-candle, order-event, and grid-cycle audit entries are collected and persisted as JSON blob columns on `BacktestRun` |

`FeeModel` (`src/TradingApp.Domain/Trading/FeeModel.cs`): `MakerFeeRate` (default 0.0001), `TakerFeeRate` (default 0.00035), `SlippageRate` (default 0). Use `FeeModel.Default` for standard Hyperliquid rates. Provides `CalculateFee(size, price, isMaker)` and `ApplySlippage(price, side)`. Owned by `ExecutionConfig` — not a direct field on `BacktestConfig`.

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

# Execution Behavior

**Fill priority**: Within each candle, buys are processed before sells. This avoids incorrect same-candle pairing when both a buy and sell price range would be satisfied.

**Portfolio heat enforcement**: Within each candle, after candidate entry signals are generated, `BacktestRiskEngine.ValidateAsync` blocks signals if the total portfolio risk would exceed the configured limit. Blocked signals are counted and reported in `HeatBlockedSignalCount` for post-backtest analysis. Mirrors live trading behaviour.

**FIFO trade pairing**: The trade log pairs entries and exits in order:
- `GridFill` entries → `TakeProfit` exits
- `HedgeOpen` entries → `HedgeClose` exits

**Fees**: Deducted immediately at fill time into `SimulatedPosition.RealisedPnL`. Limit orders use maker rate; market orders use taker rate.

**Higher-timeframe alignment**: `CandleReplayEngine.GetLatestClosedCandle(higherTfCandles, triggerCandleOpenTimeUtc)` returns the most recent candle whose `Timestamp + intervalMs <= triggerCandleOpenTimeUtc`. This prevents look-ahead bias.

**SimulatedExecutionEngine scope**: Lives in the Application layer (no I/O). Instantiated directly inside `BacktestRunner.RunAsync`, not injected via DI, so each run gets a fresh in-memory order book.

---

# Leverage and Liquidation Simulation

When backtesting starts, `BacktestRunner` calls `executionEngine.SetMaxLeverage(symbol, fallbackMaxLeverage)` to initialize the engine with the asset's maximum allowable leverage.

For each open position, `SimulatedExecutionEngine` computes a **liquidation price** based on leverage and the derived maintenance margin rate. Within each candle, the engine checks:

1. **Stop-loss first** — SL trigger fires if the candle breaches the SL price
2. **Liquidation fallback** — if SL did not fill (e.g., price gapped beyond SL to liquidation level), the position is force-closed at the liquidation price with `CancellationReason.LiquidationTriggered`

Key methods: `TryProcessProtectionOrLiquidation`, `TryCreateLiquidationFill`, `IsLiquidationBreached`

File: `src/TradingApp.Application/Backtesting/Services/SimulatedExecutionEngine.cs`

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

`BacktestResult` (`src/TradingApp.Application/Backtesting/Models/BacktestResult.cs`):

| Field | Type | Description |
|-------|------|-------------|
| `TotalTrades` | `int` | Count of completed trades (entry and exit both recorded) |
| `WinningTrades` / `LosingTrades` | `int` | Count of trades with positive/negative PnL |
| `WinRate` | `decimal` | Percentage (0–100) of winning trades |
| `TotalPnL` | `decimal` | Sum of realised PnL across all trades |
| `MaxDrawdownAbsolute` / `MaxDrawdownPercent` | `decimal` | Worst peak-to-trough equity decline in absolute and % terms |
| `AverageTradePnL` | `decimal` | Mean per-trade PnL |
| `AverageHoldTime` | `TimeSpan` | Mean duration from entry fill to exit fill |
| `HedgesOpened` | `int` | Count of `TradeType.HedgeOpen` fills |
| `TotalFeesPaid` | `decimal` | Sum of all fees across all fills |
| `GridCycles` | `int` | Number of completed grid lifecycle cycles (`Closed` state reached) |
| `FinalEquity` | `decimal` | `InitialCapital + RealisedPnL + UnrealisedPnL` at last candle |
| `HeatBlockedSignalCount` | `int` | Number of entry signals rejected due to portfolio heat limit during the backtest run |
| `EquityTimeSeries` | `IReadOnlyList<EquitySnapshot>` | Equity value per candle (`record (long TimestampUtc, decimal Equity)`) |
| `TradeLog` | `IReadOnlyList<BacktestTrade>` | Full per-trade record including entry/exit price, fees, TradeType |
| `Expectancy` | `decimal?` | Mean R-multiple across all R-tracked trades (null if no R-tracked trades) |
| `ProfitFactor` | `decimal?` | Sum of positive R / abs(sum of negative R) |
| `Sqn` | `decimal?` | System Quality Number: `(Expectancy / StdDev(R)) × √N` |
| `AvgWinR` | `decimal?` | Mean R-multiple of winning trades |
| `AvgLossR` | `decimal?` | Mean R-multiple of losing trades |
| `RWinRate` | `decimal?` | Win rate among R-tracked trades (%) |
| `RDistribution` | `IReadOnlyList<decimal>?` | Raw R-multiple values for histogram rendering |
| `KellyPercent` | `decimal?` | Kelly Criterion optimal allocation: `W - (1-W)/R` where W = win fraction, R = AvgWinR/|AvgLossR|. Null if < 2 R-tracked trades or no losers |
| `HalfKellyPercent` | `decimal?` | Conservative half-Kelly allocation (KellyPercent / 2). Recommended for live use |
| `WinLossRRatio` | `decimal?` | AvgWinR / |AvgLossR| — reward-risk asymmetry ratio |

`TradeType` enum (`src/TradingApp.Application/Trading/Models/TradeType.cs`): `GridFill`, `TakeProfit`, `HedgeOpen`, `HedgeClose`.

---

# Backtest Persistence

Completed runs are persisted as `BacktestRun` domain entities immediately after `IBacktestRunner.RunAsync` completes.

`BacktestRun` (`src/TradingApp.Domain/Entities/BacktestRun.cs`):

- Created via `BacktestRun.CreateQueued(...)` for the async background path (status: `Queued → Running → Completed/Failed`) or `BacktestRun.Create(...)` for direct creation with final metrics
- Summary metrics (TotalTrades, WinRate, TotalPnl, MaxDrawdown, etc.) stored as scalar columns
- R-multiple aggregate metrics (`Expectancy`, `ProfitFactor`, `Sqn`, `KellyPercent`, `HalfKellyPercent`, `WinLossRRatio`) stored as nullable float columns; derived metrics (`AvgWinR`, `AvgLossR`, `RWinRate`, `RDistribution`) are recomputed from `TradesJson` at read time by `BacktestRunResponseMapper`
- JSON blob columns: `StrategyConfigJson`, `ExecutionConfigJson`, `TradesJson`, `IntervalsJson`, `EquityTimeSeriesJson`
- Audit log blob columns (nullable): `CandleLogJson`, `OrderEventLogJson`, `GridCycleLogJson` — populated only when `AuditLogEnabled = true`
- `AuditLogEnabled` (bool) — records whether audit data was collected for this run; controls whether the debug endpoint returns data
- **Not tenant-scoped** — runs are keyed by a generated Guid with no UserId

**What is NOT persisted:** `FinalEquity`, `MaxDrawdownPercent`, and `GridCycles` from `BacktestResult` are not stored. Only `MaxDrawdown` (absolute) is stored. `EquityTimeSeriesJson` **is** persisted. Derived R-metrics (`AvgWinR`, `AvgLossR`, `RWinRate`, `RDistribution`) are not persisted — they are recomputed from trade data at query time.

`IBacktestRunRepository` (`src/TradingApp.Application/Abstractions/Repositories/IBacktestRunRepository.cs`):

| Method | Description |
|--------|-------------|
| `AddAsync(BacktestRun, ct)` | Persist a completed run |
| `GetByIdAsync(Guid, ct)` | Full run by ID; returns null if absent |
| `GetPagedSummariesAsync(page, pageSize, ct)` | `PagedResult<BacktestRunSummary>` — summary projection without trades |

Implementation: `src/TradingApp.Persistence/Repositories/BacktestRunRepository.cs`

`BacktestRunResponseMapper` (`src/TradingApp.Application/Backtesting/BacktestRunResponseMapper.cs`): internal static helper that serializes `GridStrategyConfig`/trades to JSON for storage, and maps `BacktestRun` entity → `BacktestRunResponse` for API responses.

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

The system supports parameter sweeps for optimization. The optimizer
generates multiple strategy configs by sweeping specified parameters,
runs a backtest for each, and reports summary statistics.

Sweepable parameters:

- Grid spacing
- Take profit percent
- Stop-loss type and distance:
  - `FixedPercent`: value range (e.g., 1%–5%)
  - `AtrTrailing`: multiplier range (e.g., 2.0–4.0)
  - `AtrInitial`: multiplier and period ranges (e.g., multiplier 2.0–4.0, period 10/14/21)
- Risk per trade (`RiskBased` sizing): percent range
- Hedge percent
- Max exposure

When sweeping ATR-based exits, `ParameterBounds` provides `StopLossTypes`,
`AtrMultiplierOptions`, and `AtrPeriodOptions` fields. `StrategyConfigGenerator`
cross-products these to generate all combinations.

A sweep runs many backtests automatically and compares performance.

---

# Key Components

| Component | Purpose | File |
|-----------|---------|------|
| `BacktestRunner` | Orchestrates the full backtest: validation → data load → warmup → candle loop → metrics | `src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs` |
| `CandleReplayEngine` | Loads and aligns historical candles from the database; resolves warmup boundary; provides `GetLatestClosedCandle` for HTF context | `src/TradingApp.Application/Backtesting/Services/CandleReplayEngine.cs` |
| `SimulatedExecutionEngine` | Pure in-memory `IExecutionEngine`; accepts `OrderRequest`s, processes candles, simulates fills, tracks position and fees; computes liquidation prices, simulates liquidation triggers, force-closes gapped positions | `src/TradingApp.Application/Backtesting/Services/SimulatedExecutionEngine.cs` |
| `BacktestMetricsCalculator` | Computes summary statistics from the trade log and equity curve | `src/TradingApp.Application/Backtesting/Services/BacktestMetricsCalculator.cs` |
| `IBacktestRunner` | Public contract for the orchestrator | `src/TradingApp.Application/Abstractions/Services/IBacktestRunner.cs` |
| `IExecutionEngine` | Execution boundary; `SimulatedExecutionEngine` (backtest) and a future `LiveExecutionEngine` both implement this | `src/TradingApp.Application/Abstractions/Services/IExecutionEngine.cs` |
| `RunBacktestCommand` | CQRS command: maps request → `BacktestConfig`, runs via `IBacktestRunner`, persists `BacktestRun` entity, enforces 5-minute server-side timeout | `src/TradingApp.Application/Backtesting/RunBacktestCommand.cs` |
| `GetBacktestResultQuery` | CQRS query: loads `BacktestRun` by Guid; throws `NotFoundException` if absent | `src/TradingApp.Application/Backtesting/GetBacktestResultQuery.cs` |
| `GetBacktestListQuery` | CQRS query: returns `PagedResult<BacktestRunSummary>` from repository | `src/TradingApp.Application/Backtesting/GetBacktestListQuery.cs` |
| `GetCandleCoverageQuery` | CQRS query: calls `ICandleRepository.GetCoverageAsync` per interval; returns `CandleCoverageResponse` | `src/TradingApp.Application/Backtesting/GetCandleCoverageQuery.cs` |
| `UnavailableBacktestRunner` | API-host placeholder `IBacktestRunner` — throws `InvalidOperationException` because the full strategy pipeline is not yet composed in the API host | `src/TradingApp.Api/Services/UnavailableBacktestRunner.cs` |
| `IBacktestAuditCollector` | Contract for collecting audit entries during replay: `LogCandleEvaluation`, `LogOrderEvent`, `LogGridCycleCompleted`. Injected into `StrategyScheduler` and `BacktestPositionManager`. | `src/TradingApp.Application/Backtesting/Services/IBacktestAuditCollector.cs` |
| `BacktestAuditCollector` | Thread-safe in-memory implementation; accumulates entries via `ConcurrentQueue<T>`; exposes `CandleEvaluations`, `OrderEvents`, `GridCycles` read-only lists at run end | `src/TradingApp.Application/Backtesting/Services/BacktestAuditCollector.cs` |
| `NullBacktestAuditCollector` | Singleton no-op implementation (`NullBacktestAuditCollector.Instance`); used when `EnableAuditLog = false` — null-object pattern avoids null checks in callers | `src/TradingApp.Application/Backtesting/Services/NullBacktestAuditCollector.cs` |
| `GetBacktestDebugQuery` | CQRS query: deserialises `CandleLogJson`, `OrderEventLogJson`, `GridCycleLogJson` from a saved run and filters all three collections by `CycleId`; returns `BacktestDebugResponse` | `src/TradingApp.Application/Backtesting/GetBacktestDebugQuery.cs` |

---

# API

Implemented in `src/TradingApp.Api/Controllers/BacktestsController.cs`:

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/backtests` | Run backtest; returns 201 with `BacktestRunResponse`. Returns 408 if cancelled or exceeds 5-minute server timeout. |
| `GET` | `/api/backtests/{id}` | Retrieve full result for a saved run; 404 if not found |
| `GET` | `/api/backtests` | List backtest summaries with paging (`page`, `pageSize` query params; max 100 per page) |
| `GET` | `/api/backtests/validate` | Check candle coverage for a symbol + comma-separated intervals before running |
| `GET` | `/api/backtests/{id}/debug` | Per-cycle debug data (candle evaluations, order events, grid cycle summary). Requires `cycleId` query param. Returns 204 if audit log absent for this run; 404 if run not found. |

`BacktestsController` extends `ApiController` and dispatches all operations via MediatR.

**Response types:**
- `BacktestSummaryDto` (`src/TradingApp.Api/Models/BacktestSummaryDto.cs`) — the API-layer projection returned by `GET /api/backtests` (mapped from `BacktestRunSummary`; same fields).
- `PagedResult<T>` (`src/TradingApp.Application/Abstractions/Models/PagedResult.cs`) — generic paging envelope: `Items`, `Page`, `PageSize`, `TotalCount`, `TotalPages` (computed). Used by both the repository interface and the API response.

# UI

The backtesting UI lives at `/backtesting` and is implemented as a tabbed page shell in `frontend/trading-ui/src/app/features/backtesting/`.

| Component | Purpose | File |
|-----------|---------|------|
| `BacktestPageComponent` | Tab shell: Run / Past Results / Compare. Owns `latestResult`, `compareResultA/B`, and tab navigation | `backtest-page.component.ts` |
| `BacktestFormComponent` | Reactive form for strategy config, symbol, date range, capital. Emits `(runBacktest)` and `(validateCoverage)` | `backtest-form/` |
| `CoverageReportComponent` | Displays `CandleCoverageResponse` — per-interval candle count and date range | `coverage-report/` |
| `BacktestResultComponent` | Metric cards: PnL, win rate, drawdown, trades, fees, hold time; conditional R-metric KPI section (expectancy, profit factor, SQN, win rate, avg winner/loser) shown only when R-tracked trades exist | `backtest-result/` |
| `EquityChartComponent` | Lightweight Charts area chart with trade markers and optional comparison overlay | `equity-chart/` |
| `TradeLogTableComponent` | Sortable table of `BacktestTrade[]` entries; conditionally displays InitialR, R-Multiple, MFE, MAE columns when R-tracked data exists; each row expands to a debug panel showing per-cycle candle evaluations (filterable by signal type / setup-detected), order events, and grid cycle summary; supports JSON and CSV export; data loaded on demand via `BacktestService.getDebugData` | `trade-log-table/` |
| `BacktestListComponent` | Paginated past-results list with `mat-paginator`; emits IDs for comparison | `backtest-list/` |
| `BacktestCompareComponent` | Side-by-side metric diff and overlaid equity curves for two runs | `backtest-compare/` |
| `RDistributionChartComponent` | CSS bar-chart histogram bucketing realised R-multiples for visual distribution analysis; shown when `RDistribution` data exists | `r-distribution-chart/` |
| `BacktestService` | API client: `runBacktest`, `getBacktest`, `validateCoverage`, `getBacktestList` | `src/app/core/services/backtest.service.ts` |

Angular models mirror the API shapes and live in `frontend/trading-ui/src/app/core/models/backtest.model.ts`: `BacktestRequest`, `BacktestResult`, `BacktestSummary`, `BacktestTrade`, `EquitySnapshot`, `PagedResult<T>`, `CoverageReport`.

Debug-specific models (`CandleEvaluation`, `OrderEvent`, `GridCycleSummary`, `BacktestDebugResponse`, `OrderEventType`, `CancellationReason`) live in `frontend/trading-ui/src/app/core/models/backtest-debug.model.ts`.

`BacktestService` (`src/app/core/services/backtest.service.ts`) exposes: `runBacktest`, `getBacktest`, `validateCoverage`, `getBacktestList`, `getDebugData(id, cycleId)`.

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