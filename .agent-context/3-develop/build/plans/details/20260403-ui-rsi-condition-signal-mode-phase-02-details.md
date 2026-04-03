<!-- markdownlint-disable-file -->

# Task Details: F6 — UI: RSI Condition Card + Signal Mode

## Phase 2: UI Components — RSI Condition Item & Entry Conditions Card

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — Standalone components, `inject()` DI, `@Input`/`@Output` with explicit accessibility, SCSS with kebab-case classes, Flexbox layout, modern `@if`/`@for` control flow in templates, double quotes
- `.agent-context/0-knowledge/07-ui-design.md` — Strategy Builder card map and component naming
- `frontend/trading-ui/src/app/features/strategy-builder/components/grid-config-card/grid-config-card.component.ts` — Card pattern: `@Input({ required: true }) group!: FormGroup` + `hasError()` helper

## Design References

- PBI spec: RSI condition item fields: Period (number, default 14), Operator (dropdown), Value (number, 0–100, default 40)
- PBI spec: Common condition shell: enabled checkbox, label input, duplicate button, remove button
- PBI spec: Entry conditions card: "Add RSI" button adds an RSI condition to the FormArray

---

### Task 2.1: Create RSI condition item component {#task-21-create-rsi-condition-item-component}

Create the RSI condition item component that renders a single RSI condition within a FormGroup. Includes the common condition shell (enabled toggle, label, duplicate, remove) and RSI-specific fields (period, operator, value).

- **Complexity**: Medium
- **Risk Factors**: Must match grid-config-card pattern for consistency; operator dropdown must use `RSI_OPERATORS` constant
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/components/rsi-condition-item/rsi-condition-item.component.ts` — New file
  - `frontend/trading-ui/src/app/features/strategy-builder/components/rsi-condition-item/rsi-condition-item.component.html` — New file
  - `frontend/trading-ui/src/app/features/strategy-builder/components/rsi-condition-item/rsi-condition-item.component.scss` — New file
- **Success**:
  - Component renders period, operator, value fields with correct form bindings
  - Enabled toggle, label input, duplicate button, remove button present
  - Validation errors shown for period and value when touched and invalid
  - `duplicate` and `remove` outputs emit on button click
- **Dependencies**:
  - Phase 1 (types and RSI_OPERATORS must exist)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/components/rsi-condition-item/rsi-condition-item.component.ts — new file

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
import { RSI_OPERATORS, RsiOperatorOption } from "../../enums/rsi-operator.enum";

@Component({
  selector: "app-rsi-condition-item",
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
  templateUrl: "./rsi-condition-item.component.html",
  styleUrl: "./rsi-condition-item.component.scss"
})
export class RsiConditionItemComponent {
  @Input({ required: true }) public group!: FormGroup;
  @Input() public index = 0;

  @Output() public duplicate = new EventEmitter<void>();
  @Output() public remove = new EventEmitter<void>();

  public readonly operators: RsiOperatorOption[] = RSI_OPERATORS;

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
<!-- frontend/trading-ui/src/app/features/strategy-builder/components/rsi-condition-item/rsi-condition-item.component.html — new file -->

<mat-card class="condition-item" [formGroup]="group">
  <div class="condition-item__header">
    <mat-checkbox formControlName="enabled" color="primary">
      <span class="condition-item__type-label">RSI</span>
    </mat-checkbox>

    <mat-form-field class="condition-item__label-field" appearance="outline">
      <mat-label>Label</mat-label>
      <input matInput formControlName="label" placeholder="e.g. RSI Oversold" />
    </mat-form-field>

    <div class="condition-item__actions">
      <button mat-icon-button type="button" matTooltip="Duplicate" (click)="onDuplicate()">
        <mat-icon>content_copy</mat-icon>
      </button>
      <button mat-icon-button type="button" matTooltip="Remove" color="warn" (click)="onRemove()">
        <mat-icon>delete</mat-icon>
      </button>
    </div>
  </div>

  <div class="condition-item__fields">
    <mat-form-field appearance="outline">
      <mat-label>Period</mat-label>
      <input matInput type="number" formControlName="period" />
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
      @if (hasError("operator", "required")) {
        <mat-error>Operator is required.</mat-error>
      }
    </mat-form-field>

    <mat-form-field appearance="outline">
      <mat-label>Value</mat-label>
      <input matInput type="number" formControlName="value" />
      @if (hasError("value", "required")) {
        <mat-error>Value is required.</mat-error>
      }
      @if (hasError("value", "min")) {
        <mat-error>RSI value must be between 0 and 100.</mat-error>
      }
      @if (hasError("value", "max")) {
        <mat-error>RSI value must be between 0 and 100.</mat-error>
      }
    </mat-form-field>
  </div>
</mat-card>
```

```scss
// frontend/trading-ui/src/app/features/strategy-builder/components/rsi-condition-item/rsi-condition-item.component.scss — new file

.condition-item {
  margin-bottom: 8px;

  &__header {
    display: flex;
    align-items: center;
    gap: 12px;
    margin-bottom: 12px;
  }

  &__type-label {
    font-weight: 500;
  }

  &__label-field {
    flex: 1;
  }

  &__actions {
    display: flex;
    gap: 4px;
  }

  &__fields {
    display: flex;
    gap: 12px;
    flex-wrap: wrap;

    mat-form-field {
      flex: 1;
      min-width: 140px;
    }
  }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/components/grid-config-card/grid-config-card.component.ts` — `@Input({ required: true }) group!: FormGroup`, `hasError()` helper pattern
- `frontend/trading-ui/src/app/features/strategy-builder/components/grid-config-card/grid-config-card.component.html` — Material form fields with `@if (hasError(...))` error blocks

---

### Task 2.2: Transform entry conditions card from stub to functional {#task-22-transform-entry-conditions-card}

Replace the empty stub `EntryConditionsCardComponent` with a functional card that orchestrates a `FormArray` of conditions. Includes "Add RSI" button and renders condition items.

- **Complexity**: Medium
- **Risk Factors**: FormArray manipulation (push, removeAt, insert for duplicate) must maintain form state correctly
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/components/entry-conditions-card/entry-conditions-card.component.ts` — Replace stub
  - `frontend/trading-ui/src/app/features/strategy-builder/components/entry-conditions-card/entry-conditions-card.component.html` — Replace placeholder
  - `frontend/trading-ui/src/app/features/strategy-builder/components/entry-conditions-card/entry-conditions-card.component.scss` — Update styles
- **Success**:
  - Card receives `FormArray` via `@Input`
  - "Add RSI" button creates and appends a new RSI condition FormGroup
  - Each condition renders via `RsiConditionItemComponent`
  - Duplicate clones condition values into a new FormGroup at next index
  - Remove deletes condition from FormArray
  - Empty state message shown when no conditions exist
- **Dependencies**:
  - Task 2.1 (RSI condition item component must exist)
  - Phase 1 (ConditionFactoryService must exist)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/components/entry-conditions-card/entry-conditions-card.component.ts — replacement

import { Component, Input, inject } from "@angular/core";
import { FormArray, FormGroup, ReactiveFormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatIconModule } from "@angular/material/icon";
import { ConditionFactoryService } from "../../services/condition-factory.service";
import { RsiConditionItemComponent } from "../rsi-condition-item/rsi-condition-item.component";

@Component({
  selector: "app-entry-conditions-card",
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    RsiConditionItemComponent,
  ],
  templateUrl: "./entry-conditions-card.component.html",
  styleUrl: "./entry-conditions-card.component.scss"
})
export class EntryConditionsCardComponent {
  private readonly _conditionFactory = inject(ConditionFactoryService);

  @Input({ required: true }) public conditions!: FormArray;

  public get conditionGroups(): FormGroup[] {
    return this.conditions.controls as FormGroup[];
  }

  public onAddRsi(): void {
    this.conditions.push(this._conditionFactory.createRsiCondition());
  }

  public onDuplicate(index: number): void {
    const source = this.conditions.at(index) as FormGroup;
    const values = source.getRawValue() as Record<string, unknown>;
    const duplicate = this._conditionFactory.createRsiCondition({
      enabled: values["enabled"] as boolean,
      label: values["label"] as string,
      period: values["period"] as number,
      operator: values["operator"] as string,
      value: values["value"] as number,
    });
    this.conditions.insert(index + 1, duplicate);
  }

  public onRemove(index: number): void {
    this.conditions.removeAt(index);
  }
}
```

```html
<!-- frontend/trading-ui/src/app/features/strategy-builder/components/entry-conditions-card/entry-conditions-card.component.html — replacement -->

<mat-card class="conditions-card">
  <mat-card-header>
    <mat-card-title>Entry Conditions</mat-card-title>
  </mat-card-header>

  <mat-card-content>
    @if (conditionGroups.length === 0) {
      <p class="conditions-card__empty">No conditions added. Click "Add RSI" to create a condition.</p>
    }

    @for (group of conditionGroups; track group; let i = $index) {
      <app-rsi-condition-item
        [group]="group"
        [index]="i"
        (duplicate)="onDuplicate(i)"
        (remove)="onRemove(i)"
      />
    }
  </mat-card-content>

  <mat-card-actions>
    <button mat-stroked-button type="button" (click)="onAddRsi()">
      <mat-icon>add</mat-icon>
      <span>Add RSI</span>
    </button>
  </mat-card-actions>
</mat-card>
```

```scss
// frontend/trading-ui/src/app/features/strategy-builder/components/entry-conditions-card/entry-conditions-card.component.scss — replacement

.conditions-card {
  &__empty {
    margin: 0;
    color: var(--colour-muted);
    font-style: italic;
    padding: 16px 0;
  }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/components/grid-config-card/grid-config-card.component.ts` — Card pattern with `@Input({ required: true })` FormGroup input
- `frontend/trading-ui/src/app/features/strategy-builder/components/entry-conditions-card/entry-conditions-card.component.ts` — Current stub being replaced
- `frontend/trading-ui/src/app/features/strategy-builder/components/entry-conditions-card/entry-conditions-card.component.html` — Current placeholder being replaced

---

### Task 2.3: Frontend build and lint verification {#task-23-frontend-build-and-lint}

Run `ng build` and `ng lint` from the `frontend/trading-ui` directory. The entry conditions card is not referenced in the page template until Phase 3, so the new components should compile cleanly in isolation. If the existing page template already references the stub with a required input binding, a temporary workaround may be needed. The goal here is to verify the new components themselves compile cleanly.

- **Complexity**: Low
- **Risk Factors**: Entry conditions card now requires a `FormArray` input — the page template still passes nothing. Build warnings about this are expected until Phase 3.
- **Files**: None (verification only)
- **Success**:
  - New component files compile without syntax errors
  - `ng lint` passes on new files

## Phase Success Criteria

- `RsiConditionItemComponent` renders RSI fields (period, operator, value) with validation error display
- `RsiConditionItemComponent` includes common shell elements (enabled checkbox, label, duplicate/remove buttons)
- `EntryConditionsCardComponent` accepts a `FormArray`, renders condition items, and supports add/duplicate/remove
- Empty state message displayed when no conditions exist
- New components follow existing card patterns (standalone, Material, reactive forms)
