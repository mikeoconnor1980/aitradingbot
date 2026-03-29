<!-- markdownlint-disable-file -->

# Task Details: Stop Loss & Take Profit

## Phase 3: Frontend — Positions Table SL/TP Management

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — standalone components, `inject()`, typed reactive forms, BEM SCSS, smart/dumb split, MatDialog modals
- `.agent-context/0-knowledge/07-ui-design.md` — dashboard UI features
- Angular Material: `MatDialog`, `MatFormField`, `MatInput`, `MatSnackBar`

## Design References

- **Smart/dumb split**: `DashboardComponent` (smart) owns service calls and dialog opening; `PositionsTableComponent` (dumb) receives `@Input` data and emits `@Output` events
- **Dialog pattern**: `ModifyOrderModalComponent` — inject `MAT_DIALOG_DATA`, typed reactive form, return DTO on close
- **Positions table expand pattern**: `expandedPositionKeys: Set<string>` with `@if (isDetailsExpanded(position))` for expandable detail rows

---

### Task 3.1: Extend Position and OpenOrder models with SL/TP fields {#task-31-extend-position-and-openorder-models-with-sltp-fields}

Add SL/TP-related fields to the Position model (matching the enriched backend `PositionDto`) and trigger fields to the OpenOrder model.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/core/models/position.model.ts` — modification
  - `frontend/trading-ui/src/app/core/models/open-order.model.ts` — modification
- **Success**:
  - `Position` has `stopLossPrice`, `stopLossOrderId`, `takeProfitPrice`, `takeProfitOrderId` (all optional/nullable)
  - `OpenOrder` has `triggerPrice`, `tpslType`, `isReduceOnly` fields
  - No build errors

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/models/position.model.ts — modification
export interface Position {
  // ... existing fields ...
  stopLossPrice: number | null;
  stopLossOrderId: string | null;
  takeProfitPrice: number | null;
  takeProfitOrderId: string | null;
}
```

```typescript
// frontend/trading-ui/src/app/core/models/open-order.model.ts — modification
export interface OpenOrder {
  // ... existing fields ...
  triggerPrice: number | null;
  tpslType: string | null;     // "sl" | "tp" | null
  isReduceOnly: boolean;
}
```

##### Pattern References

- `frontend/trading-ui/src/app/core/models/position.model.ts` — existing interface
- `frontend/trading-ui/src/app/core/models/open-order.model.ts` — existing interface

---

### Task 3.2: Add trigger order API methods to OrderService {#task-32-add-trigger-order-api-methods-to-orderservice}

Add `placeTriggerOrder`, `modifyTriggerOrder`, and `cancelTriggerOrder` methods to the `OrderService`.

- **Complexity**: Low
- **Risk Factors**: None — follows existing service method patterns exactly
- **Files**:
  - `frontend/trading-ui/src/app/core/services/order.service.ts` — modification
  - `frontend/trading-ui/src/app/core/models/trigger-order.model.ts` — new file (request/response models)
- **Success**:
  - `placeTriggerOrder(request)` calls `POST /api/orders/trigger`
  - `modifyTriggerOrder(orderId, dto)` calls `PUT /api/orders/trigger/{orderId}`
  - `cancelTriggerOrder(orderId)` calls `DELETE /api/orders/trigger/{orderId}`
  - No build errors
- **Dependencies**:
  - Task 3.1 (model types)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/models/trigger-order.model.ts — new file
export interface PlaceTriggerOrderRequest {
  asset: string;
  side: 'buy' | 'sell';
  size: number;
  triggerPrice: number;
  tpslType: 'sl' | 'tp';
}

export interface ModifyTriggerOrderDto {
  triggerPrice: number;
  size: number;
}
```

```typescript
// frontend/trading-ui/src/app/core/services/order.service.ts — modification
// Add these methods:

  public placeTriggerOrder(request: PlaceTriggerOrderRequest): Observable<PlaceOrderResponse> {
    return this._apiClient.post<PlaceOrderResponse>('orders/trigger', request);
  }

  public modifyTriggerOrder(orderId: string, dto: ModifyTriggerOrderDto): Observable<void> {
    return this._apiClient.put<void>(`orders/trigger/${orderId}`, dto);
  }

  public cancelTriggerOrder(orderId: string): Observable<void> {
    return this._apiClient.delete<void>(`orders/trigger/${orderId}`);
  }
```

##### Pattern References

- `frontend/trading-ui/src/app/core/services/order.service.ts` — existing `placeOrder`, `cancelOrder`, `modifyOrder` methods
- `frontend/trading-ui/src/app/core/services/api-rest-client.service.ts` — `post`, `put`, `delete` methods

---

### Task 3.3: Display SL/TP columns in positions table {#task-33-display-sltp-columns-in-positions-table}

Add SL and TP display to the positions table. Show SL/TP prices as clickable values (for inline edit) or a "Set SL/TP" button when none are set.

- **Complexity**: Medium
- **Risk Factors**: Table column count changes — update any hardcoded `colspan` values. UX decision: SL/TP in main row vs expandable details row.
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html` — modification
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts` — modification
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.scss` — modification
- **Success**:
  - SL column shows stop loss price with red-tinted text (or "—" if not set)
  - TP column shows take profit price with green-tinted text (or "—" if not set)
  - "Set SL/TP" button appears when position has no SL or TP
  - Clicking SL/TP value emits an event for inline edit
  - `colspan` updated for details row
  - No build errors
- **Dependencies**:
  - Task 3.1 (Position model fields)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts — modification
// Add outputs:
@Output() setSlTp = new EventEmitter<Position>();
@Output() editSlTp = new EventEmitter<{ position: Position; field: 'sl' | 'tp'; newPrice?: number }>();
@Output() removeSlTp = new EventEmitter<{ position: Position; field: 'sl' | 'tp' }>();
```

```html
<!-- frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html — modification -->
<!-- Add columns to the header row (before Actions column): -->
<th class="positions-table__header">SL</th>
<th class="positions-table__header">TP</th>

<!-- Add cells in the data row (matching header position): -->
<td class="positions-table__cell positions-table__cell--sl">
  @if (position.stopLossPrice) {
    <span class="positions-table__sl-price"
          title="Stop loss price">
      {{ position.stopLossPrice | number:'1.2-2' }}
    </span>
    <button class="positions-table__remove-btn"
            (click)="removeSlTp.emit({ position, field: 'sl' })"
            title="Remove stop loss">×</button>
  } @else {
    <span class="positions-table__no-value">—</span>
  }
</td>

<td class="positions-table__cell positions-table__cell--tp">
  @if (position.takeProfitPrice) {
    <span class="positions-table__tp-price"
          title="Take profit price">
      {{ position.takeProfitPrice | number:'1.2-2' }}
    </span>
    <button class="positions-table__remove-btn"
            (click)="removeSlTp.emit({ position, field: 'tp' })"
            title="Remove take profit">×</button>
  } @else {
    <span class="positions-table__no-value">—</span>
  }
</td>

<!-- Note: Click-to-edit behavior on SL/TP prices is added in Task 3.5 (inline editing) -->

<!-- Add "Set SL/TP" button in the actions area when neither SL nor TP is set: -->
@if (!position.stopLossPrice && !position.takeProfitPrice) {
  <button class="positions-table__action-btn"
          (click)="setSlTp.emit(position)">
    Set SL/TP
  </button>
}

<!-- Update any hardcoded colspan to account for 2 new columns -->
```

```scss
// frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.scss — modification
.positions-table {
  &__sl-price {
    color: var(--colour-loss);
    cursor: pointer;
    &:hover { text-decoration: underline; }
  }

  &__tp-price {
    color: var(--colour-profit);
    cursor: pointer;
    &:hover { text-decoration: underline; }
  }

  &__remove-btn {
    background: none;
    border: none;
    color: var(--colour-muted);
    cursor: pointer;
    font-size: 0.8rem;
    margin-left: 0.25rem;
    padding: 0 0.25rem;
    &:hover { color: var(--colour-loss); }
  }

  &__no-value {
    color: var(--colour-muted);
  }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html` — existing table column structure
- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts` — existing `@Output` pattern for `closePosition`

---

### Task 3.4: Create Set SL/TP dialog component {#task-34-create-set-sltp-dialog-component}

Create a new dialog component for setting SL and TP on an existing position. Follows the `ModifyOrderModalComponent` pattern.

- **Complexity**: Medium
- **Risk Factors**: Validation must account for position side (long vs short) to validate SL/TP direction. Entry price comes from position data.
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/set-sltp-modal/set-sltp.modal.component.ts` — new file
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/set-sltp-modal/set-sltp.modal.component.html` — new file
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/set-sltp-modal/set-sltp.modal.component.scss` — new file
- **Success**:
  - Dialog opens with position context (asset, side, entry price, current SL/TP)
  - SL and TP inputs with validation (correct side relative to entry price)
  - Warning if SL is beyond liquidation price
  - On confirm, returns `{ stopLossPrice: number | null, takeProfitPrice: number | null }` result
  - Cancel closes dialog without result
  - No build errors
- **Dependencies**:
  - Task 3.3 (setSlTp output wired)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/positions-table/set-sltp-modal/set-sltp.modal.component.ts — new file
import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormControl, FormGroup, ReactiveFormsModule, ValidatorFn, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { Position } from '../../../../core/models/position.model';

export interface SetSlTpDialogData {
  position: Position;
}

export interface SetSlTpResult {
  stopLossPrice: number | null;
  takeProfitPrice: number | null;
}

interface SetSlTpForm {
  stopLossPrice: FormControl<number | null>;
  takeProfitPrice: FormControl<number | null>;
}

@Component({
  selector: 'app-set-sltp-modal',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
  ],
  templateUrl: './set-sltp.modal.component.html',
  styleUrl: './set-sltp.modal.component.scss',
})
export class SetSlTpModalComponent {
  private readonly _fb = inject(FormBuilder);
  private readonly _dialogRef = inject(MatDialogRef<SetSlTpModalComponent>);
  protected readonly data: SetSlTpDialogData = inject(MAT_DIALOG_DATA);

  protected readonly form: FormGroup<SetSlTpForm>;
  protected readonly isLong: boolean;
  protected readonly liquidationWarning = false;

  constructor() {
    this.isLong = this.data.position.side.toLowerCase() === 'long'
                  || this.data.position.size > 0;

    this.form = this._fb.group<SetSlTpForm>({
      stopLossPrice: this._fb.control<number | null>(
        this.data.position.stopLossPrice,
        [this.createSlValidator()]
      ),
      takeProfitPrice: this._fb.control<number | null>(
        this.data.position.takeProfitPrice,
        [this.createTpValidator()]
      ),
    });
  }

  protected onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const result: SetSlTpResult = {
      stopLossPrice: this.form.controls.stopLossPrice.value,
      takeProfitPrice: this.form.controls.takeProfitPrice.value,
    };
    this._dialogRef.close(result);
  }

  protected onCancel(): void {
    this._dialogRef.close();
  }

  protected isSlBeyondLiquidation(): boolean {
    const sl = this.form.controls.stopLossPrice.value;
    if (sl == null || !this.data.position.liquidationPrice) return false;
    if (this.isLong) return sl <= this.data.position.liquidationPrice;
    return sl >= this.data.position.liquidationPrice;
  }

  private createSlValidator(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const slPrice = control.value;
      if (slPrice == null) return null;
      const entry = this.data.position.entryPrice;
      if (this.isLong && slPrice >= entry) {
        return { slInvalidSide: 'Stop loss must be below entry price for long positions' };
      }
      if (!this.isLong && slPrice <= entry) {
        return { slInvalidSide: 'Stop loss must be above entry price for short positions' };
      }
      return null;
    };
  }

  private createTpValidator(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const tpPrice = control.value;
      if (tpPrice == null) return null;
      const entry = this.data.position.entryPrice;
      if (this.isLong && tpPrice <= entry) {
        return { tpInvalidSide: 'Take profit must be above entry price for long positions' };
      }
      if (!this.isLong && tpPrice >= entry) {
        return { tpInvalidSide: 'Take profit must be below entry price for short positions' };
      }
      return null;
    };
  }
}
```

```html
<!-- frontend/trading-ui/src/app/features/dashboard/positions-table/set-sltp-modal/set-sltp.modal.component.html — new file -->
<h2 mat-dialog-title>Set SL/TP — {{ data.position.asset }}</h2>

<mat-dialog-content>
  <div class="set-sltp__info">
    <span class="set-sltp__label">Side:</span>
    <span [class]="isLong ? 'set-sltp__long' : 'set-sltp__short'">
      {{ isLong ? 'Long' : 'Short' }}
    </span>
    <span class="set-sltp__label">Entry:</span>
    <span>{{ data.position.entryPrice | number:'1.2-2' }}</span>
    <span class="set-sltp__label">Size:</span>
    <span>{{ data.position.size | number:'1.4-4' }}</span>
  </div>

  <form [formGroup]="form" class="set-sltp__form">
    <mat-form-field appearance="outline" class="set-sltp__input">
      <mat-label>Stop Loss Price (USD)</mat-label>
      <input matInput type="number" formControlName="stopLossPrice" step="0.01" />
      @if (form.controls.stopLossPrice.hasError('slInvalidSide')) {
        <mat-error>{{ form.controls.stopLossPrice.getError('slInvalidSide') }}</mat-error>
      }
    </mat-form-field>

    @if (isSlBeyondLiquidation()) {
      <div class="set-sltp__liq-warning">
        ⚠ Stop loss is beyond your liquidation price (${{ data.position.liquidationPrice | number:'1.2-2' }})
      </div>
    }

    <mat-form-field appearance="outline" class="set-sltp__input">
      <mat-label>Take Profit Price (USD)</mat-label>
      <input matInput type="number" formControlName="takeProfitPrice" step="0.01" />
      @if (form.controls.takeProfitPrice.hasError('tpInvalidSide')) {
        <mat-error>{{ form.controls.takeProfitPrice.getError('tpInvalidSide') }}</mat-error>
      }
    </mat-form-field>
  </form>
</mat-dialog-content>

<mat-dialog-actions align="end">
  <button mat-button (click)="onCancel()">Cancel</button>
  <button mat-flat-button color="primary" (click)="onSubmit()">Confirm</button>
</mat-dialog-actions>
```

```scss
// frontend/trading-ui/src/app/features/dashboard/positions-table/set-sltp-modal/set-sltp.modal.component.scss — new file
.set-sltp {
  &__info {
    display: grid;
    grid-template-columns: auto 1fr;
    gap: 0.25rem 0.75rem;
    margin-bottom: 1rem;
    font-size: 0.85rem;
  }

  &__label {
    color: var(--colour-muted);
  }

  &__long { color: var(--colour-profit); }
  &__short { color: var(--colour-loss); }

  &__form {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
  }

  &__input {
    width: 100%;
  }

  &__liq-warning {
    color: #f59e0b;
    font-size: 0.75rem;
    padding: 0.25rem 0.5rem;
    border-left: 2px solid #f59e0b;
    margin-bottom: 0.5rem;
  }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/orders-table/modify-order-modal/modify-order.modal.component.ts` — dialog with `MAT_DIALOG_DATA`, typed form, `markAllAsTouched`, close with result
- `frontend/trading-ui/src/app/features/dashboard/orders-table/modify-order-modal/modify-order.modal.component.html` — dialog template pattern

---

### Task 3.5: Add inline editing for existing SL/TP values {#task-35-add-inline-editing-for-existing-sltp-values}

Allow clicking an existing SL or TP price in the positions table to activate inline editing. A small input field replaces the displayed price, and pressing Enter or blur confirms the change.

- **Complexity**: Medium
- **Risk Factors**: State management for active inline edit — need to track which position/field is being edited. Must prevent conflicting edits.
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts` — modification
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html` — modification
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.scss` — modification
- **Success**:
  - Clicking an SL/TP value switches to an inline input
  - Enter key confirms the edit (emits event)
  - Escape key or blur cancels the edit
  - Only one inline edit active at a time
  - No build errors
- **Dependencies**:
  - Task 3.3 (SL/TP displayed)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts — modification
// Add inline edit state and methods:

protected inlineEdit: { positionKey: string; field: 'sl' | 'tp'; value: number } | null = null;

protected startInlineEdit(position: Position, field: 'sl' | 'tp'): void {
  const currentValue = field === 'sl' ? position.stopLossPrice : position.takeProfitPrice;
  if (currentValue == null) return;

  this.inlineEdit = {
    positionKey: position.asset,
    field,
    value: currentValue,
  };
}

protected confirmInlineEdit(position: Position): void {
  if (!this.inlineEdit) return;

  this.editSlTp.emit({
    position,
    field: this.inlineEdit.field,
    newPrice: this.inlineEdit.value,
  });
  this.inlineEdit = null;
}

protected cancelInlineEdit(): void {
  this.inlineEdit = null;
}

protected isInlineEditing(position: Position, field: 'sl' | 'tp'): boolean {
  return this.inlineEdit?.positionKey === position.asset
      && this.inlineEdit?.field === field;
}

// Update the editSlTp output to include newPrice:
@Output() editSlTp = new EventEmitter<{ position: Position; field: 'sl' | 'tp'; newPrice?: number }>();
```

```html
<!-- In the SL cell, replace the click handler to use inline edit: -->
<td class="positions-table__cell positions-table__cell--sl">
  @if (isInlineEditing(position, 'sl')) {
    <input class="positions-table__inline-input"
           type="number"
           step="0.01"
           [(ngModel)]="inlineEdit!.value"
           (keydown.enter)="confirmInlineEdit(position)"
           (keydown.escape)="cancelInlineEdit()"
           (blur)="cancelInlineEdit()"
           #slInput />
  } @else if (position.stopLossPrice) {
    <span class="positions-table__sl-price"
          (click)="startInlineEdit(position, 'sl')"
          title="Click to edit stop loss">
      {{ position.stopLossPrice | number:'1.2-2' }}
    </span>
    <button class="positions-table__remove-btn"
            (click)="removeSlTp.emit({ position, field: 'sl' })"
            title="Remove stop loss">×</button>
  } @else {
    <span class="positions-table__no-value">—</span>
  }
</td>
<!-- Same pattern for TP cell -->
```

Note: Import `FormsModule` in the component's `imports` array for `ngModel` on the inline input.

```scss
// positions-table.component.scss — modification
.positions-table {
  &__inline-input {
    background: var(--colour-bg-input, #1e293b);
    border: 1px solid var(--colour-label);
    color: inherit;
    width: 80px;
    padding: 0.15rem 0.25rem;
    font-size: 0.8rem;
    border-radius: 3px;
    &:focus { outline: 1px solid var(--colour-label); }
  }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts` — existing expandable row state management pattern
- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html` — existing `@if`/`@else` conditional rendering

---

### Task 3.6: Wire SL/TP actions in dashboard component {#task-36-wire-sltp-actions-in-dashboard-component}

Handle the `setSlTp`, `editSlTp`, and `removeSlTp` events from `PositionsTableComponent` in the smart `DashboardComponent`. Open the dialog, call API, handle errors, and update state.

- **Complexity**: High
- **Risk Factors**: Optimistic state updates — must revert on API failure. Multiple API calls for set SL+TP (two trigger order placements).
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — modification
  - `frontend/trading-ui/src/app/features/dashboard/dashboard.component.html` — modification
- **Success**:
  - "Set SL/TP" button opens the dialog and places trigger orders on confirm
  - Inline edit triggers a modify trigger order API call
  - Remove SL/TP triggers a cancel trigger order API call
  - Success/error notifications displayed via `NotificationService`
  - Positions refresh after SL/TP changes
  - No build errors
- **Dependencies**:
  - Task 3.2 (OrderService trigger methods)
  - Task 3.3 (PositionsTable outputs)
  - Task 3.4 (SetSlTpModalComponent)
  - Task 3.5 (inline edit outputs)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts — modification
// Import SetSlTpModalComponent and trigger order types

// Add handler for setSlTp event:
protected onSetSlTp(position: Position): void {
  const dialogRef = this._dialog.open(SetSlTpModalComponent, {
    data: { position } as SetSlTpDialogData,
    width: '400px',
  });

  dialogRef.afterClosed().subscribe((result: SetSlTpResult | undefined) => {
    if (!result) return;

    // Determine the closing side (opposite of position side)
    const closingSide: 'buy' | 'sell' = position.side.toLowerCase() === 'long' || position.size > 0
      ? 'sell' : 'buy';
    const size = Math.abs(position.size);

    const requests: Observable<any>[] = [];

    if (result.stopLossPrice != null) {
      requests.push(this._orderService.placeTriggerOrder({
        asset: position.asset,
        side: closingSide,
        size,
        triggerPrice: result.stopLossPrice,
        tpslType: 'sl',
      }));
    }

    if (result.takeProfitPrice != null) {
      requests.push(this._orderService.placeTriggerOrder({
        asset: position.asset,
        side: closingSide,
        size,
        triggerPrice: result.takeProfitPrice,
        tpslType: 'tp',
      }));
    }

    if (requests.length === 0) return;

    forkJoin(requests).subscribe({
      next: () => {
        this._notifications.success('SL/TP set successfully');
        this.refreshPositions();
      },
      error: (err) => {
        this._notifications.error('Failed to set SL/TP');
        this.refreshPositions();
      },
    });
  });
}

// Add handler for editSlTp (inline edit confirmed):
protected onEditSlTp(event: { position: Position; field: 'sl' | 'tp'; newPrice?: number }): void {
  if (event.newPrice == null) return;

  const orderId = event.field === 'sl'
    ? event.position.stopLossOrderId
    : event.position.takeProfitOrderId;

  if (!orderId) return;

  this._orderService.modifyTriggerOrder(orderId, {
    triggerPrice: event.newPrice,
    size: Math.abs(event.position.size),
  }).subscribe({
    next: () => {
      this._notifications.success(`${event.field === 'sl' ? 'Stop loss' : 'Take profit'} updated`);
      this.refreshPositions();
    },
    error: () => {
      this._notifications.error(`Failed to update ${event.field === 'sl' ? 'stop loss' : 'take profit'}`);
      this.refreshPositions();
    },
  });
}

// Add handler for removeSlTp:
protected onRemoveSlTp(event: { position: Position; field: 'sl' | 'tp' }): void {
  const orderId = event.field === 'sl'
    ? event.position.stopLossOrderId
    : event.position.takeProfitOrderId;

  if (!orderId) return;

  this._orderService.cancelTriggerOrder(orderId).subscribe({
    next: () => {
      this._notifications.success(`${event.field === 'sl' ? 'Stop loss' : 'Take profit'} removed`);
      this.refreshPositions();
    },
    error: () => {
      this._notifications.error(`Failed to remove ${event.field === 'sl' ? 'stop loss' : 'take profit'}`);
      this.refreshPositions();
    },
  });
}
```

```html
<!-- frontend/trading-ui/src/app/features/dashboard/dashboard.component.html — modification -->
<!-- Add event bindings to the positions-table component: -->
<app-positions-table
  [positions]="positions()"
  [equity]="equity()"
  (closePosition)="onClosePosition($event)"
  (setSlTp)="onSetSlTp($event)"
  (editSlTp)="onEditSlTp($event)"
  (removeSlTp)="onRemoveSlTp($event)">
</app-positions-table>
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — existing dialog-open-and-handle pattern for `ModifyOrderModalComponent`, existing `refreshPositions()` and `_notifications` usage
- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.html` — existing `(closePosition)` event binding pattern

---

### Task 3.7: Add SL/TP removal (cancel trigger order) {#task-37-add-sltp-removal-cancel-trigger-order}

Ensure the "×" remove button on SL/TP values works end-to-end: emits event from positions table, dashboard handles cancellation, and position refreshes.

- **Complexity**: Low
- **Risk Factors**: None — the remove handler is implemented in Task 3.6; this task verifies the full flow works.
- **Files**:
  - No new files — verification of Task 3.3 remove button + Task 3.6 handler wiring
- **Success**:
  - Clicking "×" next to an SL price cancels the trigger order on the exchange
  - Position row updates to show "—" for the removed SL/TP
  - Success notification displayed
  - Error notification displayed if cancel fails

---

### Task 3.8: Build frontend and lint {#task-38-build-frontend-and-lint}

Build the Angular frontend and run linting to verify no errors after all Phase 3 changes.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification only)
- **Success**:
  - `npx ng build` succeeds with no errors
  - `npx ng lint` passes with no errors
- **Dependencies**:
  - All previous tasks in Phase 3

## Phase Success Criteria

- Positions table displays SL/TP columns with trigger order prices
- "Set SL/TP" button opens dialog for positions without SL/TP
- Dialog validates SL/TP direction relative to entry price
- Liquidation price warning appears when SL is beyond liquidation
- Inline editing of existing SL/TP values works (click → input → Enter to confirm)
- "×" button removes SL/TP by cancelling the trigger order
- Success/error notifications display for all SL/TP operations
- Positions refresh after SL/TP changes
- Frontend builds and lints cleanly
