<!-- markdownlint-disable-file -->

# Task Details: F7 — Trend Filter + EMA Condition Handler + UI

## Phase 4: Frontend Price vs EMA Condition & EMA Pullback Template

## Standards and Knowledge References

- **angular.instructions.md**: standalone components, inject() DI, explicit accessibility, double quotes, SCSS, @if/@for control flow
- **07-ui-design.md**: strategy builder card components, entry conditions card
- **11-angular-instructions.md**: Angular 19 standalone, Angular Material dark theme

## Design References

- F7 PBI: "Add Price vs EMA" button, condition item fields, EMA Pullback template pre-population
- `rsi-condition-item.component.ts/html` — condition item with FormGroup, operator dropdown, duplicate/remove buttons
- `condition-factory.service.ts` — factory for creating condition FormGroups
- `strategy-template-selector.component.ts` — template selection with `available` flag

### Task 4.1: Create price-vs-ema-condition-item component {#task-41-create-price-vs-ema-condition-item-component}

Create a new component for rendering and editing a `price_vs_ema` entry condition, following the `rsi-condition-item` pattern.

Fields: Enabled checkbox, Label, EMA Period, Operator dropdown (near, above, below, cross_above, cross_below, touch), Distance Type dropdown (percent, atr_multiple, absolute), Distance Value. Distance fields only visible when operator = near.

- **Complexity**: Medium
- **Risk Factors**: Conditional field visibility for distance fields; must integrate with FormArray in entry-conditions-card
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/components/price-vs-ema-condition-item/price-vs-ema-condition-item.component.ts` — new file
  - `frontend/trading-ui/src/app/features/strategy-builder/components/price-vs-ema-condition-item/price-vs-ema-condition-item.component.html` — new file
  - `frontend/trading-ui/src/app/features/strategy-builder/components/price-vs-ema-condition-item/price-vs-ema-condition-item.component.scss` — new file
  - `frontend/trading-ui/src/app/features/strategy-builder/enums/price-vs-ema-operator.enum.ts` — new file
- **Success**:
  - Component renders with all fields
  - Operator dropdown shows 6 options
  - Distance fields visible only when operator=near, hidden otherwise
  - Duplicate/remove buttons work
  - `ng build` succeeds

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/enums/price-vs-ema-operator.enum.ts — new file
import { PriceVsEmaOperator } from "../models/strategy.model";

export interface PriceVsEmaOperatorOption {
  value: PriceVsEmaOperator;
  label: string;
}

export const PRICE_VS_EMA_OPERATORS: PriceVsEmaOperatorOption[] = [
  { value: "near", label: "Near (within distance)" },
  { value: "above", label: "Above" },
  { value: "below", label: "Below" },
  { value: "cross_above", label: "Cross above" },
  { value: "cross_below", label: "Cross below" },
  { value: "touch", label: "Touch (wick)" },
];
```

```typescript
// price-vs-ema-condition-item.component.ts — new file
import { Component, DestroyRef, EventEmitter, Input, OnInit, Output, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormGroup, ReactiveFormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatIconModule } from "@angular/material/icon";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import { MatTooltipModule } from "@angular/material/tooltip";
import {
  PRICE_VS_EMA_OPERATORS,
  PriceVsEmaOperatorOption,
} from "../../enums/price-vs-ema-operator.enum";

@Component({
  selector: "app-price-vs-ema-condition-item",
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTooltipModule,
  ],
  templateUrl: "./price-vs-ema-condition-item.component.html",
  styleUrl: "./price-vs-ema-condition-item.component.scss"
})
export class PriceVsEmaConditionItemComponent implements OnInit {
  private readonly _destroyRef = inject(DestroyRef);

  @Input({ required: true }) public group!: FormGroup;
  @Input() public index = 0;

  @Output() public readonly duplicate = new EventEmitter<void>();
  @Output() public readonly remove = new EventEmitter<void>();

  public readonly operators: PriceVsEmaOperatorOption[] = PRICE_VS_EMA_OPERATORS;

  public get showDistanceFields(): boolean {
    return this.group.get("operator")?.value === "near";
  }

  public ngOnInit(): void {
    this._syncDistanceFieldVisibility();
  }

  public hasError(controlName: string, errorCode: string): boolean {
    const control = this.group.get(controlName);
    return Boolean(control?.hasError(errorCode) && (control.touched || control.dirty));
  }

  public onDuplicate(): void {
    this.duplicate.emit();
  }

  public onRemove(): void {
    this.remove.emit();
  }

  private _syncDistanceFieldVisibility(): void {
    const operatorControl = this.group.get("operator");
    if (operatorControl === null) { return; }

    operatorControl.valueChanges
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((value: string) => {
        const distanceType = this.group.get("distanceType");
        const distanceValue = this.group.get("distanceValue");
        if (distanceType === null || distanceValue === null) { return; }

        if (value === "near") {
          distanceType.enable();
          distanceValue.enable();
        } else {
          distanceType.disable();
          distanceValue.disable();
        }
      });
  }
}
```

```html
<!-- price-vs-ema-condition-item.component.html — new file -->
<mat-card class="condition-item" [formGroup]="group">
  <div class="condition-item__header">
    <mat-checkbox formControlName="enabled" color="primary">
      <span class="condition-item__type-label">Price vs EMA</span>
    </mat-checkbox>

    <mat-form-field class="condition-item__label-field" appearance="outline">
      <mat-label>Label</mat-label>
      <input matInput formControlName="label" placeholder="e.g. Price near EMA 50" />
    </mat-form-field>

    <div class="condition-item__actions">
      <button
        mat-icon-button
        type="button"
        matTooltip="Duplicate"
        [attr.aria-label]="'Duplicate Price vs EMA condition ' + (index + 1)"
        (click)="onDuplicate()"
      >
        <mat-icon>content_copy</mat-icon>
      </button>

      <button
        mat-icon-button
        type="button"
        color="warn"
        matTooltip="Remove"
        [attr.aria-label]="'Remove Price vs EMA condition ' + (index + 1)"
        (click)="onRemove()"
      >
        <mat-icon>delete</mat-icon>
      </button>
    </div>
  </div>

  <div class="condition-item__fields">
    <mat-form-field appearance="outline">
      <mat-label>EMA Period</mat-label>
      <input matInput type="number" formControlName="period" min="1" step="1" />
      @if (hasError("period", "required")) {
        <mat-error>Period is required.</mat-error>
      }
      @if (hasError("period", "min")) {
        <mat-error>Period must be at least 1.</mat-error>
      }
    </mat-form-field>

    <mat-form-field appearance="outline">
      <mat-label>Operator</mat-label>
      <mat-select formControlName="operator">
        @for (op of operators; track op.value) {
          <mat-option [value]="op.value">{{ op.label }}</mat-option>
        }
      </mat-select>
    </mat-form-field>

    @if (showDistanceFields) {
      <mat-form-field appearance="outline">
        <mat-label>Distance type</mat-label>
        <mat-select formControlName="distanceType">
          <mat-option value="percent">Percent</mat-option>
          <mat-option value="absolute">Absolute</mat-option>
          <mat-option value="atr_multiple" disabled>ATR multiple (coming soon)</mat-option>
        </mat-select>
      </mat-form-field>

      <mat-form-field appearance="outline">
        <mat-label>Distance value</mat-label>
        <input matInput type="number" formControlName="distanceValue" min="0.01" step="0.01" />
        @if (hasError("distanceValue", "required")) {
          <mat-error>Distance value is required.</mat-error>
        }
        @if (hasError("distanceValue", "min")) {
          <mat-error>Distance value must be greater than 0.</mat-error>
        }
      </mat-form-field>
    }
  </div>
</mat-card>
```

##### Pattern References

- `rsi-condition-item.component.ts/html` — condition item structure, FormGroup input, EventEmitter outputs, `hasError()`
- `rsi-condition-item.component.scss` — use same `.condition-item` class names for consistent styling

---

### Task 4.2: Add PriceVsEma condition factory method {#task-42-add-pricevsema-condition-factory-method}

Add a `createPriceVsEmaCondition` method to `ConditionFactoryService`.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/services/condition-factory.service.ts` — add method
- **Success**:
  - `createPriceVsEmaCondition()` returns a FormGroup with: id, enabled, type="price_vs_ema", label, period, operator, distanceType, distanceValue
  - Defaults: period=50, operator="near", distanceType="percent", distanceValue=0.25
  - `ng build` succeeds

#### Implementation Details

```typescript
// condition-factory.service.ts — add method and interface

export interface CreatePriceVsEmaConditionOverrides {
  id: string;
  enabled: boolean;
  label: string;
  period: number;
  operator: PriceVsEmaOperator;
  distanceType: PriceVsEmaDistanceType;
  distanceValue: number | null;
}

public createPriceVsEmaCondition(overrides?: Partial<CreatePriceVsEmaConditionOverrides>): FormGroup {
  return this._fb.group({
    id: [overrides?.id ?? this._generateId()],
    enabled: [overrides?.enabled ?? true],
    type: ["price_vs_ema"],
    label: [overrides?.label ?? ""],
    period: [overrides?.period ?? 50, [Validators.required, Validators.min(1)]],
    operator: [overrides?.operator ?? "near", Validators.required],
    distanceType: [overrides?.distanceType ?? "percent"],
    distanceValue: [overrides?.distanceValue ?? 0.25, [Validators.min(0.01)]],
  });
}
```

Import required types:
```typescript
import { PriceVsEmaDistanceType, PriceVsEmaOperator, RsiOperator } from "../models/strategy.model";
```

##### Pattern References

- `condition-factory.service.ts` — existing `createRsiCondition` method

---

### Task 4.3: Update entry-conditions-card with Add Price vs EMA button {#task-43-update-entry-conditions-card-with-add-price-vs-ema-button}

Add "Add Price vs EMA" button alongside the existing "Add RSI" button. Render `price-vs-ema-condition-item` for conditions with `type = "price_vs_ema"`.

- **Complexity**: Medium
- **Risk Factors**: Must discriminate between RSI and PriceVsEma condition group types when rendering
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/components/entry-conditions-card/entry-conditions-card.component.ts` — add onAddPriceVsEma, add duplicate for price_vs_ema
  - `frontend/trading-ui/src/app/features/strategy-builder/components/entry-conditions-card/entry-conditions-card.component.html` — add button, render correct component by type
- **Success**:
  - "Add Price vs EMA" button visible alongside "Add RSI"
  - Clicking it adds a price_vs_ema condition FormGroup
  - RSI conditions render as `rsi-condition-item`, PriceVsEma conditions render as `price-vs-ema-condition-item`
  - Duplicate/remove work for both types
  - `ng build` succeeds

#### Implementation Details

```typescript
// entry-conditions-card.component.ts — additions

import { PriceVsEmaConditionItemComponent } from "../price-vs-ema-condition-item/price-vs-ema-condition-item.component";

// Add to imports array:
// PriceVsEmaConditionItemComponent

public getConditionType(group: FormGroup): string {
  return String(group.get("type")?.value ?? "rsi");
}

public onAddPriceVsEma(): void {
  if (this.conditions === null) {
    return;
  }

  this.conditions.push(this._conditionFactory.createPriceVsEmaCondition());
}

public onDuplicate(index: number): void {
  if (this.conditions === null) {
    return;
  }

  const source = this.conditions.at(index) as FormGroup;
  const values = source.getRawValue() as Record<string, unknown>;
  const type = String(values["type"] ?? "rsi");

  if (type === "price_vs_ema") {
    this.conditions.insert(index + 1, this._conditionFactory.createPriceVsEmaCondition({
      enabled: values["enabled"] as boolean,
      label: values["label"] as string,
      period: values["period"] as number,
      operator: values["operator"] as any,
      distanceType: values["distanceType"] as any,
      distanceValue: values["distanceValue"] as number | null,
    }));
  } else {
    this.conditions.insert(index + 1, this._conditionFactory.createRsiCondition({
      enabled: values["enabled"] as boolean,
      label: values["label"] as string,
      period: values["period"] as number,
      operator: values["operator"] as any,
      value: values["value"] as number,
    }));
  }
}
```

```html
<!-- entry-conditions-card.component.html — update loop and actions -->
@for (group of conditionGroups; track group; let index = $index) {
  @if (getConditionType(group) === "price_vs_ema") {
    <app-price-vs-ema-condition-item
      [group]="group"
      [index]="index"
      (duplicate)="onDuplicate(index)"
      (remove)="onRemove(index)"
    />
  } @else {
    <app-rsi-condition-item
      [group]="group"
      [index]="index"
      (duplicate)="onDuplicate(index)"
      (remove)="onRemove(index)"
    />
  }
}

<!-- Add button in mat-card-actions alongside existing Add RSI -->
<mat-card-actions>
  <button mat-stroked-button type="button" (click)="onAddRsi()">
    <mat-icon>add</mat-icon>
    <span>Add RSI</span>
  </button>
  <button mat-stroked-button type="button" (click)="onAddPriceVsEma()">
    <mat-icon>add</mat-icon>
    <span>Add Price vs EMA</span>
  </button>
</mat-card-actions>
```

##### Pattern References

- Existing `entry-conditions-card.component.ts/html` — RSI condition rendering pattern

---

### Task 4.4: Implement EMA Pullback template pre-population {#task-44-implement-ema-pullback-template-pre-population}

When "EMA Pullback" template is selected, pre-populate the form with default values for the EMA pullback strategy.

Pre-population: direction=long, trend filter (ema_cross, fast=50, slow=200, operator=gt, appliesTo=long), entry conditions: [price near EMA 50 (0.25%, percent), RSI(14) < 40], exit: TP 3% fixed_percent + SL swing_low lookback 5.

- **Complexity**: Medium
- **Risk Factors**: Must handle template selection → form patching; must create FormArray entries for conditions; must set exit rule types correctly
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` — update `onTemplateSelected` to apply template defaults
- **Success**:
  - Selecting "EMA Pullback" populates: direction=long, trendFilter enabled with ema_cross 50>200, conditions array with price_vs_ema near + RSI lt 40, exit TP 3% + SL swing_low lookback 5
  - All form controls update correctly
  - Form is dirty after template application
- **Dependencies**:
  - Tasks 4.1–4.3, Task 3.4 (swing_low enabled)
  - F6 (strategyMode form control, trendFilter FormGroup, entryConditions FormArray)

#### Implementation Details

```typescript
// strategy-builder-page.component.ts — update onTemplateSelected

public onTemplateSelected(templateId: string): void {
  this.form.patchValue({ templateId });

  if (templateId === "ema_pullback") {
    this._applyEmaPullbackTemplate();
  }
}

private _applyEmaPullbackTemplate(): void {
  // Patch scalar values
  this.form.patchValue({
    direction: "long",
    // Assumes F6 added: strategyMode control
    // strategyMode: "signal",
  });

  // Patch trend filter group (assumes F6 created the FormGroup)
  const trendFilterGroup = this.form.get("trendFilter") as FormGroup | null;
  if (trendFilterGroup !== null) {
    trendFilterGroup.patchValue({
      enabled: true,
      type: "ema_cross",
      fastPeriod: 50,
      slowPeriod: 200,
      operator: "gt",
      appliesTo: "long",
    });
  }

  // Replace entry conditions (assumes F6 created the FormArray)
  const conditionsArray = this.form.get("entryConditions") as FormArray | null;
  if (conditionsArray !== null) {
    conditionsArray.clear();

    const conditionFactory = inject(ConditionFactoryService);
    conditionsArray.push(conditionFactory.createPriceVsEmaCondition({
      label: "Price near EMA 50",
      period: 50,
      operator: "near",
      distanceType: "percent",
      distanceValue: 0.25,
    }));

    conditionsArray.push(conditionFactory.createRsiCondition({
      label: "RSI Oversold",
      period: 14,
      operator: "lt",
      value: 40,
    }));
  }

  // Patch exit rules
  const exitGroup = this.form.get("exit") as FormGroup;
  exitGroup.patchValue({
    takeProfit: {
      enabled: true,
      type: "fixed_percent",
      value: 3,
    },
    stopLoss: {
      enabled: true,
      type: "swing_low",
      value: null,
      lookback: 5,
    },
  });
}
```

**NOTE**: The `ConditionFactoryService` must be injected at class level (not inline). Adjust the template application to use `this._conditionFactory` which should already be injected in the page component (add it if not already present after F6).

##### Pattern References

- Existing `onTemplateSelected` and `_loadStrategy` methods — form patching patterns

---

### Task 4.5: Update preview-summary-card for signal mode {#task-45-update-preview-summary-card-for-signal-mode}

Add signal mode branch to `PreviewSummaryCardComponent` that generates preview text for trend filter and entry conditions.

- **Complexity**: Medium
- **Risk Factors**: Must detect strategy mode to switch preview text generation
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/components/preview-summary-card/preview-summary-card.component.ts` — add signal mode preview
- **Success**:
  - Grid mode preview unchanged
  - Signal mode preview includes: trend filter text ("when the 50 EMA is above the 200 EMA") + condition text ("price is within 0.25% of the 50 EMA")
  - `ng build` succeeds

#### Implementation Details

```typescript
// preview-summary-card.component.ts — add signal mode branch in get previewText()

// After grid mode check, add:
const strategyMode = String(formValue["strategyMode"] ?? "grid");
if (strategyMode === "signal") {
  return this._buildSignalPreview(formValue);
}

// Existing grid preview continues...

private _buildSignalPreview(formValue: Record<string, unknown>): string {
  const parts: string[] = [];
  const direction = String(formValue["direction"] ?? "long");
  const market = String(formValue["market"] ?? "market");
  const timeframe = String(formValue["timeframe"] ?? "timeframe");

  parts.push(`Signal strategy on ${market} ${timeframe} (${direction}).`);

  // Trend filter text
  const trendFilter = formValue["trendFilter"] as Record<string, unknown> | null;
  if (trendFilter !== null && trendFilter["enabled"]) {
    const type = String(trendFilter["type"] ?? "");
    if (type === "ema_cross" || type === "sma_cross") {
      const maType = type === "ema_cross" ? "EMA" : "SMA";
      const fast = Number(trendFilter["fastPeriod"] ?? 0);
      const slow = Number(trendFilter["slowPeriod"] ?? 0);
      const op = String(trendFilter["operator"] ?? "gt");
      const opText = op === "gt" ? "is above" : op === "lt" ? "is below" : op;
      parts.push(`When the ${fast} ${maType} ${opText} the ${slow} ${maType}.`);
    } else if (type === "price_above_ema") {
      const period = Number(trendFilter["period"] ?? 0);
      const op = String(trendFilter["operator"] ?? "above");
      parts.push(`When price is ${op} the ${period} EMA.`);
    }
  }

  // Entry conditions text
  const conditions = formValue["entryConditions"] as Record<string, unknown>[] | null;
  if (Array.isArray(conditions) && conditions.length > 0) {
    for (const cond of conditions) {
      const type = String(cond["type"] ?? "");
      if (type === "price_vs_ema") {
        const period = Number(cond["period"] ?? 0);
        const op = String(cond["operator"] ?? "near");
        if (op === "near") {
          const dist = this._formatNumber(cond["distanceValue"]);
          parts.push(`Price is within ${dist}% of the ${period} EMA.`);
        } else {
          parts.push(`Price is ${op} the ${period} EMA.`);
        }
      } else if (type === "rsi") {
        const period = Number(cond["period"] ?? 14);
        const op = String(cond["operator"] ?? "lt");
        const value = this._formatNumber(cond["value"]);
        const opText = op === "lt" ? "<" : op === "gt" ? ">" : op;
        parts.push(`RSI(${period}) ${opText} ${value}.`);
      }
    }
  }

  return parts.join(" ");
}
```

##### Pattern References

- Existing `previewText` getter — grid mode preview generation

---

### Task 4.6: Enable EMA Pullback template {#task-46-enable-ema-pullback-template}

Set `available: true` for the `ema_pullback` template in `STRATEGY_TEMPLATES`.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts` — change `available: false` to `true`
- **Success**:
  - "EMA Pullback" button is clickable in template selector
  - `ng build` succeeds

#### Implementation Details

```typescript
// strategy.model.ts — update STRATEGY_TEMPLATES
{ id: "ema_pullback", label: "EMA Pullback", available: true },
```

##### Pattern References

- Existing `STRATEGY_TEMPLATES` array

---

### Task 4.7: Build and lint {#task-47-build-and-lint}

Run frontend build and lint to verify Phase 4 changes compile and meet standards.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: N/A
- **Success**:
  - `ng build` succeeds
  - `npm run lint` passes

## Phase Success Criteria

- Price vs EMA condition item renders with all fields
- Distance fields show/hide based on operator selection
- "Add Price vs EMA" button adds a correctly typed condition
- EMA Pullback template pre-populates all form values correctly
- Preview text updates for signal mode strategies
- EMA Pullback template is selectable in template selector
- Frontend builds and lints successfully
