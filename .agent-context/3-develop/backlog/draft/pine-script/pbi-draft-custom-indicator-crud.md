# Custom Indicator CRUD

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-31T12:00:00Z
**Epic:** Pine Script Indicator Integration (Option F)

## User Story

As a **trader**, I want to **create, edit, and manage custom indicators by pasting Pine Script source** so that **I can build a personal library of indicators that persist across sessions and are available for charting and strategy configuration**.

### Business Value

Without persistence and management, users would need to re-paste Pine Script every session. This PBI establishes the domain entity, API, and UI that ties together the extractor (PBI 1), chart rendering (PBI 4), and signal mapping (PBI 5). It's the central CRUD that all other Pine Script PBIs depend on.

---

## Requirements

### Functional Requirements

- [ ] **`CustomIndicator` domain entity** — New entity with: `Id` (Guid), `UserId` (tenant scope), `Name`, `PineSource` (raw text), `ExtractedConfigJson` (serialized `ExtractionResult`), `IsValid` (bool), `ValidationErrors` (string[]), `IsOverlay` (bool — from `indicator(overlay=true)`), `CreatedUtc`, `UpdatedUtc`
- [ ] **Create indicator** — `POST /api/indicators` — accepts `{ name, pineSource }`. Runs extractor, stores result. Returns created entity with extraction result
- [ ] **Update indicator** — `PUT /api/indicators/{id}` — accepts `{ name?, pineSource? }`. Re-runs extractor if source changed. Returns updated entity
- [ ] **Delete indicator** — `DELETE /api/indicators/{id}` — soft delete or hard delete (no cascade concerns at this stage)
- [ ] **List indicators** — `GET /api/indicators` — returns all indicators for the current user (tenant-scoped)
- [ ] **Get indicator** — `GET /api/indicators/{id}` — returns full indicator with extraction result
- [ ] **Validate-only endpoint** — `POST /api/indicators/validate` — accepts `{ pineSource }`, runs extractor, returns extraction result without persisting. Used for real-time validation in the editor
- [ ] **Angular Indicator Management page** — New page accessible from the main navigation. List of saved indicators with name, validity status, indicator count, last updated
- [ ] **Pine Script Editor** — Textarea/code editor for pasting Pine Script source. Shows real-time validation feedback (calls validate endpoint on debounced input). Displays extracted indicators, plots, and alerts in a summary panel. Shows warnings/errors for unsupported constructs
- [ ] **Input parameter overrides** — For each extracted `input()` parameter, show an editable field with the default value. Overridden values are stored alongside the indicator as `InputOverrides` JSON
- [ ] **Indicator name auto-detection** — Pre-fill the indicator name from the `indicator("name")` or `strategy("name")` call in the Pine Script. User can override
- [ ] **Tenant scoping** — All queries filter by current `UserId`. Users cannot access other users' indicators
- [ ] **Duplicate detection** — Warn (not block) if user saves an indicator with the same name as an existing one

### Non-Functional Requirements

- [ ] API responses return in < 200ms for CRUD operations
- [ ] Validate endpoint returns in < 300ms (includes extraction time)
- [ ] Pine Script source stored as-is (no modification) to support future re-extraction with improved extractors
- [ ] Maximum Pine Script source size: 50KB (prevents abuse)

---

## User Flow

### Creating a New Indicator

1. User navigates to Indicators page from main nav
2. User clicks "New Indicator" button
3. Pine Script editor opens (empty)
4. User pastes Pine Script from TradingView
5. System calls `/api/indicators/validate` after 500ms debounce
6. Validation panel shows:
   - Green checkmarks for each extracted indicator (e.g., "✓ EMA(close, 21)", "✓ RSI(close, 14)")
   - Plot configuration summary (e.g., "2 overlay lines, 1 sub-pane")
   - Alert conditions (e.g., "1 alert: Buy Signal")
   - Amber warnings for any unsupported constructs
7. Name field is auto-filled from `indicator("My Setup")` → "My Setup"
8. User adjusts input parameter defaults if desired (e.g., RSI period from 14 to 20)
9. User clicks "Save"
10. System calls `POST /api/indicators` → indicator saved
11. User returns to indicator list, sees new indicator with green valid status

### Editing an Existing Indicator

1. User clicks an indicator in the list
2. Editor loads with existing Pine Script source and extraction summary
3. User modifies source or input overrides
4. Real-time re-validation shows updated extraction
5. User clicks "Save" → `PUT /api/indicators/{id}`

### Invalid Script

1. User pastes script that fails extraction entirely
2. Validation panel shows red error with explanation
3. "Save" button is disabled (cannot save invalid indicators)
4. User can still save a partially-valid indicator (with warnings) — their choice

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| API error during save | Toast notification with retry option |
| Concurrent edit (stale data) | Return 409 Conflict, prompt user to reload |
| Pine Script exceeds 50KB | Frontend validation rejects before API call |
| Name exceeds 200 characters | Frontend validation rejects |
| Unauthorized access to another user's indicator | Return 404 (not 403, to avoid information disclosure) |

---

## Technical Considerations

### Bounded Context

**Context:** Domain entity in `TradingApp.Domain`, CRUD operations in `TradingApp.Application/PineScript`, API controller in `TradingApp.Api/Controllers`, persistence in `TradingApp.Persistence`.

### New/Modified Components

#### Backend

| Component | Layer | Action |
|-----------|-------|--------|
| `CustomIndicator` | Domain/Entities | **New** — Domain entity |
| `ICustomIndicatorRepository` | Application/Abstractions/Repositories | **New** — Repository interface |
| `CustomIndicatorRepository` | Persistence/Repositories | **New** — EF Core implementation |
| `CreateCustomIndicatorCommand` | Application/PineScript/Commands | **New** — MediatR command handler |
| `UpdateCustomIndicatorCommand` | Application/PineScript/Commands | **New** — MediatR command handler |
| `DeleteCustomIndicatorCommand` | Application/PineScript/Commands | **New** — MediatR command handler |
| `GetCustomIndicatorsQuery` | Application/PineScript/Queries | **New** — MediatR query (list, by-id) |
| `ValidatePineScriptQuery` | Application/PineScript/Queries | **New** — MediatR query (validate-only) |
| `IndicatorController` | Api/Controllers | **New** — REST controller |
| `TradingAppDbContext` | Persistence | **Modified** — Add `DbSet<CustomIndicator>` |
| EF Migration | Persistence/Migrations | **New** — Add `CustomIndicators` table |

#### Frontend

| Component | Action |
|-----------|--------|
| `IndicatorListComponent` | **New** — Page listing saved indicators with status badges |
| `IndicatorEditorComponent` | **New** — Pine Script editor + validation panel + input overrides |
| `IndicatorService` | **New** — Angular service calling `/api/indicators/*` endpoints |
| `CustomIndicator` model | **New** — TypeScript interface matching API response |
| App routing | **Modified** — Add `/indicators` route |
| Navigation | **Modified** — Add "Indicators" link to sidebar/nav |

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/indicators` | List all indicators for current user |
| GET | `/api/indicators/{id}` | Get single indicator with extraction result |
| POST | `/api/indicators` | Create indicator (runs extractor) |
| PUT | `/api/indicators/{id}` | Update indicator (re-runs extractor if source changed) |
| DELETE | `/api/indicators/{id}` | Delete indicator |
| POST | `/api/indicators/validate` | Validate Pine Script without persisting |

### Response Shape

```json
{
  "id": "guid",
  "name": "My EMA Crossover",
  "pineSource": "//@version=5\nindicator(\"My EMA Crossover\", overlay=true)...",
  "isValid": true,
  "isOverlay": true,
  "validationErrors": [],
  "extractedConfig": {
    "indicators": [
      { "function": "ta.ema", "source": "close", "parameters": { "length": 21 }, "variableName": "ema21" },
      { "function": "ta.ema", "source": "close", "parameters": { "length": 50 }, "variableName": "ema50" }
    ],
    "plots": [
      { "variableName": "ema21", "title": "EMA 21", "color": "#2196F3", "style": "line", "isOverlay": true }
    ],
    "alerts": [
      { "title": "Buy Signal", "condition": { "type": "logical", "operator": "and", "left": {...}, "right": {...} } }
    ],
    "inputs": [
      { "name": "fastLength", "type": "int", "defaultValue": 21, "minValue": 1, "maxValue": 500 }
    ],
    "unsupportedConstructs": [],
    "warnings": []
  },
  "inputOverrides": { "fastLength": 25 },
  "createdUtc": "2026-03-31T12:00:00Z",
  "updatedUtc": "2026-03-31T12:00:00Z"
}
```

### Database Schema

```sql
CREATE TABLE CustomIndicators (
    Id TEXT PRIMARY KEY,          -- Guid as TEXT for SQLite
    UserId TEXT NOT NULL,
    Name TEXT NOT NULL,
    PineSource TEXT NOT NULL,
    ExtractedConfigJson TEXT,     -- Serialized ExtractionResult
    InputOverridesJson TEXT,      -- Serialized input overrides
    IsValid INTEGER NOT NULL,
    IsOverlay INTEGER NOT NULL,
    ValidationErrorsJson TEXT,
    CreatedUtc TEXT NOT NULL,
    UpdatedUtc TEXT NOT NULL
);

CREATE INDEX IX_CustomIndicators_UserId ON CustomIndicators(UserId);
```

---

## Dependencies

- **PBI: Pine Script Pattern Extractor** — the `PineScriptExtractor` is called during create/update/validate

---

## Out of Scope

- Indicator computation against candle data (see PBI: Indicator Computation Pipeline)
- Chart rendering of indicators (see PBI: Chart Indicator Overlay Rendering)
- Alert-to-signal mapping configuration (see PBI: Alert Condition → Signal Mapping)
- Sharing indicators between users
- Importing/exporting indicator definitions
- Pine Script syntax highlighting in the editor (nice-to-have, not required)
- Version control for indicator source changes

---

## Acceptance Criteria

- [ ] `CustomIndicator` entity persists in SQLite with all required fields
- [ ] `POST /api/indicators` creates an indicator, runs extractor, returns full entity with extraction result
- [ ] `PUT /api/indicators/{id}` updates and re-extracts when source changes
- [ ] `DELETE /api/indicators/{id}` removes the indicator
- [ ] `GET /api/indicators` returns only the current user's indicators (tenant isolation)
- [ ] `GET /api/indicators/{id}` returns 404 for other users' indicators
- [ ] `POST /api/indicators/validate` returns extraction result without persisting
- [ ] Angular indicator list page displays saved indicators with name, validity status, and last updated
- [ ] Pine Script editor shows real-time validation feedback on paste/edit
- [ ] Extracted indicators, plots, and alerts display in the validation summary panel
- [ ] Unsupported constructs display as amber warnings
- [ ] Invalid indicators cannot be saved (Save button disabled)
- [ ] Partially valid indicators (with warnings) can be saved
- [ ] Input parameter overrides are editable and persisted
- [ ] Indicator name auto-fills from `indicator()` header
- [ ] Pine Script source capped at 50KB
- [ ] All CRUD operations have unit tests for the command/query handlers
- [ ] Repository has integration tests verifying tenant isolation

### Release Notes Information

- **Heading**: Custom Indicator Management
- **Release note type**: Feature
- **Release Note Summary**: Create and manage custom indicators by pasting TradingView Pine Script. The system automatically extracts indicator functions, parameters, and alert conditions. Manage your indicator library with real-time validation feedback.
- **Release Notes Audience**: Product
- **Breaking Change**: No (new table, no schema changes to existing tables)
