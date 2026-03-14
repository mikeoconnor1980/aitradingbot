# Strategy Runtime Model

This document defines how strategies run inside the trading system.

The runtime model separates:

Strategy definition  
Strategy configuration  
Strategy execution  
Strategy performance tracking

---

# Key Concepts

Strategy Plugin

Implemented in C# as a class that implements:

ITradingStrategy

Example:

GridStrategy

Future strategies may include:

TrendBreakoutStrategy  
MeanReversionStrategy

---

# Strategy

Represents a user-created strategy instance.

Fields:

Id  
Name  
StrategyType  
CreatedAt  
IsActive

Example:

BTC Pullback Grid

Multiple strategies may exist but typically only one is active.

---

# StrategyConfig

JSON configuration stored in the database.

Example fields:

trend  
bias  
entry  
grid  
exit  
hedge  
risk

This configuration is interpreted by the strategy plugin.

---

# ActiveStrategy

At runtime the worker loads the active strategy.

Execution flow:

Worker Start  
↓  
Load ActiveStrategy  
↓  
Load StrategyConfig JSON  
↓  
Instantiate Strategy Plugin  
↓  
Execute Strategy Loop

---

# StrategyRun

Represents one runtime execution period for a strategy.

Fields:

StrategyId  
StartTime  
EndTime  
Status

Example status:

Running  
Stopped  
Error

---

# StrategyPerformance

Stores performance metrics for each strategy.

Fields:

StrategyId  
TotalTrades  
WinRate  
TotalPnL  
MaxDrawdown  
AverageTrade

These metrics allow comparison between strategy versions.

---

# Strategy Versioning

Users may create multiple versions of the same strategy.

Example:

BTC Pullback Grid v1  
BTC Pullback Grid v2  
BTC Pullback Grid v3

Only one version is active at a time.

This allows experimentation without losing historical results.

---

# Runtime Execution Loop

Worker execution loop:

Update Market Data  
↓  
Calculate Indicators  
↓  
Load StrategyConfig  
↓  
Execute Strategy Plugin  
↓  
Generate Signals  
↓  
Risk Engine Validation  
↓  
Order Execution

---

# Strategy Safety

The strategy plugin never bypasses the risk engine.

All orders must pass:

Max exposure limits  
Daily loss limits  
Leverage limits

Risk enforcement happens after signals are generated.

---

# Future Extensions

Possible future improvements:

Multi-strategy execution  
Strategy portfolio management  
A/B strategy testing  
Automated strategy optimisation