# Trading Strategy

## Overview

The platform initially supports one strategy plugin:

GridStrategy

The strategy is implemented in C# as a plugin that implements the interface:

ITradingStrategy

The system is designed so that additional strategies can be added later without
modifying the core trading engine.

Examples of future strategies:

- TrendBreakoutStrategy
- MeanReversionStrategy
- FundingArbitrageStrategy

---

# Grid Strategy

The GridStrategy plugin implements a pullback grid trading system.

Timeframes used:

4H — macro trend filter  
1H — directional bias  
15m — pullback entry

---

# Strategy Flow

Market data arrives from Hyperliquid.

The worker performs:

1. indicator calculations
2. trend filter
3. bias confirmation
4. pullback detection
5. grid deployment
6. hedge protection
7. take profit management

Execution flow:

Market Data  
↓  
Strategy Engine  
↓  
Signal Generation  
↓  
Risk Engine  
↓  
Order Execution

---

# Trend Filter (4H)

Example conditions:

Price > EMA(200)  
EMA(20) > EMA(50)

Outcome:

Bullish  
Neutral  
Bearish

Grid deployment only occurs during bullish environments.

---

# Bias Filter (1H)

Momentum confirmation.

Examples:

EMA(20) > EMA(50)  
RSI > 50  
Price > VWAP

---

# Entry Trigger (15m)

Entry occurs when:

- price pulls back toward support
- bullish reclaim occurs
- momentum stabilises

---

# Grid Deployment

Example grid levels:

L1 -0.35%  
L2 -0.70%  
L3 -1.05%  
L4 -1.40%

Example size distribution:

20%  
25%  
25%  
30%

---

# Take Profit

Grid exit occurs when:

AverageEntry + 0.8%

---

# Hedge Logic

If breakdown occurs below the lowest grid level:

A defensive short hedge is opened.

Typical hedge size:

25–50% of exposure

---

# Risk Controls

The strategy is always constrained by the Risk Engine.

Risk protections include:

- max position size
- max leverage
- daily loss limit
- cooldown periods