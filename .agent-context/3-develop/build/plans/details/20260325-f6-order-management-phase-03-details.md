<!-- markdownlint-disable-file -->

# Task Details: F6 — Order Management

## Phase 3: Frontend — Orders Table Actions + Optimistic UI

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — Standalone components, `inject()` DI, `@Output()` for parent communication, `takeUntilDestroyed` for infinite observables, BEM SCSS, CSS custom properties
- `.agent-context/0-knowledge/11-angular-instructions.md` — Angular Material dark theme, green primary (`--colour-profit`), red for destructive (`--colour-loss`)
- `.agent-context/0-knowledge/07-ui-design.md` — Dashboard layout and component hierarchy

## Design References

- `OrdersTableComponent` currently accepts `@Input() orders` and is purely presentational — F6 adds action buttons, loading states, and event emissions
- `DashboardComponent` owns polling via `_refresh$` Subject — orders-table emits events that trigger `_refresh$.next()` for immediate data refresh
- `MatSnackBar` already injected in `DashboardComponent` — F6 uses it for all toast notifications
- `ConfirmDialogComponent` from F5 is reused for all confirmation dialogs
- `MatMenu` (from `@angular/material/menu`) used for the context menu — new import for this project
- Optimistic UI pattern: mutate local array copy, fire API call, revert on error via `catchError`

### Task 3.1: Add Cancel and Modify action buttons to order table rows {#task-31-add-action-buttons-to-rows}

Add an "Actions" column to the orders table with Cancel and Modify buttons per row. Buttons use Angular Material icon buttons.

- **Complexity**: Medium
- **Risk Factors**: Table layout changes, button styling consistency
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.html` — modification
  - `frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.ts` — modification
  - `frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.scss` — modification
- **Success**:
  - Each order row has Cancel and Modify buttons in an Actions column
  - Buttons are styled as icon buttons with clear labels/tooltips
  - Buttons emit events for the parent to handle
- **Dependencies**: None

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.ts — modification
import { Component, EventEmitter, Input, Output } from "@angular/core";
import { DecimalPipe, NgClass } from "@angular/common";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatTooltipModule } from "@angular/material/tooltip";
import { OpenOrder } from "../../../core/models/open-order.model";

@Component({
  selector: "app-orders-table",
  standalone: true,
  imports: [
    DecimalPipe,
    NgClass,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
  ],
  templateUrl: "./orders-table.component.html",
  styleUrl: "./orders-table.component.scss",
})
export class OrdersTableComponent {
  @Input() public orders: OpenOrder[] = [];

  @Output() public cancelOrder = new EventEmitter<OpenOrder>();
  @Output() public cancelAllOrders = new EventEmitter<void>();
  @Output() public modifyOrder = new EventEmitter<OpenOrder>();

  public loadingOrderIds = new Set<string>();

  public isLoading(orderId: string): boolean {
    return this.loadingOrderIds.has(orderId);
  }

  public setLoading(orderId: string, loading: boolean): void {
    if (loading) {
      this.loadingOrderIds.add(orderId);
    } else {
      this.loadingOrderIds.delete(orderId);
    }
  }

  public onCancelClick(order: OpenOrder): void {
    this.cancelOrder.emit(order);
  }

  public onModifyClick(order: OpenOrder): void {
    this.modifyOrder.emit(order);
  }

  public onCancelAllClick(): void {
    this.cancelAllOrders.emit();
  }

  public getSideClass(side: string): string {
    // ... existing implementation ...
  }
}
```

```html
<!-- frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.html — modification -->
<!-- Add Cancel All button above table and Actions column -->

<div class="orders-table__header">
  <h3 class="orders-table__title">Open Orders</h3>
  @if (orders.length > 0) {
    <button
      mat-stroked-button
      color="warn"
      class="orders-table__cancel-all"
      (click)="onCancelAllClick()"
      [disabled]="loadingOrderIds.size > 0">
      Cancel All ({{ orders.length }})
    </button>
  }
</div>

<!-- Existing table wrapper -->
<div class="orders-table__wrapper">
  @if (orders.length === 0) {
    <p class="orders-table__empty">No open orders</p>
  } @else {
    <table class="orders-table__table">
      <thead>
        <tr>
          <th>Side</th>
          <th>Asset</th>
          <th>Type</th>
          <th>Price</th>
          <th>Size</th>
          <th>Status</th>
          <th>Actions</th>
        </tr>
      </thead>
      <tbody>
        @for (order of orders; track order.orderId) {
          <tr [class.orders-table__row--loading]="isLoading(order.orderId)">
            <td [ngClass]="getSideClass(order.side)">{{ order.side }}</td>
            <td>{{ order.asset }}</td>
            <td>{{ order.orderType }}</td>
            <td>{{ order.price | number: "1.2-2" }}</td>
            <td>{{ order.size | number: "1.4-6" }}</td>
            <td>{{ order.status }}</td>
            <td class="orders-table__actions">
              @if (isLoading(order.orderId)) {
                <mat-spinner diameter="20"></mat-spinner>
              } @else {
                <button
                  mat-icon-button
                  matTooltip="Modify order"
                  (click)="onModifyClick(order)">
                  <mat-icon>edit</mat-icon>
                </button>
                <button
                  mat-icon-button
                  matTooltip="Cancel order"
                  color="warn"
                  (click)="onCancelClick(order)">
                  <mat-icon>close</mat-icon>
                </button>
              }
            </td>
          </tr>
        }
      </tbody>
    </table>
  }
</div>
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.ts` — existing component structure
- `frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.html` — existing table template

---

### Task 3.2: Add Cancel All button above orders table {#task-32-add-cancel-all-button}

The Cancel All button is included in Task 3.1's template update. This task covers the SCSS styling for the header area.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.scss` — modification
- **Success**:
  - Cancel All button is visually distinct (warn/red colour)
  - Header layout with title and button is properly aligned
  - Button shows order count
- **Dependencies**:
  - Task 3.1

#### Implementation Details

```scss
// frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.scss — modification
// Add to existing styles:

.orders-table {
  &__header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 12px;
  }

  &__title {
    color: var(--colour-text-primary);
    margin: 0;
    font-size: 1rem;
  }

  &__cancel-all {
    font-size: 0.75rem;
  }

  &__actions {
    white-space: nowrap;
    text-align: center;
    min-width: 80px;
  }

  &__row--loading {
    opacity: 0.5;
    pointer-events: none;
  }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.scss` — existing BEM SCSS patterns
- `frontend/trading-ui/src/styles.scss` — CSS custom properties (`--colour-*`)

---

### Task 3.3: Implement row-level loading state {#task-33-implement-row-loading-state}

The loading state mechanism (`loadingOrderIds` Set) is included in Task 3.1's component update. This task covers the integration — how the parent (DashboardComponent) or the component itself manages the loading state during API calls.

- **Complexity**: Medium
- **Risk Factors**: Loading state must be synchronised with API call lifecycle; prevent double-clicks
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.ts` — already updated in Task 3.1
- **Success**:
  - When an API call starts for an order, that row shows a spinner and buttons are hidden
  - When the API call completes (success or failure), the row returns to normal
  - Multiple orders can be in loading state simultaneously
  - Double-clicking a button while loading is prevented
- **Dependencies**:
  - Task 3.1

---

### Task 3.4: Add context menu with cancel and modify options {#task-34-add-context-menu}

Add a right-click context menu to each order row using `MatMenu`. The menu provides Cancel and Modify options as an alternative entry point.

- **Complexity**: Medium
- **Risk Factors**: Context menu positioning, preventing default browser context menu
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.ts` — modification (add MatMenuModule import)
  - `frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.html` — modification (add mat-menu)
  - `frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.scss` — modification (context menu styles)
- **Success**:
  - Right-clicking on an order row opens a context menu
  - Menu has "Modify" and "Cancel" options
  - Options are disabled when the row is in loading state
  - Selecting an option triggers the same flow as the button click
- **Dependencies**:
  - Task 3.1 (event emitters)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.ts — modification
// Add MatMenuModule to imports array:
import { MatMenuModule, MatMenuTrigger } from "@angular/material/menu";

// Add to component imports:
// imports: [...existing..., MatMenuModule]

// Add to class:
@ViewChild(MatMenuTrigger) private contextMenuTrigger!: MatMenuTrigger;

public contextMenuOrder: OpenOrder | null = null;
public contextMenuPosition = { x: "0px", y: "0px" };

public onContextMenu(event: MouseEvent, order: OpenOrder): void {
  event.preventDefault();
  this.contextMenuOrder = order;
  this.contextMenuPosition = {
    x: event.clientX + "px",
    y: event.clientY + "px",
  };
  this.contextMenuTrigger.openMenu();
}
```

```html
<!-- Add to orders-table.component.html — after the table -->
<div
  class="orders-table__context-trigger"
  [style.left]="contextMenuPosition.x"
  [style.top]="contextMenuPosition.y"
  [matMenuTriggerFor]="contextMenu">
</div>

<mat-menu #contextMenu="matMenu">
  @if (contextMenuOrder) {
    <button mat-menu-item
      [disabled]="isLoading(contextMenuOrder.orderId)"
      (click)="onModifyClick(contextMenuOrder)">
      <mat-icon>edit</mat-icon>
      <span>Modify Order</span>
    </button>
    <button mat-menu-item
      [disabled]="isLoading(contextMenuOrder.orderId)"
      (click)="onCancelClick(contextMenuOrder)">
      <mat-icon color="warn">close</mat-icon>
      <span>Cancel Order</span>
    </button>
  }
</mat-menu>

<!-- Add (contextmenu) binding to each <tr> -->
<!-- <tr ... (contextmenu)="onContextMenu($event, order)"> -->
```

```scss
// Add to orders-table.component.scss:
.orders-table {
  &__context-trigger {
    position: fixed;
    visibility: hidden;
    width: 0;
    height: 0;
  }
}
```

##### Pattern References

- Angular Material `MatMenu` documentation
- `frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.ts` — existing component

---

### Task 3.5: Wire cancel single order flow with optimistic UI {#task-35-wire-cancel-single-order}

Wire the cancel single order flow in `DashboardComponent`: listen to the `cancelOrder` event from orders-table → show confirmation dialog → optimistic remove → API call → revert on failure → toast.

- **Complexity**: High
- **Risk Factors**: Optimistic update must correctly revert; race conditions with polling refresh
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — modification
  - `frontend/trading-ui/src/app/features/dashboard/dashboard.component.html` — modification
- **Success**:
  - Cancel button triggers confirmation dialog (reuses F5's ConfirmDialogComponent)
  - On confirm: order removed from local array immediately, API call fires
  - On API success: success toast shown
  - On API failure: order reinserted to local array, error toast shown
  - Row loading state active during API call
- **Dependencies**:
  - Tasks 3.1–3.3 (buttons and loading state)
  - F5's ConfirmDialogComponent and OrderService

#### Implementation Details

> **Note**: Add `@ViewChild(OrdersTableComponent) private ordersTable?: OrdersTableComponent;` to `DashboardComponent` to enable loading state control from parent. Import `ViewChild` from `@angular/core`.

```typescript
// frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts — modification
// Add to existing class imports and constructor:
import { MatDialog } from "@angular/material/dialog";
import { OrderService } from "../../core/services/order.service";
import { ConfirmDialogComponent } from "..."; // from F5's location
import { OpenOrder } from "../../core/models/open-order.model";

// Add inject:
private readonly _dialog = inject(MatDialog);
private readonly _orderService = inject(OrderService);

// Add method:
public onCancelOrder(order: OpenOrder): void {
  const dialogRef = this._dialog.open(ConfirmDialogComponent, {
    data: {
      title: "Cancel Order",
      message: `Cancel order #${order.orderId}?`,
      confirmText: "Cancel Order",
      cancelText: "Keep Order",
    },
  });

  dialogRef.afterClosed().subscribe((confirmed: boolean) => {
    if (!confirmed) return;

    // Optimistic removal
    const orderIndex = this.orders.findIndex(o => o.orderId === order.orderId);
    const removedOrder = this.orders[orderIndex];
    this.orders = this.orders.filter(o => o.orderId !== order.orderId);

    // Set loading state on orders-table
    this.ordersTable?.setLoading(order.orderId, true);

    this._orderService.cancelOrder(order.orderId).subscribe({
      next: () => {
        this.ordersTable?.setLoading(order.orderId, false);
        this._snackBar.open("Order cancelled successfully", "Dismiss", { duration: 3000 });
        this._refresh$.next();
      },
      error: (err) => {
        // Revert optimistic removal
        this.orders = [...this.orders.slice(0, orderIndex), removedOrder, ...this.orders.slice(orderIndex)];
        this.ordersTable?.setLoading(order.orderId, false);
        this._snackBar.open(
          `Failed to cancel order: ${err.error?.errorMessage || err.message || "Unknown error"}`,
          "Dismiss",
          { duration: 5000 }
        );
      },
    });
  });
}
```

```html
<!-- frontend/trading-ui/src/app/features/dashboard/dashboard.component.html — modification -->
<!-- Update the orders-table element to bind events: -->
<app-orders-table
  [orders]="orders"
  (cancelOrder)="onCancelOrder($event)"
  (cancelAllOrders)="onCancelAllOrders()"
  (modifyOrder)="onModifyOrder($event)">
</app-orders-table>
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — existing polling and snackbar pattern
- F5's `ConfirmDialogComponent` — dialog data interface and afterClosed() pattern

---

### Task 3.6: Wire cancel all orders flow with optimistic UI {#task-36-wire-cancel-all-orders}

Wire the cancel all orders flow: show confirmation with count → optimistic removal of all orders → API call → revert on failure → toast.

- **Complexity**: Medium
- **Risk Factors**: All orders must be restored on failure; count in dialog must be accurate
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — modification
- **Success**:
  - Cancel All triggers confirmation dialog with order count
  - On confirm: all orders removed from local array, API call fires
  - On success: success toast
  - On failure: all orders restored, error toast
- **Dependencies**:
  - Task 3.5 (cancel single pattern to follow)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts — modification

public onCancelAllOrders(): void {
  const orderCount = this.orders.length;
  if (orderCount === 0) return;

  const dialogRef = this._dialog.open(ConfirmDialogComponent, {
    data: {
      title: "Cancel All Orders",
      message: `Cancel all ${orderCount} open orders for BTC-PERP?`,
      confirmText: "Cancel All",
      cancelText: "Keep Orders",
    },
  });

  dialogRef.afterClosed().subscribe((confirmed: boolean) => {
    if (!confirmed) return;

    // Optimistic removal — save snapshot for revert
    const previousOrders = [...this.orders];
    this.orders = [];

    this._orderService.cancelAllOrders("BTC").subscribe({
      next: () => {
        this._snackBar.open(`Cancelled ${orderCount} orders`, "Dismiss", { duration: 3000 });
        this._refresh$.next();
      },
      error: (err) => {
        // Revert
        this.orders = previousOrders;
        this._snackBar.open(
          `Failed to cancel orders: ${err.error?.errorMessage || err.message || "Unknown error"}`,
          "Dismiss",
          { duration: 5000 }
        );
      },
    });
  });
}
```

##### Pattern References

- Task 3.5 — cancel single flow (same optimistic pattern)

---

### Task 3.7: Wire modify order flow with optimistic UI {#task-37-wire-modify-order}

Wire the modify order flow: open ModifyOrderModal → optimistic update with new values → API call → revert on failure → toast.

- **Complexity**: High
- **Risk Factors**: Optimistic update must replace the correct order; form values must be validated before submission
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — modification
- **Success**:
  - Modify button opens ModifyOrderModalComponent with current values
  - On submit: order updated in local array with new price/size, API call fires
  - On success: success toast
  - On failure: order reverted to original values, error toast
  - Row loading state active during API call
- **Dependencies**:
  - Phase 2 Task 2.3 (ModifyOrderModalComponent)
  - Phase 2 Task 2.2 (OrderService.modifyOrder)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts — modification
import { ModifyOrderModalComponent, ModifyOrderDialogData } from "../orders-table/modify-order-modal/modify-order.modal.component";
import { ModifyOrderDto } from "../../core/models/modify-order.model";

public onModifyOrder(order: OpenOrder): void {
  const dialogRef = this._dialog.open(ModifyOrderModalComponent, {
    data: { order } as ModifyOrderDialogData,
    width: "400px",
  });

  dialogRef.afterClosed().subscribe((result: ModifyOrderDto | undefined) => {
    if (!result) return;

    // Save original for revert
    const orderIndex = this.orders.findIndex(o => o.orderId === order.orderId);
    const originalOrder = { ...this.orders[orderIndex] };

    // Optimistic update
    this.orders = this.orders.map(o =>
      o.orderId === order.orderId
        ? { ...o, price: result.price, size: result.size }
        : o
    );

    this.ordersTable?.setLoading(order.orderId, true);

    this._orderService.modifyOrder(order.orderId, result).subscribe({
      next: () => {
        this.ordersTable?.setLoading(order.orderId, false);
        this._snackBar.open("Order modified successfully", "Dismiss", { duration: 3000 });
        this._refresh$.next();
      },
      error: (err) => {
        // Revert to original
        this.orders = this.orders.map(o =>
          o.orderId === order.orderId ? originalOrder : o
        );
        this.ordersTable?.setLoading(order.orderId, false);
        this._snackBar.open(
          `Failed to modify order: ${err.error?.errorMessage || err.message || "Unknown error"}`,
          "Dismiss",
          { duration: 5000 }
        );
      },
    });
  });
}
```

##### Pattern References

- Task 3.5 — cancel single flow (same optimistic + revert pattern)
- Phase 2 Task 2.3 — ModifyOrderModalComponent dialog data interface

---

### Task 3.8: Wire refresh trigger from orders-table to dashboard {#task-38-wire-refresh-trigger}

Ensure that after any successful cancel or modify operation, the dashboard polling refreshes immediately via `_refresh$.next()`. This is already included in Tasks 3.5–3.7 in the `next` callback. This task verifies the integration works end-to-end.

- **Complexity**: Low
- **Risk Factors**: Race condition between optimistic state and polling refresh
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — verify `_refresh$.next()` calls
- **Success**:
  - After successful cancel/modify, dashboard data refreshes immediately from Hyperliquid
  - Polling timer resets after manual refresh
  - No stale optimistic state after refresh completes
- **Dependencies**:
  - Tasks 3.5–3.7

---

### Task 3.9: Frontend build and lint verification {#task-39-frontend-build-and-lint}

Run the Angular build and lint to verify no compilation or lint errors after all UI changes.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification only)
- **Success**:
  - `npx ng build` completes without errors
  - `npx ng lint` completes without errors
- **Dependencies**:
  - Tasks 3.1–3.8

## Phase Success Criteria

- Cancel button per order row triggers confirmation → optimistic remove → API call → toast
- Cancel All button triggers confirmation with count → optimistic remove all → API call → toast
- Modify button opens pre-filled modal → optimistic update → API call → toast
- Failed API calls revert optimistic changes and show error toast
- Row-level loading state shows spinner and disables buttons during API calls
- Context menu provides cancel and modify options on right-click
- Dashboard refreshes immediately after successful operations
- Frontend builds and lints without errors
