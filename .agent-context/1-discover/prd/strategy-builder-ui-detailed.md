
# Strategy Builder UI – Detailed Angular Implementation Guide

## Overview

This document describes a detailed Angular-based Strategy Builder UI for a crypto trading application. It is intended to support:

- template-driven strategy creation from known patterns
- advanced rule composition using indicators
- clean mapping from UI form state to engine-ready JSON
- Angular Material implementation using reactive forms
- mobile and desktop friendly layout
- future expansion into natural language, AST generation, and backtesting

This guide focuses on an example strategy pattern such as:

- trend filter: EMA 50 above EMA 200
- entry: price near EMA 50 and RSI below 40
- exit: 3% take profit and swing-low stop loss
- risk: 5% wallet position size

---

## Goals

The Strategy Builder UI should:

1. let non-technical users build strategies without editing JSON
2. support advanced users through configurable conditions
3. provide immediate validation and human-readable preview
4. serialize consistently into a well-defined JSON contract
5. support strategy templates and future AI-assisted population

---

## Recommended Screen Layout

### Desktop layout

Use a two-column layout.

- **Left column**
  - Template Selection
  - Strategy Details
  - Trend Filter
  - Entry Conditions
  - Exit Rules
  - Risk Management
  - Execution Controls (optional)
- **Right column**
  - Preview Summary
  - Validation / Warnings
  - JSON Preview (optional developer mode)
  - Indicator Notes / Help

### Mobile layout

Use a vertical stack of cards:

1. Template Selection
2. Strategy Details
3. Trend Filter
4. Entry Conditions
5. Exit Rules
6. Risk Management
7. Preview Summary
8. Validation

Use accordions or expansion panels for advanced sections.

---

## Suggested Angular Module Structure

```text
strategy-builder/
├─ components/
│  ├─ strategy-builder-page/
│  ├─ strategy-template-selector/
│  ├─ strategy-details-card/
│  ├─ trend-filter-card/
│  ├─ entry-conditions-card/
│  ├─ entry-condition-item/
│  ├─ exit-rules-card/
│  ├─ risk-management-card/
│  ├─ preview-summary-card/
│  ├─ validation-card/
│  └─ json-preview-card/
├─ models/
│  ├─ strategy-builder.models.ts
│  ├─ strategy-json.models.ts
│  └─ indicator.models.ts
├─ services/
│  ├─ strategy-template.service.ts
│  ├─ strategy-preview.service.ts
│  ├─ strategy-validation.service.ts
│  └─ strategy-mapper.service.ts
├─ utils/
│  └─ condition-factories.ts
└─ strategy-builder.module.ts
```

---

## Angular Material Dependencies

Recommended Angular Material modules:

- MatCardModule
- MatFormFieldModule
- MatInputModule
- MatSelectModule
- MatSlideToggleModule
- MatButtonModule
- MatIconModule
- MatDividerModule
- MatChipsModule
- MatTooltipModule
- MatExpansionModule
- MatButtonToggleModule
- MatCheckboxModule
- MatRadioModule
- MatTabsModule
- MatSnackBarModule

---

## Primary Reactive Form Model

```ts
this.form = this.fb.group({
  templateId: ['ema-pullback'],
  strategyName: ['EMA Pullback BTC 15m', [Validators.required, Validators.maxLength(100)]],
  exchange: ['Hyperliquid', Validators.required],
  market: ['BTC-USD', Validators.required],
  timeframe: ['15m', Validators.required],
  direction: ['long', Validators.required],
  enabled: [true],

  trendFilter: this.fb.group({
    enabled: [true],
    type: ['ema_cross', Validators.required],
    fastPeriod: [50, [Validators.required, Validators.min(1)]],
    slowPeriod: [200, [Validators.required, Validators.min(2)]],
    operator: ['gt', Validators.required],
    appliesTo: ['long']
  }),

  entryLogic: ['all', Validators.required],
  entryConditions: this.fb.array([]),

  exit: this.fb.group({
    takeProfit: this.fb.group({
      enabled: [true],
      type: ['fixed_percent', Validators.required],
      value: [3, [Validators.min(0.01)]]
    }),
    stopLoss: this.fb.group({
      enabled: [true],
      type: ['swing_low', Validators.required],
      value: [null],
      lookback: [5, [Validators.min(1)]]
    }),
    exitOnOppositeSignal: [false]
  }),

  risk: this.fb.group({
    positionSizeType: ['percent_wallet', Validators.required],
    positionSizeValue: [5, [Validators.required, Validators.min(0.01)]],
    leverage: [1, [Validators.required, Validators.min(1)]],
    maxOpenTrades: [1, [Validators.required, Validators.min(1)]],
    cooldownValue: [0, [Validators.min(0)]],
    cooldownUnit: ['candles'],
    allowSameCandleReentry: [false]
  }),

  metadata: this.fb.group({
    tags: [[]],
    notes: ['']
  })
});
```

---

## Full Field Tables

## 1. Template Selector Card

### Purpose
Allows users to quickly start from a known strategy pattern.

| Field | Control | Required | Allowed values | Default | Notes |
|---|---|---:|---|---|---|
| Template | mat-select / cards | No | blank, ema-pullback, rsi-reversal, macd-cross, breakout, bollinger-bounce | ema-pullback | Selecting a template pre-populates form fields |
| Replace Existing Values | mat-checkbox | No | true/false | true | Useful when editing an existing strategy |
| Show Advanced Fields | mat-slide-toggle | No | true/false | false | Expands optional controls |

## 2. Strategy Details Card

| Field | Control | Required | Allowed values | Default | Validation / Notes |
|---|---|---:|---|---|---|
| Strategy Name | text input | Yes | free text | EMA Pullback BTC 15m | max 100 chars |
| Exchange | dropdown | Yes | Hyperliquid, Binance, Bybit, Paper Trading | Hyperliquid | determines available markets and execution capabilities |
| Market | searchable dropdown | Yes | BTC-USD, ETH-USD, SOL-USD, etc. | BTC-USD | should be dynamically loaded per exchange |
| Timeframe | dropdown | Yes | 1m, 3m, 5m, 15m, 30m, 1h, 4h, 1d | 15m | strategy evaluation interval |
| Direction | button toggle / select | Yes | long, short, both | long | impacts trend filter and exits |
| Enabled | slide toggle | No | true/false | true | activation state |
| Notes | textarea | No | free text | empty | optional annotation |

## 3. Trend Filter Card

| Field | Control | Required | Allowed values | Default | Notes |
|---|---|---:|---|---|---|
| Enabled | slide toggle | No | true/false | true | disable to ignore trend filter |
| Indicator Type | dropdown | Yes when enabled | ema_cross, sma_cross, price_above_ema, macd_trend | ema_cross | first version can focus on ema_cross |
| Fast Period | number | Yes when enabled | integer > 0 | 50 | typically shorter MA |
| Slow Period | number | Yes when enabled | integer > 1 | 200 | typically longer MA |
| Operator | dropdown | Yes when enabled | gt, lt, cross_above, cross_below | gt | gt = fast > slow |
| Applies To | dropdown | Yes when enabled | long, short, both | long | useful if direction = both |

### Trend filter operator meanings

| Operator | Meaning |
|---|---|
| gt | Fast MA is above slow MA |
| lt | Fast MA is below slow MA |
| cross_above | Fast MA crosses above slow MA |
| cross_below | Fast MA crosses below slow MA |

## 4. Entry Conditions Card

This should use a `FormArray`.

Each entry condition is an item card with a common shell plus dynamic fields based on condition type.

### Common entry condition shell

| Field | Control | Required | Allowed values | Notes |
|---|---|---:|---|---|
| Condition Type | dropdown | Yes | rsi, price_vs_ema, macd, bollinger, atr, volume, candle_pattern, support_resistance | drives dynamic form fields |
| Enabled | checkbox or toggle | No | true/false | lets users disable without deleting |
| Label | text input | No | free text | optional user-friendly name |
| Remove | icon button | No | n/a | removes item |
| Duplicate | icon button | No | n/a | duplicates item |

### Supported condition types and fields

#### RSI

| Field | Control | Required | Allowed values | Default | Notes |
|---|---|---:|---|---|---|
| Period | number | Yes | integer > 0 | 14 | |
| Operator | dropdown | Yes | lt, lte, gt, gte, cross_above, cross_below | lt | |
| Value | number | Yes | 0 to 100 | 40 | |

#### Price vs EMA

| Field | Control | Required | Allowed values | Default | Notes |
|---|---|---:|---|---|---|
| EMA Period | number | Yes | integer > 0 | 50 | |
| Operator | dropdown | Yes | near, above, below, cross_above, cross_below, touch | near | |
| Distance Type | dropdown | No | percent, atr_multiple, absolute | percent | only relevant for `near` |
| Distance Value | number | No | > 0 | 0.25 | interpret based on selected type |

#### MACD

| Field | Control | Required | Allowed values | Default | Notes |
|---|---|---:|---|---|---|
| Fast Period | number | Yes | integer > 0 | 12 | |
| Slow Period | number | Yes | integer > 0 | 26 | |
| Signal Period | number | Yes | integer > 0 | 9 | |
| Operator | dropdown | Yes | bullish_cross, bearish_cross, histogram_gt, histogram_lt | bullish_cross | |
| Value | number | No | any number | 0 | used for histogram rules |

#### Bollinger Band

| Field | Control | Required | Allowed values | Default | Notes |
|---|---|---:|---|---|---|
| Period | number | Yes | integer > 0 | 20 | |
| StdDev | number | Yes | > 0 | 2 | |
| Operator | dropdown | Yes | touches_upper, touches_lower, closes_above_upper, closes_below_lower, inside_bands | touches_lower | |

#### ATR

| Field | Control | Required | Allowed values | Default | Notes |
|---|---|---:|---|---|---|
| Period | number | Yes | integer > 0 | 14 | |
| Operator | dropdown | Yes | gt, lt, between | gt | |
| Value | number | Yes | > 0 | 1 | could represent ATR threshold |
| Secondary Value | number | No | > 0 | empty | needed for `between` |

#### Volume

| Field | Control | Required | Allowed values | Default | Notes |
|---|---|---:|---|---|---|
| Lookback | number | Yes | integer > 0 | 20 | |
| Operator | dropdown | Yes | gt_avg_multiple, lt_avg_multiple | gt_avg_multiple | |
| Value | number | Yes | > 0 | 1.5 | 1.5 means 1.5x average volume |

#### Candle Pattern

| Field | Control | Required | Allowed values | Default | Notes |
|---|---|---:|---|---|---|
| Pattern | dropdown | Yes | bullish_engulfing, bearish_engulfing, pin_bar, inside_bar | bullish_engulfing | |
| Strength Filter | dropdown | No | none, small_wick, strong_body | none | optional |

#### Support / Resistance

| Field | Control | Required | Allowed values | Default | Notes |
|---|---|---:|---|---|---|
| Level Source | dropdown | Yes | recent_high, recent_low, pivot, manual | recent_low | |
| Lookback | number | No | integer > 0 | 20 | |
| Operator | dropdown | Yes | breakout_above, rejection_from, retest_of | retest_of | |

## 5. Entry Logic

| Field | Control | Required | Allowed values | Default | Notes |
|---|---|---:|---|---|---|
| Entry Logic | button toggle | Yes | all, any | all | whether all conditions must pass |

## 6. Exit Rules Card

### Take Profit

| Field | Control | Required | Allowed values | Default | Notes |
|---|---|---:|---|---|---|
| Enabled | slide toggle | No | true/false | true | |
| Type | dropdown | Yes when enabled | fixed_percent, risk_reward, previous_high_low, atr_multiple, indicator_signal | fixed_percent | |
| Value | number | Optional by type | > 0 | 3 | 3 = 3% for fixed_percent |
| Secondary Value | number | Optional by type | > 0 | empty | useful if type needs two values |

### Stop Loss

| Field | Control | Required | Allowed values | Default | Notes |
|---|---|---:|---|---|---|
| Enabled | slide toggle | No | true/false | true | |
| Type | dropdown | Yes when enabled | fixed_percent, swing_low, swing_high, atr_multiple, indicator_signal, trailing_stop | swing_low | |
| Value | number | Optional by type | > 0 | empty | |
| Lookback | number | Optional by type | integer > 0 | 5 | used for swing low/high |
| Trailing Trigger | number | Optional | > 0 | empty | used for trailing stop |
| Trailing Distance | number | Optional | > 0 | empty | used for trailing stop |

### Other exit controls

| Field | Control | Required | Allowed values | Default | Notes |
|---|---|---:|---|---|---|
| Exit on Opposite Signal | checkbox | No | true/false | false | optional advanced rule |
| Partial Take Profit | checkbox | No | true/false | false | future feature |
| Close On Time Limit | checkbox | No | true/false | false | future feature |

## 7. Risk Management Card

| Field | Control | Required | Allowed values | Default | Notes |
|---|---|---:|---|---|---|
| Position Size Type | dropdown | Yes | fixed_usd, percent_wallet, risk_based | percent_wallet | |
| Position Size Value | number | Yes | > 0 | 5 | interpreted by type |
| Leverage | number | Yes | >= 1 | 1 | exchange-specific validation may apply |
| Max Open Trades | number | Yes | integer >= 1 | 1 | |
| Cooldown Value | number | No | >= 0 | 0 | |
| Cooldown Unit | dropdown | No | minutes, candles | candles | |
| Allow Same Candle Re-entry | slide toggle | No | true/false | false | |
| Max Daily Loss % | number | No | > 0 | empty | future safety feature |
| Stop Trading After N Losses | number | No | integer > 0 | empty | optional |

## 8. Preview Summary Card

### Purpose

Generate a user-friendly description of the configured strategy.

### Example output

```text
Enter a long trade on BTC-USD on the 15m timeframe when the 50 EMA is above the 200 EMA, price is within 0.25% of the 50 EMA, and RSI(14) is below 40.

Exit at 3% take profit or at a swing low using a 5-candle lookback.

Risk settings: use 5% of wallet, 1x leverage, and allow only one open trade.
```

### Preview recommendations

- update live on form changes
- highlight missing sections in muted text
- optionally show both plain-English and structured summary
- optionally show strategy tags as chips

## 9. Validation Card

### Recommended validations

| Rule | Severity | Message |
|---|---|---|
| strategyName missing | error | Strategy name is required |
| no entry conditions | error | At least one entry condition is required |
| fastPeriod >= slowPeriod for ema_cross | warning | Fast EMA is usually lower than slow EMA |
| both TP and SL disabled | warning | Consider enabling at least one exit rule |
| leverage too high for selected exchange | warning/error | Leverage exceeds configured exchange limits |
| position size missing | error | Position size is required |
| direction = both but trend filter applies only to long | info | Consider defining short-side trend behaviour |

---

## JSON Contract

```json
{
  "templateId": "ema-pullback",
  "strategyName": "EMA Pullback BTC 15m",
  "exchange": "Hyperliquid",
  "market": "BTC-USD",
  "timeframe": "15m",
  "direction": "long",
  "enabled": true,
  "trendFilter": {
    "enabled": true,
    "type": "ema_cross",
    "fastPeriod": 50,
    "slowPeriod": 200,
    "operator": "gt",
    "appliesTo": "long"
  },
  "entryLogic": "all",
  "entryConditions": [
    {
      "id": "cond-1",
      "enabled": true,
      "type": "price_vs_ema",
      "label": "Price Near EMA 50",
      "params": {
        "emaPeriod": 50,
        "operator": "near",
        "distanceType": "percent",
        "distanceValue": 0.25
      }
    },
    {
      "id": "cond-2",
      "enabled": true,
      "type": "rsi",
      "label": "RSI Pullback",
      "params": {
        "period": 14,
        "operator": "lt",
        "value": 40
      }
    }
  ],
  "exit": {
    "takeProfit": {
      "enabled": true,
      "type": "fixed_percent",
      "value": 3
    },
    "stopLoss": {
      "enabled": true,
      "type": "swing_low",
      "lookback": 5
    },
    "exitOnOppositeSignal": false
  },
  "risk": {
    "positionSizeType": "percent_wallet",
    "positionSizeValue": 5,
    "leverage": 1,
    "maxOpenTrades": 1,
    "cooldownValue": 0,
    "cooldownUnit": "candles",
    "allowSameCandleReentry": false
  },
  "metadata": {
    "tags": ["trend", "pullback", "ema", "rsi"],
    "notes": ""
  }
}
```

---

## TypeScript Interfaces

```ts
export interface StrategyConfig {
  templateId?: string;
  strategyName: string;
  exchange: string;
  market: string;
  timeframe: string;
  direction: 'long' | 'short' | 'both';
  enabled: boolean;
  trendFilter?: TrendFilterConfig;
  entryLogic: 'all' | 'any';
  entryConditions: EntryConditionConfig[];
  exit: ExitConfig;
  risk: RiskConfig;
  metadata?: StrategyMetadata;
}

export interface TrendFilterConfig {
  enabled: boolean;
  type: 'ema_cross' | 'sma_cross' | 'price_above_ema' | 'macd_trend';
  fastPeriod?: number;
  slowPeriod?: number;
  operator: 'gt' | 'lt' | 'cross_above' | 'cross_below';
  appliesTo: 'long' | 'short' | 'both';
}

export interface EntryConditionConfig {
  id: string;
  enabled: boolean;
  type:
    | 'rsi'
    | 'price_vs_ema'
    | 'macd'
    | 'bollinger'
    | 'atr'
    | 'volume'
    | 'candle_pattern'
    | 'support_resistance';
  label?: string;
  params: Record<string, unknown>;
}

export interface ExitConfig {
  takeProfit: ExitRuleConfig;
  stopLoss: ExitRuleConfig;
  exitOnOppositeSignal?: boolean;
}

export interface ExitRuleConfig {
  enabled: boolean;
  type: string;
  value?: number | null;
  lookback?: number | null;
  trailingTrigger?: number | null;
  trailingDistance?: number | null;
}

export interface RiskConfig {
  positionSizeType: 'fixed_usd' | 'percent_wallet' | 'risk_based';
  positionSizeValue: number;
  leverage: number;
  maxOpenTrades: number;
  cooldownValue?: number;
  cooldownUnit?: 'minutes' | 'candles';
  allowSameCandleReentry?: boolean;
  maxDailyLossPercent?: number;
  stopTradingAfterLosses?: number;
}

export interface StrategyMetadata {
  tags?: string[];
  notes?: string;
}
```

---

## JSON Schema Draft

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "StrategyConfig",
  "type": "object",
  "required": [
    "strategyName",
    "exchange",
    "market",
    "timeframe",
    "direction",
    "entryLogic",
    "entryConditions",
    "exit",
    "risk"
  ],
  "properties": {
    "strategyName": {
      "type": "string",
      "minLength": 1,
      "maxLength": 100
    },
    "exchange": {
      "type": "string"
    },
    "market": {
      "type": "string"
    },
    "timeframe": {
      "type": "string"
    },
    "direction": {
      "type": "string",
      "enum": ["long", "short", "both"]
    },
    "entryLogic": {
      "type": "string",
      "enum": ["all", "any"]
    },
    "entryConditions": {
      "type": "array",
      "minItems": 1,
      "items": {
        "type": "object",
        "required": ["id", "enabled", "type", "params"],
        "properties": {
          "id": { "type": "string" },
          "enabled": { "type": "boolean" },
          "type": { "type": "string" },
          "label": { "type": "string" },
          "params": { "type": "object" }
        }
      }
    }
  }
}
```

---

## FormArray Condition Factory

```ts
createEntryCondition(type: string): FormGroup {
  switch (type) {
    case 'rsi':
      return this.fb.group({
        id: [crypto.randomUUID()],
        enabled: [true],
        type: ['rsi'],
        label: ['RSI Pullback'],
        params: this.fb.group({
          period: [14, [Validators.required, Validators.min(1)]],
          operator: ['lt', Validators.required],
          value: [40, [Validators.required, Validators.min(0), Validators.max(100)]]
        })
      });

    case 'price_vs_ema':
      return this.fb.group({
        id: [crypto.randomUUID()],
        enabled: [true],
        type: ['price_vs_ema'],
        label: ['Price Near EMA 50'],
        params: this.fb.group({
          emaPeriod: [50, [Validators.required, Validators.min(1)]],
          operator: ['near', Validators.required],
          distanceType: ['percent'],
          distanceValue: [0.25, [Validators.min(0.0001)]]
        })
      });

    case 'macd':
      return this.fb.group({
        id: [crypto.randomUUID()],
        enabled: [true],
        type: ['macd'],
        label: ['MACD Bullish Cross'],
        params: this.fb.group({
          fastPeriod: [12, [Validators.required, Validators.min(1)]],
          slowPeriod: [26, [Validators.required, Validators.min(1)]],
          signalPeriod: [9, [Validators.required, Validators.min(1)]],
          operator: ['bullish_cross', Validators.required],
          value: [0]
        })
      });

    default:
      return this.fb.group({
        id: [crypto.randomUUID()],
        enabled: [true],
        type: [type],
        label: [''],
        params: this.fb.group({})
      });
  }
}
```

---

## Angular Material HTML Scaffolding

## Parent page scaffold

```html
<div class="strategy-builder-page">
  <div class="page-header">
    <div>
      <h1>Create Strategy</h1>
      <p>Configure entry, exit, and risk rules for this strategy.</p>
    </div>

    <div class="page-actions">
      <button mat-stroked-button type="button">Cancel</button>
      <button mat-stroked-button color="primary" type="button">Save Draft</button>
      <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid">
        Save Strategy
      </button>
    </div>
  </div>

  <form [formGroup]="form" class="strategy-builder-grid">
    <div class="left-column">
      <app-strategy-template-selector [form]="form"></app-strategy-template-selector>
      <app-strategy-details-card [form]="form"></app-strategy-details-card>
      <app-trend-filter-card [group]="form.get('trendFilter')"></app-trend-filter-card>
      <app-entry-conditions-card
        [conditions]="entryConditions"
        [entryLogicControl]="form.get('entryLogic')">
      </app-entry-conditions-card>
      <app-exit-rules-card [group]="form.get('exit')"></app-exit-rules-card>
      <app-risk-management-card [group]="form.get('risk')"></app-risk-management-card>
    </div>

    <div class="right-column">
      <app-preview-summary-card [formValue]="form.getRawValue()"></app-preview-summary-card>
      <app-validation-card [messages]="validationMessages"></app-validation-card>
      <app-json-preview-card *ngIf="developerMode" [json]="mappedStrategyJson"></app-json-preview-card>
    </div>
  </form>
</div>
```

## Strategy Details card scaffold

```html
<mat-card class="builder-card" [formGroup]="form">
  <mat-card-header>
    <mat-card-title>Strategy Details</mat-card-title>
  </mat-card-header>

  <mat-card-content class="card-grid">
    <mat-form-field appearance="outline">
      <mat-label>Strategy Name</mat-label>
      <input matInput formControlName="strategyName" maxlength="100" />
    </mat-form-field>

    <mat-form-field appearance="outline">
      <mat-label>Exchange</mat-label>
      <mat-select formControlName="exchange">
        <mat-option value="Hyperliquid">Hyperliquid</mat-option>
        <mat-option value="Binance">Binance</mat-option>
        <mat-option value="Bybit">Bybit</mat-option>
        <mat-option value="Paper Trading">Paper Trading</mat-option>
      </mat-select>
    </mat-form-field>

    <mat-form-field appearance="outline">
      <mat-label>Market</mat-label>
      <mat-select formControlName="market">
        <mat-option value="BTC-USD">BTC-USD</mat-option>
        <mat-option value="ETH-USD">ETH-USD</mat-option>
        <mat-option value="SOL-USD">SOL-USD</mat-option>
      </mat-select>
    </mat-form-field>

    <mat-form-field appearance="outline">
      <mat-label>Timeframe</mat-label>
      <mat-select formControlName="timeframe">
        <mat-option value="1m">1m</mat-option>
        <mat-option value="5m">5m</mat-option>
        <mat-option value="15m">15m</mat-option>
        <mat-option value="1h">1h</mat-option>
        <mat-option value="4h">4h</mat-option>
        <mat-option value="1d">1d</mat-option>
      </mat-select>
    </mat-form-field>

    <mat-form-field appearance="outline">
      <mat-label>Direction</mat-label>
      <mat-select formControlName="direction">
        <mat-option value="long">Long</mat-option>
        <mat-option value="short">Short</mat-option>
        <mat-option value="both">Both</mat-option>
      </mat-select>
    </mat-form-field>

    <mat-slide-toggle formControlName="enabled">
      Enabled
    </mat-slide-toggle>
  </mat-card-content>
</mat-card>
```

## Trend Filter card scaffold

```html
<mat-card class="builder-card" [formGroup]="group">
  <mat-card-header>
    <mat-card-title>Trend Filter</mat-card-title>
  </mat-card-header>

  <mat-card-content class="card-grid">
    <mat-slide-toggle formControlName="enabled">Use Trend Filter</mat-slide-toggle>

    <mat-form-field appearance="outline">
      <mat-label>Indicator</mat-label>
      <mat-select formControlName="type">
        <mat-option value="ema_cross">EMA Cross</mat-option>
        <mat-option value="sma_cross">SMA Cross</mat-option>
        <mat-option value="price_above_ema">Price Above EMA</mat-option>
        <mat-option value="macd_trend">MACD Trend</mat-option>
      </mat-select>
    </mat-form-field>

    <mat-form-field appearance="outline">
      <mat-label>Fast Period</mat-label>
      <input matInput type="number" formControlName="fastPeriod" />
    </mat-form-field>

    <mat-form-field appearance="outline">
      <mat-label>Slow Period</mat-label>
      <input matInput type="number" formControlName="slowPeriod" />
    </mat-form-field>

    <mat-form-field appearance="outline">
      <mat-label>Operator</mat-label>
      <mat-select formControlName="operator">
        <mat-option value="gt">Fast > Slow</mat-option>
        <mat-option value="lt">Fast < Slow</mat-option>
        <mat-option value="cross_above">Cross Above</mat-option>
        <mat-option value="cross_below">Cross Below</mat-option>
      </mat-select>
    </mat-form-field>

    <mat-form-field appearance="outline">
      <mat-label>Applies To</mat-label>
      <mat-select formControlName="appliesTo">
        <mat-option value="long">Long</mat-option>
        <mat-option value="short">Short</mat-option>
        <mat-option value="both">Both</mat-option>
      </mat-select>
    </mat-form-field>
  </mat-card-content>
</mat-card>
```

## Entry Conditions card scaffold

```html
<mat-card class="builder-card">
  <mat-card-header>
    <mat-card-title>Entry Conditions</mat-card-title>
    <button mat-icon-button type="button" (click)="addCondition('rsi')">
      <mat-icon>add</mat-icon>
    </button>
  </mat-card-header>

  <mat-card-content>
    <div class="condition-toolbar">
      <mat-button-toggle-group [formControl]="entryLogicControl">
        <mat-button-toggle value="all">ALL</mat-button-toggle>
        <mat-button-toggle value="any">ANY</mat-button-toggle>
      </mat-button-toggle-group>

      <button mat-stroked-button type="button" (click)="addCondition('rsi')">Add RSI</button>
      <button mat-stroked-button type="button" (click)="addCondition('price_vs_ema')">Add Price vs EMA</button>
      <button mat-stroked-button type="button" (click)="addCondition('macd')">Add MACD</button>
    </div>

    <div class="condition-list">
      <app-entry-condition-item
        *ngFor="let condition of conditions.controls; let i = index"
        [group]="condition"
        [index]="i"
        (remove)="removeCondition(i)"
        (duplicate)="duplicateCondition(i)">
      </app-entry-condition-item>
    </div>
  </mat-card-content>
</mat-card>
```

## Entry Condition item scaffold

```html
<mat-card class="condition-card" [formGroup]="group">
  <mat-card-content>
    <div class="condition-header">
      <mat-form-field appearance="outline">
        <mat-label>Condition Type</mat-label>
        <mat-select formControlName="type">
          <mat-option value="rsi">RSI</mat-option>
          <mat-option value="price_vs_ema">Price vs EMA</mat-option>
          <mat-option value="macd">MACD</mat-option>
          <mat-option value="bollinger">Bollinger</mat-option>
          <mat-option value="atr">ATR</mat-option>
          <mat-option value="volume">Volume</mat-option>
        </mat-select>
      </mat-form-field>

      <mat-checkbox formControlName="enabled">Enabled</mat-checkbox>

      <button mat-icon-button type="button" (click)="duplicate.emit()">
        <mat-icon>content_copy</mat-icon>
      </button>

      <button mat-icon-button type="button" (click)="remove.emit()">
        <mat-icon>delete</mat-icon>
      </button>
    </div>

    <div class="card-grid" formGroupName="params">
      <ng-container [ngSwitch]="group.get('type')?.value">

        <ng-container *ngSwitchCase="'rsi'">
          <mat-form-field appearance="outline">
            <mat-label>Period</mat-label>
            <input matInput type="number" formControlName="period" />
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Operator</mat-label>
            <mat-select formControlName="operator">
              <mat-option value="lt"><</mat-option>
              <mat-option value="lte"><=</mat-option>
              <mat-option value="gt">></mat-option>
              <mat-option value="gte">>=</mat-option>
              <mat-option value="cross_above">Cross Above</mat-option>
              <mat-option value="cross_below">Cross Below</mat-option>
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Value</mat-label>
            <input matInput type="number" formControlName="value" />
          </mat-form-field>
        </ng-container>

        <ng-container *ngSwitchCase="'price_vs_ema'">
          <mat-form-field appearance="outline">
            <mat-label>EMA Period</mat-label>
            <input matInput type="number" formControlName="emaPeriod" />
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Operator</mat-label>
            <mat-select formControlName="operator">
              <mat-option value="near">Near</mat-option>
              <mat-option value="above">Above</mat-option>
              <mat-option value="below">Below</mat-option>
              <mat-option value="cross_above">Cross Above</mat-option>
              <mat-option value="cross_below">Cross Below</mat-option>
              <mat-option value="touch">Touch</mat-option>
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Distance Type</mat-label>
            <mat-select formControlName="distanceType">
              <mat-option value="percent">Percent</mat-option>
              <mat-option value="atr_multiple">ATR Multiple</mat-option>
              <mat-option value="absolute">Absolute</mat-option>
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Distance Value</mat-label>
            <input matInput type="number" formControlName="distanceValue" />
          </mat-form-field>
        </ng-container>

        <ng-container *ngSwitchCase="'macd'">
          <mat-form-field appearance="outline">
            <mat-label>Fast Period</mat-label>
            <input matInput type="number" formControlName="fastPeriod" />
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Slow Period</mat-label>
            <input matInput type="number" formControlName="slowPeriod" />
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Signal Period</mat-label>
            <input matInput type="number" formControlName="signalPeriod" />
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Operator</mat-label>
            <mat-select formControlName="operator">
              <mat-option value="bullish_cross">Bullish Cross</mat-option>
              <mat-option value="bearish_cross">Bearish Cross</mat-option>
              <mat-option value="histogram_gt">Histogram ></mat-option>
              <mat-option value="histogram_lt">Histogram <</mat-option>
            </mat-select>
          </mat-form-field>
        </ng-container>

      </ng-container>
    </div>
  </mat-card-content>
</mat-card>
```

## Exit Rules card scaffold

```html
<mat-card class="builder-card" [formGroup]="group">
  <mat-card-header>
    <mat-card-title>Exit Rules</mat-card-title>
  </mat-card-header>

  <mat-card-content>
    <div class="section-subcard" formGroupName="takeProfit">
      <h3>Take Profit</h3>

      <mat-slide-toggle formControlName="enabled">Enable TP</mat-slide-toggle>

      <div class="card-grid">
        <mat-form-field appearance="outline">
          <mat-label>TP Type</mat-label>
          <mat-select formControlName="type">
            <mat-option value="fixed_percent">Fixed Percent</mat-option>
            <mat-option value="risk_reward">Risk Reward</mat-option>
            <mat-option value="previous_high_low">Previous High / Low</mat-option>
            <mat-option value="atr_multiple">ATR Multiple</mat-option>
            <mat-option value="indicator_signal">Indicator Signal</mat-option>
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>TP Value</mat-label>
          <input matInput type="number" formControlName="value" />
        </mat-form-field>
      </div>
    </div>

    <mat-divider></mat-divider>

    <div class="section-subcard" formGroupName="stopLoss">
      <h3>Stop Loss</h3>

      <mat-slide-toggle formControlName="enabled">Enable SL</mat-slide-toggle>

      <div class="card-grid">
        <mat-form-field appearance="outline">
          <mat-label>SL Type</mat-label>
          <mat-select formControlName="type">
            <mat-option value="fixed_percent">Fixed Percent</mat-option>
            <mat-option value="swing_low">Swing Low</mat-option>
            <mat-option value="swing_high">Swing High</mat-option>
            <mat-option value="atr_multiple">ATR Multiple</mat-option>
            <mat-option value="trailing_stop">Trailing Stop</mat-option>
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>SL Value</mat-label>
          <input matInput type="number" formControlName="value" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Lookback</mat-label>
          <input matInput type="number" formControlName="lookback" />
        </mat-form-field>
      </div>
    </div>

    <div class="extra-options">
      <mat-checkbox formControlName="exitOnOppositeSignal">
        Exit on opposite signal
      </mat-checkbox>
    </div>
  </mat-card-content>
</mat-card>
```

## Risk Management card scaffold

```html
<mat-card class="builder-card" [formGroup]="group">
  <mat-card-header>
    <mat-card-title>Risk Management</mat-card-title>
  </mat-card-header>

  <mat-card-content class="card-grid">
    <mat-form-field appearance="outline">
      <mat-label>Position Size Type</mat-label>
      <mat-select formControlName="positionSizeType">
        <mat-option value="fixed_usd">Fixed USD</mat-option>
        <mat-option value="percent_wallet">% of Wallet</mat-option>
        <mat-option value="risk_based">Risk Based</mat-option>
      </mat-select>
    </mat-form-field>

    <mat-form-field appearance="outline">
      <mat-label>Position Size Value</mat-label>
      <input matInput type="number" formControlName="positionSizeValue" />
    </mat-form-field>

    <mat-form-field appearance="outline">
      <mat-label>Leverage</mat-label>
      <input matInput type="number" formControlName="leverage" />
    </mat-form-field>

    <mat-form-field appearance="outline">
      <mat-label>Max Open Trades</mat-label>
      <input matInput type="number" formControlName="maxOpenTrades" />
    </mat-form-field>

    <mat-form-field appearance="outline">
      <mat-label>Cooldown Value</mat-label>
      <input matInput type="number" formControlName="cooldownValue" />
    </mat-form-field>

    <mat-form-field appearance="outline">
      <mat-label>Cooldown Unit</mat-label>
      <mat-select formControlName="cooldownUnit">
        <mat-option value="minutes">Minutes</mat-option>
        <mat-option value="candles">Candles</mat-option>
      </mat-select>
    </mat-form-field>

    <mat-slide-toggle formControlName="allowSameCandleReentry">
      Allow same candle re-entry
    </mat-slide-toggle>
  </mat-card-content>
</mat-card>
```

## Preview Summary card scaffold

```html
<mat-card class="builder-card preview-card">
  <mat-card-header>
    <mat-card-title>Preview</mat-card-title>
  </mat-card-header>

  <mat-card-content>
    <p>{{ summaryText }}</p>

    <mat-chip-set *ngIf="tags?.length">
      <mat-chip *ngFor="let tag of tags">{{ tag }}</mat-chip>
    </mat-chip-set>
  </mat-card-content>
</mat-card>
```

## Validation card scaffold

```html
<mat-card class="builder-card validation-card">
  <mat-card-header>
    <mat-card-title>Validation</mat-card-title>
  </mat-card-header>

  <mat-card-content>
    <div *ngIf="!messages?.length" class="empty-state">
      No validation issues.
    </div>

    <div *ngFor="let message of messages" class="validation-item" [class]="message.severity">
      <strong>{{ message.severity | uppercase }}</strong>
      <span>{{ message.text }}</span>
    </div>
  </mat-card-content>
</mat-card>
```

---

## Suggested SCSS

```scss
.strategy-builder-page {
  padding: 24px;
}

.page-header {
  display: flex;
  justify-content: space-between;
  gap: 16px;
  align-items: flex-start;
  margin-bottom: 24px;
}

.strategy-builder-grid {
  display: grid;
  grid-template-columns: minmax(0, 2fr) minmax(320px, 1fr);
  gap: 24px;
}

.left-column,
.right-column {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.builder-card {
  width: 100%;
}

.card-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 16px;
}

.condition-card {
  margin-bottom: 12px;
}

.condition-header {
  display: grid;
  grid-template-columns: 2fr auto auto auto;
  gap: 12px;
  align-items: center;
  margin-bottom: 12px;
}

.condition-toolbar {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  margin-bottom: 16px;
}

.section-subcard {
  padding: 8px 0 16px;
}

.validation-item {
  display: flex;
  gap: 8px;
  padding: 8px 0;
}

.validation-item.error {
  color: #b3261e;
}

.validation-item.warning {
  color: #b26a00;
}

.validation-item.info {
  color: #245ea6;
}

@media (max-width: 960px) {
  .strategy-builder-grid {
    grid-template-columns: 1fr;
  }

  .card-grid {
    grid-template-columns: 1fr;
  }

  .page-header {
    flex-direction: column;
  }

  .condition-header {
    grid-template-columns: 1fr;
  }
}
```

---

## Suggested Component Responsibilities

### StrategyTemplateService
- returns template definitions
- applies template values onto form
- optionally preserves user-modified fields

### StrategyPreviewService
- maps raw form values to natural-language summary
- generates tags such as `trend`, `ema`, `momentum`, `pullback`

### StrategyValidationService
- runs synchronous validation rules
- returns structured messages:
  - severity
  - field path
  - text

### StrategyMapperService
- converts raw form to engine-ready JSON contract
- strips disabled optional sections if required
- normalizes numeric and enum values

---

## Example Validation Message Interface

```ts
export interface ValidationMessage {
  severity: 'error' | 'warning' | 'info';
  fieldPath?: string;
  text: string;
}
```

---

## Implementation Notes

1. Use reactive forms throughout.
2. Keep the UI JSON contract separate from runtime execution models.
3. Prefer enum-like string constants rather than free text.
4. Use a factory for each entry condition type.
5. Keep preview and validation in dedicated services to avoid bloating components.
6. Support template preload on page init.
7. Keep dynamic fields hidden when not relevant to selected condition type.
8. Consider adding a developer-mode JSON preview toggle.
9. Support import/export later using the same JSON contract.
10. Reserve a future adapter for natural language → form population.

---

## Suggested Delivery Phases

### Phase 1
- Strategy Details
- Trend Filter
- RSI / Price vs EMA / MACD conditions
- Fixed TP
- Swing-low SL
- Basic risk settings
- Preview
- Validation

### Phase 2
- More condition types
- Template library
- JSON preview
- import/export

### Phase 3
- Natural language input
- AST mapping
- backtest preview panel
- strategy execution status integration

---

## Summary

This UI setup gives you:

- a scalable strategy builder
- strong alignment with Angular reactive forms
- a clear engine-facing JSON schema
- reusable Angular Material components
- room to expand into natural language and AST pipelines later

It is a solid middle ground between:
- beginner-friendly templates
- power-user configurability
- backend-safe structured output
