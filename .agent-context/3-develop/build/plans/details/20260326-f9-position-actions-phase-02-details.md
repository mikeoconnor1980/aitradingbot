<!-- markdownlint-disable-file -->

# Task Details: F9 — Position Actions

## Phase 2: Frontend — Actions Menu & TP/SL Modal

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — Standalone components, `inject()` only, modal suffix `.modal.component.ts` / `ModalComponent`, double quotes, `@if`/`@for` control flow, `MatFormField appearance="outline"`, reactive forms
- `.agent-context/0-knowledge/11-angular-instructions.md` — Row-level loading pattern (`Set<string>`, `@ViewChild`), `@Output` emit from child, parent orchestrates dialogs
- `.agent-context/0-knowledge/07-ui-design.md` — Dashboard layout, actions column, colour variables

## Design References

- `ModifyOrderModalComponent` — form dialog pattern (inject MAT_DIALOG_DATA, typed FormGroup, close with result)
- `ConfirmDialogComponent` — generic confirmation with order summary
- `DashboardComponent.onClosePosition` — optimistic UI + pending key guard + row-level loading pattern

---

### Task 2.1: Update Angular models for trigger orders and position enrichment {#task-21-update-angular-models-for-trigger-orders-and-position-enrichment}

Extend the Angular `PlaceOrderRequest` interface and `Position` interface to match the backend changes from Phase 1.

- **Complexity**: Low
- **Risk Factors**: Must match backend DTO property names exactly (camelCase in JSON)
- **Files**:
  - `frontend/trading-ui/src/app/core/models/place-order.model.ts` — Add `triggerPrice`, `reduceOnly`, `tpSlType`
  - `frontend/trading-ui/src/app/core/models/position.model.ts` — Add `marginUsed`, `positionValue`
- **Success**:
  - `PlaceOrderRequest` interface includes optional `triggerPrice`, `reduceOnly`, and `tpSlType`
  - `Position` interface includes `marginUsed` and `positionValue`
  - Existing usages of both interfaces continue to compile
- **Dependencies**: Phase 1 backend changes

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/models/place-order.model.ts — modification
export interface PlaceOrderRequest {
  asset: string;
  side: "buy" | "sell";
  orderType: "market" | "limit" | "stop-market";
  price: number | null;
  size: number;
  triggerPrice?: number;
  reduceOnly?: boolean;
  tpSlType?: "tp" | "sl";
}
```

```typescript
// frontend/trading-ui/src/app/core/models/position.model.ts — modification
export interface Position {
  asset: string;
  size: number;
  side: string;
  entryPrice: number;
  markPrice: number;
  unrealisedPnl: number;
  unrealisedPnlPercent: number;
  liquidationPrice: number;
  leverage: number;
  marginMode: string;
  marginUsed: number;
  positionValue: number;
}
```

##### Pattern References

- `frontend/trading-ui/src/app/core/models/place-order.model.ts` — existing `PlaceOrderRequest` interface
- `frontend/trading-ui/src/app/core/models/position.model.ts` — existing `Position` interface

---

### Task 2.2: Add actions column to positions table {#task-22-add-actions-column-to-positions-table}

Replace the single "Close" button with an actions area containing: a "Close" button, a "TP/SL" button, and a menu button with "Partial Close" and "Reverse" options. Add new `@Output` emitters for each action.

- **Complexity**: Medium
- **Risk Factors**: Must not break existing close-position flow; menu must close correctly; loading state must disable all actions in the row
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts` — Add output emitters, import MatMenuModule, MatIconModule
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html` — Replace actions cell
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.scss` — Style action buttons
- **Success**:
  - Each position row shows: "Close" button, "TP/SL" button, and a "⋮" menu icon with "Partial Close" and "Reverse" items
  - All action buttons emit the corresponding `@Output` event with the `Position`
  - All actions are disabled (replaced with spinner) when the row is in loading state
  - Existing close flow continues to work
- **Dependencies**: None

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts — modification
import { DecimalPipe } from "@angular/common";
import { Component, EventEmitter, Input, Output } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatMenuModule } from "@angular/material/menu";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { Position } from "../../../core/models/position.model";

@Component({
  selector: "app-positions-table",
  standalone: true,
  imports: [DecimalPipe, MatButtonModule, MatIconModule, MatMenuModule, MatProgressSpinnerModule],
  templateUrl: "./positions-table.component.html",
  styleUrl: "./positions-table.component.scss"
})
export class PositionsTableComponent {
  @Input()
  public positions: Position[] = [];

  @Output()
  public closePosition = new EventEmitter<Position>();

  @Output()
  public setTpSl = new EventEmitter<Position>();

  @Output()
  public partialClose = new EventEmitter<Position>();

  @Output()
  public reversePosition = new EventEmitter<Position>();

  // ... existing loadingPositionKeys, getPositionKey, isLoading, setLoading, getPnlClass methods unchanged ...

  public onCloseClick(position: Position): void {
    if (this.isLoading(position)) { return; }
    this.closePosition.emit(position);
  }

  public onTpSlClick(position: Position): void {
    if (this.isLoading(position)) { return; }
    this.setTpSl.emit(position);
  }

  public onPartialCloseClick(position: Position): void {
    if (this.isLoading(position)) { return; }
    this.partialClose.emit(position);
  }

  public onReverseClick(position: Position): void {
    if (this.isLoading(position)) { return; }
    this.reversePosition.emit(position);
  }
}
```

```html
<!-- frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html -->
<!-- Replace the actions <td> cell content -->
<td class="positions-table__actions">
  @if (isLoading(position)) {
    <mat-spinner diameter="20" aria-label="Processing"></mat-spinner>
  } @else {
    <button
      mat-stroked-button
      type="button"
      color="warn"
      aria-label="Close position"
      (click)="onCloseClick(position)">
      Close
    </button>
    <button
      mat-stroked-button
      type="button"
      aria-label="Set take-profit / stop-loss"
      (click)="onTpSlClick(position)">
      TP/SL
    </button>
    <button
      mat-icon-button
      type="button"
      [matMenuTriggerFor]="positionMenu"
      aria-label="More actions">
      <mat-icon>more_vert</mat-icon>
    </button>
    <mat-menu #positionMenu="matMenu">
      <button mat-menu-item (click)="onPartialCloseClick(position)">
        <mat-icon>content_cut</mat-icon>
        <span>Partial Close</span>
      </button>
      <button mat-menu-item (click)="onReverseClick(position)">
        <mat-icon>swap_vert</mat-icon>
        <span>Reverse</span>
      </button>
    </mat-menu>
  }
</td>
```

```scss
// frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.scss — add
.positions-table__actions {
  text-align: right;
  white-space: nowrap;
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 0.25rem;

  mat-spinner {
    margin: 0 0 0 auto;
  }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts` — existing component with `@Output` emit pattern
- `frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.html` — `mat-icon-button` + `[matMenuTriggerFor]` pattern

---

### Task 2.3: Create TpSlModalComponent {#task-23-create-tpslmodalcomponent}

Create a new modal component for setting Take Profit and Stop Loss prices. The modal displays the position's entry price and mark price for reference, has two form fields (TP price and SL price — both optional but at least one required), and validates TP/SL price direction relative to position side.

- **Complexity**: High
- **Risk Factors**: Validation logic depends on position side (long/short); form must support setting TP only, SL only, or both simultaneously; result must map to `PlaceOrderRequest[]`
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/tp-sl-modal/tp-sl.modal.component.ts` — New file
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/tp-sl-modal/tp-sl.modal.component.html` — New file
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/tp-sl-modal/tp-sl.modal.component.scss` — New file
- **Success**:
  - Modal shows entry price and mark price for reference
  - TP price field validates: above entry for longs, below entry for shorts
  - SL price field validates: below entry for longs, above entry for shorts
  - At least one of TP or SL must be filled before submit is enabled
  - Modal closes with `TpSlResult` containing the filled price(s) and position reference
  - Form uses `mat-form-field appearance="outline"` with inline `@if` error messages
- **Dependencies**: Task 2.1

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/positions-table/tp-sl-modal/tp-sl.modal.component.ts — new file
import { DecimalPipe } from "@angular/common";
import { Component, inject } from "@angular/core";
import { AbstractControl, FormBuilder, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from "@angular/material/dialog";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { Position } from "../../../../core/models/position.model";

export interface TpSlDialogData {
  position: Position;
}

export interface TpSlResult {
  takeProfitPrice: number | null;
  stopLossPrice: number | null;
  position: Position;
}

interface TpSlForm {
  takeProfitPrice: FormControl<number | null>;
  stopLossPrice: FormControl<number | null>;
}

@Component({
  selector: "app-tp-sl-modal",
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    DecimalPipe
  ],
  templateUrl: "./tp-sl.modal.component.html",
  styleUrl: "./tp-sl.modal.component.scss"
})
export class TpSlModalComponent {
  private readonly _fb = inject(FormBuilder);
  private readonly _dialogRef = inject(MatDialogRef<TpSlModalComponent>);
  private readonly _data: TpSlDialogData = inject(MAT_DIALOG_DATA);

  public readonly position = this._data.position;
  public readonly isLong = this.position.side === "Long";

  public readonly form: FormGroup<TpSlForm> = this._fb.group<TpSlForm>({
    takeProfitPrice: this._fb.control<number | null>(null, [this._tpPriceValidator.bind(this)]),
    stopLossPrice: this._fb.control<number | null>(null, [this._slPriceValidator.bind(this)]),
  });

  public get isSubmitDisabled(): boolean {
    return (this.form.controls.takeProfitPrice.value === null &&
            this.form.controls.stopLossPrice.value === null) ||
           this.form.invalid;
  }

  public onCancel(): void {
    this._dialogRef.close();
  }

  public onSubmit(): void {
    if (this.isSubmitDisabled) {
      this.form.markAllAsTouched();
      return;
    }

    const result: TpSlResult = {
      takeProfitPrice: this.form.controls.takeProfitPrice.value,
      stopLossPrice: this.form.controls.stopLossPrice.value,
      position: this.position,
    };

    this._dialogRef.close(result);
  }

  private _tpPriceValidator(control: AbstractControl): ValidationErrors | null {
    const value = control.value;
    if (value === null || value === undefined || value === "") {
      return null; // optional field
    }
    if (value <= 0) {
      return { min: true };
    }
    if (this.isLong && value <= this.position.entryPrice) {
      return { tpDirection: true };
    }
    if (!this.isLong && value >= this.position.entryPrice) {
      return { tpDirection: true };
    }
    return null;
  }

  private _slPriceValidator(control: AbstractControl): ValidationErrors | null {
    const value = control.value;
    if (value === null || value === undefined || value === "") {
      return null; // optional field
    }
    if (value <= 0) {
      return { min: true };
    }
    if (this.isLong && value >= this.position.entryPrice) {
      return { slDirection: true };
    }
    if (!this.isLong && value <= this.position.entryPrice) {
      return { slDirection: true };
    }
    return null;
  }
}
```

```html
<!-- frontend/trading-ui/src/app/features/dashboard/positions-table/tp-sl-modal/tp-sl.modal.component.html — new file -->
<h2 mat-dialog-title>Set TP/SL</h2>

<mat-dialog-content>
  <div class="tp-sl-modal__info">
    <div class="tp-sl-modal__row">
      <span class="tp-sl-modal__label">Position</span>
      <span [class]="isLong ? 'side--long' : 'side--short'">{{ position.side }} {{ position.asset }}</span>
    </div>
    <div class="tp-sl-modal__row">
      <span class="tp-sl-modal__label">Entry Price</span>
      <span>{{ position.entryPrice | number: "1.2-2" }}</span>
    </div>
    <div class="tp-sl-modal__row">
      <span class="tp-sl-modal__label">Mark Price</span>
      <span>{{ position.markPrice | number: "1.2-2" }}</span>
    </div>
  </div>

  <form [formGroup]="form" class="tp-sl-modal__form">
    <mat-form-field appearance="outline" class="tp-sl-modal__field">
      <mat-label>Take Profit Price</mat-label>
      <input matInput type="number" formControlName="takeProfitPrice" step="0.01"
             [placeholder]="isLong ? 'Above ' + position.entryPrice : 'Below ' + position.entryPrice" />
      @if (form.controls.takeProfitPrice.hasError("min")) {
        <mat-error>Price must be greater than 0</mat-error>
      }
      @if (form.controls.takeProfitPrice.hasError("tpDirection")) {
        <mat-error>
          @if (isLong) {
            TP must be above entry price ({{ position.entryPrice | number: "1.2-2" }})
          } @else {
            TP must be below entry price ({{ position.entryPrice | number: "1.2-2" }})
          }
        </mat-error>
      }
    </mat-form-field>

    <mat-form-field appearance="outline" class="tp-sl-modal__field">
      <mat-label>Stop Loss Price</mat-label>
      <input matInput type="number" formControlName="stopLossPrice" step="0.01"
             [placeholder]="isLong ? 'Below ' + position.entryPrice : 'Above ' + position.entryPrice" />
      @if (form.controls.stopLossPrice.hasError("min")) {
        <mat-error>Price must be greater than 0</mat-error>
      }
      @if (form.controls.stopLossPrice.hasError("slDirection")) {
        <mat-error>
          @if (isLong) {
            SL must be below entry price ({{ position.entryPrice | number: "1.2-2" }})
          } @else {
            SL must be above entry price ({{ position.entryPrice | number: "1.2-2" }})
          }
        </mat-error>
      }
    </mat-form-field>
  </form>
</mat-dialog-content>

<mat-dialog-actions align="end">
  <button mat-button type="button" (click)="onCancel()">Cancel</button>
  <button mat-flat-button color="primary" type="button" (click)="onSubmit()" [disabled]="isSubmitDisabled">
    Set TP/SL
  </button>
</mat-dialog-actions>
```

```scss
// frontend/trading-ui/src/app/features/dashboard/positions-table/tp-sl-modal/tp-sl.modal.component.scss — new file
.tp-sl-modal {
  &__info {
    margin-bottom: 1.5rem;
  }

  &__row {
    display: flex;
    justify-content: space-between;
    padding: 0.25rem 0;
  }

  &__label {
    color: var(--colour-label);
  }

  &__form {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
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

- `frontend/trading-ui/src/app/features/dashboard/orders-table/modify-order-modal/modify-order.modal.component.ts` — form dialog pattern (inject MAT_DIALOG_DATA, typed FormGroup, close with result)
- `frontend/trading-ui/src/app/features/dashboard/orders-table/modify-order-modal/modify-order.modal.component.html` — `mat-form-field appearance="outline"` with inline `@if` error messages

---

### Task 2.4: Wire TP/SL flow in DashboardComponent {#task-24-wire-tpsl-flow-in-dashboardcomponent}

Add `onSetTpSl` handler in `DashboardComponent` that opens the `TpSlModalComponent`, processes the result into one or two `PlaceOrderRequest`s, submits them via `OrderService.placeOrder()`, and shows appropriate toasts. Wire the `(setTpSl)` output from `PositionsTableComponent` in the template.

- **Complexity**: High
- **Risk Factors**: Must handle placing two orders (TP + SL) with proper error handling; must use row-level loading during submission. **Note**: `forkJoin` errors on any single failure — if one order succeeds and the other fails, the generic error toast does not inform the user about the partial success. Consider wrapping each observable with `catchError` to track and report individual order results (e.g., "TP placed, SL failed for {Asset}")
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — Add `onSetTpSl` method, import `TpSlModalComponent`
  - `frontend/trading-ui/src/app/features/dashboard/dashboard.component.html` — Bind `(setTpSl)` output
- **Success**:
  - TP/SL modal opens with position data when "TP/SL" button is clicked
  - One or two stop-market orders are placed via `OrderService.placeOrder()`
  - Row-level loading state is active during submission
  - Success toast shows "TP/SL orders placed for {Asset}"
  - Error toast on failure; row loading cleared
  - Orders appear in the Orders tab after refresh
- **Dependencies**: Tasks 2.2, 2.3

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts — add method

// Add to imports at top of file:
import { TpSlModalComponent, TpSlDialogData, TpSlResult } from "./positions-table/tp-sl-modal/tp-sl.modal.component";
import { forkJoin } from "rxjs";

// Add to DashboardComponent class:
public onSetTpSl(position: Position): void {
  const positionKey = this.positionsTable?.getPositionKey(position) ?? position.asset + position.side;

  if (this.positionsTable?.loadingPositionKeys.has(positionKey)) {
    return;
  }

  this._dialog.open(TpSlModalComponent, {
    data: { position } as TpSlDialogData,
    width: "450px"
  }).afterClosed().subscribe((result: TpSlResult | undefined) => {
    if (result === undefined) {
      return;
    }

    const closeSide: "buy" | "sell" = position.side === "Long" ? "sell" : "buy";
    const orders: PlaceOrderRequest[] = [];

    if (result.takeProfitPrice !== null) {
      orders.push({
        asset: position.asset,
        side: closeSide,
        orderType: "stop-market",
        price: null,
        size: Math.abs(position.size),
        triggerPrice: result.takeProfitPrice,
        reduceOnly: true,
        tpSlType: "tp",
      });
    }

    if (result.stopLossPrice !== null) {
      orders.push({
        asset: position.asset,
        side: closeSide,
        orderType: "stop-market",
        price: null,
        size: Math.abs(position.size),
        triggerPrice: result.stopLossPrice,
        reduceOnly: true,
        tpSlType: "sl",
      });
    }

    if (orders.length === 0) {
      return;
    }

    this.positionsTable?.setLoading(positionKey, true);

    const orderRequests$ = orders.map((order) => this._orderService.placeOrder(order));

    forkJoin(orderRequests$).subscribe({
      next: () => {
        this.positionsTable?.setLoading(positionKey, false);
        this._notifications.success(`TP/SL orders placed for ${position.asset}`);
        this._refresh$.next();
      },
      error: () => {
        this.positionsTable?.setLoading(positionKey, false);
        this._notifications.error(`Failed to place TP/SL orders for ${position.asset}`);
      }
    });
  });
}
```

```html
<!-- frontend/trading-ui/src/app/features/dashboard/dashboard.component.html — modification -->
<!-- Add (setTpSl) binding to app-positions-table -->
<app-positions-table
  [positions]="positions"
  (closePosition)="onClosePosition($event)"
  (setTpSl)="onSetTpSl($event)"
  (partialClose)="onPartialClose($event)"
  (reversePosition)="onReversePosition($event)">
</app-positions-table>
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — `onClosePosition` method (optimistic UI + row loading + dialog open pattern)
- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — `onModifyOrder` method (dialog → API → toast pattern)

---

### Task 2.5: Frontend build and lint {#task-25-frontend-build-and-lint}

Run the Angular build and lint to verify no compilation or style errors.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (build/lint only)
- **Success**:
  - `npx ng build` succeeds with no errors
  - `npx ng lint` passes with no errors or warnings
- **Dependencies**: Tasks 2.1–2.4

## Phase Success Criteria

- Angular `PlaceOrderRequest` and `Position` models are extended with new fields
- Positions table shows TP/SL, Partial Close, and Reverse action buttons per row
- TP/SL modal opens with position reference data, validates TP/SL price directions, and submits stop-market orders
- Row-level loading prevents duplicate submissions
- Frontend compiles and lints without errors
