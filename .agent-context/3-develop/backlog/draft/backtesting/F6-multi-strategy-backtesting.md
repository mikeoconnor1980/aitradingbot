# Multi-Strategy Backtesting Support

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-28

## User Story

As a **trader**, I want to **backtest strategies beyond grid trading (e.g., EMA crossover, MACD, RSI mean reversion, Bollinger Band breakout)** so that **I can evaluate different trading approaches against historical data before risking capital**.

## Problem Statement

The backtesting engine currently only supports the grid strategy. The pipeline is hard-wired: `IStrategyEngine.EvaluateAsync()` produces a `StrategyEvaluation` (setup detected: yes/no), which always flows through `IGridController`. Non-grid strategies like EMA crossover or MACD need to express directional signals (go long, go short, flatten) rather than "grid setup exists." This coupling prevents backtesting any strategy that isn't grid-based.

## Requirements

### Functional Requirements

1. **Unified strategy interface (`ITradingStrategy`)** — A new interface that all strategies implement, returning trading signals directly. Each strategy owns its own signal production logic.
2. **Grid strategy adapter** — The existing `GridStrategyEngine` + `GridController` are wrapped in an `ITradingStrategy` implementation so existing grid backtesting continues to work unchanged.
3. **EMA Crossover strategy** — Long when fast EMA crosses above slow EMA, short on the inverse. Configurable EMA periods.
4. **MACD strategy** — Long/short based on MACD line/signal line crossover and histogram direction. Configurable fast/slow/signal periods.
5. **RSI Mean Reversion strategy** — Buy when RSI drops below oversold threshold, sell when RSI rises above overbought threshold. Configurable period and thresholds.
6. **Bollinger Band Breakout strategy** — Enter on price breaking out of a Bollinger Band squeeze. Configurable period and standard deviation multiplier.
7. **Strategy factory (`ITradingStrategyFactory`)** — Resolves the correct `ITradingStrategy` implementation from a `StrategyType` string (e.g., `"Grid"`, `"EmaCrossover"`, `"Macd"`, `"RsiMeanReversion"`, `"BollingerBreakout"`).
8. **New position-based signal types** — `OpenLong`, `CloseLong`, `OpenShort`, `CloseShort`, `AdjustStopLoss`, `SetTakeProfit` added alongside existing grid signals.
9. **Expanded `IndicatorSnapshot`** — Add typed properties for MACD (line, signal, histogram), Bollinger Bands (upper, middle, lower), EMA(200), and VWAP.
10. **Expanded `BacktestMarketContextBuilder`** — Calculate the new indicators (MACD, Bollinger Bands, EMA 200, VWAP) during the backtest warmup and evaluation loop.
11. **Multi-position tracking in `SimulatedExecutionEngine`** — Support multiple concurrent positions, partial fills, and pyramiding (scaling in/out).
12. **`BacktestConfig` extended** — Add `StrategyType` field so the backtest runner can resolve the correct strategy via the factory.
13. **`BacktestRunner` updated** — Use `ITradingStrategyFactory` to resolve the strategy. The core replay loop (candle loading, warmup, evaluation, metrics) remains unchanged.
14. **Backtest API updated** — The backtest request endpoint accepts `strategyType` and strategy-specific configuration JSON.
15. **Backtest UI updated** — Strategy type selector dropdown, strategy-specific configuration fields that change dynamically based on selected strategy.

### Non-Functional Requirements

- Backtesting a non-grid strategy over 1 year of 15m candles should complete within the same performance envelope as grid backtesting (~seconds, not minutes).
- All new strategies must be testable with the same `BacktestRunner` — no strategy-specific runner code.
- Adding a new strategy in the future should require only: implementing `ITradingStrategy`, registering in the factory, and adding any new indicators to `IndicatorSnapshot`.

## Acceptance Criteria

- [ ] **Given** a backtest is configured with `strategyType: "EmaCrossover"` and valid EMA parameters, **When** the backtest runs against historical candle data, **Then** the engine produces `OpenLong`/`CloseLong`/`OpenShort`/`CloseShort` signals at EMA crossover points and returns a valid `BacktestResult` with PnL metrics.
- [ ] **Given** a backtest is configured with `strategyType: "Macd"` and valid MACD parameters, **When** the backtest runs, **Then** the engine produces trading signals based on MACD line/signal crossovers and returns a valid `BacktestResult`.
- [ ] **Given** a backtest is configured with `strategyType: "RsiMeanReversion"` and valid RSI parameters, **When** the backtest runs, **Then** the engine opens positions at oversold/overbought thresholds and returns a valid `BacktestResult`.
- [ ] **Given** a backtest is configured with `strategyType: "BollingerBreakout"` and valid Bollinger parameters, **When** the backtest runs, **Then** the engine enters positions on band breakouts and returns a valid `BacktestResult`.
- [ ] **Given** a backtest is configured with `strategyType: "Grid"`, **When** the backtest runs, **Then** the existing grid backtesting behavior is preserved unchanged (regression test).
- [ ] **Given** an unknown `strategyType` is provided, **When** the backtest is requested, **Then** a validation error is returned.
- [ ] **Given** the `IndicatorSnapshot` is built during backtesting, **When** a MACD strategy runs, **Then** `MacdLine`, `MacdSignal`, and `MacdHistogram` are populated correctly.
- [ ] **Given** a non-grid strategy produces an `OpenLong` signal, **When** the `SimulatedExecutionEngine` processes subsequent candles, **Then** the position is tracked with entry price, size, and unrealised PnL.
- [ ] **Given** a strategy scales into a position (pyramiding), **When** multiple `OpenLong` signals are produced at different prices, **Then** the position tracks multiple entries and calculates blended average entry.
- [ ] **Given** the backtest UI, **When** a user selects "EMA Crossover" from the strategy dropdown, **Then** the configuration form shows EMA-specific fields (fast period, slow period) and hides grid-specific fields.
- [ ] **Given** the backtest API, **When** a request includes `strategyType: "EmaCrossover"` and `strategyConfigJson` with EMA parameters, **Then** the API accepts the request and runs the backtest.

### Release Notes Information

- **Heading**: Multi-Strategy Backtesting Support
- **Release note type**: Feature
- **Release Note Summary**: The backtesting engine now supports multiple trading strategies beyond grid trading, including EMA Crossover, MACD, RSI Mean Reversion, and Bollinger Band Breakout. Users can select a strategy type, configure its parameters, and run backtests against historical data.
- **Release Notes Audience**: Product
- **Breaking Change**: No

## Technical Considerations

### Strategy Interface

New `ITradingStrategy` interface that all strategies implement:
- Returns `IReadOnlyList<TradingSignal>` directly (strategy owns signal production)
- `GridTradingStrategy` adapter wraps existing `GridStrategyEngine` + `GridController` internally
- `ITradingStrategyFactory` resolves the correct implementation from a `StrategyType` string

### Indicator Model

Expand `IndicatorSnapshot` with typed properties:
- MACD: `MacdLine`, `MacdSignal`, `MacdHistogram`
- Bollinger Bands: `BollingerUpper`, `BollingerMiddle`, `BollingerLower`
- `Ema200`, `Vwap`

### Signal Types

New `SignalType` constants: `OpenLong`, `CloseLong`, `OpenShort`, `CloseShort`, `AdjustStopLoss`, `SetTakeProfit`

### Position Simulation

`SimulatedExecutionEngine` extended to support:
- Multiple concurrent positions
- Partial fills
- Pyramiding (scaling in/out)
- Stop-loss and take-profit order types

### API Endpoints (if relevant)

Existing backtest endpoint updated to accept `strategyType` field in the request body.

### Integration Events (if relevant)

None — backtesting is a stateless request/response operation.

### Jobs (if relevant)

None.

## Out of Scope

- **Live trading with non-grid strategies** — the `StrategyScheduler` / Worker pipeline stays grid-only. A separate PBI will cover wiring `ITradingStrategy` into live trading.
- **Detailed technical design** — class diagrams, sequence diagrams, and file-level changes are deferred to the implementation planning phase.
