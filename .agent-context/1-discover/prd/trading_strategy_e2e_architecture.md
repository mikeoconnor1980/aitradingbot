# End-to-End Trading Strategy Architecture

## Overview

This document describes the architecture for building trading strategies using multiple input methods and converting them into a unified execution model.

---

## High-Level Flow

```
UI Input
Pine Script
Natural Language
        ↓
     Parsers / Converters
        ↓
         AST
        ↓
   Validation & Normalisation
        ↓
   Canonical JSON Model
        ↓
     Trading Engine
```

---

## 1. Strategy Input Methods

### 1.1 UI Selectors
- Structured form inputs
- Dropdowns, toggles, numeric inputs
- Fully deterministic

### 1.2 Natural Language
- Free-text user input
- Parsed using LLM into structured intent
- Requires validation layer

### 1.3 Pine Script Import
- Parsed using Python service (e.g. pynescript)
- Extract supported constructs
- Map into internal model

---

## 2. Parsing Layer

Each input type is converted into a unified internal representation.

### Responsibilities:
- Extract structure
- Identify strategy type
- Capture parameters
- Detect unsupported features

---

## 3. Abstract Syntax Tree (AST)

AST represents the logical structure of the strategy.

### Example:
```
Strategy(Grid, Long)
├── EntryPlan(Levels=10, Spacing=0.5%)
└── ExitPlan
    ├── TakeProfit(+2% avg)
    └── StopLoss(-6% avg)
```

### Responsibilities:
- Represent strategy logic
- Enable validation
- Allow transformations

---

## 4. Validation & Normalisation

### Validation:
- Required fields present
- Values within valid ranges
- Supported features only

### Normalisation:
- Apply defaults
- Standardise formats
- Resolve ambiguities

---

## 5. Canonical JSON Model

The AST is converted into a stable JSON structure.

### Example:
```json
{
  "strategyType": "grid",
  "direction": "long",
  "levels": 10,
  "spacingPercent": 0.5,
  "takeProfit": {
    "reference": "average_entry_price",
    "value": 2
  },
  "stopLoss": {
    "reference": "average_entry_price",
    "value": 6
  }
}
```

### Purpose:
- Storage in database
- Versioning
- API communication
- Debugging

---

## 6. Trading Engine

The engine consumes the canonical JSON.

### Responsibilities:
- Deserialize JSON into runtime objects
- Build execution plan
- Run backtests
- Execute live trades

---

## 7. Execution Flow

```
Load JSON
→ Deserialize to StrategyDefinition
→ Validate again
→ Compile Execution Plan
→ Run Strategy
```

---

## 8. Key Design Principles

### Separation of Concerns
- Input parsing separate from execution
- LLM isolated from core engine

### Deterministic Core
- All execution logic is code-driven
- No AI in execution path

### Extensibility
- New input methods can be added easily
- AST and JSON act as stable contracts

### Safety
- Validation before execution
- Clear handling of unsupported features

---

## 9. Benefits

- Multiple user entry points
- Unified execution model
- Scalable architecture
- Easier testing and debugging
- Future-proof design

---

## 10. Summary

This architecture ensures:

- Flexibility in how users define strategies
- Strong validation and control
- Reliable execution
- Clear separation between AI and trading logic

---

## One-Line Summary

**Multiple inputs → AST → validated JSON → deterministic trading engine**
