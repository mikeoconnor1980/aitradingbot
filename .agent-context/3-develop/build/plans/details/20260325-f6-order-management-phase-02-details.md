<!-- markdownlint-disable-file -->

# Task Details: F6 — Order Management

## Phase 2: Frontend — Order Management Service + Modify Modal

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — Standalone components, `inject()` for DI, SCSS with BEM, `ReactiveFormsModule`, `MatDialog` modal pattern, suffix `.modal.component.ts` for modals, explicit `public`/`private` on all members, double quotes for strings
- `.agent-context/0-knowledge/11-angular-instructions.md` — Angular Material dark theme, green primary, CSS custom properties (`--colour-*`), no CommonModule
- `.agent-context/0-knowledge/07-ui-design.md` — Dashboard layout, component structure

## Design References

- F5 establishes `OrderService` in `frontend/trading-ui/src/app/core/services/order.service.ts` with `placeOrder()` method using `ApiRestClient`
- F5 establishes `ConfirmDialogComponent` for confirmation dialogs using `MatDialog`
- `ApiRestClient` already provides `delete<T>(path)` and `put<T>(path, body)` methods
- Modify modal follows the same MatDialog pattern as F5's ConfirmDialogComponent

### Task 2.1: Create ModifyOrderDto TypeScript interface {#task-21-create-modifyorderdto-interface}

Create the TypeScript interface matching the backend `ModifyOrderDto` for the PUT request body.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/core/models/modify-order.model.ts` — new file
- **Success**:
  - Interface has `price` (number) and `size` (number) properties
  - File follows existing model pattern
- **Dependencies**: None

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/models/modify-order.model.ts — new file
export interface ModifyOrderDto {
  price: number;
  size: number;
}
```

##### Pattern References

- `frontend/trading-ui/src/app/core/models/open-order.model.ts` — existing interface pattern

---

### Task 2.2: Add cancel and modify methods to OrderService {#task-22-add-cancel-modify-to-orderservice}

Extend the existing `OrderService` from F5 with `cancelOrder`, `cancelAllOrders`, and `modifyOrder` methods using `ApiRestClient`.

- **Complexity**: Low
- **Risk Factors**: None — straightforward HTTP methods
- **Files**:
  - `frontend/trading-ui/src/app/core/services/order.service.ts` — modification (add methods)
- **Success**:
  - `cancelOrder(orderId: string)` calls `DELETE orders/{orderId}`
  - `cancelAllOrders(asset: string)` calls `DELETE orders?asset={asset}`
  - `modifyOrder(orderId: string, dto: ModifyOrderDto)` calls `PUT orders/{orderId}`
  - All methods return `Observable<void>`
- **Dependencies**:
  - Task 2.1 (ModifyOrderDto)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/services/order.service.ts — modification
// Add to existing OrderService class:

import { ModifyOrderDto } from "../models/modify-order.model";

// ... existing placeOrder method from F5 ...

public cancelOrder(orderId: string): Observable<void> {
  return this._apiClient.delete<void>(`orders/${orderId}`);
}

public cancelAllOrders(asset: string): Observable<void> {
  return this._apiClient.delete<void>(`orders?asset=${encodeURIComponent(asset)}`);
}

public modifyOrder(orderId: string, dto: ModifyOrderDto): Observable<void> {
  return this._apiClient.put<void>(`orders/${orderId}`, dto);
}
```

##### Pattern References

- `frontend/trading-ui/src/app/core/services/api-rest-client.service.ts` — `delete<T>(path)`, `put<T>(path, body)` methods
- F5's `OrderService.placeOrder()` — same service, same pattern

---

### Task 2.3: Create ModifyOrderModalComponent with reactive form {#task-23-create-modify-order-modal}

Create a standalone modal dialog component for modifying an order. Uses `MatDialogRef` for lifecycle, `ReactiveFormsModule` for the form, and validation for price > 0 and size > 0. Pre-populates with current order values passed via `MAT_DIALOG_DATA`.

- **Complexity**: Medium
- **Risk Factors**: First modify modal in the app; must handle form validation UX correctly
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/orders-table/modify-order-modal/modify-order.modal.component.ts` — new file
  - `frontend/trading-ui/src/app/features/dashboard/orders-table/modify-order-modal/modify-order.modal.component.html` — new file
  - `frontend/trading-ui/src/app/features/dashboard/orders-table/modify-order-modal/modify-order.modal.component.scss` — new file
- **Success**:
  - Modal opens with pre-filled price and size from current order
  - Validation prevents submission when price ≤ 0 or size ≤ 0
  - Cancel button closes without action
  - Submit returns `ModifyOrderDto` to the caller via `MatDialogRef.close()`
  - Component is standalone with all required Material imports
- **Dependencies**:
  - Task 2.1 (ModifyOrderDto)
  - F5's MatDialog setup (already available)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/orders-table/modify-order-modal/modify-order.modal.component.ts — new file
import { Component, inject } from "@angular/core";
import { FormBuilder, ReactiveFormsModule, Validators } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from "@angular/material/dialog";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { OpenOrder } from "../../../../core/models/open-order.model";
import { ModifyOrderDto } from "../../../../core/models/modify-order.model";

export interface ModifyOrderDialogData {
  order: OpenOrder;
}

@Component({
  selector: "app-modify-order-modal",
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
  ],
  templateUrl: "./modify-order.modal.component.html",
  styleUrl: "./modify-order.modal.component.scss",
})
export class ModifyOrderModalComponent {
  private readonly _fb = inject(FormBuilder);
  private readonly _dialogRef = inject(MatDialogRef<ModifyOrderModalComponent>);
  private readonly _data: ModifyOrderDialogData = inject(MAT_DIALOG_DATA);

  public readonly form = this._fb.group({
    price: [this._data.order.price, [Validators.required, Validators.min(0.000001)]],
    size: [this._data.order.size, [Validators.required, Validators.min(0.000001)]],
  });

  public readonly order = this._data.order;

  public onCancel(): void {
    this._dialogRef.close();
  }

  public onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const result: ModifyOrderDto = {
      price: this.form.value.price!,
      size: this.form.value.size!,
    };
    this._dialogRef.close(result);
  }
}
```

```html
<!-- frontend/trading-ui/src/app/features/dashboard/orders-table/modify-order-modal/modify-order.modal.component.html — new file -->
<h2 mat-dialog-title>Modify Order</h2>

<mat-dialog-content>
  <p class="modify-order-modal__info">
    {{ order.side }} {{ order.asset }} — Order #{{ order.orderId }}
  </p>

  <form [formGroup]="form" class="modify-order-modal__form">
    <mat-form-field appearance="outline" class="modify-order-modal__field">
      <mat-label>Price</mat-label>
      <input matInput type="number" formControlName="price" step="0.01" />
      @if (form.controls.price.hasError("required")) {
        <mat-error>Price is required</mat-error>
      }
      @if (form.controls.price.hasError("min")) {
        <mat-error>Price must be greater than 0</mat-error>
      }
    </mat-form-field>

    <mat-form-field appearance="outline" class="modify-order-modal__field">
      <mat-label>Size</mat-label>
      <input matInput type="number" formControlName="size" step="0.001" />
      @if (form.controls.size.hasError("required")) {
        <mat-error>Size is required</mat-error>
      }
      @if (form.controls.size.hasError("min")) {
        <mat-error>Size must be greater than 0</mat-error>
      }
    </mat-form-field>
  </form>
</mat-dialog-content>

<mat-dialog-actions align="end">
  <button mat-button (click)="onCancel()">Cancel</button>
  <button mat-flat-button color="primary" (click)="onSubmit()" [disabled]="form.invalid">
    Modify Order
  </button>
</mat-dialog-actions>
```

```scss
// frontend/trading-ui/src/app/features/dashboard/orders-table/modify-order-modal/modify-order.modal.component.scss — new file
.modify-order-modal {
  &__info {
    color: var(--colour-muted);
    margin-bottom: 16px;
    font-size: 0.875rem;
  }

  &__form {
    display: flex;
    flex-direction: column;
    gap: 8px;
    min-width: 300px;
  }

  &__field {
    width: 100%;
  }
}
```

##### Pattern References

- F5's `ConfirmDialogComponent` — MatDialog pattern, `MAT_DIALOG_DATA` injection, `MatDialogRef.close()`
- F5's `OrderEntryComponent` — ReactiveFormsModule form with validation
- `frontend/trading-ui/src/app/features/market-data/market-data.component.ts` — MatFormField/MatInput usage

---

### Task 2.4: Frontend build and lint verification {#task-24-frontend-build-and-lint}

Run the Angular build and lint to verify no compilation or lint errors.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification only)
- **Success**:
  - `npx ng build` completes without errors
  - `npx ng lint` completes without errors
- **Dependencies**:
  - Tasks 2.1–2.3

## Phase Success Criteria

- `ModifyOrderDto` TypeScript interface matches backend DTO
- `OrderService` has `cancelOrder`, `cancelAllOrders`, and `modifyOrder` methods
- `ModifyOrderModalComponent` opens with pre-filled values and validates inputs
- Frontend builds and lints without errors
