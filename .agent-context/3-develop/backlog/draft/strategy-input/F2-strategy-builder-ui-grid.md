# PBI Specification: F2 — Strategy Builder UI (Grid Template)

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-04-02
**Last Updated:** 2026-04-02
**PRD:** [02-strategy-input-pipeline.md](../../../prd-draft/02-strategy-input-pipeline.md)
**Reference:** [strategy-builder-ui-detailed.md](../../1-discover/prd/strategy-builder-ui-detailed.md)
**Implementation Phase:** 1a (Foundation)
**Risk Level:** Medium
**Depends On:** F1 (Extensible Strategy Schema)

---

## Summary

Build the full Strategy Builder UI layout from the [UI spec](../../1-discover/prd/strategy-builder-ui-detailed.md) — all cards, two-column responsive layout, preview, validation — but with **grid as the only working template**. Trend filter and entry condition cards are present in the layout but disabled/hidden for grid mode, ready to be enabled when signal mode ships. Users can build a grid strategy → validate → save. Includes a basic strategy list page for viewing, editing, and deleting strategies.

This is the first end-to-end user-facing feature: every strategy the UI produces works in the engine today.

### User Story

> As a **trader**, I want to **build a grid strategy using a visual form** so that **I can configure, validate, and save strategies without editing JSON**.

### Business Value

The Strategy Builder is the primary user interface for strategy authoring. Shipping it with grid support means immediate value for existing users. The full layout (cards, preview, validation) is built once and reused as conditions are added incrementally.

---

## Requirements

### Functional Requirements

#### Angular Module Structure (full layout — from UI spec)

- [ ] `strategy-builder/` feature folder with all components, models, services, utils
- [ ] All card components created: `strategy-builder-page`, `strategy-template-selector`, `strategy-details-card`, `trend-filter-card`, `entry-conditions-card`, `entry-condition-item`, `exit-rules-card`, `risk-management-card`, `preview-summary-card`, `validation-card`, `json-preview-card`
- [ ] `strategy-list/` page component for listing, editing, and deleting strategies
- [ ] Services: `strategy-template.service`, `strategy-preview.service`, `strategy-validation.service`, `strategy-mapper.service`, `strategy-api.service`
- [ ] Models: TypeScript interfaces matching F1's C# models exactly

#### Angular Routing

- [ ] `/strategies` — Strategy list page (lazy-loaded feature module)
- [ ] `/strategies/new` — Strategy Builder in create mode
- [ ] `/strategies/:id/edit` — Strategy Builder in edit mode (loads existing strategy)
- [ ] Route guard: unsaved changes confirmation on navigation away from builder (dirty form check)

#### What Works in This PBI (Grid Mode)

- [ ] **Template selector**: "Grid" template pre-populates the form. "Blank" starts empty. Other templates (EMA Pullback, RSI Reversal, etc.) are listed but show "Coming soon" badge — not selectable
- [ ] **Strategy details card**: Strategy Name, Exchange, Market, Timeframe, Direction — all functional. Market and Timeframe dropdowns populated dynamically from `GET /api/reference-data/markets`
- [ ] **Grid config card** (new, grid mode only): Levels (number), Spacing % (number), Entry Mode (dropdown: auto_from_signal_candle, manual), Anchor Price (number, shown when manual), Breakdown Threshold (number)
- [ ] **Trend filter card**: Present in layout but **disabled** with subtle "Available in signal mode" label. Form group exists and is wired — just not editable for grid strategies
- [ ] **Entry conditions card**: Present in layout but **disabled** with "Available in signal mode" label. FormArray and condition factory exist — just not usable for grid strategies
- [ ] **Exit rules card**: Take Profit (fixed_percent — functional), Stop Loss (fixed_percent, swing_low — functional). Other exit types shown in dropdown but disabled/"Coming soon"
- [ ] **Risk management card**: Position size type/value, Leverage, Max open trades, Cooldown — all functional
- [ ] **Preview summary card**: Generates plain English description for grid strategies. e.g., "Deploy a long grid on BTC-USD 15m with 10 levels at 0.5% spacing. Take profit at 2%, stop loss at 6%. Risk: 5% of wallet, 1x leverage."
- [ ] **Validation card**: Displays errors/warnings/info from `StrategyValidationService`. All grid-relevant rules enforced client-side
- [ ] **JSON preview card**: Developer mode toggle shows the canonical JSON (optional but recommended)

#### Strategy List Page

- [ ] Table with columns: Strategy Name, Market, Timeframe, Direction, Mode, Created, Updated
- [ ] Edit button per row → navigates to `/strategies/:id/edit`
- [ ] Delete button per row → confirmation dialog → soft delete (`DELETE /api/strategies/{id}`)
- [ ] "New Strategy" button → navigates to `/strategies/new`
- [ ] Empty state when no strategies exist

#### Layout

- [ ] Desktop: Two-column grid (left: template, details, grid config, trend filter, entry conditions, exit, risk; right: preview, validation, JSON)
- [ ] Mobile (< 960px): Single-column stack
- [ ] Page header: Cancel and Save Strategy buttons (no Save Draft)
- [ ] Cancel button: if form has unsaved changes → confirmation dialog; then navigate to `/strategies`
- [ ] Save Strategy button: disabled when form is invalid
- [ ] Angular Material components per the UI spec

#### Backend API

- [ ] `StrategyAuthoringController` with endpoints:
  - `POST /api/strategies` — create strategy (runs F1's `IStrategyValidator`, persists). Strategy name must be unique per user (HTTP 409 if duplicate)
  - `PUT /api/strategies/{id}` — update strategy (validates, persists). Name uniqueness enforced (excluding self)
  - `GET /api/strategies/{id}` — load strategy for editing
  - `GET /api/strategies` — list strategies for current user (active only)
  - `DELETE /api/strategies/{id}` — soft delete (sets `IsActive = false`)
- [ ] HTTP 400 returns `{ errors: [{ severity, fieldPath, code, message }] }` matching the validation card
- [ ] HTTP 409 returns `{ errors: [{ severity: "error", fieldPath: "strategyName", code: "DUPLICATE_NAME", message: "..." }] }` for duplicate names
- [ ] Server re-runs F1's `IStrategyValidator` on every POST/PUT (defense in depth) before persisting
- [ ] `IStrategyAuthoringService` orchestrates: validate → check name uniqueness → persist
- [ ] `IStrategyRepository` + `StrategyRepository` — EF Core persistence
- [ ] Uses existing `Strategy` + `StrategyConfig` domain entities (per existing domain model). `Strategy` holds metadata; `StrategyConfig` holds `ConfigJson`

#### Reference Data API

- [ ] `ReferenceDataController` with endpoint:
  - `GET /api/reference-data/markets` — returns available markets (BTC-USD, ETH-USD, etc.) and timeframes (5m, 15m, 1h, 4h) from `HyperliquidAssetMapper`
- [ ] Market names use the user-friendly format (BTC-USD) — mapping to Hyperliquid coin names (BTC) happens at order execution time, not at authoring time

#### `StrategyMapperService` (Angular)

- [ ] Converts reactive form values → canonical JSON matching F1's schema
- [ ] Sets `strategyMode = "grid"` for grid templates
- [ ] Sets `source.entryPoint = "ui_builder"`
- [ ] Strips disabled optional sections (trend filter, entry conditions) as `null`

#### Validation Constraints

| Field | Min | Max |
|-------|-----|-----|
| Strategy Name | 1 char | 100 chars |
| Grid Levels | 1 | 50 |
| Grid Spacing % | 0.01 | 10 |
| Leverage | 1 | 50 |
| Position Size % | 0.01 | 100 |
| Max Open Trades | 1 | 10 |
| Take Profit % | 0.01 | 50 |
| Stop Loss % | 0.01 | 50 |
| Breakdown Threshold | 0 | 10 |

#### Validation Timing

- [ ] Inline field errors shown on blur
- [ ] Validation panel updates on any form change (debounced ~300ms)
- [ ] Save button disabled when any error-severity validation exists

### Non-Functional Requirements

- [ ] Page loads within 500ms
- [ ] Preview updates within 100ms of form change (client-side)
- [ ] Responsive layout at 960px breakpoint
- [ ] Keyboard navigation and ARIA labels on all controls

---

## User Flow

### Happy Path — Create Grid Strategy

1. Trader navigates to `/strategies` (strategy list page)
2. Clicks "New Strategy" → navigates to `/strategies/new`
3. Selects "Grid" template
4. Form shows: Strategy Details + Grid Config + Exit Rules + Risk Management
5. Trend filter and Entry conditions cards are visible but greyed out ("Available in signal mode")
6. Market and Timeframe dropdowns populated from reference data API
7. Fills in: BTC-USD, 15m, long, 10 levels, 0.5% spacing, TP 2%, SL 6%, 5% wallet
8. Preview card shows plain English description
9. Validation panel shows no issues (green)
10. Clicks "Save Strategy"
11. Backend validates and persists canonical JSON with `strategyMode = "grid"`
12. Navigates to `/strategies` with success snackbar: "Strategy 'BTC Grid Long' saved"

### Happy Path — Edit Strategy

1. Trader navigates to `/strategies`
2. Clicks "Edit" on an existing strategy
3. Builder loads in edit mode with form populated from `GET /api/strategies/{id}`
4. Makes changes
5. Clicks "Save Strategy" → `PUT /api/strategies/{id}`
6. Navigates to `/strategies` with success snackbar

### Happy Path — Delete Strategy

1. Trader navigates to `/strategies`
2. Clicks "Delete" on a strategy
3. Confirmation dialog: "Are you sure you want to delete 'BTC Grid Long'?"
4. Confirms → `DELETE /api/strategies/{id}` (soft delete)
5. Strategy removed from list with success snackbar

### Error Path

| Scenario | Behavior |
|----------|----------|
| Grid levels = 0 | Inline field error on blur, Save disabled |
| Strategy name missing | Validation panel: "Strategy name is required" (error) |
| Duplicate strategy name | Server returns HTTP 409, displayed in validation panel |
| Both TP and SL disabled | Validation panel: warning (not blocking) |
| Server validation fails | HTTP 400 errors displayed in validation panel |
| Navigate away with unsaved changes | Confirmation dialog: "Unsaved changes will be lost" |

---

## Technical Considerations

### Reactive Form Model (Grid Mode)

```ts
this.form = this.fb.group({
  templateId: ['grid'],
  strategyName: ['', [Validators.required, Validators.maxLength(100)]],
  exchange: ['Hyperliquid', Validators.required],
  market: ['BTC-USD', Validators.required],
  timeframe: ['15m', Validators.required],
  direction: ['long', Validators.required],

  grid: this.fb.group({
    levels: [10, [Validators.required, Validators.min(1), Validators.max(50)]],
    spacing: [0.5, [Validators.required, Validators.min(0.01), Validators.max(10)]],
    entryMode: ['auto_from_signal_candle', Validators.required],
    anchorPrice: [null],
    breakdownThreshold: [1.5, [Validators.min(0), Validators.max(10)]]
  }),

  trendFilter: this.fb.group({ /* full shape, disabled */ }),
  entryLogic: ['all'],
  entryConditions: this.fb.array([]),

  exit: this.fb.group({
    takeProfit: this.fb.group({
      enabled: [true],
      type: ['fixed_percent'],
      value: [2, [Validators.min(0.01), Validators.max(50)]]
    }),
    stopLoss: this.fb.group({
      enabled: [true],
      type: ['fixed_percent'],
      value: [6, [Validators.min(0.01), Validators.max(50)]],
      lookback: [null]
    }),
    exitOnOppositeSignal: [false]
  }),

  risk: this.fb.group({
    positionSizeType: ['percent_wallet', Validators.required],
    positionSizeValue: [5, [Validators.required, Validators.min(0.01), Validators.max(100)]],
    leverage: [1, [Validators.required, Validators.min(1), Validators.max(50)]],
    maxOpenTrades: [1, [Validators.required, Validators.min(1), Validators.max(10)]],
    cooldownValue: [0],
    cooldownUnit: ['candles'],
    allowSameCandleReentry: [false]
  }),

  metadata: this.fb.group({ tags: [[]], notes: [''] })
});
```

### Components Built (all from UI spec layout)

| Component | Status in this PBI |
|-----------|-------------------|
| `StrategyListPageComponent` | **Functional** — list, edit, delete |
| `StrategyBuilderPageComponent` | **Functional** — full layout, create + edit modes |
| `StrategyTemplateSelectorComponent` | **Functional** — grid works, others "coming soon" |
| `StrategyDetailsCardComponent` | **Functional** — no Enabled toggle |
| `GridConfigCardComponent` | **Functional** — new card for grid params |
| `TrendFilterCardComponent` | **Shell built, disabled** for grid mode |
| `EntryConditionsCardComponent` | **Shell built, disabled** for grid mode |
| `EntryConditionItemComponent` | **Shell built, disabled** |
| `ExitRulesCardComponent` | **Functional** — fixed_percent TP/SL |
| `RiskManagementCardComponent` | **Functional** |
| `PreviewSummaryCardComponent` | **Functional** — grid preview text |
| `ValidationCardComponent` | **Functional** |
| `JsonPreviewCardComponent` | **Functional** — developer mode |

---

## Decisions Log

| # | Question | Decision |
|---|----------|----------|
| 1 | Save Draft vs Save Strategy? | **No drafts.** Single "Save Strategy" button. Cancel + Save Strategy only. |
| 2 | Strategy entity — single or split? | **Keep separate Strategy + StrategyConfig** per existing domain model. Strategy holds metadata; StrategyConfig holds ConfigJson. |
| 3 | Post-save navigation? | **Navigate to strategy list page** (`/strategies`) with success snackbar. |
| 4 | Strategy list page in scope? | **Yes** — basic list with name, market, mode, dates, edit/delete actions. |
| 5 | Edit flow? | **Edit from list page** → Builder loads in edit mode at `/strategies/:id/edit`. |
| 6 | Market/Timeframe source? | **Dynamic from API** — new `GET /api/reference-data/markets` endpoint exposing `HyperliquidAssetMapper` data. |
| 7 | Market display format? | **BTC-USD** (user-friendly, exchange-agnostic). Mapping to Hyperliquid coin names at execution time. |
| 8 | Max validation constraints? | Levels ≤ 50, spacing ≤ 10%, leverage ≤ 50x (exchange-governed), position size ≤ 100%, max open trades ≤ 10, TP/SL ≤ 50%. |
| 9 | Duplicate strategy names? | **Unique per user.** Server enforces; HTTP 409 on conflict. |
| 10 | Delete strategy? | **In scope — soft delete** with confirmation dialog. `DELETE /api/strategies/{id}` sets `IsActive = false`. |
| 11 | Cancel button behavior? | **Confirm if dirty**, then navigate to `/strategies`. |
| 12 | Validation timing? | **On blur** (inline field errors) + **live validation panel** (debounced ~300ms on form change). |
| 13 | Enabled toggle? | **Removed from F2.** Strategies saved as inactive. Activation deferred to Worker integration PBI. |
| 14 | Server-side validation on save? | **Yes — defense in depth.** POST/PUT runs F1's `IStrategyValidator` before persisting. |
| 15 | Rename strategy? | **Via edit flow.** No separate rename action. |

---

## Out of Scope

- Signal mode / entry condition evaluation (F5)
- Trend filter evaluation (F5)
- RSI / EMA / MACD condition UI cards (F6)
- Strategy versioning (F3)
- Backward compatibility migration (F4)
- Natural language input
- Strategy activation / Enabled toggle (deferred to Worker integration PBI)
- Save Draft functionality
- Clone / duplicate strategy
- Strategy export / import

---

## Acceptance Criteria

- [ ] **Given** the strategy list page (`/strategies`), **When** loaded, **Then** all active strategies for the current user are displayed with name, market, timeframe, direction, mode, created, and updated columns
- [ ] **Given** the strategy list page, **When** "New Strategy" is clicked, **Then** the user navigates to `/strategies/new`
- [ ] **Given** the Strategy Builder page, **When** loaded, **Then** two-column layout (desktop) or single-column (mobile) is displayed
- [ ] **Given** "Grid" template selected, **When** form renders, **Then** Grid Config card is active; Trend Filter and Entry Conditions cards show "Available in signal mode" (disabled); no Enabled toggle is shown
- [ ] **Given** the Strategy Details card, **When** Market dropdown is opened, **Then** options are populated from `GET /api/reference-data/markets` using BTC-USD format
- [ ] **Given** the grid config card, **When** levels = 10 and spacing = 0.5, **Then** preview shows "Deploy a long grid... with 10 levels at 0.5% spacing"
- [ ] **Given** grid levels set to 51, **When** the field loses focus, **Then** an inline validation error is shown and the Save button is disabled
- [ ] **Given** a valid grid strategy form, **When** "Save Strategy" clicked, **Then** canonical JSON is sent to `POST /api/strategies` with `strategyMode = "grid"` and `trendFilter = null`, `entryConditions = null`
- [ ] **Given** a successful save, **When** the server responds 201, **Then** the user navigates to `/strategies` and a success snackbar is displayed
- [ ] **Given** a strategy name already used by this user, **When** "Save Strategy" clicked, **Then** HTTP 409 is returned and the error is shown in the validation panel
- [ ] **Given** the server returns HTTP 400 with validation errors, **When** displayed, **Then** errors appear in the validation card with severity and field path
- [ ] **Given** `GET /api/strategies/{id}`, **When** an existing grid strategy is loaded in edit mode, **Then** the form is populated with all values
- [ ] **Given** the edit flow, **When** strategy is updated and saved, **Then** `PUT /api/strategies/{id}` is called and the user navigates to `/strategies` with a success snackbar
- [ ] **Given** the strategy list page, **When** "Delete" is clicked on a strategy, **Then** a confirmation dialog appears; on confirm, the strategy is soft-deleted and removed from the list
- [ ] **Given** unsaved changes in the builder, **When** the Cancel button is clicked, **Then** a confirmation dialog appears warning about unsaved changes
- [ ] **Given** "EMA Pullback" template, **When** selected, **Then** it shows "Coming soon" and is not selectable
- [ ] **Given** the exit rules card, **When** TP type dropdown is opened, **Then** `fixed_percent` is selectable; `risk_reward`, `atr_multiple` etc. show "Coming soon"
- [ ] **Given** the JSON preview (developer mode), **When** toggled on, **Then** the canonical JSON is displayed matching F1's schema

### Release Notes Information

- **Heading**: Strategy Builder UI
- **Release note type**: Feature
- **Release Note Summary**: Visual strategy builder with grid template, exit rules, risk management, live preview, and validation. Includes strategy list page with edit and delete. Build grid strategies without writing JSON.
- **Release Notes Audience**: Product
- **Breaking Change**: No
