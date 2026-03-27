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
Symbol  
Interval (e.g. `15m`, `1H`, `4H`)  
Timestamp (Unix milliseconds — open time of the candle)  
Open  
High  
Low  
Close  
Volume  
NumTrades

Key design patterns:

- Static `Create` factory method with validation guards (null/whitespace, positive open price, high >= low)
- Private setters — immutable after creation
- Composite unique index on `(Symbol, Interval, Timestamp)` — enforces idempotent ingestion
- Bulk inserts use `INSERT OR IGNORE` for safe re-ingestion of overlapping data

File: `src/TradingApp.Domain/Entities/Candle.cs`