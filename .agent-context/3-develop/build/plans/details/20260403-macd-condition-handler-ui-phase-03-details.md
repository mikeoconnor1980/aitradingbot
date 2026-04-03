<!-- markdownlint-disable-file -->

# Task Details: F8 — MACD Condition Handler + UI Card

## Phase 3: Frontend — MACD Condition Card + Template Integration

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — `standalone: true`, `inject()`, double quotes, explicit access modifiers, SCSS only, new control flow syntax (`@if`, `@for`), member ordering
- PBI reference: `F8-macd-condition-handler-ui.md` — UI card requirements and template specification

## Design References

- MACD condition item follows the simpler RSI card pattern (no conditional field visibility like PriceVsEma)
- "MACD Cross" template: MACD condition (12/26/9, cross_above_signal), strategyMode `signal`, TP 2%, SL 1.5%
- Max 1 MACD condition per strategy — enforced by disabling "Add MACD" button

### Task 3.1: Create `MacdConditionItemComponent` (TS, HTML, SCSS) {#task-31-create-macdconditionitemcomponent}

Create the MACD condition item component following the RSI condition item pattern. MACD has 3 period fields (fast, slow, signal) and an operator dropdown with 6 options.

- **Complexity**: Medium
- **Risk Factors**: Must correctly bind FormGroup controls for `fastPeriod`, `slowPeriod`, `signalPeriod`, `operator`; validation error messages must match PBI requirements
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/components/macd-condition-item/macd-condition-item.component.ts` — new file
  - `frontend/trading-ui/src/app/features/strategy-builder/components/macd-condition-item/macd-condition-item.component.html` — new file
  - `frontend/trading-ui/src/app/features/strategy-builder/components/macd-condition-item/macd-condition-item.component.scss` — new file
- **Success**:
  - Component renders MACD condition with fast/slow/signal period inputs and operator dropdown
  - Validation errors display inline (required, min, max)
  - Duplicate and remove buttons emit correct events
  - Component uses `standalone: true` and Material components

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/components/macd-condition-item/macd-condition-item.component.ts — new file
import { Component, EventEmitter, Input, Output } from "@angular/core";
import { FormGroup, ReactiveFormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatIconModule } from "@angular/material/icon";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import { MatTooltipModule } from "@angular/material/tooltip";
import { MACD_OPERATORS, MacdOperatorOption } from "../../enums/macd-operator.enum";
import { InfoPopoverComponent } from "../info-popover/info-popover.component";

@Component({
  selector: "app-macd-condition-item",
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
    InfoPopoverComponent,
  ],
  templateUrl: "./macd-condition-item.component.html",
  styleUrl: "./macd-condition-item.component.scss",
})
export class MacdConditionItemComponent {
  @Input({ required: true }) public group!: FormGroup;
  @Input() public index = 0;

  @Output() public readonly duplicate = new EventEmitter<void>();
  @Output() public readonly remove = new EventEmitter<void>();

  public readonly operators: MacdOperatorOption[] = MACD_OPERATORS;

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
}
```

```html
<!-- frontend/trading-ui/src/app/features/strategy-builder/components/macd-condition-item/macd-condition-item.component.html — new file -->
<mat-card class="condition-item" [formGroup]="group">
  <div class="condition-item__header">
    <div class="condition-item__identity">
      <mat-checkbox formControlName="enabled" color="primary">
        <span class="condition-item__type-label">MACD</span>
      </mat-checkbox>

      <app-info-popover
        title="MACD"
        description="MACD (Moving Average Convergence Divergence) is a momentum indicator. Use it to detect signal line crossovers, zero-line direction, or histogram momentum before the strategy enters a trade."
      />
    </div>

    <mat-form-field class="condition-item__label-field" appearance="outline">
      <mat-label>Label</mat-label>
      <input matInput formControlName="label" placeholder="e.g. MACD Bullish Cross" />
    </mat-form-field>

    <div class="condition-item__actions">
      <button
        mat-icon-button
        type="button"
        matTooltip="Duplicate"
        [attr.aria-label]="'Duplicate MACD condition ' + (index + 1)"
        (click)="onDuplicate()"
      >
        <mat-icon>content_copy</mat-icon>
      </button>

      <button
        mat-icon-button
        type="button"
        color="warn"
        matTooltip="Remove"
        [attr.aria-label]="'Remove MACD condition ' + (index + 1)"
        (click)="onRemove()"
      >
        <mat-icon>delete</mat-icon>
      </button>
    </div>
  </div>

  <div class="condition-item__fields">
    <mat-form-field appearance="outline">
      <mat-label>Fast Period</mat-label>
      <input matInput type="number" formControlName="fastPeriod" min="2" max="50" step="1" />
      @if (hasError("fastPeriod", "required")) {
        <mat-error>Fast period is required.</mat-error>
      }
      @if (hasError("fastPeriod", "min") || hasError("fastPeriod", "max")) {
        <mat-error>Fast period must be between 2 and 50.</mat-error>
      }
    </mat-form-field>

    <mat-form-field appearance="outline">
      <mat-label>Slow Period</mat-label>
      <input matInput type="number" formControlName="slowPeriod" min="5" max="200" step="1" />
      @if (hasError("slowPeriod", "required")) {
        <mat-error>Slow period is required.</mat-error>
      }
      @if (hasError("slowPeriod", "min") || hasError("slowPeriod", "max")) {
        <mat-error>Slow period must be between 5 and 200.</mat-error>
      }
    </mat-form-field>

    <mat-form-field appearance="outline">
      <mat-label>Signal Period</mat-label>
      <input matInput type="number" formControlName="signalPeriod" min="2" max="50" step="1" />
      @if (hasError("signalPeriod", "required")) {
        <mat-error>Signal period is required.</mat-error>
      }
      @if (hasError("signalPeriod", "min") || hasError("signalPeriod", "max")) {
        <mat-error>Signal period must be between 2 and 50.</mat-error>
      }
    </mat-form-field>

    <mat-form-field appearance="outline">
      <mat-label>Operator</mat-label>
      <mat-select formControlName="operator">
        @for (operator of operators; track operator.value) {
          <mat-option [value]="operator.value">{{ operator.label }}</mat-option>
        }
      </mat-select>
      @if (hasError("operator", "required")) {
        <mat-error>Operator is required.</mat-error>
      }
    </mat-form-field>
  </div>
</mat-card>
```

```scss
// frontend/trading-ui/src/app/features/strategy-builder/components/macd-condition-item/macd-condition-item.component.scss — new file
// Identical to rsi-condition-item.component.scss — same BEM structure

.condition-item {
  background: var(--colour-surface-dark);
  border: 1px solid var(--colour-border-subtle);
  margin-bottom: 0.75rem;
  padding: 0.35rem 0.4rem 0.15rem;

  &__header {
    display: flex;
    align-items: center;
    gap: 0.9rem;
    margin-bottom: 0.95rem;
    flex-wrap: wrap;
  }

  &__identity {
    display: inline-flex;
    align-items: center;
    gap: 0.15rem;
    flex: 0 0 auto;
  }

  &__type-label {
    font-weight: 600;
  }

  &__label-field {
    flex: 1 1 18rem;
    min-width: 14rem;
  }

  &__actions {
    display: flex;
    gap: 0.25rem;
    margin-left: auto;
    align-self: flex-start;
  }

  &__fields {
    display: flex;
    gap: 0.9rem;
    flex-wrap: wrap;

    mat-form-field {
      flex: 1 1 12rem;
    }
  }
}

@media (max-width: 768px) {
  .condition-item {
    &__header {
      gap: 0.5rem;
    }

    &__fields {
      gap: 0.5rem;

      mat-form-field {
        flex: 1 1 100%;
      }
    }
  }
}
```

##### Pattern References

- Based on `frontend/trading-ui/src/app/features/strategy-builder/components/rsi-condition-item/` — component structure, inputs/outputs, SCSS
- BEM class naming identical to RSI and PriceVsEma condition items
- Template structure follows RSI: header (checkbox + info-popover + label + actions) + fields row

---

### Task 3.2: Update `EntryConditionsCardComponent` — add MACD dispatch, button, duplicate {#task-32-update-entryconditionscardcomponent}

Add MACD rendering in the template, "Add MACD" button with max-1 enforcement, and MACD branch in `onDuplicate()`.

- **Complexity**: Medium
- **Risk Factors**: Max-1 enforcement via `hasMacdCondition` getter; must import `MacdConditionItemComponent`; "Add MACD" button disabled when condition exists
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/components/entry-conditions-card/entry-conditions-card.component.ts` — modify
  - `frontend/trading-ui/src/app/features/strategy-builder/components/entry-conditions-card/entry-conditions-card.component.html` — modify
- **Success**:
  - MACD conditions render with `MacdConditionItemComponent`
  - "Add MACD" button appears alongside "Add RSI" and "Add Price vs EMA"
  - "Add MACD" disabled when a MACD condition already exists
  - Duplicate correctly creates MACD condition copy
  - No conditions message includes MACD option

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/components/entry-conditions-card/entry-conditions-card.component.ts — modification

// Add import for MacdConditionItemComponent and MacdOperator:
import { MacdConditionItemComponent } from "../macd-condition-item/macd-condition-item.component";

// Add to imports array:
imports: [ReactiveFormsModule, MatButtonModule, MatCardModule, MatIconModule, InfoPopoverComponent, RsiConditionItemComponent, PriceVsEmaConditionItemComponent, MacdConditionItemComponent],

// Add getter for max-1 enforcement:
public get hasMacdCondition(): boolean {
  return this.conditionGroups.some((group) => this.getConditionType(group) === "macd");
}

// Add onAddMacd method:
public onAddMacd(): void {
  if (this.conditions === null) {
    return;
  }

  this.conditions.push(this._conditionFactory.createMacdCondition());
}

// Update onDuplicate — add MACD branch before RSI fallthrough:
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
      operator: values["operator"] as "near" | "above" | "below" | "cross_above" | "cross_below" | "touch",
      distanceType: values["distanceType"] as "percent" | "atr_multiple" | "absolute",
      distanceValue: values["distanceValue"] as number | null,
    }));
    return;
  }

  if (type === "macd") {
    this.conditions.insert(index + 1, this._conditionFactory.createMacdCondition({
      enabled: values["enabled"] as boolean,
      label: values["label"] as string,
      fastPeriod: values["fastPeriod"] as number,
      slowPeriod: values["slowPeriod"] as number,
      signalPeriod: values["signalPeriod"] as number,
      operator: values["operator"] as "cross_above_signal" | "cross_below_signal" | "above_zero" | "below_zero" | "histogram_rising" | "histogram_falling",
    }));
    return;
  }

  this.conditions.insert(index + 1, this._conditionFactory.createRsiCondition({
    enabled: values["enabled"] as boolean,
    label: values["label"] as string,
    period: values["period"] as number,
    operator: values["operator"] as "lt" | "lte" | "gt" | "gte" | "cross_above" | "cross_below",
    value: values["value"] as number,
  }));
}
```

```html
<!-- frontend/trading-ui/src/app/features/strategy-builder/components/entry-conditions-card/entry-conditions-card.component.html — modification -->

<!-- Update no-conditions message: -->
<p class="conditions-card__message">No conditions added. Click "Add RSI", "Add Price vs EMA", or "Add MACD" to create a condition.</p>

<!-- Update rendering @for loop — add MACD dispatch: -->
@for (group of conditionGroups; track group.value.id; let index = $index) {
  @if (getConditionType(group) === "price_vs_ema") {
    <app-price-vs-ema-condition-item
      [group]="group"
      [index]="index"
      (duplicate)="onDuplicate(index)"
      (remove)="onRemove(index)"
    />
  } @else if (getConditionType(group) === "macd") {
    <app-macd-condition-item
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

<!-- Add "Add MACD" button in mat-card-actions: -->
<mat-card-actions>
  <button mat-stroked-button type="button" (click)="onAddRsi()">
    <mat-icon>add</mat-icon>
    <span>Add RSI</span>
  </button>
  <button mat-stroked-button type="button" (click)="onAddPriceVsEma()">
    <mat-icon>add</mat-icon>
    <span>Add Price vs EMA</span>
  </button>
  <button mat-stroked-button type="button" (click)="onAddMacd()" [disabled]="hasMacdCondition" [matTooltip]="hasMacdCondition ? 'Only one MACD condition allowed per strategy' : ''">
    <mat-icon>add</mat-icon>
    <span>Add MACD</span>
  </button>
</mat-card-actions>
```

##### Pattern References

- Based on existing `frontend/trading-ui/src/app/features/strategy-builder/components/entry-conditions-card/` — both TS and HTML
- PriceVsEma dispatch pattern from the template `@if (getConditionType(group) === "price_vs_ema")`

---

### Task 3.3: Update `strategy-builder-page.component.ts` — load MACD conditions + MACD Cross template {#task-33-update-strategy-builder-page}

Add MACD branch in `_addLoadedCondition()` for the edit flow, and add `_applyMacdCrossTemplate()` for the template selection flow. Add the MACD Cross template case in `onTemplateSelected()`.

- **Complexity**: Medium
- **Risk Factors**: Must add MACD branch BEFORE the `if (condition.type !== "rsi") return;` guard to prevent silently dropping MACD conditions; template application must clear existing conditions and set correct exits
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` — modify
- **Success**:
  - Persisted MACD conditions load correctly in edit flow
  - "MACD Cross" template selection pre-populates MACD condition + exits
  - Template clears any existing conditions before applying
  - Import for `MacdParams` and `MacdOperator` added

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts — modification

// Add import for MacdParams:
import { ..., MacdParams, MacdOperator, ... } from "./models/strategy.model";

// Update _addLoadedCondition — add MACD branch BEFORE the "rsi" guard:
private _addLoadedCondition(condition: EntryConditionConfig): void {
  if (condition.type === "price_vs_ema") {
    const params = condition.params as PriceVsEmaParams;
    this.conditionsFormArray.push(this._conditionFactory.createPriceVsEmaCondition({
      id: condition.id,
      enabled: condition.enabled,
      label: condition.label,
      period: params.period,
      operator: params.operator,
      distanceType: params.distanceType,
      distanceValue: params.distanceValue,
    }));
    return;
  }

  if (condition.type === "macd") {
    const params = condition.params as MacdParams;
    this.conditionsFormArray.push(this._conditionFactory.createMacdCondition({
      id: condition.id,
      enabled: condition.enabled,
      label: condition.label,
      fastPeriod: params.fastPeriod,
      slowPeriod: params.slowPeriod,
      signalPeriod: params.signalPeriod,
      operator: params.operator,
    }));
    return;
  }

  if (condition.type !== "rsi") {
    return;
  }

  const params = condition.params as RsiParams;
  this.conditionsFormArray.push(this._conditionFactory.createRsiCondition({
    id: condition.id,
    enabled: condition.enabled,
    label: condition.label,
    period: params.period,
    operator: params.operator,
    value: params.value,
  }));
}

// Add to onTemplateSelected() — add "macd_cross" case after the "ema_pullback" case:
// In the existing if block: if (this._isSignalTemplate(templateId)) { ... }
// After:  if (templateId === "ema_pullback") { this._applyEmaPullbackTemplate(); }
// Add:
if (templateId === "macd_cross") {
  this._applyMacdCrossTemplate();
}

// Add new private method after _applyEmaPullbackTemplate():
private _applyMacdCrossTemplate(): void {
  this.form.patchValue({
    direction: "long",
  });

  const conditionsArray = this.conditionsFormArray;
  conditionsArray.clear();
  conditionsArray.push(this._conditionFactory.createMacdCondition({
    label: "MACD Bullish Cross",
    fastPeriod: 12,
    slowPeriod: 26,
    signalPeriod: 9,
    operator: "cross_above_signal",
  }));

  const exitGroup = this.form.get("exit") as FormGroup;
  exitGroup.patchValue({
    takeProfit: {
      enabled: true,
      type: "fixed_percent",
      value: 2,
    },
    stopLoss: {
      enabled: true,
      type: "fixed_percent",
      value: 1.5,
    },
  });

  conditionsArray.markAsDirty();
  exitGroup.markAsDirty();
  this.form.markAsDirty();
  this.form.updateValueAndValidity();
}
```

##### Pattern References

- `_addLoadedCondition()` in `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` (lines 462-495)
- `_applyEmaPullbackTemplate()` in same file (line 457) — clears conditions, adds template conditions, sets exits, marks dirty
- `onTemplateSelected()` in same file (line 158) — dispatches to template methods inside `_isSignalTemplate()` guard

---

### Task 3.4: Run frontend build and lint {#task-34-run-frontend-build-and-lint}

Run the frontend build and lint to verify all changes compile and meet style standards.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification only)
- **Success**:
  - `ng build` succeeds
  - `npm run lint` passes

Run commands:
```powershell
Set-Location frontend/trading-ui; npx ng build; npm run lint
```

## Phase Success Criteria

- `MacdConditionItemComponent` renders correctly with all 4 fields (fast, slow, signal periods + operator dropdown)
- "Add MACD" button appears, disabled when max 1 MACD condition exists
- MACD conditions duplicate/remove correctly
- Persisted MACD conditions load in edit flow
- "MACD Cross" template pre-populates correctly (12/26/9, cross_above_signal, TP 2%, SL 1.5%)
- Frontend builds and lints cleanly
