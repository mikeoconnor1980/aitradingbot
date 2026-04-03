# Safety & Observability — Paper Trading, Reconciliation, and Health Monitoring

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-04-03T00:00:00Z

## User Story

As a **platform operator**, I want to **validate the live trading pipeline end-to-end without risking real capital, reconcile order fills against exchange state, and monitor system health** so that **I can confidently transition from backtesting to live trading with real money**.

### Business Value

The execution engine and Worker runtime make live trading technically possible, but running real money requires confidence. This PBI provides three safety layers: (1) paper trading mode to verify the full pipeline end-to-end without submitting orders, (2) fill reconciliation to detect discrepancies between expected and actual exchange state, and (3) health monitoring to ensure the system is alive and functioning. These are non-negotiable prerequisites before enabling real capital.

---

## Requirements

### Functional Requirements

#### Paper Trading Mode

- [ ] **Dry-run flag on `HyperliquidExecutionEngine`** — When enabled, the execution engine logs every order it would place (symbol, side, price, size, order type) but does not submit to Hyperliquid. Logs at `Information` level with a `[PAPER]` prefix
- [ ] **Paper trade recording** — Paper orders are persisted to a `PaperTrade` table with full order details and the timestamp, allowing post-session review and comparison against what backtesting predicted
- [ ] **Configuration toggle** — Paper mode is controlled via `appsettings.json` (`Trading:PaperMode: true/false`), defaulting to `true`. Must not require code changes to switch between paper and live
- [ ] **Pipeline fidelity** — In paper mode, the full pipeline executes identically: `StrategyEngine` → `GridController` → `RiskEngine` → `PositionManager` → `HyperliquidExecutionEngine`. Only the final order submission is suppressed. `GridState` and `PositionState` are updated as if the order filled at the requested price

#### Fill & Position Reconciliation

- [ ] **Per-user fill monitoring** — Subscribe to Hyperliquid per-user WebSocket streams (fills, order updates) for each active subscriber. When a fill is received, compare against the expected fill from `PositionManager`
- [ ] **Position reconciliation** — Periodically (every 5 minutes, configurable) query each subscriber's Hyperliquid positions via REST and compare against the system's internal `PositionState`. Log discrepancies as warnings
- [ ] **Discrepancy alerting** — When a reconciliation mismatch is detected (unexpected fill, missing fill, position size mismatch), log at `Warning` level with full details. Future: webhook/email notification (out of scope for this PBI)
- [ ] **Reconciliation state** — Store the last reconciliation result per subscriber (timestamp, match/mismatch, details) for API/UI consumption

#### Health Monitoring

- [ ] **WebSocket heartbeat** — Track the last received message timestamp. If no message received for > 30 seconds (configurable), log a `Warning` and attempt reconnection
- [ ] **Strategy execution heartbeat** — Track the last successful strategy evaluation per subscriber. If a subscriber misses more than 2 consecutive expected candle evaluations, log a `Warning`
- [ ] **Health endpoint** — Expose a `/health` endpoint on the Worker (or via the API querying Worker state) that reports: WebSocket connection status, active subscriber count, last candle close timestamp, any active warnings
- [ ] **Structured logging** — All Worker operations use structured logging with consistent properties: `SubscriberId`, `Symbol`, `Timeframe`, `EventType`, `SignalType`

### Non-Functional Requirements

- [ ] Paper mode must have zero performance overhead compared to live mode (same pipeline, just no HTTP call to exchange)
- [ ] Reconciliation polling must not exceed Hyperliquid rate limits
- [ ] Health endpoint must respond in < 100ms
- [ ] All monitoring state must be thread-safe (multiple subscribers reconciling concurrently)

---

## Technical Considerations

### Bounded Context

**Context:** Infrastructure (reconciliation, health), Worker (hosting), Application (paper trade recording)

### New Components

| Component | Layer | Description |
|-----------|-------|-------------|
| `PaperTradeRecorder` | Application/Trading/Services | Records paper trades to database |
| `PaperTrade` (entity) | Domain/Entities | Persisted paper order record |
| `FillReconciliationService` | Application/Trading/Services | Compares expected vs. actual fills |
| `PositionReconciliationService` | Application/Trading/Services | Compares internal vs. exchange position state |
| `ReconciliationBackgroundService` | Worker/Services | Periodic reconciliation job |
| `WorkerHealthService` | Worker/Services | Tracks heartbeats, exposes health state |
| `HyperliquidUserEventClient` (extend) | Infrastructure/Services | Already exists — extend to process fill events for reconciliation |

### Paper Mode Architecture

```
StrategyEngine → GridController → RiskEngine → PositionManager
  → HyperliquidExecutionEngine
      if PaperMode:
        → PaperTradeRecorder.RecordAsync(orderRequest)
        → Update GridState/PositionState as if filled
        → return synthetic orderId
      else:
        → HyperliquidOrderService.PlaceOrderAsync(orderRequest)
        → return real orderId
```

### Key Design Decisions

- Paper mode simulates fills at the requested price (same as `SimulatedExecutionEngine` in backtesting) — this gives a fair comparison between backtest predictions and paper results
- Reconciliation is per-subscriber and runs on a background timer, not on every fill (to avoid rate limit issues)
- Health monitoring is passive (event-driven heartbeat tracking) not active (no polling the exchange for health)
- The `/health` endpoint follows ASP.NET `IHealthCheck` conventions for future integration with Docker healthchecks and monitoring tools

---

## Acceptance Criteria

- [ ] Given paper mode is enabled, when the strategy emits a `DeployGrid` signal, then orders are logged with `[PAPER]` prefix and persisted to the `PaperTrade` table — no orders appear on Hyperliquid
- [ ] Given paper mode is enabled, when a paper order is recorded, then `GridState` and `PositionState` update as if the order filled at the requested price
- [ ] Given paper mode is disabled, when the strategy emits a signal, then real orders are placed on Hyperliquid
- [ ] Given live mode with real orders, when a fill is received from the Hyperliquid user event stream, then it is compared against the expected fill and any mismatch is logged as a `Warning`
- [ ] Given a reconciliation cycle runs, when the subscriber's Hyperliquid position size differs from internal `PositionState`, then a discrepancy warning is logged with both values
- [ ] Given the WebSocket has received no data for > 30 seconds, then a warning is logged and reconnection is attempted
- [ ] Given the health endpoint is queried, then it returns WebSocket status, subscriber count, last candle timestamp, and any active warnings within 100ms
- [ ] Given the Worker has been running in paper mode for 24 hours, then paper trade records can be queried and compared against what a backtest over the same period would have produced

---

## Dependencies

- **PBI 1 (Live Execution Engine)** — `HyperliquidExecutionEngine` must exist to add paper mode flag
- **PBI 2 (Worker Runtime Hosting)** — Background services must exist to add reconciliation and health services
- Existing `HyperliquidUserEventClient` (implemented — needs extension for fill processing)
- Existing `HyperliquidRestClient` (implemented — for position queries)
