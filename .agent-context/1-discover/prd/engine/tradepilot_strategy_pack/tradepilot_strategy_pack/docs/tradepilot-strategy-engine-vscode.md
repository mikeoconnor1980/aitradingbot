# TradePilot Strategy Engine Pack

This pack is designed for **C# / .NET** projects and assumes:

- YAML is the **authoring format**
- C# models are the **runtime format**
- validation happens **before a strategy becomes active**
- strategy type is the discriminator: `signal`, `dca`, `grid`

---

## Folder layout

```text
schema/
  tradepilot-strategy-schema.yaml

instances/
  tradepilot-strategy-instances.yaml

groups/
  tradepilot-strategy-groups.yaml

docs/
  README.md
  tradepilot-strategy-engine-vscode.md
  useful-direction.md
  csharp-implementation-notes.md

src/
  TradePilot.Strategies/StrategyContracts.cs
```

---

## What each file is for

## `schema/tradepilot-strategy-schema.yaml`
Human-readable schema contract for the strategy system.

Use it to:
- guide YAML authoring
- define required vs optional fields
- drive JSON Schema or FluentValidation generation
- shape your C# contracts

---

## `instances/tradepilot-strategy-instances.yaml`
Concrete strategy examples.

Use it to:
- seed development data
- test the parser
- test validation
- test strategy selection and execution

These are **reference instances**, not final production configs.

---

## `groups/tradepilot-strategy-groups.yaml`
Defines how strategies are grouped for UI, orchestration, enablement, or capital allocation.

Use it to:
- show strategies in the UI
- enable packs of strategies
- attach budget or account scope rules
- separate onboarding packs from advanced packs

---

## Recommended engine flow

```text
YAML file
  -> deserialize to raw DTO
  -> validate against strategy_type rules
  -> map to typed C# model
  -> compile conditions into runtime evaluators
  -> register strategy with orchestrator
  -> subscribe to candle / tick / position streams
  -> emit signals / orders / telemetry
```

---

## Suggested C# project structure

```text
src/
  TradePilot.Strategies/
    Contracts/
      StrategyBase.cs
      SignalStrategy.cs
      DcaStrategy.cs
      GridStrategy.cs
      Condition.cs
      ValueRef.cs
      IndicatorDefinition.cs
      SignalDefinition.cs

    Validation/
      StrategyValidator.cs
      SignalStrategyValidator.cs
      DcaStrategyValidator.cs
      GridStrategyValidator.cs

    Parsing/
      YamlStrategyLoader.cs
      StrategyMapper.cs

    Runtime/
      StrategyOrchestrator.cs
      ConditionEvaluator.cs
      IndicatorRegistry.cs
      SignalRegistry.cs
      StateStores/

    Compilation/
      StrategyCompiler.cs
      ExpressionFactory.cs
```

---

## How to think about the strategy types

## Signal
Event-driven directional logic.

Good for:
- trend pullback
- break and retest
- VWAP
- liquidity sweep

Runtime pattern:
- evaluate conditions
- emit entry signal
- manage trade lifecycle

---

## DCA
Staged accumulation or staged reduction.

Good for:
- buy below X
- buy every Y percent down
- blended average entry models

Runtime pattern:
- activate
- build ladder
- track fills
- update average price
- apply disable / exit logic

---

## Grid
Inventory and ladder management inside a range.

Good for:
- Pionex-style grids
- mean-reversion in bounded ranges
- fee-adjusted repeated fill capture

Runtime pattern:
- activate within range
- place laddered buy and sell orders
- recycle filled levels
- track realised grid profit and inventory state

---

## Important design guidance

## 1. Do not make the engine depend on YAML
YAML is an authoring format only.

At runtime you want:
- typed C# objects
- validated references
- compiled condition evaluators

---

## 2. Use a discriminator
Use `strategy_type` as the discriminator.

That means:
- base strategy fields are shared
- subtype fields are validated separately

This is cleaner than one giant optional object.

---

## 3. Split authoring and execution
A useful pattern is:

- YAML authoring model
- parsed DTO model
- validated domain model
- compiled runtime model

That reduces a lot of bugs.

---

## 4. Treat custom concepts as engine primitives
Some fields in the examples are intentionally engine-facing concepts:

- `liquidity_sweep`
- `structure_shift`
- `range_state`
- `candle_pattern`
- `regime_state`

These should be implemented as **named derived signals** in your engine, not ad hoc string magic spread everywhere.

---

## 5. Keep state explicit
This matters most for `dca` and `grid`.

A grid strategy needs state such as:
- active range
- open ladder levels
- realised grid pnl
- inventory held
- break-even price

A DCA strategy needs:
- activation status
- next buy level
- average entry
- total invested
- remaining budget

Do not try to fake this with signal-only logic.

---

## Suggested VS Code workflow

1. Open the whole pack folder
2. Review schema first
3. Review instance files against schema
4. Start building C# contracts from the schema
5. Add a validation layer before runtime loading
6. Add strategy unit tests using the instance file as fixtures

---

## Recommended libraries for C#

Possible choices:

- `YamlDotNet` for YAML parsing
- `FluentValidation` for validation
- `OneOf` or inheritance/discriminator mapping for strategy subtypes
- `System.Text.Json` for intermediate normalized export if helpful

---

## Suggested first implementation order

1. Parse schema and instances into DTOs
2. Build discriminated subtype mapping
3. Validate all referenced indicator ids and signal ids
4. Compile conditions into evaluators
5. Implement `signal` runtime first
6. Add `dca`
7. Add `grid`

That order will save you pain.