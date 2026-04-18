# TradePilot Strategy Pack

This pack contains a starter set of files for a **C# / .NET strategy engine** that supports:

- **signal** strategies
- **dca** strategies
- **grid** strategies

## Included files

- `schema/tradepilot-strategy-schema.yaml`
- `instances/tradepilot-strategy-instances.yaml`
- `groups/tradepilot-strategy-groups.yaml`
- `docs/tradepilot-strategy-engine-vscode.md`
- `docs/useful-direction.md`
- `docs/csharp-implementation-notes.md`
- `src/TradePilot.Strategies/StrategyContracts.cs`

## What this pack is trying to do

It gives you:

- a shared schema family
- concrete strategy examples
- grouping structure
- implementation guidance for a C# engine
- a starting point for contracts

## Recommended next step

Turn the schema into:
- JSON Schema for machine validation
- C# contracts + FluentValidation rules
- unit tests using the example strategy instances