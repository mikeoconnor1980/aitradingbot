# Control Plane → Agent Architecture

The implemented system uses a poll-based control-plane protocol between the API and the Windows execution agent. The API owns orchestration, dashboard commands, agent visibility, and update metadata. The worker owns wallet custody, exchange connectivity, live execution, and local safety checks.

This boundary exists because the control plane never stores or uses private keys. Wallet addresses are known to the platform, but signing remains local to the execution agent.

## Architecture Overview

```
Angular dashboard
   → TradingController / AgentController
      → AgentCommandStore
         → pending command queue + agent registry
            → Worker AgentCheckInService heartbeat every 5s
               → TradingSession / LiveExecutionEngine / UpdateCheckerService
                  → Hyperliquid + local Windows Service runtime
```

## Key Components

| Component | Project | Purpose |
|---|---|---|
| `AgentController` | `TradePilot.Api` | Receives heartbeats, exposes agent state, kill-switch actions, and update metadata |
| `TradingController` | `TradePilot.Api` | Queues start, stop, order, cancel, leverage, and trigger-order commands |
| `AgentCommandStore` | `TradePilot.Application` | In-memory registry of agents, pending commands, kill state, and heartbeat-derived status |
| `AgentHeartbeat` / `HeartbeatResponse` | `TradePilot.Application` | Shared control-plane protocol models |
| `AgentCheckInService` | `TradePilot.Worker` | Worker `BackgroundService` that sends heartbeats, receives commands, and reports order results |
| `TradingSession` | `TradePilot.Worker` | On-demand live execution session created when a Start command is received |
| `UpdateCheckerService` | `TradePilot.Worker` | Hosted background service that applies installer-based updates when the heartbeat advertises a newer version |

## Heartbeat Contract

Every 5 seconds the worker sends an `AgentHeartbeat` containing:

- agent identity and runtime state
- machine and wallet information
- active strategy summary when a session is running
- `TimestampUtc`
- completed `OrderResults` since the last heartbeat
- `AgentVersion`
- `UpdateState`
- `UpdateDeferredReason`

The API responds with `HeartbeatResponse`, which can include:

- `PendingCommands`
- `MustShutdown`
- `ShutdownReason`
- `UpdateAvailable`
- `LatestVersion`
- `UpdateDownloadUrl`
- `UpdateSha256Hash`

Those update fields are part of the production control-plane protocol. `AgentController` compares the agent-reported version with `AgentUpdateOptions` and advertises installer metadata only when a newer version exists.

## Command and Update Flow

1. The dashboard calls a `TradingController` endpoint such as start, stop, place order, or cancel.
2. The API enqueues an `AgentCommand` in `AgentCommandStore` for that agent.
3. `AgentCheckInService` posts the next heartbeat to `/api/agent/heartbeat`.
4. The API processes the heartbeat, refreshes the agent registry entry, then drains pending commands.
5. The worker executes each command locally.
6. Completed order-style command results are returned on the next heartbeat.
7. If the heartbeat response includes update metadata, `AgentCheckInService` forwards it to `UpdateCheckerService`.
8. `UpdateCheckerService` downloads the installer, verifies SHA256, and only applies it when there is no active trading session; otherwise it defers and reports the reason back in subsequent heartbeats.

## Session Creation Reality

The worker does not create trading sessions through a dedicated factory or by resolving a prebuilt `TradingSession` service from DI. `AgentCheckInService.CreateSession()` manually composes the session by pulling its dependencies from DI, creating a scoped repository lifetime, instantiating `FillProcessor`, configuring `LivePositionManager`, and then calling the `TradingSession` constructor.

`TradingSession` itself uses constructor injection for its dependencies and accepts `GridState` as an optional constructor parameter, defaulting to `new GridState()` when none is supplied.

## Hosted-Service Shape

Always-on worker hosted services are:

- `AgentCheckInService`
- `HealthMonitorService`
- `UpdateCheckerService`

Additional hosted services are conditional:

- `MarketDataStreamService`
- `UserEventStreamService`

Those two are only registered when `Azure:SignalR:ConnectionString` is configured. In non-Azure mode, the agent still trades, but it does not start the Azure SignalR publishing services.

## Kill Switch Behaviour

Kill-switch behaviour is controlled through `AgentCommandStore`, `AgentController`, and the worker heartbeat loop:

- the API can mark an agent as killed immediately or at a scheduled time
- killed agents receive `MustShutdown` on heartbeat
- the worker stops the active session but keeps heartbeating so it can be reinstated later
- wallet-based kill enforcement prevents an operator from bypassing the kill state by changing agent ID while reusing the same wallet

## Security and Durability Notes

| Area | Current State |
|---|---|
| Private keys | Never stored in the API; only the worker signs |
| Agent authentication | Bearer token shared-secret supported: when `Agent:SecretKey` is set, all heartbeat requests include `Authorization: Bearer {SecretKey}`. Full mutual-auth (signed commands, mTLS) is not yet implemented |
| Command durability | In-memory only; API restart loses queued commands |
| Delivery latency | Bounded by heartbeat polling, typically up to 5 seconds |
| Update integrity | Installer downloads are SHA256-verified before apply |

## Key Files

| File | Purpose |
|---|---|
| `src/TradePilot.Api/Controllers/AgentController.cs` | Heartbeat endpoint, kill switch, reinstate, and update advertisement |
| `src/TradePilot.Api/Controllers/TradingController.cs` | Dashboard command routing |
| `src/TradePilot.Application/Agent/Services/AgentCommandStore.cs` | Agent registry and command queue |
| `src/TradePilot.Application/Agent/Models/AgentHeartbeat.cs` | Shared heartbeat request/response models and `UpdateState` enum |
| `src/TradePilot.Worker/Services/AgentCheckInService.cs` | Heartbeat loop, command dispatch, and manual session creation |
| `src/TradePilot.Worker/Services/UpdateCheckerService.cs` | Auto-update workflow |
| `src/TradePilot.Worker/Services/TradingSession.cs` | Live trading session runtime |

## Future Recommendations

- Persist commands and agent state in durable storage so API restarts do not drop queued actions.
- Add strong agent authentication and signed command provenance before broader production rollout.
- Add explicit operator visibility for update rollout waves, failed updates, and deferred-update reasons.
