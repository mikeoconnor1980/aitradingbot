# Live Execution Engine — Plumbing for Live Trading

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-04-03T00:00:00Z

## User Story

As a **platform operator**, I want to **have a live execution engine that places real orders on Hyperliquid using a subscriber's keys** so that **strategies proven in backtesting can execute identically in live trading by swapping only the execution engine implementation**.

### Business Value

The backtest pipeline is fully proven using `SimulatedExecutionEngine`. The entire trading pipeline (`StrategyEngine` → `GridController` → `RiskEngine` → `PositionManager`) is shared between backtest and live. The only missing piece is a real `IExecutionEngine` implementation that submits orders to Hyperliquid. This PBI closes that gap and also builds the `CandleBuilder` that converts the existing WebSocket trade stream into confirmed candles — the live data source that replaces `CandleReplayEngine`.

---

## Requirements

### Functional Requirements

- [ ] **`HyperliquidExecutionEngine : IExecutionEngine`** — New class in the Infrastructure layer that wraps the existing `HyperliquidOrderService` and `HyperliquidSigner` to implement `PlaceOrderAsync`, `CancelOrderAsync`, and `CancelAllOrdersAsync` using a subscriber's wallet keys
- [ ] **Per-subscriber key resolution** — The execution engine must resolve the active subscriber's private key at execution time (from encrypted storage) so that orders are signed with the correct wallet
- [ ] **`CandleBuilder`** — New component that assembles confirmed OHLCV candles from the `TradeTickDto` stream provided by the existing `HyperliquidWebSocketClient`. Must bucket trades into 15m, 1h, and 4h candles and detect candle close boundaries
- [ ] **`MarketStateStore`** — Shared mutable store that the WebSocket trade feed updates; provides latest candle state for each symbol/timeframe. The `CandleClock` reads from this store to detect close transitions
- [ ] **Candle persistence** — Confirmed candles produced by `CandleBuilder` are persisted via the existing `ICandleRepository` so that indicator warmup and history queries work identically to backtesting
- [ ] **Integration with existing `CandleClock`** — When `CandleBuilder` confirms a candle, it feeds it to the existing `CandleClock.ProcessCandleAsync()` which emits `CandleClosedEvent` exactly once (same class used in backtesting)

### Non-Functional Requirements

- [ ] `HyperliquidExecutionEngine` must never hold private keys in memory longer than the signing operation
- [ ] `CandleBuilder` must handle out-of-order trades and WebSocket reconnection gaps gracefully
- [ ] All new components must be unit-testable with injected dependencies (no static state)
- [ ] No changes to `CandleClock`, `StrategyScheduler`, `StrategyEngine`, `GridController`, `RiskEngine`, or `PositionManager` — these are shared and already proven

---

## Technical Considerations

### Bounded Context

**Context:** Infrastructure (execution engine), Application/Scheduling (candle builder, market state store)

### Existing Components to Reuse

| Component | Location | Usage |
|-----------|----------|-------|
| `IExecutionEngine` | `Application/Abstractions/Services/IExecutionEngine.cs` | Interface to implement |
| `HyperliquidOrderService` | `Api/Services/HyperliquidOrderService.cs` | Wrap for order placement |
| `HyperliquidSigner` | Infrastructure | Sign orders with subscriber keys |
| `HyperliquidWebSocketClient` | `Infrastructure/Services/HyperliquidWebSocketClient.cs` | Trade stream source |
| `CandleClock` | `Application/Scheduling/CandleClock.cs` | Candle close detection (unchanged) |
| `ICandleRepository` | `Application/Abstractions/Repositories/ICandleRepository.cs` | Persist confirmed candles |
| `TradeTickDto` | `Application/MarketData/Models/TradeTickDto.cs` | Input to CandleBuilder |

### New Components

| Component | Layer | Description |
|-----------|-------|-------------|
| `HyperliquidExecutionEngine` | Infrastructure/Services | Live `IExecutionEngine` implementation |
| `CandleBuilder` | Application/Scheduling | Assembles confirmed candles from trade ticks |
| `MarketStateStore` | Application/Scheduling | Thread-safe shared state for latest candles per symbol/timeframe |

### Key Design Decisions

- `CandleBuilder` detects candle close by timestamp boundary crossing (e.g., a trade at 12:15:00.001 closes the 12:00–12:15 candle), not by wall-clock time
- `MarketStateStore` must be thread-safe (`ConcurrentDictionary`) as WebSocket writes and strategy reads happen on different threads
- `HyperliquidExecutionEngine` takes a subscriber context/key provider, not a hardcoded key

---

## Acceptance Criteria

- [ ] Given a strategy config and subscriber keys, when the strategy emits a `DeployGrid` signal, then `HyperliquidExecutionEngine` places limit orders on Hyperliquid testnet
- [ ] Given a continuous trade stream from the WebSocket, when a 15m boundary is crossed, then `CandleBuilder` emits a confirmed candle with correct OHLCV values
- [ ] Given a confirmed candle from `CandleBuilder`, when fed to `CandleClock.ProcessCandleAsync()`, then a `CandleClosedEvent` is emitted exactly once
- [ ] Given `CandleBuilder` producing candles, then each confirmed candle is persisted via `ICandleRepository`
- [ ] Given a WebSocket reconnection gap, when trades resume, then `CandleBuilder` does not emit a partial candle for the gap period

---

## Dependencies

- Existing `HyperliquidWebSocketClient` trade stream (implemented)
- Existing `HyperliquidOrderService` and `HyperliquidSigner` (implemented)
- Existing `CandleClock` and `CandleClosedEvent` (implemented, shared with backtesting)
- Hyperliquid testnet access for integration testing
