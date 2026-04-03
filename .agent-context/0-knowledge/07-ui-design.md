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
| `EntryConditionsCardComponent` | Signal-mode entry conditions |
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