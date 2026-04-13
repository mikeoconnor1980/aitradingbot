# Worker Execution Pipeline

The Worker (TradingApp.ExecutionAgent) is a Windows Service that connects to Hyperliquid
via WebSocket, assembles candles from raw trades, evaluates the grid strategy on each
confirmed candle close, and places/manages orders using locally-signed EIP-712 transactions.
The private key never leaves the machine.

This document traces the full data path from exchange tick to filled order, covering
every component and how they maintain real-time pace with the exchange.

## End-to-End Data Flow

```
Hyperliquid WebSocket (trades)  ──→  CandleBuilder  ──→  CandleClock  ──→  StrategyScheduler
                                           │                                       │
                                    MarketStateStore                         MarketContextBuilder
                                    (15m, 1h, 4h                              IndicatorEngine
                                     accumulators)                                  │
                                                                            StrategyEngine
                                                                                    │
                                                                   ┌────────────────┴───────────┐
                                                                   │ Grid mode        Signal mode │
                                                                   ▼                  ▼           │
                                                            GridController    SignalController     │
                                                                   │                  │           │
                                                                   └────────┬─────────┘           │
                                                                            │ TradingSignal[]     │
                                                                            ▼                     │
                                                                       RiskEngine                 │
                                                                            │ approved signals    │
                                                                            ▼                     │
                                                                    LivePositionManager           │
                                                                            │ OrderRequest        │
                                                                            ▼                     │
                                                                    LiveExecutionEngine           │
                                                                            │ EIP-712 sign + POST │
                                                                            ▼                     │
                                                                     Hyperliquid REST             │
                                                                      (/exchange)                 │
                                                                                                  │
Hyperliquid WebSocket (userEvents) ──→ FillProcessor ──→ GridState update + DB persistence ───────┘
```

## Background Services

The Worker host runs four `BackgroundService` instances concurrently:

| Service | Purpose | Interval |
|---------|---------|----------|
| `AgentCheckInService` | Heartbeats to the API control plane, picks up dashboard commands, reports order results | 5 s (15 s on error) |
| `TradingSession` | Manages dual WebSocket connections, wires candle pipeline and fill detection | Continuous |
| `HealthMonitorService` | Watchdog that logs warnings when candles or trades go stale or WebSocket disconnects | Periodic |
| `UpdateCheckerService` | Checks for agent updates via API heartbeat, applies silent upgrades when safe | Piggybacks on heartbeat |

`TradingSession` is not registered as a hosted service — it is created on-demand by
`AgentCheckInService` when the dashboard sends a **Start** command, and torn down on **Stop**.

## Session Lifecycle

### Creation

1. Dashboard sends `POST /api/trading/{agentId}/start` with a `StrategyConfig`
2. API enqueues a `Start` command in `AgentCommandStore`
3. Worker's `AgentCheckInService` picks it up on the next heartbeat (≤ 5 s)
4. `HandleStartAsync` stops any existing session, then calls `CreateSession(config)`
5. `CreateSession` manually wires all components with a shared `GridState` reference:

```
GridState (new, shared by reference)
    ├── StrategyScheduler — reads/writes lifecycle + filled levels
    ├── FillProcessor     — increments filled levels on fill receipt
    └── TradingSession    — owns the GridState, passes to both above
```

6. `session.Start()` fires a background task (`_runTask`) that enters the reconnect loop

### Shutdown

1. `StopAsync()` cancels the `CancellationTokenSource`
2. Waits up to 30 s for `_runTask` to complete
3. Calls `_executionEngine.CancelAllOrdersAsync(symbol)` with a 30 s timeout — ensures no orphaned orders remain on the exchange
4. Disconnects both WebSocket clients

## Dual WebSocket Architecture

Two independent `ClientWebSocket` connections run concurrently inside `TradingSession.RunAsync`:

| WebSocket | Client | Subscription | Data |
|-----------|--------|-------------|------|
| Market data | `HyperliquidWebSocketClient` | `{ type: "trades", coin: "{coin}" }` | Every trade tick for the coin (price, size, side, timestamp) |
| User events | `HyperliquidUserEventClient` | `{ type: "userEvents", user: "{walletAddress}" }` | Fills and order-status updates for the agent's wallet |

Both use 4096-byte receive buffers with `MemoryStream` accumulation for multi-frame messages.

**Reconnect strategy:** `Task.WhenAny(marketDataTask, userEventTask)` — if either WebSocket
exits, both reconnect together. Exponential backoff from 1 s to 60 s, up to 20 retries.
After 20 consecutive failures, the session logs `LogCritical` and exits.

## Candle Assembly (Tick → OHLCV)

`CandleBuilder` converts raw trade ticks into confirmed OHLCV candles using
timestamp-based bucketing — **not** wall-clock time. This ensures deterministic
behaviour identical to backtesting.

### Bucketing Algorithm

For each incoming `TradeTickDto`, and for each of the three supported intervals:

```
bucketTimestamp = tick.TimestampMs / intervalMs * intervalMs   (integer floor division)
```

| Interval | Milliseconds |
|----------|-------------|
| 15m | 900,000 |
| 1h | 3,600,000 |
| 4h | 14,400,000 |

### Candle Close Detection

A candle is confirmed closed when the first tick of the **next** bucket arrives:

1. If `existingAccumulator.BucketTimestamp < newBucketTimestamp` and accumulator has data →
   emit confirmed candle
2. Persist candle via `ICandleRepository.BulkInsertAsync`
3. Pass candle to `CandleClock.ProcessCandleAsync`
4. Remove old accumulator from `MarketStateStore`

This means candle close latency equals the time between the last tick of one bucket
and the first tick of the next — typically sub-second on liquid pairs.

### Deduplication

`CandleClock` maintains `Dictionary<string, long>` keyed on `"{symbol}:{interval}"`.
Each candle close time is recorded. If a candle with `closeTime ≤ _lastClosed[key]`
arrives (e.g., from WebSocket reconnect), it is silently dropped.
This guarantees **exactly-once** strategy evaluation per candle.

## Strategy Evaluation

When `CandleClock` emits a `CandleClosedEvent`, `TradingSession` routes it to
`StrategyScheduler.HandleCandleClosedAsync`:

### Step 1 — Timeframe Gate

Only the **trigger timeframe** (15m for grid strategy) invokes evaluation.
1h and 4h candles are cached as side-channel context but do not trigger evaluation.

### Step 2 — Market Context

`LiveMarketContextBuilder.Build(triggerCandle, 1hCandle, 4hCandle, requiredIndicators)`
constructs a `MarketContext` with:

- Current + historical candles (from DB)
- Computed indicators (EMA, ATR, RSI, etc. — specified by `IStrategyConfig.RequiredIndicators`)
- Account equity (from position state)
- Higher-timeframe candles for multi-TF analysis

### Step 3 — Strategy Engine

`CompositeStrategyEngine` routes on `StrategyMode`:

| Mode | Engine | Decision |
|------|--------|----------|
| Grid | `GridStrategyEngine` | Returns `SetupDetected=true` unless regime is `RiskOff` or higher-TF candles are missing |
| Signal | `IConditionEvaluator` + `ITrendFilterEvaluator` | Evaluates user-defined entry conditions with optional trend filter |

### Step 4 — Grid Controller / Signal Controller

The evaluation result flows to the appropriate controller:

- **Grid mode** → `GridController.ProcessAsync` — manages the full grid lifecycle state machine
  (see [15-grid-controller.md](15-grid-controller.md))
- **Signal mode** → `SignalController.ProcessAsync` — evaluates entry/exit for single-position strategies

Both produce `TradingSignal[]` — the contract boundary between strategy logic and execution.

### Step 5 — Risk Engine

`LiveRiskEngine.ValidateAsync` checks each signal against:

| Check | Config Key | Applies To |
|-------|-----------|-----------|| Portfolio heat | `MaxPortfolioHeatPercent` | Entry signals (`DeployGrid`, `OpenPosition`) only; blocks if `currentHeat + estimatedRiskUsd > equity × limit / 100` || Circuit breaker | `CircuitBreakerCooldownMinutes` | Entry signals only |
| Max daily loss | `MaxDailyLossUsd` | Rolling 24h window |
| Max order size | `MaxOrderSizeUsd` | `DeployGrid` notional, `OpenPosition` size |
| Max open orders | `MaxOpenOrders` | `DeployGrid` level count |

**Risk-reducing signals bypass all checks:** `TakeProfit`, `CancelGrid`, `FlattenPosition`, `CloseHedge`.

**Portfolio state tracking:** `StrategyScheduler` calls `UpdatePortfolioState(equity)` each candle close to keep heat percentage calculations current. `FillProcessor` calls `RecordPositionClosed(symbol)` when exit fills arrive to clear tracked risk for that symbol.

### Step 6 — Position Manager

`LivePositionManager.ExecuteSignalsAsync` translates approved signals into exchange orders:

| Signal | Action |
|--------|--------|
| `DeployGrid` | Set leverage via `SetLeverageAsync(symbol, leverage, isIsolated)` from signal parameters, cancel all existing orders, then place grid ladder (market + limits depending on `EntryMode`) |
| `TakeProfit` | Cancel all pending orders, place sell at target price |
| `CancelGrid` | Cancel all orders for the symbol |
| `OpenPosition` | Place a single entry order |
| `FlattenPosition` | Market sell entire position |

Each placed order is tracked via `IOrderTracker.TrackOrder(orderId, gridCycleId, level, ...)`.

## Order Signing and Submission

`LiveExecutionEngine` handles the cryptographic signing required by Hyperliquid:

### Limit Orders

1. Resolve `assetIndex` from cached universe metadata (lazy-loaded once via `SemaphoreSlim`)
2. Build order action struct via `HyperliquidEip712.BuildOrderAction(assetIndex, isBuy, price, size, "Gtc")`
3. Generate nonce via `_nonceProvider.GetNextNonce()`
4. Compute action hash: `HyperliquidEip712.ComputeActionHash(action, nonce, vaultAddress: null)`
5. Compute EIP-712 hash: `HyperliquidEip712.ComputeEip712Hash(connectionId, isMainnet)`
6. Sign: `_signer.SignHash(eip712Hash)` → `(r, s, v)` (ECDSA secp256k1)
7. POST to `/exchange` with `{ action, nonce, signature: {r,s,v} }`
8. Extract `orderId` from response: `Resting?.Oid ?? Filled?.Oid`

### Market Orders

Hyperliquid has no native market order type. The engine simulates one:

1. Fetch current mid price via `GetMarketInfoAsync`
2. Apply ±5% slippage: `Buy → midPrice × 1.05`, `Sell → midPrice × 0.95`
3. Submit as IOC (Immediate-or-Cancel) limit at the slippage price
4. Round to 5 significant figures via `RoundToSignificantFigures`

### Cancel Orders

- `CancelOrderAsync(orderId)` — builds `{ type: "cancel", cancels: [{a: assetIndex, o: oid}] }`, signs, posts
- `CancelAllOrdersAsync(symbol)` — uses `cancelByCloid` action with the asset index

## Fill Detection

When orders fill on Hyperliquid, the user-events WebSocket delivers fill notifications
to `HyperliquidUserEventClient`, which routes them to `FillProcessor`:

### Fill Processing Flow

1. Look up `_orderTracker.GetOrder(fill.OrderId)`
2. If untracked (e.g., manual trade or previous session) → log at Debug, skip
3. Set `tracked.Status = Filled`
4. Route by `TradeType`:

| TradeType | Action |
|-----------|--------|
| `GridFill` | Increment `GridState.FilledLevels`, transition lifecycle: `Deploying→Active`, `Active→PartiallyFilled`, or `→FullyFilled` when all levels hit |
| `TakeProfit` | Reset `GridState` to `Lifecycle=Closed`, clear all fields |
| `SignalEntry` | Info log only |

5. Persist to database: `LiveFill` record, update `LiveOrder.Status`, update `GridCycle.FilledLevels`

### Order Cancellation

`ProcessOrderUpdateAsync` handles `status == "canceled"` by setting `tracked.Status = Cancelled`.

## State Recovery on Restart

When the Worker restarts (service restart, machine reboot, update), `StateRecoveryService`
reconstructs the in-flight grid state before the WebSocket loop begins:

1. Query DB for the active `GridCycle` (non-Closed, non-Inactive)
2. If none found → return fresh `GridState()` (start clean)
3. Query Hyperliquid REST for fills since cycle start: `GET /userFillsByTime`
4. Query Hyperliquid REST for currently open orders: `POST /info { type: "openOrders" }`
5. Load all `LiveOrder` records for the cycle from DB
6. Cross-reference each DB order against fills and open orders:
   - In fills → mark `Filled`
   - In open orders → leave as `Resting`
   - Neither → mark `Cancelled`
7. Rebuild `GridState` with recovered `Lifecycle`, `FilledLevels`, `TotalLevels`, `GridCycleId`
8. Rehydrate `IOrderTracker` so the fill detection loop can correlate future fills
9. Copy recovered state into the shared `GridState` object owned by `TradingSession`

Recovery is best-effort: if it fails, the session starts with a fresh state and logs a warning.

## Timing and Latency Budget

The critical path from candle close to order on the exchange:

| Step | Typical Latency | Component |
|------|----------------|-----------|
| Last tick of bucket → first tick of next bucket | < 1 s (liquid pairs) | CandleBuilder |
| Candle close event emission | < 1 ms | CandleClock |
| Market context + indicator calculation | 10–50 ms | MarketContextBuilder |
| Strategy evaluation + grid controller | 1–5 ms | CompositeStrategyEngine + GridController |
| Risk validation | < 1 ms | LiveRiskEngine |
| Order signing (EIP-712 + ECDSA) | 5–10 ms | LiveExecutionEngine |
| REST POST to Hyperliquid | 50–200 ms | Network |
| **Total** | **~100–300 ms** after candle close detection | |

The main variable is the gap between the last trade of one candle bucket and the first trade
of the next. On BTC-PERP this is typically < 1 s; on low-liquidity pairs it can be minutes.

## Key File Paths

| File | Purpose |
|------|---------|
| `src/TradingApp.Worker/Program.cs` | Host configuration, DI registrations, Windows Service setup |
| `src/TradingApp.Worker/Services/AgentCheckInService.cs` | Heartbeat loop, command dispatch, session factory |
| `src/TradingApp.Worker/Services/TradingSession.cs` | Dual WebSocket management, candle pipeline wiring, reconnect loop |
| `src/TradingApp.Infrastructure/Services/HyperliquidWebSocketClient.cs` | Market data WebSocket, trade tick streaming |
| `src/TradingApp.Infrastructure/Services/HyperliquidUserEventClient.cs` | Per-wallet user event WebSocket (fills, order updates) |
| `src/TradingApp.Infrastructure/Services/HyperliquidRestClient.cs` | REST API client (`/info`, `/exchange`, candles, fills) |
| `src/TradingApp.Infrastructure/Services/LiveExecutionEngine.cs` | EIP-712 signing, order/cancel submission |
| `src/TradingApp.Application/Scheduling/CandleBuilder.cs` | Trade tick → OHLCV bucket assembly |
| `src/TradingApp.Application/Scheduling/CandleClock.cs` | Deduplicating candle-close event emitter |
| `src/TradingApp.Application/Scheduling/StrategyScheduler.cs` | Candle-close handler, market context, evaluation orchestration |
| `src/TradingApp.Application/Trading/Services/CompositeStrategyEngine.cs` | Mode router (Grid vs Signal) |
| `src/TradingApp.Application/Trading/Services/GridStrategyEngine.cs` | Grid setup detection with regime gating |
| `src/TradingApp.Application/Trading/Services/GridController.cs` | Grid lifecycle state machine, signal emission |
| `src/TradingApp.Application/Trading/Services/LivePositionManager.cs` | Signal → order translation, grid deployment |
| `src/TradingApp.Application/Trading/Services/LiveRiskEngine.cs` | Pre-order validation, circuit breaker, daily loss |
| `src/TradingApp.Application/Trading/Services/FillProcessor.cs` | Fill routing to grid state, DB persistence |
| `src/TradingApp.Application/Trading/Services/StateRecoveryService.cs` | DB + Hyperliquid reconciliation on restart |
| `src/TradingApp.Application/Trading/Services/InMemoryOrderTracker.cs` | ConcurrentDictionary-backed order correlation |

## Shared vs Per-Session State

| Object | Lifetime | Scope |
|--------|----------|-------|
| `GridState` | Per-session | Created in `CreateSession`, shared by reference between `TradingSession`, `StrategyScheduler`, and `FillProcessor` |
| `InMemoryOrderTracker` | Singleton | Survives across sessions (cleared on new session or recovery) |
| `HyperliquidWebSocketClient` | Singleton | Reconnected per session, but same instance reused |
| `HyperliquidUserEventClient` | Singleton | Same as above |
| `LiveExecutionEngine` | Singleton | Caches asset indexes across sessions |
| `CandleBuilder` / `CandleClock` | Singleton | State resets implicitly on reconnect (new accumulator buckets) |
| `LiveRiskEngine` | Singleton | Circuit breaker and loss queue persist across sessions |
| Repositories (`ILiveOrderRepository`, etc.) | Scoped | Created per-session via `IServiceScope` |

## Resilience

| Scenario | Behaviour |
|----------|-----------|
| WebSocket disconnect | Both sockets reconnect together with exponential backoff (1 s → 60 s, max 20 retries) |
| Hyperliquid REST 429 | Polly retry: 5 attempts, exponential backoff 1 s → 60 s with jitter |
| Hyperliquid REST 5xx | Same Polly retry policy |
| Service restart | `StateRecoveryService` reconstructs grid state from DB + exchange |
| Session stop (graceful) | All open orders cancelled via `CancelAllOrdersAsync` before disconnect |
| Strategy evaluation exception | Logged, session continues on next candle close |
| Fill for untracked order | Ignored with Debug log (handles manual trades or previous sessions) |
| Max retries exceeded (20) | `LogCritical`, session exits, agent continues heartbeating |

## Relationship to Other Knowledge Docs

- [15-grid-controller.md](15-grid-controller.md) — Grid lifecycle state machine details
- [16-signal-contracts.md](16-signal-contracts.md) — Signal types and parameter contracts
- [19-scheduling-architecture.md](19-scheduling-architecture.md) — CandleClock/StrategyScheduler design goals
- [29-control-plane-agent-architecture.md](29-control-plane-agent-architecture.md) — API ↔ Worker command flow
- [02-hyperliquid-integration.md](02-hyperliquid-integration.md) — Exchange API details and authentication
