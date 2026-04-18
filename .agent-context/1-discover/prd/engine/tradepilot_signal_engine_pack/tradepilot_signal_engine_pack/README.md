# TradePilot Derived Signal Engine Pack

A C# starter pack for the **engine-defined derived signals** used by TradePilot strategies.

Included:
- signal engine abstractions
- registry
- evaluation context interfaces
- candle and market data models
- concrete starter implementations for:
  - candle patterns
  - liquidity sweep
  - structure shift
  - range state
  - regime state

## Intent

These signals are **not raw indicators** like EMA or RSI.
They are higher-level derived signals that should be implemented once in the engine and reused by all strategies.

## Design goals

- keep strategy YAML clean
- centralize logic for complex concepts
- avoid duplicated strategy-specific implementations
- make signals testable in isolation
- allow parameterized signal definitions

## Suggested dependencies

No external packages are required for this starter pack.

## Important note

These implementations are **starter definitions**, not canonical market truth.
You will probably tune thresholds and pivot logic as TradePilot evolves.