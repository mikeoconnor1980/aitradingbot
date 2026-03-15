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

At runtime the worker loads active strategies for all active subscribers.

Execution flow:

Worker Start  
↓  
Load all active subscribers  
↓  
For each subscriber:  
  Load ActiveStrategy  
  ↓  
  Load StrategyConfig JSON  
  ↓  
  Instantiate Strategy Plugin  
  ↓  
  Execute Strategy (using subscriber's keys)

---

# StrategyRun

Represents one runtime execution period for a strategy.

Fields:

UserId  
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

UserId  
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

Worker execution loop (runs per subscriber on candle close):

Update Market Data (shared)  
↓  
Calculate Indicators (shared)  
↓  
For each active subscriber:  
  Load StrategyConfig  
  ↓  
  Execute Strategy Plugin  
  ↓  
  Generate Signals  
  ↓  
  Risk Engine Validation  
  ↓  
  Order Execution (using subscriber's keys)

---

# Strategy Safety

The strategy plugin never bypasses the risk engine.

All orders must pass:

Max exposure limits  
Daily loss limits  
Leverage limits

Risk enforcement happens after signals are generated.

Per-user risk limits are applied.
Platform-level risk limits are applied on top of per-user limits.

---

# Future Extensions

Possible future improvements:

Multi-strategy execution  
Strategy portfolio management  
A/B strategy testing  
Automated strategy optimisation