# Strategy Customisation

Users can create their own strategy instances using the GridStrategy plugin — either via form-based configuration or by describing their intent in natural language (see [Strategy Interpreter Architecture](24-strategy-interpreter-architecture.md)).

Each user strategy consists of:

Strategy record  
StrategyConfig JSON (stored in `StrategyConfig.ConfigJson`; parsed as `TradingApp.Application.StrategyAuthoring.Models.StrategyConfig`)

Example configuration (grid mode):

```json
{
  "schemaVersion": 1,
  "strategyMode": "grid",
  "strategyName": "BTC Pullback Grid",
  "market": "BTC",
  "timeframe": "15m",
  "direction": "long",
  "grid": {
    "levels": 4,
    "spacing": 0.35,
    "entryMode": "auto_from_signal_candle",
    "breakdownThreshold": 0.02
  },
  "exit": {
    "takeProfit": { "type": "percent_from_entry", "value": 0.8 },
    "stopLoss": { "type": "percent_from_entry", "value": 2.0 }
  },
  "risk": {
    "positionSizeType": "percent_of_equity",
    "positionSizeValue": 10,
    "leverage": 3
  }
}
```

See [Strategy Config Schema](13-strategy-config-schema.md) for full schema reference and validation rules.

Users may:

create strategy  
name strategy  
edit parameters  
activate strategy

Multiple strategies may exist but typically only one runs at a time.

The worker loads the active strategy configuration at startup.

## API Endpoints

### Core Operations

| Method | Endpoint | Notes |
|--------|----------|-------|
| `GET` | `/api/strategies` | Returns `StrategySummaryDto[]` for authenticated user |
| `GET` | `/api/strategies/{id}` | Returns `StrategyDto` with full `StrategyConfig` |
| `POST` | `/api/strategies` | Validates config + name uniqueness, creates strategy |
| `PUT` | `/api/strategies/{id}` | Validates config + name uniqueness (excluding self), updates |
| `DELETE` | `/api/strategies/{id}` | Soft-deletes (`IsActive = false`) |
| `POST` | `/api/strategies/validate` | Runs `CompositeStrategyValidator` without persisting |

### Strategy Interpretation (F9)

| Method | Endpoint | Notes |
|--------|----------|-------|
| `POST` | `/api/strategies/interpret` | Interprets natural language input → `StrategyIntentDto` with config, confidence, assumptions; rate-limited 10 req/min/IP |

Duplicate strategy names (per user) return HTTP 409.

### Revision History (F3)

| Method | Endpoint | Notes |
|--------|----------|-------|
| `GET` | `/api/strategies/{id}/versions` | Returns `PagedResult<StrategyRevisionSummaryDto>` (paginated revision list); accepts `page` and `pageSize` query params |
| `GET` | `/api/strategies/{id}/versions/{rev:int}` | Returns `StrategyRevisionDto` (single revision with full config); 404 if strategy or revision not found |
| `GET` | `/api/strategies/{id}/diff` | Returns `StrategyDiffDto` (field-level diff); accepts `from` and `to` query params (revision numbers) |
| `POST` | `/api/strategies/{id}/versions/{rev:int}/restore` | Restores a previous revision as a new revision with source=`Restore`; returns 204; 409 if strategy is running |

## Persistence and Versioning

`ConfigJson` is stored directly on the `Strategy` entity. Each `PUT` increments `Version` and creates a new `StrategyRevision` snapshot. Soft-delete via `Strategy.SoftDelete()` sets `IsActive = false`; listings only return active records.

Every create, update, and restore operation automatically generates a `StrategyRevision` with:
- Full JSON snapshot of the config at that point
- Auto-generated change summary (field-level diff)
- Source metadata (`Ui`, `Api`, `Import`, or `Restore`)
- Original natural language input (`SourceMetadata.SourceText`) if created via `/api/strategies/interpret`

Repository: `IStrategyRepository` / `IStrategyRevisionRepository`

Application services:
- `ChangeSummaryGenerator` — computes field-level diff summary between JSON snapshots
- `StrategyDiffService` — detailed field-level diff with JSON paths, old/new values
- `RevisionSourceMapper` — maps `StrategyEntryPoint` to `RevisionSource` enum

## Frontend

See [UI Design — Strategy Builder](07-ui-design.md) for the full card-based builder component map and service list.