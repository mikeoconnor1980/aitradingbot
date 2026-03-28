# Live Trading Pipeline: Multi-Strategy Support

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-28

## User Story

As a **trader**, I want to **run non-grid strategies (EMA crossover, MACD, RSI, Bollinger) in live trading** so that **I can trade using the strategies I've validated through backtesting**.

## Problem Statement

The live trading pipeline (`StrategyScheduler` → Worker) is hard-wired to `IStrategyEngine` + `IGridController`. After the multi-strategy backtesting PBI (F6) introduces the unified `ITradingStrategy` interface and non-grid strategies, those strategies can only be backtested — not run live. This PBI wires the `ITradingStrategy` abstraction into the live `StrategyScheduler` and Worker so that any strategy validated in backtesting can be deployed for live trading.

## Requirements

### Functional Requirements

1. `StrategyScheduler` updated to use `ITradingStrategy` instead of `IStrategyEngine` + `IGridController`.
2. Worker resolves the correct `ITradingStrategy` implementation per subscriber via `ITradingStrategyFactory`.
3. `ActiveStrategy` / `StrategyConfig` database records carry a `StrategyType` field.
4. Position-based signals (`OpenLong`, `CloseLong`, `OpenShort`, `CloseShort`, `AdjustStopLoss`, `SetTakeProfit`) are handled by the real `ExecutionEngine` via Hyperliquid.
5. `IPositionManager` handles non-grid position lifecycle (open, scale, close).
6. `IRiskEngine` validates non-grid signals with appropriate risk rules.

### Non-Functional Requirements

- Adding a new strategy to live trading should require only: implementing `ITradingStrategy`, registering in the factory, and configuring risk rules.
- No degradation to existing grid strategy live trading performance.

## Acceptance Criteria

- [ ] **Given** a subscriber has an active EMA Crossover strategy configured, **When** a 15m candle closes, **Then** the `StrategyScheduler` evaluates using the `EmaCrossoverStrategy` and produces position signals.
- [ ] **Given** a subscriber has an active Grid strategy configured, **When** a 15m candle closes, **Then** the existing grid trading behavior is preserved unchanged (regression).
- [ ] **Given** an `OpenLong` signal is approved by the `RiskEngine`, **When** `PositionManager.ExecuteSignalsAsync` runs, **Then** a limit/market order is placed on Hyperliquid.
- [ ] **Given** risk limits are exceeded, **When** a non-grid signal is validated, **Then** the `RiskEngine` rejects the signal.

### Release Notes Information

- **Heading**: Live Trading: Multi-Strategy Support
- **Release note type**: Feature
- **Release Note Summary**: The live trading pipeline now supports running non-grid strategies (EMA Crossover, MACD, RSI Mean Reversion, Bollinger Band Breakout) that were previously only available in backtesting.
- **Release Notes Audience**: Product
- **Breaking Change**: No

## Technical Considerations

- Depends on F6 (Multi-Strategy Backtesting Support) being completed first — requires `ITradingStrategy`, `ITradingStrategyFactory`, expanded signal types, and expanded `IndicatorSnapshot`.
- `StrategyScheduler` refactored to use `ITradingStrategy.ProcessAsync()` instead of the two-step `IStrategyEngine.EvaluateAsync()` → `IGridController.ProcessAsync()`.
- Live `MarketContextBuilder` (not just the backtest variant) needs to calculate expanded indicators.
- `ExecutionEngine` (Hyperliquid) needs to handle the new signal types for real order placement.

## Out of Scope

- New strategies beyond those introduced in F6.
- Strategy parameter optimization during live trading.
- Multi-strategy running concurrently on a single account.
