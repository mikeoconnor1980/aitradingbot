# Macro Calendar Subsystem

The macro calendar subsystem tracks upcoming macroeconomic events (CPI, FOMC, Non-Farm Payrolls, etc.) and blocks new trade entries during high-impact event windows while always allowing exits, stop-losses, and reduce-only actions.

---

## Key Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `MacroEvent` | `src/TradingApp.Domain/Entities/MacroEvent.cs` | Domain entity storing event details, importance, block window timestamps |
| `MacroSyncRun` | `src/TradingApp.Domain/Entities/MacroSyncRun.cs` | Tracks each sync run (success/failure, counts) |
| `MacroEventImportance` | `src/TradingApp.Domain/Enums/MacroEventImportance.cs` | Unknown, Low, Medium, High, Critical |
| `MacroEventStatus` | `src/TradingApp.Domain/Enums/MacroEventStatus.cs` | Scheduled, Live, Released, Revised, Cancelled |
| `IMacroCalendarProvider` | `src/TradingApp.Application/MacroCalendar/Services/IMacroCalendarProvider.cs` | Abstraction for fetching events from any provider |
| `StubMacroCalendarProvider` | `src/TradingApp.Infrastructure/Providers/MacroCalendar/StubMacroCalendarProvider.cs` | Generates deterministic fake events for development |
| `IMacroCalendarIngestionService` | `src/TradingApp.Application/MacroCalendar/Services/IMacroCalendarIngestionService.cs` | Sync interface |
| `MacroCalendarIngestionService` | `src/TradingApp.Persistence/Services/MacroCalendarIngestionService.cs` | Upserts events from provider into database, computes block windows |
| `IMacroCalendarQueryService` | `src/TradingApp.Application/MacroCalendar/Services/IMacroCalendarQueryService.cs` | Query interface for upcoming events and active blocks |
| `MacroCalendarQueryService` | `src/TradingApp.Persistence/Services/MacroCalendarQueryService.cs` | EF Core queries against MacroEvents table |
| `IMacroBlockWindowCalculator` | `src/TradingApp.Application/MacroCalendar/Services/IMacroBlockWindowCalculator.cs` | Determines pre/post block minutes by importance |
| `MacroBlockWindowCalculator` | `src/TradingApp.Application/MacroCalendar/Services/MacroBlockWindowCalculator.cs` | Reads policy from `MacroCalendarOptions` |
| `IMacroEventRiskCheck` | `src/TradingApp.Application/MacroCalendar/Services/IMacroEventRiskCheck.cs` | Checks if new entries should be blocked |
| `MacroEventRiskCheck` | `src/TradingApp.Persistence/Services/MacroEventRiskCheck.cs` | Queries active high-importance block windows |
| `MacroCalendarSyncWorker` | `src/TradingApp.Api/Services/MacroCalendarSyncWorker.cs` | BackgroundService with full, incremental, and near-event sync modes |
| `MacroCalendarController` | `src/TradingApp.Api/Controllers/MacroCalendarController.cs` | REST endpoints for events, active blocks, and manual sync |
| `MacroCalendarOptions` | `src/TradingApp.Application/MacroCalendar/Configuration/MacroCalendarOptions.cs` | Typed options for sync intervals, look-ahead, block policies |
| `MacroImportanceMapper` | `src/TradingApp.Application/MacroCalendar/Services/MacroImportanceMapper.cs` | Maps raw importance strings to enum |
| `MacroStatusMapper` | `src/TradingApp.Application/MacroCalendar/Services/MacroStatusMapper.cs` | Maps raw status strings to enum |

---

## Architecture

```
Provider (Stub / Finnhub / etc.)
  → IMacroCalendarProvider.GetEventsAsync()
    → ExternalMacroEventDto (normalized)
      → MacroCalendarIngestionService.SyncAsync()
        → MacroEvent entities (with computed block windows)
          → Database (MacroEvents, MacroSyncRuns tables)
            → MacroCalendarQueryService (read)
              → API endpoints → Angular UI
            → MacroEventRiskCheck (read)
              → Risk Engine integration
```

---

## Provider Abstraction

The system is designed for easy provider swaps. The `IMacroCalendarProvider` interface returns `ExternalMacroEventDto` objects — a provider-agnostic DTO. The rest of the pipeline (ingestion, normalization, storage, querying) is completely decoupled from the data source.

**Current provider:** `StubMacroCalendarProvider` generates deterministic fake events per day using day-of-year seeding. It produces 2–4 events per weekday from a template list of real event types (FOMC, CPI, NFP, etc.).

**To swap to a real provider (e.g. Finnhub):**
1. Create `FinnhubMacroCalendarProvider : IMacroCalendarProvider`
2. Change one DI registration in `Program.cs`
3. Add API key to `appsettings.json`

---

## Block Window Behaviour

Block windows are computed per-event based on importance and configured policy:

| Importance | Default Pre-Block | Default Post-Block |
|------------|-------------------|---------------------|
| High / Critical | 30 minutes | 30 minutes |
| Medium | 10 minutes | 10 minutes |
| Low | 0 minutes | 0 minutes |

**Rules:**
- New entries are **blocked** during active high-importance windows
- Exits, stop-losses, take-profits, and reduce-only actions are **always allowed**
- Block windows are stored as millisecond timestamps on the `MacroEvent` entity

---

## Background Sync Strategy

The `MacroCalendarSyncWorker` runs three sync modes:

1. **Full sync** — every 6 hours (configurable). Fetches look-back through look-ahead range.
2. **Incremental sync** — every 5 minutes. Fetches now through next 24 hours.
3. **Near-event sync** — every 60 seconds, only when a high-impact event is within 10 minutes.

If the provider fails, the worker logs the error and retries after 60 seconds. Cached events remain available.

---

## API Endpoints

| Method | Route | Purpose |
|--------|-------|---------|
| `GET` | `/api/macro-calendar/events?fromUtc=&toUtc=&currency=&minimumImportance=` | Query upcoming events |
| `GET` | `/api/macro-calendar/active-blocks` | Get currently active block windows |
| `POST` | `/api/macro-calendar/sync` | Trigger manual sync |

---

## Angular UI

The Macro Calendar page (`/macro-calendar`) provides:
- **Events table** with columns: importance, title, country, currency, category, scheduled time, status, forecast, previous, actual, block window
- **Active block banner** when high-importance events are blocking entries
- **Client-side search** filtering by title, country, currency, category, importance
- **Currency filter** dropdown
- **Manual sync** button
- Importance badges colour-coded: Critical (red), High (orange), Medium (yellow), Low (green)

---

## Configuration

Section: `MacroCalendar` in `appsettings.json`

| Setting | Default | Purpose |
|---------|---------|---------|
| `Provider` | `Stub` | Active provider name |
| `Enabled` | `true` | Enable/disable background sync |
| `LookAheadDays` | `7` | Days ahead to fetch |
| `LookBackDays` | `1` | Days behind to fetch |
| `NearEventWindowMinutes` | `10` | Threshold for near-event fast sync |
| `FullSyncIntervalMinutes` | `360` | Full sync frequency |
| `IncrementalSyncIntervalMinutes` | `5` | Incremental sync frequency |
| `NearEventSyncIntervalSeconds` | `60` | Near-event sync frequency |
| `DefaultPolicies.High.PreBlockMinutes` | `30` | Pre-event block for high impact |
| `DefaultPolicies.High.PostBlockMinutes` | `30` | Post-event block for high impact |

---

## Database

Two tables added via migration `AddMacroCalendarTables`:

- **MacroEvents** — stores all events with unique index on `(Provider, ProviderEventId)`. Indexed on `ScheduledAtUtc`, `BlockStartUtc`, `BlockEndUtc`, `Importance`.
- **MacroSyncRuns** — audit trail of sync operations.

All timestamps are stored as Unix milliseconds (`long`) consistent with the rest of the domain.
