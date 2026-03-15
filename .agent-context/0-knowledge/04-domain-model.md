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

All trading entities (Order, Fill, Position, Signal, Strategy, StrategyConfig, BotState)
are tenant-scoped — they belong to a specific User.

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