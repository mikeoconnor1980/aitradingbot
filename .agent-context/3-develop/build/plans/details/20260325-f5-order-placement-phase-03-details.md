<!-- markdownlint-disable-file -->

# Task Details: F5 — Order Placement

## Phase 3: Angular Order Entry UI

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — Standalone components, `inject()` DI, explicit return types, double quotes, SCSS BEM, new control flow syntax (`@if`, `@for`), `takeUntilDestroyed`
- `.agent-context/0-knowledge/11-angular-instructions.md` — Angular 19 patterns, polling, service patterns
- `.agent-context/0-knowledge/07-ui-design.md` — Dashboard layout, component conventions

## Design References

### Order Entry as a New Route

The PBI specifies "Order Entry tab" — this is a new route in the application navigation (`/order-entry`), not embedded in the dashboard. The order entry form is a standalone feature component.

### Material Components to Introduce

| Component | Usage |
|-----------|-------|
| `MatButtonToggleModule` | Buy/Sell side toggle |
| `MatFormFieldModule` | Form field wrappers (already used in market-data) |
| `MatSelectModule` | Order type selector (already used in market-data) |
| `MatInputModule` | Price and size number inputs (new) |
| `ReactiveFormsModule` | Reactive form for order entry (new) |
| `MatDialogModule` | Confirmation dialog (new) |
| `MatSnackBarModule` | Success/error feedback (already used in dashboard) |
| `MatButtonModule` | Submit button (already used) |
| `MatProgressSpinnerModule` | Loading state during submission (already used) |

### Mid Price Pre-population

The limit order price field is pre-populated with `MarketInfo.midPrice` from `MarketDataService.getMarketInfo("BTC-PERP")`. The service already exists from F3.

---

### Task 3.1: Create TypeScript models {#task-31-create-typescript-models}

Create TypeScript interfaces for order placement request/response matching the backend API contract.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/core/models/place-order.model.ts` — New file: request/response interfaces
- **Success**:
  - Interfaces match the backend DTOs exactly
  - Export all types for use in services and components
- **Dependencies**: None

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/models/place-order.model.ts — new file
export interface PlaceOrderRequest {
  asset: string;
  side: "buy" | "sell";
  orderType: "market" | "limit";
  price: number | null;
  size: number;
}

export interface PlaceOrderResponse {
  success: boolean;
  orderId: string | null;
  status: string | null;
  detail: string | null;
}

export interface TestSignResponse {
  domainSeparator: string;
  typeHash: string;
  messageHash: string;
  signature: SignatureInfo;
}

export interface SignatureInfo {
  v: number;
  r: string;
  s: string;
}
```

##### Pattern References

- `frontend/trading-ui/src/app/core/models/open-order.model.ts` — Existing model interface pattern
- `frontend/trading-ui/src/app/core/models/market-info.model.ts` — Existing model with typed fields

---

### Task 3.2: Create OrderService {#task-32-create-orderservice}

Create an Angular service for order placement and test-sign API calls using `ApiRestClient`.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/core/services/order.service.ts` — New file
- **Success**:
  - `placeOrder(request)` calls `POST /api/orders`
  - `testSign()` calls `POST /api/orders/test-sign`
  - Uses `ApiRestClient` (not raw `HttpClient`)
- **Dependencies**: Task 3.1 (models)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/services/order.service.ts — new file
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { ApiRestClient } from "./api-rest-client.service";
import { PlaceOrderRequest, PlaceOrderResponse, TestSignResponse } from "../models/place-order.model";

@Injectable({ providedIn: "root" })
export class OrderService {
  private readonly _apiClient = inject(ApiRestClient);

  public placeOrder(request: PlaceOrderRequest): Observable<PlaceOrderResponse> {
    return this._apiClient.post<PlaceOrderResponse>("orders", request);
  }

  public testSign(): Observable<TestSignResponse> {
    return this._apiClient.post<TestSignResponse>("orders/test-sign", {});
  }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/core/services/market-data.service.ts` — Service using `ApiRestClient` pattern
- `frontend/trading-ui/src/app/core/services/api-rest-client.service.ts` — Generic REST wrapper (`post<T>` method)

---

### Task 3.3: Create ConfirmDialogComponent {#task-33-create-confirmdialogcomponent}

Create a confirmation dialog component using Angular Material `MatDialog`. The dialog displays order summary (side, type, asset, price, size) and provides Confirm/Cancel actions.

- **Complexity**: Medium
- **Risk Factors**: `MatDialog` is new to the project — no existing dialog pattern to follow
- **Files**:
  - `frontend/trading-ui/src/app/features/order-entry/confirm-dialog/confirm-dialog.component.ts` — New file
  - `frontend/trading-ui/src/app/features/order-entry/confirm-dialog/confirm-dialog.component.html` — New file
  - `frontend/trading-ui/src/app/features/order-entry/confirm-dialog/confirm-dialog.component.scss` — New file
- **Success**:
  - Dialog displays order summary with all required fields
  - Confirm button returns `true`, Cancel returns `false`
  - Dialog uses Material dark theme (inherited from global theme)
  - Buy orders show green accent, Sell orders show red accent
- **Dependencies**: None

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/order-entry/confirm-dialog/confirm-dialog.component.ts — new file
import { Component, inject } from "@angular/core";
import { CommonModule, DecimalPipe } from "@angular/common";
import { MatDialogModule, MAT_DIALOG_DATA, MatDialogRef } from "@angular/material/dialog";
import { MatButtonModule } from "@angular/material/button";

export interface ConfirmDialogData {
  side: "buy" | "sell";
  orderType: "market" | "limit";
  asset: string;
  price: number | null;
  size: number;
}

@Component({
  selector: "app-confirm-dialog",
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatButtonModule, DecimalPipe],
  templateUrl: "./confirm-dialog.component.html",
  styleUrl: "./confirm-dialog.component.scss"
})
export class ConfirmDialogComponent {
  public readonly data: ConfirmDialogData = inject(MAT_DIALOG_DATA);
  private readonly _dialogRef = inject(MatDialogRef<ConfirmDialogComponent>);

  public onConfirm(): void {
    this._dialogRef.close(true);
  }

  public onCancel(): void {
    this._dialogRef.close(false);
  }

  public getSideClass(): string {
    return this.data.side === "buy" ? "confirm-dialog__side--buy" : "confirm-dialog__side--sell";
  }
}
```

```html
<!-- frontend/trading-ui/src/app/features/order-entry/confirm-dialog/confirm-dialog.component.html — new file -->
<h2 mat-dialog-title>Confirm Order</h2>

<mat-dialog-content>
  <div class="confirm-dialog__summary">
    <div class="confirm-dialog__row">
      <span class="confirm-dialog__label">Side</span>
      <span class="confirm-dialog__value" [class]="getSideClass()">
        {{ data.side | uppercase }}
      </span>
    </div>

    <div class="confirm-dialog__row">
      <span class="confirm-dialog__label">Type</span>
      <span class="confirm-dialog__value">{{ data.orderType | titlecase }}</span>
    </div>

    <div class="confirm-dialog__row">
      <span class="confirm-dialog__label">Asset</span>
      <span class="confirm-dialog__value">{{ data.asset }}</span>
    </div>

    @if (data.orderType === "limit" && data.price !== null) {
      <div class="confirm-dialog__row">
        <span class="confirm-dialog__label">Price</span>
        <span class="confirm-dialog__value">{{ data.price | number:'1.2-2' }}</span>
      </div>
    }

    <div class="confirm-dialog__row">
      <span class="confirm-dialog__label">Size</span>
      <span class="confirm-dialog__value">{{ data.size }}</span>
    </div>
  </div>
</mat-dialog-content>

<mat-dialog-actions align="end">
  <button mat-button (click)="onCancel()">Cancel</button>
  <button mat-flat-button color="primary" (click)="onConfirm()">Confirm Order</button>
</mat-dialog-actions>
```

```scss
// frontend/trading-ui/src/app/features/order-entry/confirm-dialog/confirm-dialog.component.scss — new file
.confirm-dialog {
  &__summary {
    display: flex;
    flex-direction: column;
    gap: 12px;
    padding: 8px 0;
  }

  &__row {
    display: flex;
    justify-content: space-between;
    align-items: center;
  }

  &__label {
    color: var(--colour-label);
    font-size: 14px;
  }

  &__value {
    font-weight: 500;
    font-size: 16px;
  }

  &__side {
    &--buy {
      color: var(--colour-profit);
    }

    &--sell {
      color: var(--colour-loss);
    }
  }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — Material component imports pattern
- `frontend/trading-ui/src/styles.scss` — CSS custom properties (`--colour-profit`, `--colour-loss`, `--colour-label`)

---

### Task 3.4: Create OrderEntryComponent {#task-34-create-orderentrycomponent}

Create the main order entry component with a reactive form containing: side toggle (Buy/Sell), order type selector (Market/Limit), price field (limit only, pre-populated with mid price), and size field. Includes confirmation dialog before submission and success/error feedback via MatSnackBar.

- **Complexity**: High
- **Risk Factors**: Reactive form validation; conditional price field; mid price pre-population timing; dialog → service → feedback flow
- **Files**:
  - `frontend/trading-ui/src/app/features/order-entry/order-entry.component.ts` — New file
  - `frontend/trading-ui/src/app/features/order-entry/order-entry.component.html` — New file
  - `frontend/trading-ui/src/app/features/order-entry/order-entry.component.scss` — New file
- **Success**:
  - Buy/Sell toggle with visual distinction (green/red)
  - Market/Limit selector toggles price field visibility
  - Limit price pre-populated with current mid price from MarketDataService
  - Size field with number input
  - Submit triggers confirmation dialog, then calls OrderService on confirm
  - Success: snackbar with order details
  - Error: snackbar with full error payload
  - Loading state during submission (spinner, disabled form)
- **Dependencies**: Tasks 3.1, 3.2, 3.3

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/order-entry/order-entry.component.ts — new file
import { Component, DestroyRef, OnInit, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { CommonModule } from "@angular/common";
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from "@angular/forms";
import { MatButtonToggleModule } from "@angular/material/button-toggle";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatSelectModule } from "@angular/material/select";
import { MatInputModule } from "@angular/material/input";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatDialog, MatDialogModule } from "@angular/material/dialog";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { OrderService } from "../../core/services/order.service";
import { MarketDataService } from "../../core/services/market-data.service";
import { PlaceOrderRequest } from "../../core/models/place-order.model";
import { ConfirmDialogComponent, ConfirmDialogData } from "./confirm-dialog/confirm-dialog.component";

@Component({
  selector: "app-order-entry",
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    MatButtonModule,
    MatCardModule,
    MatDialogModule,
    MatSnackBarModule,
    MatProgressSpinnerModule
  ],
  templateUrl: "./order-entry.component.html",
  styleUrl: "./order-entry.component.scss"
})
export class OrderEntryComponent implements OnInit {
  private readonly _fb = inject(FormBuilder);
  private readonly _orderService = inject(OrderService);
  private readonly _marketDataService = inject(MarketDataService);
  private readonly _dialog = inject(MatDialog);
  private readonly _snackBar = inject(MatSnackBar);
  private readonly _destroyRef = inject(DestroyRef);

  public orderForm!: FormGroup;
  public isSubmitting = false;
  public midPrice: number | null = null;

  public ngOnInit(): void {
    this.orderForm = this._fb.group({
      side: ["buy", Validators.required],
      orderType: ["limit", Validators.required],
      price: [null as number | null],
      size: [null as number | null, [Validators.required, Validators.min(0.000001)]]
    });

    // Watch order type to toggle price field
    this.orderForm.get("orderType")!.valueChanges
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((type: string) => {
        const priceControl = this.orderForm.get("price")!;
        if (type === "limit") {
          priceControl.setValidators([Validators.required, Validators.min(0.01)]);
          if (this.midPrice !== null) {
            priceControl.setValue(this.midPrice);
          }
        } else {
          priceControl.clearValidators();
          priceControl.setValue(null);
        }
        priceControl.updateValueAndValidity();
      });

    // Fetch mid price for pre-population (finite HTTP observable — no takeUntilDestroyed needed)
    this._marketDataService.getMarketInfo("BTC-PERP")
      .subscribe({
        next: (info) => {
          this.midPrice = info.midPrice;
          if (this.orderForm.get("orderType")!.value === "limit" && !this.orderForm.get("price")!.value) {
            this.orderForm.get("price")!.setValue(this.midPrice);
          }
        }
      });
  }

  public isLimitOrder(): boolean {
    return this.orderForm.get("orderType")?.value === "limit";
  }

  public onSubmit(): void {
    if (this.orderForm.invalid || this.isSubmitting) {
      return;
    }

    const formValue = this.orderForm.value;
    const dialogData: ConfirmDialogData = {
      side: formValue.side,
      orderType: formValue.orderType,
      asset: "BTC-PERP",
      price: formValue.orderType === "limit" ? formValue.price : null,
      size: formValue.size
    };

    const dialogRef = this._dialog.open(ConfirmDialogComponent, {
      data: dialogData,
      width: "400px"
    });

    dialogRef.afterClosed()
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((confirmed: boolean) => {
        if (confirmed) {
          this.submitOrder(formValue);
        }
      });
  }

  private submitOrder(formValue: Record<string, unknown>): void {
    this.isSubmitting = true;

    const request: PlaceOrderRequest = {
      asset: "BTC-PERP",
      side: formValue["side"] as "buy" | "sell",
      orderType: formValue["orderType"] as "market" | "limit",
      price: formValue["orderType"] === "limit" ? formValue["price"] as number : null,
      size: formValue["size"] as number
    };

    this._orderService.placeOrder(request)
      .subscribe({
        next: (response) => {
          this.isSubmitting = false;
          if (response.success) {
            this._snackBar.open(
              `Order placed successfully (ID: ${response.orderId}, Status: ${response.status})`,
              "Dismiss",
              { duration: 5000 }
            );
          } else {
            this._snackBar.open(
              `Order rejected: ${response.detail}`,
              "Dismiss",
              { duration: 8000 }
            );
          }
        },
        error: (err) => {
          this.isSubmitting = false;
          this._snackBar.open(
            `Order submission failed: ${err.message || "Unknown error"}`,
            "Dismiss",
            { duration: 8000 }
          );
        }
      });
  }
}
```

```html
<!-- frontend/trading-ui/src/app/features/order-entry/order-entry.component.html — new file -->
<div class="order-entry">
  <mat-card class="order-entry__card">
    <mat-card-header>
      <mat-card-title>Place Order — BTC-PERP</mat-card-title>
    </mat-card-header>

    <mat-card-content>
      <form [formGroup]="orderForm" (ngSubmit)="onSubmit()" class="order-entry__form">

        <!-- Side Toggle -->
        <div class="order-entry__field">
          <label class="order-entry__label">Side</label>
          <mat-button-toggle-group formControlName="side" class="order-entry__side-toggle">
            <mat-button-toggle value="buy" class="order-entry__side-toggle--buy">Buy</mat-button-toggle>
            <mat-button-toggle value="sell" class="order-entry__side-toggle--sell">Sell</mat-button-toggle>
          </mat-button-toggle-group>
        </div>

        <!-- Order Type -->
        <div class="order-entry__field">
          <mat-form-field appearance="outline" class="order-entry__input">
            <mat-label>Order Type</mat-label>
            <mat-select formControlName="orderType">
              <mat-option value="limit">Limit</mat-option>
              <mat-option value="market">Market</mat-option>
            </mat-select>
          </mat-form-field>
        </div>

        <!-- Price (limit only) -->
        @if (isLimitOrder()) {
          <div class="order-entry__field">
            <mat-form-field appearance="outline" class="order-entry__input">
              <mat-label>Price (USD)</mat-label>
              <input matInput type="number" formControlName="price" step="0.01">
            </mat-form-field>
          </div>
        }

        <!-- Size -->
        <div class="order-entry__field">
          <mat-form-field appearance="outline" class="order-entry__input">
            <mat-label>Size (BTC)</mat-label>
            <input matInput type="number" formControlName="size" step="0.001">
          </mat-form-field>
        </div>

        <!-- Submit -->
        <div class="order-entry__actions">
          @if (isSubmitting) {
            <mat-spinner diameter="36"></mat-spinner>
          } @else {
            <button mat-flat-button color="primary" type="submit"
                    [disabled]="orderForm.invalid">
              Submit Order
            </button>
          }
        </div>

      </form>
    </mat-card-content>
  </mat-card>
</div>
```

```scss
// frontend/trading-ui/src/app/features/order-entry/order-entry.component.scss — new file
.order-entry {
  display: flex;
  justify-content: center;
  padding: 24px;

  &__card {
    width: 100%;
    max-width: 480px;
  }

  &__form {
    display: flex;
    flex-direction: column;
    gap: 16px;
    padding-top: 16px;
  }

  &__field {
    display: flex;
    flex-direction: column;
  }

  &__label {
    color: var(--colour-label);
    font-size: 14px;
    margin-bottom: 8px;
  }

  &__input {
    width: 100%;
  }

  &__side-toggle {
    width: 100%;

    .mat-button-toggle {
      flex: 1;
      text-align: center;
    }

    &--buy.mat-button-toggle-checked {
      background-color: var(--colour-profit);
      color: white;
    }

    &--sell.mat-button-toggle-checked {
      background-color: var(--colour-loss);
      color: white;
    }
  }

  &__actions {
    display: flex;
    justify-content: center;
    padding-top: 8px;

    button {
      width: 100%;
      height: 48px;
      font-size: 16px;
    }
  }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/market-data/market-data.component.ts` — MatFormField/MatSelect pattern
- `frontend/trading-ui/src/app/features/market-data/market-data.component.html` — `appearance="outline"` form fields
- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — MatSnackBar feedback pattern
- `frontend/trading-ui/src/app/core/services/market-data.service.ts` — `getMarketInfo()` for midPrice

---

### Task 3.5: Add route and navigation {#task-35-add-route-and-navigation}

Register the new Order Entry route and add a navigation link to the app shell.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/app.routes.ts` — Add lazy-loaded route
  - `frontend/trading-ui/src/app/app.component.html` — Add nav link
- **Success**:
  - `/order-entry` route loads `OrderEntryComponent`
  - Navigation bar includes "Order Entry" link
  - Link active state works correctly
- **Dependencies**: Task 3.4 (component)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/app.routes.ts — modification
// Add BEFORE the default redirect ({ path: "", redirectTo: "dashboard" }) and wildcard ({ path: "**" }) routes.
// If inserted after the wildcard, the route will be unreachable.

{
  path: "order-entry",
  loadComponent: () => import("./features/order-entry/order-entry.component").then(m => m.OrderEntryComponent),
  title: "Order Entry"
},
```

```html
<!-- frontend/trading-ui/src/app/app.component.html — modification -->
<!-- Add new nav link alongside existing links (Dashboard, Market Data, Connection): -->

<a routerLink="/order-entry" routerLinkActive="app-shell__link--active" class="app-shell__link">
  Order Entry
</a>
```

##### Pattern References

- `frontend/trading-ui/src/app/app.routes.ts` — Existing lazy-loaded route pattern
- `frontend/trading-ui/src/app/app.component.html` — Existing nav link pattern

---

### Task 3.6: Frontend build and lint {#task-36-frontend-build-and-lint}

Verify the Angular application builds and lints without errors after all Phase 3 changes.

- **Complexity**: Low
- **Risk Factors**: New Material module imports may need additional Angular Material sub-packages
- **Files**: None (verification only)
- **Success**:
  - `ng build` succeeds without errors or warnings
  - `ng lint` passes without violations
  - Application loads in browser and navigates to Order Entry tab
- **Dependencies**: All previous Phase 3 tasks

Run:
```bash
cd frontend/trading-ui
npx ng build
npx ng lint
```

---

## Phase Success Criteria

- Order Entry route loads at `/order-entry` with navigation link
- Reactive form has Buy/Sell toggle, Market/Limit selector, Price field (conditional), Size field
- Limit order price field pre-populated with mid price from MarketDataService
- Confirmation dialog shows order summary before submission
- OrderService calls `POST /api/orders` on confirmation
- Success feedback via MatSnackBar with order ID and status
- Error feedback via MatSnackBar with full Hyperliquid error payload
- Loading spinner during order submission
- Frontend builds and lints without errors
