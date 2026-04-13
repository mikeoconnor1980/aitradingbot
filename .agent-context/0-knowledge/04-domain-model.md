# Domain Model

Core entities:

User  
Subscription  
UserExchangeCredential  
Order  
Fill  
Position  
Signal  
Strategy  
StrategyConfig  
BotState  
Candle

All trading entities (Order, Fill, Position, Signal, Strategy, StrategyConfig, BotState)
are tenant-scoped — they belong to a specific User.

`Candle` is a market data entity and is **not** tenant-scoped. Candle data is shared across all users.

---

# User

Represents a registered subscriber.

Fields:

Id  
Email  
DisplayName  
CreatedAt  
IsActive

---

# Subscription

Tracks the user's subscription status.

Fields:

UserId  
Plan  
Status  
StartedAt  
ExpiresAt  
ExternalBillingId

Example plans:

Basic  
Pro

Example statuses:

Active  
Paused  
Cancelled  
Expired

---

# UserExchangeCredential

Stores a subscriber's encrypted Hyperliquid wallet key.

Fields:

UserId  
Exchange  
EncryptedPrivateKey  
WalletAddress  
CreatedAt  
IsActive

The private key is encrypted at rest.
In Azure, this maps to a Key Vault secret per user.

---

# Strategy

Represents a saved user strategy.

Fields:

Id  
UserId  
Name  
StrategyType (always `"GridStrategy"` in v1)  
ConfigJson (serialized `StrategyConfig`; see [Strategy Config Schema](13-strategy-config-schema.md))  
Version (starts at 1; incremented on each `Update()`)  
IsActive (soft-delete flag)  
IsRunning (tracks live execution state; default false; stub in POC)  
CreatedAtUtc (Unix milliseconds)  
UpdatedAtUtc (Unix milliseconds)

Behavior:

- `Strategy.Create(userId, name, strategyType, configJson)` — static factory; validates all inputs; sets `Version = 1`, `IsActive = true`, `IsRunning = false`
- `Strategy.Update(name, configJson)` — increments `Version`, updates `UpdatedAtUtc`
- `Strategy.SoftDelete()` — sets `IsActive = false` and `IsRunning = false`; active queries exclude soft-deleted records
- `Strategy.SetRunningState(bool isRunning)` — updates `IsRunning`; throws if attempting to set `true` on an inactive strategy

File: `src/TradingApp.Domain/Entities/Strategy.cs`

---

# StrategyRevision

Immutable audit record capturing each save of a strategy. Created automatically when a strategy is created, updated, or restored.

Fields:

Id (Guid)
StrategyId
RevisionNumber (auto-incrementing per strategy; starts at 1)
ConfigJson (full JSON snapshot of strategy configuration at this revision)
Source (how this revision was created — see RevisionSource enum)
Label (optional user-provided label, e.g. "Restored from revision 3")
ChangeSummary (auto-generated diff summary highlighting field changes)
CreatedAtUtc (Unix milliseconds)

Behavior:

- `StrategyRevision.Create(strategyId, revisionNumber, configJson, source, changeSummary, label)` — static factory; validates all inputs including enum bounds; generates unique Guid Id
- Immutable after creation (private setters)

File: `src/TradingApp.Domain/Entities/StrategyRevision.cs`

---

# StrategyReview

Immutable review record created when a user requests an AI analysis of a saved strategy revision.

Fields:

Id (Guid)
StrategyId
RevisionNumber (references StrategyRevision linked by (StrategyId, RevisionNumber))
ReviewMarkdown (full AI-generated review in Markdown format)
ModelName (LLM model name used to generate the review; e.g., "gemini-2.5-flash-lite")
CreatedAtUtc (Unix milliseconds)

Behavior:

- `StrategyReview.Create(strategyId, revisionNumber, reviewMarkdown, modelName)` — static factory; validates all inputs including positive revision number and non-empty markdown/model name; generates unique Guid Id; sets CreatedAtUtc
- Immutable after creation (private setters)
- Linked to StrategyRevision via composite key (StrategyId, RevisionNumber); reviews are created on-demand when requested
- When a review is re-requested for the same revision, the prior review is overwritten (upsert pattern)

File: `src/TradingApp.Domain/Entities/StrategyReview.cs`

---

# RevisionSource

Enum indicating how a revision was created.

Values:

| Value | Int | Description |
|-------|-----|-------------|
| `Ui` | 0 | User created or edited via web interface |
| `Api` | 1 | Created via natural language API |
| `Import` | 2 | Created via Pine Script import or data migration |
| `Restore` | 3 | Created by restoring a previous revision |

Maps from `StrategyEntryPoint` enum in the Application layer via `RevisionSourceMapper`.

File: `src/TradingApp.Domain/Enums/RevisionSource.cs`

---

# Position

Represents open or closed positions. The `PositionDto` returned by the account API includes enriched SL/TP state read live from the exchange.

Fields (PositionDto):

Asset, Size, Side, EntryPrice, MarkPrice  
UnrealisedPnl, UnrealisedPnlPercent  
LiquidationPrice, Leverage, MarginMode, MarginUsed, FundingRate  
StopLossPrice?, StopLossOrderId? — populated from open reduce-only trigger orders  
TakeProfitPrice?, TakeProfitOrderId? — populated from open reduce-only trigger orders

SL/TP fields are **not persisted in the database**. They are correlated from the exchange's open orders on each position fetch. See [Hyperliquid Integration](02-hyperliquid-integration.md#trigger-orders-stop-loss--take-profit).

File: `src/TradingApp.Api/Models/PositionDto.cs`

---

# Candle

OHLCV market data. Persisted to the database for backtesting and historical analysis. Not tenant-scoped.

Fields:

Id (long, auto-increment)  
Source (e.g. `Hyperliquid`, `Binance`) — identifies the data provider  
Symbol  
Interval (e.g. `15m`, `1h`, `4h`; mark price klines use prefix `mark-15m`)  
Timestamp (Unix milliseconds — open time of the candle)  
Open  
High  
Low  
Close  
Volume  
NumTrades

Key design patterns:

- Static `Create` factory method with validation guards (null/whitespace, non-negative OHLC, positive timestamp, high >= low)
- Backward-compatible overload uses `source = "Hyperliquid"` as default to preserve existing Hyperliquid ingestion callsites
- Private setters — immutable after creation
- Composite unique index on `(Source, Symbol, Interval, Timestamp)` — enforces idempotent ingestion per data provider
- Bulk inserts use `INSERT OR IGNORE` for safe re-ingestion of overlapping data

File: `src/TradingApp.Domain/Entities/Candle.cs`

---

# FundingRate

Perpetual futures funding rate history. Persisted for backtesting, strategy context, and market regime analysis. Not tenant-scoped — shared market data.

Fields:

Id (long, auto-increment)  
Symbol (display symbol, e.g. `BTC`)  
Timestamp (Unix milliseconds — time the funding rate was applied)  
Rate (decimal; can be negative)  
MarkPrice (decimal; mark price at the funding settlement time)

Key design patterns:

- Static `Create` factory method with guards (null symbol, non-positive timestamp, negative mark price; `Rate` allows negative values)
- Private setters — immutable after creation
- Composite unique index on `(Symbol, Timestamp)` — enforces idempotent ingestion
- Bulk inserts use `INSERT OR IGNORE`
- Source is always Binance USDⓈ-M Futures — see [Binance Integration](23-binance-integration.md)

File: `src/TradingApp.Domain/Entities/FundingRate.cs`

---

# BacktestRun

Persisted record of a completed backtest execution. Not tenant-scoped — backtest results are shared market-data artefacts, not user-specific.

Fields:

Id (Guid)
Symbol
IntervalsJson (serialised `string[]`)
StartDateUtc / EndDateUtc (Unix ms)
StrategyConfigJson
ExecutionConfigJson
InitialCapital
CandlesReplayed
ElapsedMs
TotalTrades / WinningTrades / LosingTrades
WinRate
TotalPnl
MaxDrawdown
AverageTradePnl
AverageHoldTimeMinutes (double)
HedgesOpened
TotalFeesPaid
TradesJson (serialised trade log)
EquityTimeSeriesJson (serialised equity curve)
AuditLogEnabled (bool — whether audit log was collected for this run)
CandleLogJson / OrderEventLogJson / GridCycleLogJson (nullable — populated when AuditLogEnabled = true; queried by the debug endpoint)
Expectancy (nullable decimal — mean R-multiple across all R-tracked trades)
ProfitFactor (nullable decimal — sum of positive R / abs(sum of negative R))
Sqn (nullable decimal — System Quality Number: `(Expectancy / StdDev(R)) × √N`)
KellyPercent (nullable decimal — Kelly Criterion optimal allocation %)
HalfKellyPercent (nullable decimal — conservative half-Kelly allocation %)
WinLossRRatio (nullable decimal — ratio of average winning R to average losing R)
CreatedAtUtc (Unix ms)

Key design patterns:

- `CreateQueued(...)` factory for async background runs (initial status: `Queued`); `Create(...)` factory for direct synchronous creation with final metrics
- Private setters — immutable after creation; metrics and audit blobs written via `MarkCompleted(...)`
- No `UserId` — not tenant-scoped

File: `src/TradingApp.Domain/Entities/BacktestRun.cs`

---

# Domain Trading Types

Typed value objects in `src/TradingApp.Domain/Trading/` that represent strategy configuration and execution parameters. These are used throughout the pipeline (live and backtest) instead of raw JSON strings.

| Type | Kind | Purpose | File |
|------|------|---------|------|
| `IStrategyConfig` | Marker interface | Common type accepted by `IStrategyEngine` and `IGridController` | `src/TradingApp.Domain/Trading/IStrategyConfig.cs` |
| `GridStrategyConfig` | Sealed record | Typed config for the grid strategy: `GridLevels`, `GridSpacing`, `TakeProfitPercent`, `StopLossPercent`, `BreakdownThreshold`, `EntryMode`, `ManualAnchorPrice`, `PositionSize` | `src/TradingApp.Domain/Trading/GridStrategyConfig.cs` |
| `ExecutionConfig` | Sealed record | Execution parameters shared across live and backtest: `FeeModel` + `Leverage` (default 1×). `FeeModel.Default` provides standard Hyperliquid rates | `src/TradingApp.Domain/Trading/ExecutionConfig.cs` |
| `FeeModel` | Sealed record | Maker/taker fee rates and slippage. `CalculateFee(size, price, isMaker)` and `ApplySlippage(price, side)`. Default: maker 0.0001, taker 0.00035 | `src/TradingApp.Domain/Trading/FeeModel.cs` |
| `EntryModes` | Static class | Constants for grid anchor price mode: `AutoFromSignalCandle`, `Manual` | `src/TradingApp.Domain/Trading/EntryModes.cs` |
| `OrderSide` | Enum | `Buy` / `Sell` | `src/TradingApp.Domain/Enums/OrderSide.cs` |

`StrategyScheduler` holds a typed `IStrategyConfig` instance (not a JSON string) and passes it directly to `IStrategyEngine.EvaluateAsync` and `IGridController.ProcessAsync`.