# UI Design

Main dashboard shows:

chart  
positions (Actions column: Close; SL/TP columns showing trigger prices; "Set SL/TP" button when none set; inline remove per field)  
orders (Actions column: Cancel, Cancel All, Modify per row)  
activity feed (live fill and order update events; 100-event cap; third tab alongside Positions and Orders)  
signals  
bot state

---

# Strategy Builder

Two-page feature under `frontend/trading-ui/src/app/features/strategy-builder/`.

**Strategy List** (`/strategies`) — lists all user strategies. Shows market, timeframe, direction, version. Edit and delete actions per row.

**Strategy Builder** (`/strategies/new`, `/strategies/:id/edit`) — reactive form with two-column card layout. Same component handles create and edit; route parameter `id` controls mode.

| Card Component | Purpose |
|---|---|
| `StrategyTemplateSelectorComponent` | Select a starting template |
| `StrategyDetailsCardComponent` | Name, market, timeframe, direction |
| `GridConfigCardComponent` | Grid levels, spacing, entry mode, anchor price |
| `ExitRulesCardComponent` | Take profit / stop loss rules |
| `RiskManagementCardComponent` | Position sizing, leverage, cooldown |
| `TrendFilterCardComponent` | Optional macro filter (not active in v1) |
| `EntryConditionsCardComponent` | Signal-mode entry conditions; renders per-condition sub-components based on `type` |
| `RsiConditionItemComponent` | RSI condition card (`rsi-condition-item/`) |
| `PriceVsEmaConditionItemComponent` | Price vs EMA condition card (`price-vs-ema-condition-item/`) |
| `MacdConditionItemComponent` | MACD condition card (`macd-condition-item/`); inputs: fastPeriod, slowPeriod, signalPeriod, operator |
| `PreviewSummaryCardComponent` | Read-only config summary |
| `ValidationCardComponent` | Server-side validation results |
| `JsonPreviewCardComponent` | Live JSON preview of config |

Services (all `providedIn: 'root'`):

| Service | Responsibility |
|---|---|
| `StrategyApiService` | CRUD calls + validate against `/api/strategies` |
| `ReferenceDataService` | GET `/api/reference-data/markets`; `shareReplay` cached |
| `StrategyMapperService` | Converts reactive form values → `StrategyConfig` |
| `StrategyValidationService` | Client-side validation rules |
| `ConditionFactoryService` | Creates typed `FormGroup` instances for each entry condition type; extensible for future condition types |

**Signal Templates** — `STRATEGY_TEMPLATES` (`strategy-builder/models/strategy.model.ts`) defines available templates. The helper `_isSignalTemplate(templateId)` exists in 4 locations (builder page, mapper service, validation service, preview-summary card) and must be updated when a new signal-mode template is added. Current signal template IDs: `custom_signal`, `ema_pullback`, `macd_cross`.

Route guard: `unsavedChangesGuard` (`CanDeactivateFn`) — prompts confirmation dialog when form is dirty.

---

# Chart

Chart uses TradingView Lightweight Charts.

Displays:

candles  
grid levels  
entry line  
hedge line  
take profit

---

# Strategy Revision History

Two components handle strategy versioning in the builder page (edit mode only).

| Component | Purpose |
|---|---|
| `RevisionHistoryPanelComponent` | Expandable panel with paginated revision table — revision number, source, change summary, timestamp, compare checkboxes, restore button |
| `DiffViewComponent` | Field-level diff display showing JSON path, old value, and new value for selected revision comparison |

Location: `frontend/trading-ui/src/app/features/strategy-builder/components/revision-history-panel/` and `diff-view/`

API methods on `StrategyApiService`:

| Method | Endpoint | Purpose |
|---|---|---|
| `getVersions(strategyId, page, pageSize)` | `GET /versions` | Paginated revision list |
| `getVersion(strategyId, rev)` | `GET /versions/{rev}` | Single revision with full config |
| `getDiff(strategyId, from, to)` | `GET /diff` | Field-level diff between two revisions |
| `restoreVersion(strategyId, rev)` | `POST /versions/{rev}/restore` | Restore previous revision; emits event to reload form |