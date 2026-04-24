# Notification Architecture

The notification system provides a unified approach for routing events to users across multiple channels: real-time UI (SignalR → Angular), persistent notification panel, ephemeral toasts, and out-of-band Telegram messages. The architecture is split into a backend dispatcher (server → channels) and a frontend facade (component → toast + panel).

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                        BACKEND                              │
│                                                             │
│  Worker Services ──► INotificationDispatcher                │
│  (UserEventStream,   ┌────────────┬──────────────────┐      │
│   AgentCheckIn,      │            │                  │      │
│   TradingSession)    ▼            ▼                  │      │
│               ISignalRPublisher  ITelegramNotifier   │      │
│               (4 impls)          (3 impls)           │      │
│                   │                  │               │      │
│                   ▼                  ▼               │      │
│              SignalR Hub       Telegram Bot API      │      │
└──────────────────┬──────────────────────────────────┘      │
                   │                                          │
                   ▼                                          │
┌─────────────────────────────────────────────────────────────┐
│                       FRONTEND                              │
│                                                             │
│  SignalRService ──► NotificationStoreService (panel)        │
│     (fills, orders,    └──► signal<AppNotification[]>       │
│      connection)                                            │
│                                                             │
│  Components ──► NotificationFacade                          │
│                  ├── NotificationService (toast/snackbar)   │
│                  └── NotificationStoreService (panel)       │
└─────────────────────────────────────────────────────────────┘
```

## Key Components

### Backend

| Component | Location | Purpose |
|-----------|----------|---------|
| `INotificationDispatcher` | `src/TradePilot.Application/Abstractions/Services/` | Unified dispatch interface — callers use this instead of coordinating SignalR + Telegram separately |
| `NotificationDispatcher` | `src/TradePilot.Worker/Services/` | Routes to `ISignalRPublisher` (always) and `ITelegramNotifier` (conditional on `NotificationConfigHolder.TelegramChatId`) |
| `ISignalRPublisher` | `src/TradePilot.Application/Abstractions/Services/` | Real-time event broadcasting to Angular via SignalR |
| `ITelegramNotifier` | `src/TradePilot.Application/Abstractions/Services/` | Out-of-band Telegram messages — must swallow errors to never disrupt trading |
| `NotificationConfigHolder` | `src/TradePilot.Worker/Services/` | Thread-safe config holder for Telegram chat ID and bot token, populated by heartbeat |

### Backend Implementations

| Interface | Implementation | Location | When Used |
|-----------|---------------|----------|-----------|
| `ISignalRPublisher` | `HubContextSignalRPublisher` | `TradePilot.Api` | API process — direct hub context |
| `ISignalRPublisher` | `AzureSignalRPublisher` | `TradePilot.Infrastructure` | Azure deployment — serverless SignalR |
| `ISignalRPublisher` | `RelaySignalRPublisher` | `TradePilot.Worker` | Worker process — relays via API |
| `ISignalRPublisher` | `NullSignalRPublisher` | `TradePilot.Worker` | Fallback when relay unavailable |
| `ITelegramNotifier` | `DynamicTelegramNotifier` | `TradePilot.Worker` | Worker — reads bot token from `NotificationConfigHolder` |
| `ITelegramNotifier` | `TelegramNotifier` | `TradePilot.Infrastructure` | API — static bot token from config |
| `ITelegramNotifier` | `NullTelegramNotifier` | `TradePilot.Infrastructure` | Telegram disabled / not configured |

### Frontend

| Component | Location | Purpose |
|-----------|----------|---------|
| `NotificationFacade` | `frontend/.../core/services/notification-facade.service.ts` | Unified entry point — routes to toast and/or panel based on severity |
| `NotificationService` | `frontend/.../core/services/notification.service.ts` | Low-level `MatSnackBar` wrapper (deprecated for direct use — use facade) |
| `NotificationStoreService` | `frontend/.../core/services/notification-store.service.ts` | Signal-based persistent panel state (max 200 items), subscribes to SignalR events |
| `NotificationPanelComponent` | `frontend/.../core/components/notification-panel/` | Slide-out panel UI with category filters and severity icons |
| `AppNotification` model | `frontend/.../core/models/app-notification.model.ts` | Shared types: `NotificationType`, `NotificationSeverity`, `NotifyOptions` |

## Notification Types

| Type | Source | Description |
|------|--------|-------------|
| `Fill` | SignalR → `NotificationStoreService` | Trade fills from Hyperliquid (consolidated by 500ms buffer) |
| `OrderUpdate` | SignalR → `NotificationStoreService` | Order status changes (filled, cancelled, triggered) |
| `Connection` | SignalR → `NotificationStoreService` | WebSocket connection status changes |
| `System` | SignalR → `NotificationStoreService` | Execution log entries from worker |
| `Error` | `NotificationFacade` → `NotificationStoreService` | Errors persisted via facade (HTTP errors, operation failures) |
| `Action` | `NotificationFacade` → `NotificationStoreService` | User-action feedback persisted via facade |

## Severity & Routing Defaults (Frontend Facade)

| Severity | Toast | Persist to Panel |
|----------|-------|-----------------|
| `success` | Yes | No |
| `info` | Yes | No |
| `warning` | Yes | Yes |
| `error` | Yes | Yes |

Callers can override defaults with explicit `toast` / `persist` flags in `NotifyOptions`.

## Telegram Guard Pattern

The `NotificationDispatcher` centralises the Telegram availability check. The pattern:

1. `NotificationConfigHolder.TelegramChatId` is populated by heartbeat from the API control plane
2. `NotificationDispatcher.TryGetChatId()` returns `false` if chat ID is null (Telegram not linked)
3. Telegram calls are skipped silently — no errors, no logs
4. `ITelegramNotifier` implementations swallow all exceptions to never disrupt trading

## Fill SignalR Deduplication

`NotificationDispatcher` maintains a `ConcurrentDictionary<string, DateTimeOffset>` of recently seen fill keys. Before broadcasting a fill via SignalR, it checks whether that fill key was already sent within the last minute (`FillSignalRDeduplicationWindow = 1 minute`). Duplicate fill events are suppressed at the dispatcher level; Telegram is not subject to this window. The dictionary is bounded to 10,000 entries to prevent unbounded memory growth.

This is a worker-side safeguard against WebSocket reconnect replaying fills to the UI multiple times. It does not affect the persistence path — fills are still written to the database on first receipt regardless.

## DI Registration

- **Worker**: `INotificationDispatcher` → `NotificationDispatcher` registered as singleton in `Program.cs`
- **API**: Does not register `INotificationDispatcher` — API services use `ISignalRPublisher` directly

## Services Using INotificationDispatcher

| Service | Events Dispatched |
|---------|-------------------|
| `UserEventStreamService` (Worker) | Fills, fill batches, order updates, user connection status |
| `AgentCheckInService` (Worker) | Strategy start/stop events |
| `TradingSession` (Worker) | Fill and order notifications during live trading |

## Services Still on Raw IHubContext

| Service | Reason |
|---------|--------|
| `BacktestProcessorService` (API) | Broadcasts anonymous progress objects, not typed DTOs |
| `OptimizationProcessorService` (API) | Same — progress events with anonymous structure |

These are a separate concern (progress reporting) and unsuitable for the typed `ISignalRPublisher` interface without adding generic broadcast methods.

## Extending Notifications

### Adding a new backend notification type

1. Add method to `INotificationDispatcher` (e.g. `NotifyNewEventAsync(...)`)
2. Implement in `NotificationDispatcher` — decide which channels (SignalR only, Telegram only, or both)
3. If SignalR needed, add corresponding `BroadcastXxxAsync` to `ISignalRPublisher` and all 4 implementations
4. If Telegram needed, add to `ITelegramNotifier` and implementations
5. Call from the originating service via injected `INotificationDispatcher`

### Adding a new frontend notification from a component

1. Inject `NotificationFacade` — use convenience methods (`success()`, `error()`, etc.) or `notify()` with `NotifyOptions` for full control
2. Do **not** inject `NotificationService` directly (deprecated for direct use)

### Adding a new SignalR-sourced notification type

1. Add observable to `SignalRService` (e.g. `newEvent$`)
2. Subscribe in `NotificationStoreService._subscribeToEvents()` and map to `AppNotification`
3. Add the new `NotificationType` value to the union type in `app-notification.model.ts`
4. Update `NotificationPanelComponent` filter and icon mapping if a new category is needed
