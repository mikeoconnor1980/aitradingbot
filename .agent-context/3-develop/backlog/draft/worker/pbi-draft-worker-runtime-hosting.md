# Worker Runtime Hosting — Background Services for Live Strategy Execution

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-04-03T00:00:00Z

## User Story

As a **platform operator**, I want to **have a long-running Worker process that maintains a WebSocket connection, detects candle closes, and executes strategies for all active subscribers** so that **live trading runs autonomously without manual intervention**.

### Business Value

The live execution engine (PBI 1) provides the components — `HyperliquidExecutionEngine`, `CandleBuilder`, `MarketStateStore`. This PBI wires them together as hosted `BackgroundService`s in the `TradePilot.Worker` project, creating the runtime that keeps the trading system alive 24/7. This is the bridge from "strategies work in backtest" to "strategies trade real markets".

---

## Requirements

### Functional Requirements

- [ ] **`MarketDataBackgroundService : BackgroundService`** — Manages the shared Hyperliquid WebSocket connection. Feeds `TradeTickDto` events into `CandleBuilder` → `CandleClock`. Handles automatic reconnection with exponential backoff on disconnection
- [ ] **`StrategyExecutionBackgroundService : BackgroundService`** — Subscribes to `CandleClock.CandleClosed` events. On each event: loads all active subscribers, resolves their strategy configs, and fans out execution through `StrategyScheduler` per subscriber. Market context (indicators) is built once and shared; strategy evaluation and order execution are per-subscriber
- [ ] **Subscriber lifecycle management** — Load active subscribers on startup. Support dynamic reload when subscribers are activated/deactivated via the API (without requiring Worker restart). Maintain a registry of active subscriber contexts
- [ ] **Indicator warmup on startup** — When the Worker starts (or a subscriber activates), load the last N candles from `ICandleRepository` and feed them through `IMarketContextBuilder.UpdateIndicators()` to seed indicator state before the first live candle triggers strategy evaluation
- [ ] **Graceful shutdown** — On `CancellationToken` signal, complete any in-flight strategy evaluations before stopping. Do not leave orphaned orders
- [ ] **Worker DI registration** — Wire all dependencies in `TradePilot.Worker/Program.cs`: persistence, infrastructure (WebSocket, REST client, signer), application services (strategy engine, grid controller, risk engine, position manager), and the two background services

### Non-Functional Requirements

- [ ] WebSocket reconnection must be automatic with exponential backoff (1s → 2s → 4s → ... → 60s max)
- [ ] Subscriber fan-out must be concurrent but bounded (configurable max parallelism, default: 5)
- [ ] Worker must log each candle close event and each strategy evaluation (structured logging)
- [ ] Worker startup must complete indicator warmup before processing the first live candle
- [ ] No shared mutable state between subscriber executions (each gets its own `GridState`, `PositionState`, scoped services)

---

## Technical Considerations

### Bounded Context

**Context:** Worker (hosting), Application/Scheduling (shared pipeline)

### Architecture

```
TradePilot.Worker
├── Program.cs                              — DI composition root
├── Services/
│   ├── MarketDataBackgroundService.cs      — WebSocket → CandleBuilder → CandleClock
│   └── StrategyExecutionBackgroundService.cs — CandleClosed → per-subscriber fan-out
└── Models/
    └── SubscriberContext.cs                — Per-subscriber runtime state
```

### Live Trading Flow (end-to-end)

```
MarketDataBackgroundService
  → HyperliquidWebSocketClient.SubscribeToTradesAsync("BTC")
  → CandleBuilder.ProcessTrade(trade)
  → CandleClock.ProcessCandleAsync(confirmedCandle)
  → CandleClosedEvent

StrategyExecutionBackgroundService
  → CandleClock.CandleClosed event
  → For each active subscriber (parallel, bounded):
      → IMarketContextBuilder.Build()  (shared indicator state)
      → StrategyScheduler.HandleCandleClosedAsync()
        → StrategyEngine.EvaluateAsync()
        → GridController.ProcessAsync()
        → RiskEngine.ValidateAsync()
        → PositionManager.ExecuteSignalsAsync()
          → HyperliquidExecutionEngine.PlaceOrderAsync()  (subscriber's keys)
```

### Existing Components (unchanged)

| Component | Usage |
|-----------|-------|
| `CandleClock` | Candle close detection — same class as backtesting |
| `StrategyScheduler` | Strategy orchestration — same class as backtesting |
| `StrategyEngine`, `GridController`, `RiskEngine` | Core pipeline — identical to backtesting |
| `HyperliquidWebSocketClient` | Already implemented, provides trade stream |

### New Components

| Component | Layer | Description |
|-----------|-------|-------------|
| `MarketDataBackgroundService` | Worker/Services | Hosts WebSocket connection lifecycle, feeds CandleBuilder |
| `StrategyExecutionBackgroundService` | Worker/Services | Subscribes to candle events, fans out per-subscriber |
| `SubscriberContext` | Worker/Models | Holds per-subscriber `GridState`, `PositionState`, config reference |
| `ActiveSubscriberRegistry` | Worker/Services | Thread-safe registry of active subscriber contexts, supports dynamic add/remove |

### Key Design Decisions

- Market data is shared (one WebSocket connection) — indicators computed once per candle close
- Strategy evaluation is per-subscriber — isolated `GridState`, `PositionState`, `IExecutionEngine` scoped to subscriber keys
- `StrategyScheduler` is instantiated per-subscriber (it holds per-subscriber `GridState` + `PositionState`)
- The Worker does NOT serve HTTP — the API project handles all REST/SignalR endpoints; the Worker is a pure background processor
- Subscriber activation/deactivation communicated via database polling initially (simple); can upgrade to message bus later

---

## Acceptance Criteria

- [ ] Given the Worker is started, then it connects to the Hyperliquid WebSocket and begins receiving trade data
- [ ] Given a WebSocket disconnection, then the Worker reconnects automatically with exponential backoff and resumes without duplicate candle events
- [ ] Given one active subscriber with a valid strategy config, when a 15m candle closes, then the full pipeline executes and orders are placed (or paper-logged) for that subscriber
- [ ] Given three active subscribers, when a 15m candle closes, then all three strategy evaluations execute concurrently and orders use each subscriber's own keys
- [ ] Given a new subscriber activates via the API, then the Worker picks them up within 60 seconds without restart
- [ ] Given the Worker is shutting down (`Ctrl+C` / SIGTERM), then in-flight evaluations complete before the process exits
- [ ] Given the Worker starts fresh, then indicator warmup completes from historical candles before the first live strategy evaluation runs

---

## Dependencies

- **PBI 1 (Live Execution Engine)** — `HyperliquidExecutionEngine`, `CandleBuilder`, `MarketStateStore` must exist
- Existing `HyperliquidWebSocketClient` (implemented)
- Existing `CandleClock`, `StrategyScheduler` (implemented, shared with backtesting)
- Existing persistence layer for subscriber/strategy data
