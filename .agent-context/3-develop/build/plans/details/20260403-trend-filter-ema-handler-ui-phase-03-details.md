<!-- markdownlint-disable-file -->

# Task Details: F7 — Trend Filter + EMA Condition Handler + UI

## Phase 3: Frontend Trend Filter Card & Models

## Standards and Knowledge References

- **angular.instructions.md**: standalone components, inject() DI, explicit accessibility, double quotes, SCSS, @if/@for control flow, kebab-case class names
- **07-ui-design.md**: strategy builder card components, trend filter card
- **11-angular-instructions.md**: Angular 19 standalone, Angular Material dark theme

## Design References

- F7 PBI: Trend filter type dropdown (ema_cross, sma_cross, price_above_ema), dynamic fields per type, Applies To dropdown
- `exit-rules-card.component.ts` — card with `@Input() group: FormGroup`, `_syncDisabledState`, `hasError()`
- `rsi-condition-item.component.ts` — condition item with FormGroup input, operator dropdown

### Task 3.1: Add TypeScript trend filter types and interfaces {#task-31-add-typescript-trend-filter-types-and-interfaces}

Add trend filter TypeScript types and interfaces to `strategy.model.ts`. Add `PriceVsEmaOperator`, `PriceVsEmaDistanceType`, `PriceVsEmaParams` interface.

- **Complexity**: Low
- **Risk Factors**: Must match backend JSON serialization casing
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts` — add types
- **Success**:
  - `TrendFilterType`, `TrendOperator`, `TrendFilterConfig` interfaces added
  - `PriceVsEmaOperator`, `PriceVsEmaDistanceType` types added
  - `PriceVsEmaParams` interface added with `period`, `operator`, `distanceType`, `distanceValue`
  - `StrategyConfig.trendFilter` typed as `TrendFilterConfig | null`
  - `EntryConditionConfig.params` typed as union `RsiParams | PriceVsEmaParams`
  - `ng build` succeeds
- **Dependencies**:
  - F6 delivered (assumes strategyMode form control exists)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts — additions

export type TrendFilterType = "ema_cross" | "sma_cross" | "price_above_ema";
export type TrendOperator = "gt" | "lt" | "gte" | "lte" | "cross_above" | "cross_below" | "above" | "below";
export type PriceVsEmaOperator = "near" | "above" | "below" | "cross_above" | "cross_below" | "touch";
export type PriceVsEmaDistanceType = "percent" | "atr_multiple" | "absolute";

export interface TrendFilterConfig {
  enabled: boolean;
  type: TrendFilterType;
  period?: number | null;
  fastPeriod: number;
  slowPeriod: number;
  operator: TrendOperator;
  appliesTo: Direction;
}

export interface PriceVsEmaParams {
  period: number;
  operator: PriceVsEmaOperator;
  distanceType: PriceVsEmaDistanceType;
  distanceValue: number | null;
}

// Update StrategyConfig:
//   trendFilter?: TrendFilterConfig | null;  (was trendFilter?: null)
//   entryConditions?: EntryConditionConfig[] | null;

// Update EntryConditionConfig:
//   params: RsiParams | PriceVsEmaParams;  (was params: RsiParams)
```

Check if `TrendFilterType` JSON values need to be camelCase (`EmaCross`) or snake_case (`ema_cross`). The backend uses `System.Text.Json` with `JsonStringEnumConverter` — check the `Program.cs` JSON options. If global `JsonStringEnumConverter` is configured with `JsonNamingPolicy.CamelCase`, the backend sends `"emaCross"`. If `JsonNamingPolicy.SnakeCaseLower`, it sends `"ema_cross"`. If no naming policy, it sends `"EmaCross"`. The frontend type must match whichever casing the backend produces. Verify and adjust accordingly.

##### Pattern References

- Existing `strategy.model.ts` — type patterns, interface conventions

---

### Task 3.2: Implement trend-filter-card component {#task-32-implement-trend-filter-card-component}

Replace the disabled stub with a full form card. Accepts `@Input() group: FormGroup` (the trendFilter FormGroup from the parent). Shows:
- Enabled checkbox
- Type dropdown (ema_cross, sma_cross, price_above_ema)
- Dynamic fields based on type:
  - ema_cross/sma_cross: Fast Period, Slow Period, Operator, Applies To
  - price_above_ema: Period, Operator, Applies To (hide Fast/Slow)
- Operator dropdown filtered by type
- Applies To dropdown (long, short, both)

- **Complexity**: Medium
- **Risk Factors**: Dynamic field visibility based on type selection; must handle form state (enable/disable) reactively
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/components/trend-filter-card/trend-filter-card.component.ts` — replace stub
  - `frontend/trading-ui/src/app/features/strategy-builder/components/trend-filter-card/trend-filter-card.component.html` — replace stub
  - `frontend/trading-ui/src/app/features/strategy-builder/components/trend-filter-card/trend-filter-card.component.scss` — update styles
- **Success**:
  - Card renders with all fields for ema_cross type
  - Switching to price_above_ema hides Fast/Slow and shows Period
  - Enabled checkbox toggles field disabled state
  - Applies To dropdown has long/short/both options
  - Form values bind correctly to parent FormGroup
  - `ng build` succeeds

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/components/trend-filter-card/trend-filter-card.component.ts — replacement
import { Component, DestroyRef, Input, OnInit, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormGroup, ReactiveFormsModule } from "@angular/forms";
import { MatCardModule } from "@angular/material/card";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import { TrendFilterType } from "../../models/strategy.model";
import { TREND_FILTER_OPERATORS, TrendFilterOperatorOption } from "../../enums/trend-filter-operator.enum";

@Component({
  selector: "app-trend-filter-card",
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  templateUrl: "./trend-filter-card.component.html",
  styleUrl: "./trend-filter-card.component.scss"
})
export class TrendFilterCardComponent implements OnInit {
  private readonly _destroyRef = inject(DestroyRef);

  @Input() public group: FormGroup | null = null;

  public get isBound(): boolean {
    return this.group !== null;
  }

  public get selectedType(): TrendFilterType | null {
    return (this.group?.get("type")?.value as TrendFilterType) ?? null;
  }

  public get showPeriodField(): boolean {
    return this.selectedType === "price_above_ema";
  }

  public get showFastSlowFields(): boolean {
    return this.selectedType === "ema_cross" || this.selectedType === "sma_cross";
  }

  public get availableOperators(): TrendFilterOperatorOption[] {
    return TREND_FILTER_OPERATORS.filter(op => {
      if (this.selectedType === "price_above_ema") {
        return ["above", "below", "cross_above", "cross_below"].includes(op.value);
      }
      return ["gt", "lt", "cross_above", "cross_below"].includes(op.value);
    });
  }

  public ngOnInit(): void {
    this._syncEnabledState();
    this._syncTypeChanges();
  }

  public hasError(path: string, errorCode: string): boolean {
    const control = this.group?.get(path);
    return Boolean(control?.hasError(errorCode) && (control.touched || control.dirty));
  }

  private _syncEnabledState(): void {
    if (this.group === null) { return; }

    const enabledControl = this.group.get("enabled");
    if (enabledControl === null) { return; }

    enabledControl.valueChanges
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((enabled: boolean) => {
        const controls = ["type", "fastPeriod", "slowPeriod", "period", "operator", "appliesTo"];
        for (const name of controls) {
          const ctrl = this.group!.get(name);
          if (ctrl === null) { continue; }
          enabled ? ctrl.enable() : ctrl.disable();
        }
      });
  }

  private _syncTypeChanges(): void {
    if (this.group === null) { return; }

    const typeControl = this.group.get("type");
    if (typeControl === null) { return; }

    typeControl.valueChanges
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe(() => {
        // Component re-renders based on getter values
        // Reset operator if it's no longer valid for the new type
      });
  }
}
```

```html
<!-- trend-filter-card.component.html — replacement -->
<mat-card class="trend-filter-card">
  <mat-card-header>
    <mat-card-title>Trend Filter</mat-card-title>
  </mat-card-header>

  <mat-card-content>
    @if (!isBound) {
      <p class="trend-filter-card__message">Available in signal mode</p>
    } @else {
      <div class="trend-filter-card__form" [formGroup]="group!">
        <mat-checkbox formControlName="enabled" color="primary">Enable trend filter</mat-checkbox>

        <div class="trend-filter-card__fields">
          <mat-form-field appearance="outline">
            <mat-label>Filter type</mat-label>
            <mat-select formControlName="type">
              <mat-option value="ema_cross">EMA Cross</mat-option>
              <mat-option value="sma_cross">SMA Cross</mat-option>
              <mat-option value="price_above_ema">Price vs EMA</mat-option>
            </mat-select>
          </mat-form-field>

          @if (showFastSlowFields) {
            <mat-form-field appearance="outline">
              <mat-label>Fast period</mat-label>
              <input matInput type="number" formControlName="fastPeriod" min="1" step="1" />
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Slow period</mat-label>
              <input matInput type="number" formControlName="slowPeriod" min="1" step="1" />
            </mat-form-field>
          }

          @if (showPeriodField) {
            <mat-form-field appearance="outline">
              <mat-label>Period</mat-label>
              <input matInput type="number" formControlName="period" min="1" step="1" />
            </mat-form-field>
          }

          <mat-form-field appearance="outline">
            <mat-label>Operator</mat-label>
            <mat-select formControlName="operator">
              @for (op of availableOperators; track op.value) {
                <mat-option [value]="op.value">{{ op.label }}</mat-option>
              }
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Applies to</mat-label>
            <mat-select formControlName="appliesTo">
              <mat-option value="long">Long</mat-option>
              <mat-option value="short">Short</mat-option>
              <mat-option value="both">Both</mat-option>
            </mat-select>
          </mat-form-field>
        </div>
      </div>
    }
  </mat-card-content>
</mat-card>
```

##### Pattern References

- `exit-rules-card.component.ts` — `@Input() group: FormGroup`, `_syncDisabledState`, `hasError()`
- `exit-rules-card.component.html` — `[formGroup]`, `@if`, `mat-checkbox`, `mat-form-field`

---

### Task 3.3: Create trend filter operator enum and display names {#task-33-create-trend-filter-operator-enum-and-display-names}

Create a display-name mapping for trend filter operators (following the `RSI_OPERATORS` pattern).

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/enums/trend-filter-operator.enum.ts` — new file
- **Success**:
  - `TrendFilterOperatorOption` interface with `value` and `label`
  - `TREND_FILTER_OPERATORS` array with display names
  - Used by `trend-filter-card.component.ts`

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/enums/trend-filter-operator.enum.ts — new file
import { TrendOperator } from "../models/strategy.model";

export interface TrendFilterOperatorOption {
  value: TrendOperator;
  label: string;
}

export const TREND_FILTER_OPERATORS: TrendFilterOperatorOption[] = [
  { value: "gt", label: "Greater than" },
  { value: "lt", label: "Less than" },
  { value: "gte", label: "Greater or equal" },
  { value: "lte", label: "Less or equal" },
  { value: "cross_above", label: "Cross above" },
  { value: "cross_below", label: "Cross below" },
  { value: "above", label: "Above" },
  { value: "below", label: "Below" },
];
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/enums/rsi-operator.enum.ts` — `RSI_OPERATORS` array pattern

---

### Task 3.4: Enable swing_low in exit rules card {#task-34-enable-swing-low-in-exit-rules-card}

Remove the `disabled` attribute from the `swing_low` mat-option in the stop loss section. Add conditional field visibility for lookback when type=swing_low. Also enable the `swing_low` type in the stop loss type dropdown and show/hide the lookback field.

- **Complexity**: Medium
- **Risk Factors**: Must conditionally show lookback field and hide value field when swing_low selected
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/components/exit-rules-card/exit-rules-card.component.html` — remove disabled, add lookback field
  - `frontend/trading-ui/src/app/features/strategy-builder/components/exit-rules-card/exit-rules-card.component.ts` — add type change listener for stop loss
  - `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` — add `lookback` control to stopLoss FormGroup
- **Success**:
  - `swing_low` option is selectable in stop loss type dropdown
  - When `swing_low` selected: lookback field visible, value field hidden
  - When `fixed_percent` selected: value field visible, lookback field hidden
  - `lookback` form control exists in stopLoss group
  - `ng build` succeeds

#### Implementation Details

```html
<!-- exit-rules-card.component.html — stop loss section changes -->
<mat-form-field appearance="outline">
  <mat-label>Rule type</mat-label>
  <mat-select formControlName="type">
    <mat-option value="fixed_percent">Fixed percent</mat-option>
    <mat-option value="swing_low">Swing low</mat-option>
  </mat-select>
</mat-form-field>

<!-- Conditionally show value OR lookback based on type -->
```

The implementer should:
1. In the HTML template stop loss section, replace `<mat-option value="swing_low" disabled>Swing low (coming soon)</mat-option>` with `<mat-option value="swing_low">Swing low</mat-option>`
2. Add a `@if` block to show lookback field when type=swing_low and hide value field
3. Add a `lookback` form control (`[null, [Validators.min(1)]]`) to the stopLoss FormGroup in `_buildForm()`
4. Add a `_syncStopLossType()` method in `exit-rules-card.component.ts` to toggle enable/disable for value vs lookback based on type

##### Pattern References

- `exit-rules-card.component.ts` — `_syncDisabledState` reactive pattern
- `exit-rules-card.component.html` — existing stop loss section

---

### Task 3.5: Build and lint {#task-35-build-and-lint}

Run frontend build and lint to verify Phase 3 changes compile and meet standards.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: N/A
- **Success**:
  - `ng build` succeeds
  - `npm run lint` passes

## Phase Success Criteria

- Trend filter card renders correctly for all filter types
- Dynamic field visibility works (Period vs Fast/Slow based on type)
- Trend filter operators display with human-readable labels
- swing_low exit type is selectable and shows lookback field
- TypeScript types match backend JSON contract
- Frontend builds and lints successfully
