# Trading Strategy Authoring, Parsing, Validation, and Execution Requirements

## Executive summary
The application shall support three strategy authoring entry points: UI selectors, natural-language strategy input, and Pine Script import. All three shall converge into a deterministic compilation pipeline that produces a canonical JSON strategy definition. AST shall be treated as an internal compilation representation; canonical JSON shall be the persisted source of truth; and the trading engine shall execute only from compiled runtime objects.

## High-level architecture

```text
UI selectors / Natural language / Pine import
        -> input adapters
        -> internal AST
        -> validation + normalization
        -> canonical JSON strategy definition
        -> typed runtime compilation
        -> backtest / paper / live trading engine
```

## Goals
- Support UI-driven strategy authoring
- Support natural-language interpretation into a strict structured DTO
- Support best-effort Pine import using a Python-side parser
- Normalize all strategies into one canonical JSON format
- Compile canonical JSON into runtime execution plans
- Make strategies explainable, versioned, and auditable

## Key design decisions
- **AST is internal** and source-agnostic
- **Canonical JSON is persisted** in the database
- **LLM output is never executed directly**
- **Trading engine consumes compiled runtime objects only**
- **YAML may be used for export/import**, but JSON is the preferred internal contract

## Recommended pipeline
1. Receive strategy input from UI, natural language, or Pine import.
2. Convert input to a strict intermediate DTO or source adapter result.
3. Build internal AST.
4. Validate AST and business rules.
5. Normalize to canonical JSON.
6. Validate canonical JSON.
7. Persist strategy revision.
8. Compile canonical JSON to an execution plan for backtest or live trading.

## Natural-language path
- LLM returns **StrategyIntentDto** only.
- StrategyIntentDto is schema-validated.
- Deterministic .NET code converts DTO to AST.
- AST is validated and normalized to canonical JSON.

### Example StrategyIntentDto
```json
{
  "strategyType": "grid",
  "direction": "long",
  "market": { "symbol": "BTC-USD", "timeframe": "15m" },
  "grid": {
    "levels": 10,
    "spacingType": "percent",
    "spacingValue": 0.5,
    "spacingDirection": "down",
    "anchorPrice": "market"
  },
  "exit": {
    "takeProfit": { "reference": "average_entry_price", "offsetPercent": 2, "direction": "above" },
    "stopLoss": { "reference": "average_entry_price", "offsetPercent": 6, "direction": "below" }
  },
  "assumptions": ["anchor price assumed to be current market price"],
  "confidence": 0.94
}
```

## AST requirements
The AST shall support both structural nodes and expression nodes.

### Structural nodes
- StrategyNode
- GridEntryPlanNode
- DcaPlanNode
- ExitPlanNode
- SizingRuleNode
- RiskRuleNode

### Expression nodes
- AndNode
- OrNode
- ComparisonNode
- CrossesAboveNode
- IndicatorCallNode
- ConstantNode
- MarketSeriesReferenceNode

### Example grid AST
```text
StrategyNode
  Name: "10 Grid Long"
  Type: Grid
  Direction: Long
  Market: BTC-USD / 15m
  EntryPlan: GridEntryPlan(levels=10, spacing=0.5% down, anchor=market)
  PositionManagement: AverageEntry(method=weighted_average_fill_price)
  ExitPlan: TP(+2% from average entry), SL(-6% from average entry)
```

## Validation requirements
Validation shall occur at multiple levels:
- schema validation
- business validation
- AST validation
- canonical JSON validation
- runtime compilation validation

Validation shall return machine-readable errors plus user-readable messages.

## Canonical JSON requirements
Canonical JSON shall:
- be the persisted source of truth
- be versioned
- be suitable for typed deserialization in .NET
- contain enough detail for backtest and live execution
- include source metadata and revision history support

### Example canonical JSON
```json
{
  "schemaVersion": 1,
  "name": "10 Grid Long",
  "strategyType": "grid",
  "direction": "long",
  "market": { "symbol": "BTC-USD", "timeframe": "15m" },
  "entry": {
    "mode": "grid",
    "anchorPrice": { "type": "market" },
    "levels": 10,
    "spacing": { "type": "percent", "value": 0.5, "direction": "down" }
  },
  "positionManagement": {
    "averaging": { "enabled": true, "method": "weighted_average_fill_price" }
  },
  "exit": {
    "takeProfit": {
      "enabled": true,
      "reference": "average_entry_price",
      "offset": { "type": "percent", "value": 2, "direction": "above" },
      "applyTo": "full_position"
    },
    "stopLoss": {
      "enabled": true,
      "reference": "average_entry_price",
      "offset": { "type": "percent", "value": 6, "direction": "below" },
      "applyTo": "full_position"
    }
  },
  "source": {
    "entryPoint": "natural_language",
    "summary": "Long grid with 10 levels, 0.5% spacing, TP 2%, SL 6%"
  }
}
```

## How to use canonical JSON in strategies
### Storage
Store canonical JSON in the database with schema version, metadata, source details, and revision history.

### Review and editing
Render summaries and edit forms from canonical JSON so the original entry method does not matter.

### Backtesting
Compile canonical JSON into a typed execution plan that resolves data dependencies, indicators, and order lifecycle rules.

### Live trading
Use the same compiled plan wherever possible. Venue adapters may translate execution details but must not silently alter semantics.

### Analytics
Use canonical JSON to power strategy diffing, explainability, usage analytics, and validation reports.

## Suggested .NET services
- IStrategyAuthoringService
- INaturalLanguageStrategyInterpreter
- IPineImportService
- IAstBuilder
- IStrategyValidator
- IStrategyNormalizer
- IStrategyCompiler
- IStrategyRepository
- IBacktestService
- ILiveTradingService

## Delivery phases
### Phase 1
- AST model
- canonical JSON schema
- validators
- UI selector path
- runtime compiler

### Phase 2
- natural-language extraction via LLM
- DTO validation
- assumptions/confidence UI

### Phase 3
- Python sidecar for Pine import
- Pine-to-AST mapping
- unsupported feature handling

## Final recommendation
Persist canonical JSON, not AST. Use AST internally. Use LLMs only for intent extraction into a strict DTO. Execute only from compiled runtime objects.
