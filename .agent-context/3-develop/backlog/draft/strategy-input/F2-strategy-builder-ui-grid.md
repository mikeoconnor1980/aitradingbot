# PBI Specification: F2 — Strategy Builder UI (Grid Template)

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-04-02
**PRD:** [02-strategy-input-pipeline.md](../../../prd-draft/02-strategy-input-pipeline.md)
**Reference:** [strategy-builder-ui-detailed.md](../../1-discover/prd/strategy-builder-ui-detailed.md)
**Implementation Phase:** 1a (Foundation)
**Risk Level:** Medium
**Depends On:** F1 (Extensible Strategy Schema)

---

## Summary

Build the full Strategy Builder UI layout from the [UI spec](../../1-discover/prd/strategy-builder-ui-detailed.md) — all cards, two-column responsive layout, preview, validation — but with **grid as the only working template**. Trend filter and entry condition cards are present in the layout but disabled/hidden for grid mode, ready to be enabled when signal mode ships. Users can build a grid strategy → validate → save → backtest.

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
- [ ] Services: `strategy-template.service`, `strategy-preview.service`, `strategy-validation.service`, `strategy-mapper.service`
- [ ] Models: TypeScript interfaces matching F1's C# models exactly

#### What Works in This PBI (Grid Mode)

- [ ] **Template selector**: "Grid" template pre-populates the form. "Blank" starts empty. Other templates (EMA Pullback, RSI Reversal, etc.) are listed but show "Coming soon" badge — not selectable
- [ ] **Strategy details card**: Strategy Name, Exchange, Market, Timeframe, Direction, Enabled — all functional
- [ ] **Grid config card** (new, grid mode only): Levels (number), Spacing % (number), Entry Mode (dropdown: auto_from_signal_candle, manual), Anchor Price (number, shown when manual), Breakdown Threshold (number)
- [ ] **Trend filter card**: Present in layout but **disabled** with subtle "Available in signal mode" label. Form group exists and is wired — just not editable for grid strategies
- [ ] **Entry conditions card**: Present in layout but **disabled** with "Available in signal mode" label. FormArray and condition factory exist — just not usable for grid strategies
- [ ] **Exit rules card**: Take Profit (fixed_percent — functional), Stop Loss (fixed_percent, swing_low — functional). Other exit types shown in dropdown but disabled/"Coming soon"
- [ ] **Risk management card**: Position size type/value, Leverage, Max open trades, Cooldown — all functional
- [ ] **Preview summary card**: Generates plain English description for grid strategies. e.g., "Deploy a long grid on BTC-USD 15m with 10 levels at 0.5% spacing. Take profit at 2%, stop loss at 6%. Risk: 5% of wallet, 1x leverage."
- [ ] **Validation card**: Displays errors/warnings/info from `StrategyValidationService`. All grid-relevant rules enforced client-side
- [ ] **JSON preview card**: Developer mode toggle shows the canonical JSON (optional but recommended)

#### Layout

- [ ] Desktop: Two-column grid (left: template, details, grid config, trend filter, entry conditions, exit, risk; right: preview, validation, JSON)
- [ ] Mobile (< 960px): Single-column stack
- [ ] Page header: Cancel, Save Draft, Save Strategy buttons
- [ ] Angular Material components per the UI spec

#### Backend API

- [ ] `StrategyAuthoringController` with endpoints:
  - `POST /api/strategies` — create strategy (validates via F1's `IStrategyValidator`, persists)
  - `PUT /api/strategies/{id}` — update strategy
  - `GET /api/strategies/{id}` — load strategy
  - `GET /api/strategies` — list strategies for current user
- [ ] HTTP 400 returns `{ errors: [{ severity, fieldPath, code, message }] }` matching the validation card
- [ ] `IStrategyAuthoringService` orchestrates: validate → persist canonical JSON
- [ ] `IStrategyRepository` + `StrategyRepository` — EF Core persistence
- [ ] `Strategy` domain entity with `Id`, `UserId`, `CanonicalJson`, `StrategyName`, `CreatedAt`, `UpdatedAt`

#### `StrategyMapperService` (Angular)

- [ ] Converts reactive form values → canonical JSON matching F1's schema
- [ ] Sets `strategyMode = "grid"` for grid templates
- [ ] Sets `source.entryPoint = "ui_builder"`
- [ ] Strips disabled optional sections (trend filter, entry conditions) as `null`

### Non-Functional Requirements

- [ ] Page loads within 500ms
- [ ] Preview updates within 100ms of form change (client-side)
- [ ] Responsive layout at 960px breakpoint
- [ ] Keyboard navigation and ARIA labels on all controls

---

## User Flow

### Happy Path — Grid Strategy

1. Trader navigates to Strategy Builder
2. Selects "Grid" template
3. Form shows: Strategy Details + Grid Config + Exit Rules + Risk Management
4. Trend filter and Entry conditions cards are visible but greyed out ("Available in signal mode")
5. Fills in: BTC-USD, 15m, long, 10 levels, 0.5% spacing, TP 2%, SL 6%, 5% wallet
6. Preview card shows plain English description
7. Validation shows no issues (green)
8. Clicks "Save Strategy"
9. Backend validates and persists canonical JSON with `strategyMode = "grid"`
10. Success confirmation

### Error Path

| Scenario | Behavior |
|----------|----------|
| Grid levels = 0 | Client-side error, Save disabled |
| Strategy name missing | Validation card: "Strategy name is required" (error) |
| Both TP and SL disabled | Validation card: warning (not blocking) |
| Server validation fails | HTTP 400 errors displayed in validation card |

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
  enabled: [true],

  grid: this.fb.group({
    levels: [10, [Validators.required, Validators.min(1)]],
    spacing: [0.5, [Validators.required, Validators.min(0.01)]],
    entryMode: ['auto_from_signal_candle', Validators.required],
    anchorPrice: [null],
    breakdownThreshold: [1.5, [Validators.min(0)]]
  }),

  trendFilter: this.fb.group({ /* full shape, disabled */ }),
  entryLogic: ['all'],
  entryConditions: this.fb.array([]),

  exit: this.fb.group({
    takeProfit: this.fb.group({
      enabled: [true],
      type: ['fixed_percent'],
      value: [2, [Validators.min(0.01)]]
    }),
    stopLoss: this.fb.group({
      enabled: [true],
      type: ['fixed_percent'],
      value: [6, [Validators.min(0.01)]],
      lookback: [null]
    }),
    exitOnOppositeSignal: [false]
  }),

  risk: this.fb.group({
    positionSizeType: ['percent_wallet', Validators.required],
    positionSizeValue: [5, [Validators.required, Validators.min(0.01)]],
    leverage: [1, [Validators.required, Validators.min(1)]],
    maxOpenTrades: [1, [Validators.required, Validators.min(1)]],
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
| `StrategyBuilderPageComponent` | **Functional** — full layout |
| `StrategyTemplateSelectorComponent` | **Functional** — grid works, others "coming soon" |
| `StrategyDetailsCardComponent` | **Functional** |
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

## Out of Scope

- Signal mode / entry condition evaluation (F5)
- Trend filter evaluation (F5)
- RSI / EMA / MACD condition UI cards (F6)
- Strategy versioning (F3)
- Backward compatibility migration (F4)
- Natural language input

---

## Acceptance Criteria

- [ ] **Given** the Strategy Builder page, **When** loaded, **Then** two-column layout (desktop) or single-column (mobile) is displayed
- [ ] **Given** "Grid" template selected, **When** form renders, **Then** Grid Config card is active; Trend Filter and Entry Conditions cards show "Available in signal mode" (disabled)
- [ ] **Given** the grid config card, **When** levels = 10 and spacing = 0.5, **Then** preview shows "Deploy a long grid... with 10 levels at 0.5% spacing"
- [ ] **Given** a valid grid strategy form, **When** "Save Strategy" clicked, **Then** canonical JSON is sent to `POST /api/strategies` with `strategyMode = "grid"` and `trendFilter = null`, `entryConditions = null`
- [ ] **Given** the server returns HTTP 400 with validation errors, **When** displayed, **Then** errors appear in the validation card with severity and field path
- [ ] **Given** `GET /api/strategies/{id}`, **When** an existing grid strategy is loaded, **Then** the form is populated with all values
- [ ] **Given** "EMA Pullback" template, **When** selected, **Then** it shows "Coming soon" and is not selectable
- [ ] **Given** the exit rules card, **When** TP type dropdown is opened, **Then** `fixed_percent` is selectable; `risk_reward`, `atr_multiple` etc. show "Coming soon"
- [ ] **Given** the JSON preview (developer mode), **When** toggled on, **Then** the canonical JSON is displayed matching F1's schema

### Release Notes Information

- **Heading**: Strategy Builder UI
- **Release Note Summary**: Visual strategy builder with grid template, exit rules, risk management, live preview, and validation. Build grid strategies without writing JSON.
- **Breaking Change**: No
