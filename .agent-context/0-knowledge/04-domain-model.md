# Domain Model

Core entities:

Order  
Fill  
Position  
Signal  
Strategy  
StrategyConfig  
BotState

---

# Strategy

Represents a saved user strategy.

Fields:

Id  
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