# Strategy Configuration Schema

This document defines the schema used for user‑defined strategy configurations.

Strategies are stored as JSON and interpreted by the Strategy Engine.

Each saved strategy references a strategy plugin.

Example:

StrategyType = GridStrategy

The JSON configuration must conform to this schema.

---

# Example Strategy Config

{
  "name": "BTC Pullback Grid",
  "strategyType": "GridStrategy",
  "symbol": "BTC",
  "trend": {
    "emaFast": 20,
    "emaSlow": 50,
    "emaTrend": 200
  },
  "bias": {
    "rsiLength": 14,
    "rsiThreshold": 50
  },
  "entry": {
    "pullbackPercent": 1.2,
    "confirmationCandles": 2
  },
  "grid": {
    "levels": 4,
    "spacing": [0.35,0.7,1.05,1.4],
    "sizeDistribution": [0.2,0.25,0.25,0.3]
  },
  "exit": {
    "takeProfitPercent": 0.8,
    "trailingStop": false
  },
  "hedge": {
    "enabled": true,
    "percent": 0.3
  },
  "risk": {
    "maxExposure": 2,
    "dailyLossLimitPercent": 2,
    "cooldownMinutes": 30
  }
}

---

# Schema Sections

Strategy configs are divided into the following sections:

trend  
bias  
entry  
grid  
exit  
hedge  
risk

---

# Trend Section

Controls macro trend detection.

Fields:

emaFast – integer  
emaSlow – integer  
emaTrend – integer

Example:

emaFast = 20  
emaSlow = 50  
emaTrend = 200

---

# Bias Section

Used for directional confirmation.

Fields:

rsiLength – integer  
rsiThreshold – number

Example:

RSI(14) > 50

---

# Entry Section

Defines pullback entry conditions.

Fields:

pullbackPercent – number  
confirmationCandles – integer

Example:

pullbackPercent = 1.2

---

# Grid Section

Defines grid deployment behaviour.

Fields:

levels – integer  
spacing – array of percentages  
sizeDistribution – array of allocation ratios

Example:

levels = 4

spacing:

0.35%  
0.70%  
1.05%  
1.40%

---

# Exit Section

Controls how positions close.

Fields:

takeProfitPercent – number  
trailingStop – boolean

---

# Hedge Section

Defines defensive hedge behaviour.

Fields:

enabled – boolean  
percent – number

Example:

percent = 0.30 (30% hedge)

---

# Risk Section

Defines global safety limits.

Fields:

maxExposure – number  
dailyLossLimitPercent – number  
cooldownMinutes – integer

---

# Schema Validation

When a strategy is saved:

1. JSON must be validated against this schema
2. values must fall within safe limits
3. risk constraints must be enforced

The API performs validation before saving the configuration.

---

# Future Extensions

Possible future schema additions:

volatilityAdjustments  
dynamicGridSpacing  
multiSymbolStrategies  
AI sentiment filters

The schema should remain backward compatible.