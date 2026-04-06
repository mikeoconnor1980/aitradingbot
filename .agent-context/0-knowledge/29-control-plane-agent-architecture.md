# Control Plane → Agent Architecture

The system uses a **poll-based command queue** architecture to separate the dashboard (API) from trade execution (Worker). The API acts as a stateless control plane that queues commands. The Worker (execution agent) polls the API on a heartbeat interval, picks up pending commands, and executes them locally using its own wallet credentials.

This separation exists because **the API never holds private keys**. Only the Worker can sign and submit transactions to Hyperliquid.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                   Angular Dashboard                      │
│  AgentService  →  /api/trading/{agentId}/order           │
│  AgentService  →  /api/trading/{agentId}/start           │
│  AgentService  →  /api/agent/list                        │
└──────────────────────────┬──────────────────────────────┘
                           │ HTTP
┌──────────────────────────▼──────────────────────────────┐
│                     API (Control Plane)                   │
│                                                          │
│  AgentController     ← heartbeat, list agents            │
│  TradingController   ← start/stop, order routing         │
│  AgentCommandStore   ← in-memory command queue           │
│                        (ConcurrentDictionary per agent)  │
└──────────────────────────┬──────────────────────────────┘
                           │ HTTP poll (every 5s)
┌──────────────────────────▼──────────────────────────────┐
│               Worker (Execution Agent)                   │
│                                                          │
│  AgentCheckInService ← BackgroundService, heartbeat loop │
│  TradingSession      ← WebSocket + candle pipeline       │
│  LiveExecutionEngine ← EIP-712 signing, order submission │
│  ISignerProvider     ← wallet private key                │
└──────────────────────────┬──────────────────────────────┘
                           │ WebSocket + REST
                    ┌──────▼──────┐
                    │  Hyperliquid │
                    │  (Exchange)  │
                    └─────────────┘
```

## Key Components

| Component | Layer | Project | Purpose |
|-----------|-------|---------|---------|
| `TradingController` | API | `TradingApp.Api` | Dashboard-facing endpoints for start/stop/order/cancel commands |
| `AgentController` | API | `TradingApp.Api` | Agent-facing endpoints for heartbeat + agent listing |
| `AgentCommandStore` | Application | `TradingApp.Application` | In-memory singleton that tracks agents and their command queues |
| `AgentCheckInService` | Worker | `TradingApp.Worker` | `BackgroundService` that polls the API every 5s |
| `TradingSession` | Worker | `TradingApp.Worker` | Manages WebSocket connection, candle pipeline, and strategy execution |
| `LiveExecutionEngine` | Infrastructure | `TradingApp.Infrastructure` | Signs and submits orders to Hyperliquid via EIP-712 |

## File Paths

| File | Description |
|------|-------------|
| `src/TradingApp.Api/Controllers/TradingController.cs` | Dashboard command endpoints |
| `src/TradingApp.Api/Controllers/AgentController.cs` | Heartbeat + agent listing endpoints |
| `src/TradingApp.Application/Agent/Services/AgentCommandStore.cs` | In-memory queue + agent registry |
| `src/TradingApp.Application/Agent/Models/AgentCommand.cs` | Command model + `AgentCommandType` enum |
| `src/TradingApp.Application/Agent/Models/AgentHeartbeat.cs` | Heartbeat request + `HeartbeatResponse` |
| `src/TradingApp.Application/Agent/Models/AgentInfo.cs` | Agent state model |
| `src/TradingApp.Application/Agent/Models/AgentState.cs` | State enum |
| `src/TradingApp.Application/Agent/Models/OrderCommandPayload.cs` | All command payloads |
| `src/TradingApp.Worker/Services/AgentCheckInService.cs` | Worker heartbeat loop + command handlers |
| `src/TradingApp.Worker/Services/TradingSession.cs` | WebSocket + strategy pipeline |
| `frontend/trading-ui/src/app/core/services/agent.service.ts` | Angular agent service |
| `frontend/trading-ui/src/app/features/agents/agents-page.component.ts` | Agents UI |

## Heartbeat Flow

The Worker runs `AgentCheckInService` as a `BackgroundService`. Every **5 seconds** it:

1. Builds an `AgentHeartbeat` containing:
   - `AgentId`, `State` (Idle/Running), `MachineName`, `WalletAddress`
   - `ActiveStrategy` (name, market, timeframe, start time) if a session is running
   - `OrderResults` — completed results from commands executed since last heartbeat
2. POSTs to `POST /api/agent/heartbeat`
3. API calls `AgentCommandStore.ProcessHeartbeat()` to register/update the agent
4. API calls `AgentCommandStore.DrainCommands()` to dequeue all pending commands
5. Returns `HeartbeatResponse { PendingCommands }` to the Worker
6. Worker iterates each command and dispatches to the appropriate handler

```
Worker                          API
  │                              │
  │── POST /agent/heartbeat ────►│  ProcessHeartbeat()
  │   { state, results }         │  DrainCommands()
  │                              │
  │◄── { pendingCommands } ──────│
  │                              │
  │  foreach command:            │
  │    HandleCommandAsync()      │
  │                              │
```

### Error Backoff

If a heartbeat fails (network error, API down), the Worker backs off for **15 seconds** before retrying.

### Disconnection Detection

The API marks an agent as `Disconnected` if no heartbeat is received within **30 seconds**. This is evaluated lazily when `GetAllAgents()` or `GetAgent()` is called.

## Agent State Machine

```
    ┌──────────┐
    │  (new)   │
    └────┬─────┘
         │ first heartbeat
    ┌────▼─────┐   Start cmd    ┌──────────┐
    │   Idle   │───────────────►│ Running  │
    └────┬─────┘                └────┬─────┘
         │                           │ Stop cmd
         │                      ┌────▼─────┐
         │                      │   Idle   │
         │                      └──────────┘
         │ no heartbeat (30s)
    ┌────▼────────┐   kill switch   ┌────────┐
    │ Disconnected│────────────────►│ Killed │
    └─────────────┘                 └────┬───┘
                                         │ reinstate
    Any state ──── kill switch ────►     │
                                    ┌────▼────────┐
                                    │ Disconnected│
                                    └─────────────┘
```

| State | Meaning |
|-------|---------|
| `Idle` | Agent connected, no active trading session |
| `Starting` | Start command received, session initialising |
| `Running` | Active `TradingSession` with WebSocket connected |
| `Stopping` | Stop command received, session shutting down |
| `Error` | Agent encountered an error |
| `Disconnected` | No heartbeat for >30s (set by API, not by Worker) |
| `Killed` | Kill switch activated — agent forced to stop and blocked from reconnecting |

## Command Types

| Type | Payload | Description |
|------|---------|-------------|
| `Start` | `StrategyConfig` | Start a trading session with a full strategy configuration |
| `Stop` | — | Gracefully stop the active session, cancel open orders, disconnect WebSocket |
| `PlaceOrder` | `OrderCommandPayload` | Place a market or limit order |
| `CancelOrder` | `CancelOrderPayload` | Cancel a specific order by ID |
| `CancelAllOrders` | `CancelAllOrdersPayload` | Cancel all orders for an asset |
| `SetLeverage` | `SetLeveragePayload` | Set leverage for an asset (not yet implemented) |
| `PlaceTriggerOrder` | `TriggerOrderPayload` | Place a stop-loss or take-profit trigger order |
| `ModifyTriggerOrder` | `ModifyTriggerOrderPayload` | Modify an existing trigger order |
| `CancelTriggerOrder` | `CancelOrderPayload` | Cancel a trigger order by ID |

## Command Lifecycle

### 1. Dashboard Enqueues Command

The Angular dashboard calls a `TradingController` endpoint (e.g. `POST /api/trading/{agentId}/order`). The controller:

1. Validates the agent exists via `AgentCommandStore.GetAgent()`
2. Rejects with `409 Conflict` if the agent is `Disconnected`
3. Creates an `AgentCommand` with a unique `CommandId` (GUID)
4. Enqueues it into the agent's `ConcurrentQueue<AgentCommand>`
5. Returns `202 Accepted` with the `CommandId`

### 2. Agent Picks Up Command

On the next heartbeat (within 5s), `AgentCheckInService` receives the command in the `HeartbeatResponse.PendingCommands` list. All pending commands are drained atomically.

### 3. Agent Executes Command

`HandleCommandAsync()` dispatches by `AgentCommandType`:

- **Start** → Creates a `TradingSession` (resolves all strategy pipeline services from DI), calls `session.Start()` which launches `RunAsync` on a background task
- **Stop** → Calls `session.StopAsync()` (cancels CTS, cancels open orders, disconnects WebSocket), then `DisposeAsync()`
- **PlaceOrder** → Resolves `IExecutionEngine`, builds an `OrderRequest`, calls `PlaceOrderAsync()`
- **CancelOrder / CancelAllOrders** → Resolves `IExecutionEngine`, calls the appropriate cancel method
- **PlaceTriggerOrder** → Resolves `IExecutionEngine`, calls `PlaceTriggerOrderAsync()`
- **ModifyTriggerOrder** → Resolves `IExecutionEngine`, calls `ModifyTriggerOrderAsync()`
- **CancelTriggerOrder** → Resolves `IExecutionEngine`, calls `CancelOrderAsync()`

### 4. Result Reporting

Order commands enqueue an `OrderCommandResult` (success/fail + orderId + detail) into `AgentCheckInService._pendingResults`. These results are sent back to the API on the **next heartbeat** in `AgentHeartbeat.OrderResults`.

## Trading Session Lifecycle

When a `Start` command is received:

1. `AgentCheckInService.HandleStartAsync()` stops any existing session
2. Calls `CreateSession()` which resolves from DI:
   - `IHyperliquidWebSocketClient` — WebSocket trades stream
   - `CandleBuilder` — assembles candles from ticks
   - `CandleClock` — fires candle-close events
   - `IMarketContextBuilder` — builds indicator context
   - `IStrategyEngine` — evaluates strategy signals
   - `IGridController` — manages grid lifecycle
   - `IRiskEngine` — validates signals before execution
   - `IPositionManager` — tracks open positions
   - `ISignalController` — routes signals to execution
   - `IExecutionEngine` (`LiveExecutionEngine`) — signs and submits orders
3. `session.Start()` launches a background task that:
   - Connects WebSocket to `wss://api.hyperliquid-testnet.xyz/ws`
   - Subscribes to trades for the strategy's market coin
   - Enters the receive loop, feeding ticks → `CandleBuilder` → `CandleClock` → `StrategyScheduler`
4. On WebSocket disconnect, retries with exponential backoff (1s → 60s max, 20 attempts)

When a `Stop` command is received:

1. Cancels the `CancellationTokenSource`
2. Waits up to 30s for the background task to finish
3. Cancels all open orders for the strategy's market
4. Disconnects WebSocket

## Kill Switch

The kill switch allows administrators to revoke an agent's access immediately or at a scheduled future time. Use cases include subscription expiry, account suspension, and emergency shutdown.

### How It Works

1. Admin clicks Kill Switch on the Agents page (or calls `POST /api/agent/{agentId}/kill`)
2. `AgentCommandStore.KillAgent()` sets `KilledAtUtc` (now or scheduled) and `KilledReason` on the agent
3. `EvaluateEffectiveState()` sets the agent's state to `Killed` once the effective time is reached
4. On the next heartbeat, the API returns `HeartbeatResponse.MustShutdown = true` with the reason
5. The Worker stops the active trading session, cancels all orders, but **continues heartbeating** (so the API can reinstate it later)
6. All `TradingController` command endpoints reject with `409 Conflict` for killed or disconnected agents

### Wallet-Based Kill Enforcement

The kill check also scans all agents for matching wallet addresses. If agent A with wallet `0xABC...` is killed, a new agent B registering with the same wallet will also be killed — even if it has a different AgentId. This prevents circumvention by restarting with a new ID.

### Agent Identity

`AgentId` defaults to `Environment.MachineName.ToLowerInvariant()` — deterministic across restarts. This ensures a restarted Worker reconnects as the same agent entry. Override with `Agent:AgentId` in appsettings for multi-agent setups.

### Reinstating

Admin clicks Reinstate (or calls `POST /api/agent/{agentId}/reinstate`). This clears `KilledAtUtc` and `KilledReason` and sets state to `Disconnected`. The agent will resume normal operation on its next heartbeat.

### Scheduled Kills

Set `effectiveAtUtc` to a future time (e.g. subscription expiry date). The agent operates normally until that time, then is automatically killed on the next state evaluation. The dashboard shows a clock icon for agents with a pending scheduled kill.

### Key Files

| File | Description |
|------|-------------|
| `src/TradingApp.Application/Agent/Models/AgentInfo.cs` | `KilledAtUtc`, `KilledReason` properties |
| `src/TradingApp.Application/Agent/Models/AgentState.cs` | `Killed` enum value |
| `src/TradingApp.Application/Agent/Models/AgentHeartbeat.cs` | `MustShutdown`, `ShutdownReason` on response |
| `src/TradingApp.Application/Agent/Services/AgentCommandStore.cs` | `KillAgent()`, `ReinstateAgent()`, `GetKillReason()`, `FindKilledWallet()` |
| `src/TradingApp.Api/Controllers/AgentController.cs` | `POST /{agentId}/kill`, `POST /{agentId}/reinstate` |
| `src/TradingApp.Worker/Services/AgentCheckInService.cs` | `MustShutdown` handler in `CheckInAsync()` |
| `frontend/.../agents/kill-switch-dialog.component.ts` | Kill switch dialog (immediate or scheduled, with reason) |

## Command Queue Behaviour

- **Storage**: In-memory `ConcurrentDictionary<string, ConcurrentQueue<AgentCommand>>` per agent
- **Expiry**: Commands older than **2 minutes** are discarded on drain (prevents stale orders executing after reconnection)
- **Peek**: `GET /api/agent/{agentId}/pending-commands` returns queue contents without draining (used by the dashboard to show queue state)
- **Offline rejection**: All `TradingController` endpoints return `409 Conflict` if the target agent is `Disconnected` or `Killed`

## API Endpoints

### Agent Controller (`/api/agent`)

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/heartbeat` | Agent check-in, returns pending commands (or `MustShutdown` if killed) |
| `GET` | `/list` | List all agents (dashboard) |
| `GET` | `/{agentId}` | Get specific agent details |
| `GET` | `/{agentId}/pending-commands` | Peek at pending command queue |
| `POST` | `/{agentId}/kill` | Kill switch — immediate or scheduled (body: `KillAgentRequest`) |
| `POST` | `/{agentId}/reinstate` | Reinstate a killed agent |

### Trading Controller (`/api/trading`)

| Method | Path | Payload | Description |
|--------|------|---------|-------------|
| `POST` | `/{agentId}/start` | `StartTradingRequest` | Start strategy session |
| `POST` | `/{agentId}/stop` | — | Stop strategy session |
| `GET` | `/{agentId}/status` | — | Get agent trading status |
| `POST` | `/{agentId}/order` | `OrderCommandPayload` | Place order |
| `POST` | `/{agentId}/cancel-order` | `CancelOrderPayload` | Cancel order |
| `POST` | `/{agentId}/cancel-all-orders` | `CancelAllOrdersPayload` | Cancel all orders |
| `POST` | `/{agentId}/leverage` | `SetLeveragePayload` | Set leverage |
| `POST` | `/{agentId}/trigger-order` | `TriggerOrderPayload` | Place SL/TP |
| `POST` | `/{agentId}/modify-trigger-order` | `ModifyTriggerOrderPayload` | Modify SL/TP |
| `POST` | `/{agentId}/cancel-trigger-order` | `CancelOrderPayload` | Cancel SL/TP |

## JSON Serialisation

Both API and Worker use matching `JsonSerializerOptions`:

```csharp
PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
PropertyNameCaseInsensitive = true,
Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
```

Enums serialize as **snake_case lowercase**: `PlaceOrder` → `"place_order"`, `Idle` → `"idle"`.

The Angular frontend matches with lowercase string literal types (e.g. `AgentState = "idle" | "running" | ...`).

## Security Model

| Aspect | Detail |
|--------|--------|
| Private keys | Only exist on the Worker. Never sent to or stored by the API. |
| Signing | `LiveExecutionEngine` uses `ISignerProvider` for EIP-712 signing. |
| API authentication | None (POC). Production will require auth on both heartbeat and dashboard endpoints. |
| Agent identity | Deterministic `AgentId` from machine name (configurable via `Agent:AgentId` in appsettings). |
| Kill switch | Admin can kill agents immediately or on a schedule. Wallet-based enforcement prevents ID circumvention. |

## Frontend Integration

The Angular `AgentService` (`core/services/agent.service.ts`) provides:

- `refreshAgents()` — polls `GET /api/agent/list` and auto-selects the first connected agent
- `startTrading(agentId, strategyConfig)` — sends strategy to start
- `stopTrading(agentId)` — stops strategy
- `placeOrderViaAgent(agentId, request)` — routes orders through the command queue
- `cancelOrderViaAgent()`, `cancelAllOrdersViaAgent()` — cancel routing
- `placeTriggerOrderViaAgent()`, `modifyTriggerOrderViaAgent()`, `cancelTriggerOrderViaAgent()` — SL/TP routing
- `getPendingCommands(agentId)` — peek at queue for display
- `killAgent(agentId, reason?, effectiveAtUtc?)` — activate kill switch
- `reinstateAgent(agentId)` — reinstate a killed agent

The `AgentsPageComponent` displays the agent table with status, active strategy, last heartbeat, pending queue, start/stop actions, and kill switch / reinstate buttons. A `KillSwitchDialogComponent` lets the admin choose immediate or scheduled kill with an optional reason.

The `DashboardComponent` routes all position actions (Close, Close All, Set SL/TP, Edit SL/TP, Remove SL/TP) through the agent when `agentService.selectedAgentId` is set. Falls back to direct API calls when no agent is selected.

## Extending the Command Set

To add a new command type:

1. Add a value to `AgentCommandType` enum in `AgentCommand.cs`
2. Create a payload model in `OrderCommandPayload.cs`
3. Add the payload property to `AgentCommand`
4. Add a `TradingController` endpoint that validates, creates, and enqueues the command
5. Add a `HandleXxxAsync()` method in `AgentCheckInService`
6. Add the routing method to Angular `AgentService`
7. Wire the UI component to call the new service method

## Known Limitations (POC)

- **In-memory queue** — commands are lost if the API restarts. Production should persist to a database.
- **No authentication** — heartbeat and command endpoints are unauthenticated.
- **Single agent per Worker process** — each Worker instance runs one `AgentCheckInService`.
- **No command acknowledgement** — the API doesn't know if a command was successfully executed until the next heartbeat.
- **5-second latency** — commands are delivered on the next heartbeat poll, not pushed immediately.
