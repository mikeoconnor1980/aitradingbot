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
StrategyType  
CreatedAt  
IsActive

---

# StrategyConfig

Stores JSON configuration for the strategy.

Fields:

StrategyId  
ConfigJson

---

# Position

Represents open or closed positions.

Fields:

Symbol  
Direction  
AverageEntryPrice  
Quantity  
PnL

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
CreatedAtUtc (Unix ms)

Key design patterns:

- `CreateQueued(...)` factory for async background runs (initial status: `Queued`); `Create(...)` factory for direct synchronous creation with final metrics
- Private setters — immutable after creation; metrics and audit blobs written via `MarkCompleted(...)`
- No `UserId` — not tenant-scoped

File: `src/TradingApp.Domain/Entities/BacktestRun.cs`