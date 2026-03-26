<!-- markdownlint-disable-file -->

# Task Details: F9 — Position Actions

## Phase 3: Frontend — Partial Close & Reverse Position

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — Standalone components, `inject()` only, modal suffix `.modal.component.ts` / `ModalComponent`, `@if`/`@for` control flow, reactive forms, `MatFormField appearance="outline"`
- `.agent-context/0-knowledge/11-angular-instructions.md` — Row-level loading pattern, `@Output` emit + parent orchestration

## Design References

- `DashboardComponent.onClosePosition` — market order placement flow: confirm → optimistic remove → API → restore on error
- `ConfirmDialogComponent` — reusable confirmation with order summary (side, asset, size)
- Partial close uses a regular `market` order with `reduceOnly: true` and a fraction of position size
- Reverse uses a `market` order for 2× position size (close current + open opposite)

---

### Task 3.1: Create PartialCloseModalComponent {#task-31-create-partialclosemodalcomponent}

Create a new modal component for partial position close. The modal shows the current position, provides percentage quick-select buttons (25%, 50%, 75%), a custom size input, and validates that the close size is > 0 and ≤ position size.

- **Complexity**: Medium
- **Risk Factors**: Percentage buttons must correctly compute size from `Math.abs(position.size)`; custom size validation must clamp at position size; must handle floating-point precision
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/partial-close-modal/partial-close.modal.component.ts` — New file
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/partial-close-modal/partial-close.modal.component.html` — New file
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/partial-close-modal/partial-close.modal.component.scss` — New file
- **Success**:
  - Modal shows position info (side, asset, current size)
  - Percentage buttons (25%, 50%, 75%) set the close size correctly
  - Custom size input validates: > 0 and ≤ `Math.abs(position.size)`
  - Modal closes with `PartialCloseResult` containing the close size and position reference
  - Submit disabled when no valid size is entered
- **Dependencies**: Task 2.1 (updated Position model)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/positions-table/partial-close-modal/partial-close.modal.component.ts — new file
import { DecimalPipe } from "@angular/common";
import { Component, inject } from "@angular/core";
import { AbstractControl, FormBuilder, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatButtonToggleModule } from "@angular/material/button-toggle";
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from "@angular/material/dialog";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { Position } from "../../../../core/models/position.model";

export interface PartialCloseDialogData {
  position: Position;
}

export interface PartialCloseResult {
  closeSize: number;
  position: Position;
}

interface PartialCloseForm {
  closeSize: FormControl<number | null>;
}

@Component({
  selector: "app-partial-close-modal",
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatButtonToggleModule,
    DecimalPipe
  ],
  templateUrl: "./partial-close.modal.component.html",
  styleUrl: "./partial-close.modal.component.scss"
})
export class PartialCloseModalComponent {
  private readonly _fb = inject(FormBuilder);
  private readonly _dialogRef = inject(MatDialogRef<PartialCloseModalComponent>);
  private readonly _data: PartialCloseDialogData = inject(MAT_DIALOG_DATA);

  public readonly position = this._data.position;
  public readonly positionSize = Math.abs(this.position.size);
  public selectedPercentage: number | null = null;

  public readonly form: FormGroup<PartialCloseForm> = this._fb.group<PartialCloseForm>({
    closeSize: this._fb.control<number | null>(null, [
      Validators.required,
      Validators.min(0.000001),
      this._maxSizeValidator.bind(this),
    ]),
  });

  public onPercentageSelect(percent: number): void {
    this.selectedPercentage = percent;
    const size = Math.round(this.positionSize * percent / 100 * 10000) / 10000; // 4 decimal places
    this.form.controls.closeSize.setValue(size);
    this.form.controls.closeSize.markAsTouched();
  }

  public onSizeInput(): void {
    this.selectedPercentage = null;
  }

  public onCancel(): void {
    this._dialogRef.close();
  }

  public onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const result: PartialCloseResult = {
      closeSize: this.form.controls.closeSize.value!,
      position: this.position,
    };

    this._dialogRef.close(result);
  }

  private _maxSizeValidator(control: AbstractControl): ValidationErrors | null {
    const value = control.value;
    if (value === null || value === undefined) {
      return null;
    }
    if (value > this.positionSize) {
      return { maxSize: true };
    }
    return null;
  }
}
```

```html
<!-- frontend/trading-ui/src/app/features/dashboard/positions-table/partial-close-modal/partial-close.modal.component.html — new file -->
<h2 mat-dialog-title>Partial Close</h2>

<mat-dialog-content>
  <div class="partial-close-modal__info">
    <div class="partial-close-modal__row">
      <span class="partial-close-modal__label">Position</span>
      <span [class]="position.side === 'Long' ? 'side--long' : 'side--short'">
        {{ position.side }} {{ position.asset }}
      </span>
    </div>
    <div class="partial-close-modal__row">
      <span class="partial-close-modal__label">Current Size</span>
      <span>{{ positionSize | number: "1.4-4" }}</span>
    </div>
  </div>

  <div class="partial-close-modal__percentages">
    <mat-button-toggle-group [value]="selectedPercentage" (change)="onPercentageSelect($event.value)">
      <mat-button-toggle [value]="25">25%</mat-button-toggle>
      <mat-button-toggle [value]="50">50%</mat-button-toggle>
      <mat-button-toggle [value]="75">75%</mat-button-toggle>
    </mat-button-toggle-group>
  </div>

  <form [formGroup]="form" class="partial-close-modal__form">
    <mat-form-field appearance="outline" class="partial-close-modal__field">
      <mat-label>Close Size</mat-label>
      <input matInput type="number" formControlName="closeSize" step="0.0001"
             (input)="onSizeInput()" />
      @if (form.controls.closeSize.hasError("required")) {
        <mat-error>Size is required</mat-error>
      }
      @if (form.controls.closeSize.hasError("min")) {
        <mat-error>Size must be greater than 0</mat-error>
      }
      @if (form.controls.closeSize.hasError("maxSize")) {
        <mat-error>Size cannot exceed position size ({{ positionSize | number: "1.4-4" }})</mat-error>
      }
      <mat-hint>Max: {{ positionSize | number: "1.4-4" }}</mat-hint>
    </mat-form-field>
  </form>
</mat-dialog-content>

<mat-dialog-actions align="end">
  <button mat-button type="button" (click)="onCancel()">Cancel</button>
  <button mat-flat-button color="warn" type="button" (click)="onSubmit()" [disabled]="form.invalid">
    Close {{ form.controls.closeSize.value | number: "1.4-4" }} {{ position.asset }}
  </button>
</mat-dialog-actions>
```

```scss
// frontend/trading-ui/src/app/features/dashboard/positions-table/partial-close-modal/partial-close.modal.component.scss — new file
.partial-close-modal {
  &__info {
    margin-bottom: 1rem;
  }

  &__row {
    display: flex;
    justify-content: space-between;
    padding: 0.25rem 0;
  }

  &__label {
    color: var(--colour-label);
  }

  &__percentages {
    margin-bottom: 1rem;
    display: flex;
    justify-content: center;
  }

  &__form {
    display: flex;
    flex-direction: column;
  }

  &__field {
    width: 100%;
  }
}

.side--long {
  color: var(--colour-profit);
}

.side--short {
  color: var(--colour-loss);
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/orders-table/modify-order-modal/modify-order.modal.component.ts` — form dialog pattern
- `frontend/trading-ui/src/app/features/order-entry/order-entry.component.ts` — `MatButtonToggleModule` usage for quick-select

---

### Task 3.2: Wire partial close flow in DashboardComponent {#task-32-wire-partial-close-flow-in-dashboardcomponent}

Add `onPartialClose` handler in `DashboardComponent` that opens the `PartialCloseModalComponent`, processes the result into a market order with `reduceOnly: true`, submits via `OrderService.placeOrder()`, and manages row-level loading + optimistic size reduction.

- **Complexity**: Medium
- **Risk Factors**: Partial close should NOT optimistically remove the position (since it remains open with reduced size); row-level loading must be set/cleared correctly
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — Add `onPartialClose` method, import `PartialCloseModalComponent`
- **Success**:
  - Partial close modal opens with position data
  - Market order with `reduceOnly: true` is placed for the specified close size
  - Row-level loading active during submission
  - Success toast: "Partial close submitted for {Asset}"
  - Dashboard refreshes to show reduced position size
  - Error toast on failure; loading cleared
- **Dependencies**: Task 3.1, Task 2.2

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts — add method

// Add to imports:
import { PartialCloseModalComponent, PartialCloseDialogData, PartialCloseResult } from "./positions-table/partial-close-modal/partial-close.modal.component";

// Add to DashboardComponent class:
public onPartialClose(position: Position): void {
  const positionKey = this.positionsTable?.getPositionKey(position) ?? position.asset + position.side;

  if (this.positionsTable?.loadingPositionKeys.has(positionKey)) {
    return;
  }

  this._dialog.open(PartialCloseModalComponent, {
    data: { position } as PartialCloseDialogData,
    width: "400px"
  }).afterClosed().subscribe((result: PartialCloseResult | undefined) => {
    if (result === undefined) {
      return;
    }

    const closeSide: "buy" | "sell" = position.side === "Long" ? "sell" : "buy";

    this.positionsTable?.setLoading(positionKey, true);

    const closeRequest: PlaceOrderRequest = {
      asset: position.asset,
      side: closeSide,
      orderType: "market",
      price: null,
      size: result.closeSize,
      reduceOnly: true,
    };

    this._orderService.placeOrder(closeRequest).subscribe({
      next: () => {
        this.positionsTable?.setLoading(positionKey, false);
        this._notifications.success(`Partial close submitted for ${position.asset}`);
        this._refresh$.next();
      },
      error: () => {
        this.positionsTable?.setLoading(positionKey, false);
      }
    });
  });
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — `onClosePosition` method (confirm → API → toast + refresh pattern)

---

### Task 3.3: Wire reverse position flow in DashboardComponent {#task-33-wire-reverse-position-flow-in-dashboardcomponent}

Add `onReversePosition` handler that uses the existing `ConfirmDialogComponent` to confirm the reversal, then places a market order for 2× the position size on the opposite side. The confirmation dialog shows the current position direction, the new direction, and the order size.

- **Complexity**: Medium
- **Risk Factors**: Must correctly compute 2× size; optimistic UI should remove the old position (since it will flip); if the order fails, the old position must be restored
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — Add `onReversePosition` method
- **Success**:
  - Confirmation dialog shows: "Reverse {Side} {Asset} → {OppositeSide} {Asset}? This will place a market {BuySell} order for {2×Size} {Asset}."
  - Market order for 2× position size placed on opposite side
  - Optimistic position removal + restore on error
  - Row-level loading during submission
  - Success toast; dashboard refreshes to show flipped position
- **Dependencies**: Task 2.2

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts — add method

public onReversePosition(position: Position): void {
  const positionKey = this.positionsTable?.getPositionKey(position) ?? position.asset + position.side;

  if (this.positionsTable?.loadingPositionKeys.has(positionKey)) {
    return;
  }

  const reverseSide: "buy" | "sell" = position.side === "Long" ? "sell" : "buy";
  const newDirection = position.side === "Long" ? "Short" : "Long";
  const reverseSize = Math.abs(position.size) * 2;

  this._dialog.open(ConfirmDialogComponent, {
    data: {
      title: "Reverse Position",
      message: `Reverse ${position.side} ${position.asset} → ${newDirection} ${position.asset}? This will place a market ${reverseSide === "buy" ? "Buy" : "Sell"} order for ${reverseSize.toFixed(4)} ${position.asset} (2× position size).`,
      side: reverseSide,
      orderType: "market" as const,
      asset: position.asset + "-PERP",
      size: reverseSize,
      confirmText: "Reverse Position",
      cancelText: "Cancel"
    },
    width: "450px"
  }).afterClosed().subscribe((confirmed: boolean) => {
    if (!confirmed) {
      return;
    }

    const positionIndex = this.positions.findIndex(
      (item) => item.asset === position.asset && item.side === position.side
    );
    if (positionIndex < 0) {
      return;
    }

    const removedPosition = this.positions[positionIndex];
    this.positionsTable?.setLoading(positionKey, true);
    this._pendingPositionKeys.add(positionKey);
    this.positions = this.positions.filter(
      (item) => !(item.asset === position.asset && item.side === position.side)
    );

    const reverseRequest: PlaceOrderRequest = {
      asset: position.asset,
      side: reverseSide,
      orderType: "market",
      price: null,
      size: reverseSize,
    };

    this._orderService.placeOrder(reverseRequest).subscribe({
      next: () => {
        this._pendingPositionKeys.delete(positionKey);
        this.positionsTable?.setLoading(positionKey, false);
        this._notifications.success(`Position reversed: ${position.asset} is now ${newDirection}`);
        this._refresh$.next();
      },
      error: () => {
        this.positions = [...this.positions, removedPosition];
        this._pendingPositionKeys.delete(positionKey);
        this.positionsTable?.setLoading(positionKey, false);
      }
    });
  });
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — `onClosePosition` method (confirm dialog → optimistic remove → API → restore on error)
- `frontend/trading-ui/src/app/features/order-entry/confirm-dialog/confirm-dialog.component.ts` — existing `ConfirmDialogData` interface with `side`, `orderType`, `asset`, `size` fields

---

### Task 3.4: Frontend build and lint {#task-34-frontend-build-and-lint}

Run the Angular build and lint to verify no compilation or style errors.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (build/lint only)
- **Success**:
  - `npx ng build` succeeds with no errors
  - `npx ng lint` passes with no errors or warnings
- **Dependencies**: Tasks 3.1–3.3

## Phase Success Criteria

- Partial close modal opens with position data, shows percentage quick-select and custom size input
- Partial close submits a market order with `reduceOnly: true` for the specified close size
- Reverse position shows confirmation with 2× size, opposite direction, and places market order
- Both actions use row-level loading and show appropriate toasts
- Frontend compiles and lints without errors
