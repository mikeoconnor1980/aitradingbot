<!-- markdownlint-disable-file -->

# Task Details: F10 — Positions Table UX Enhancements

## Phase 3: Close All Positions

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — standalone components, `inject()` DI, explicit access modifiers and return types, `@if`/`@for` control flow, BEM SCSS naming, double-quoted strings
- `.agent-context/0-knowledge/11-angular-instructions.md` — row-level loading pattern, `globalLoading`, optimistic UI + rollback
- `.agent-context/0-knowledge/22-prelaunch-checklist.md` — emergency flatten endpoint is planned; Close All is the user-facing equivalent

## Design References

- **Sequential dispatch**: Hyperliquid nonce = `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()`. Parallel requests collide. Use RxJS `concat` to dispatch close orders one at a time, with `scan` to accumulate results for progress tracking.
- **Existing patterns**: `DashboardComponent.onCancelAllOrders()` — dialog → optimistic clear → API call → restore on error. `DashboardComponent.onClosePosition()` — side-flip logic, `OrderService.placeOrder()`.
- **ConfirmDialogComponent** — reusable for simple confirmations. For Close All, a **new dialog** is needed to show the position list and progress indicator.
- **Close order API**: `POST /api/orders` with `{ asset, side: closeSide, orderType: "market", price: null, size: Math.abs(position.size) }`. Side flip: `"Long"` → `"sell"`, `"Short"` → `"buy"`.

### Task 3.1: Create `CloseAllDialogComponent` with position list and progress {#task-31-create-close-all-dialog-component}

Create a new dialog component that lists all positions to be closed and shows a progress indicator during execution.

- **Complexity**: High
- **Risk Factors**: Dialog must handle three states (confirmation, in-progress, completed); progress tracking with partial failures; RxJS sequential dispatch pattern
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/close-all-dialog/close-all-dialog.component.ts` — new file
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/close-all-dialog/close-all-dialog.component.html` — new file
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/close-all-dialog/close-all-dialog.component.scss` — new file
- **Success**:
  - Dialog accepts `positions: Position[]` via `MAT_DIALOG_DATA`
  - Confirmation state: lists all positions with asset, side, and size
  - On confirm, dispatches close orders sequentially via injected callback
  - Progress state: "Closing positions... X/N" with `mat-progress-bar` (determinate, percentage)
  - On complete, dialog closes and returns `CloseAllResult` with succeeded/failed counts
  - On partial failure, results show which positions failed
  - User cannot dismiss dialog while closing is in progress (`disableClose: true` during execution)
- **Dependencies**: None

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/positions-table/close-all-dialog/close-all-dialog.component.ts — new file
import { Component, inject } from "@angular/core";
import { DecimalPipe } from "@angular/common";
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from "@angular/material/dialog";
import { MatButtonModule } from "@angular/material/button";
import { MatProgressBarModule } from "@angular/material/progress-bar";
import { MatIconModule } from "@angular/material/icon";
import { Position } from "../../../../core/models/position.model";

export interface CloseAllDialogData {
  readonly positions: Position[];
}

export interface CloseAllResult {
  readonly confirmed: boolean;
  readonly succeeded: number;
  readonly failed: number;
  readonly total: number;
}

@Component({
  selector: "app-close-all-dialog",
  standalone: true,
  imports: [DecimalPipe, MatDialogModule, MatButtonModule, MatProgressBarModule, MatIconModule],
  templateUrl: "./close-all-dialog.component.html",
  styleUrl: "./close-all-dialog.component.scss",
})
export class CloseAllDialogComponent {
  private readonly _dialogRef = inject(MatDialogRef<CloseAllDialogComponent>);
  private readonly _data: CloseAllDialogData = inject(MAT_DIALOG_DATA);

  public readonly positions: Position[] = this._data.positions;

  public get total(): number {
    return this.positions.length;
  }

  public onCancel(): void {
    this._dialogRef.close({ confirmed: false, succeeded: 0, failed: 0, total: this.total } as CloseAllResult);
  }

  public onConfirm(): void {
    this._dialogRef.close({ confirmed: true, succeeded: 0, failed: 0, total: this.total } as CloseAllResult);
  }
}
```

**Note**: The implementation approach follows the existing Cancel All pattern — the dialog confirms intent and closes, then the parent (`DashboardComponent`) orchestrates the actual close operations. Progress is tracked in the dashboard component and reported via the notification service. This avoids complex dialog-to-parent communication during async operations.

Simplified approach for the dialog — just confirm or cancel:

```html
<!-- close-all-dialog.component.html — new file -->
<h2 mat-dialog-title>Close All Positions</h2>

<mat-dialog-content>
  <p>Are you sure you want to close all {{ total }} position{{ total !== 1 ? 's' : '' }}?</p>

  <div class="close-all-dialog__positions-list">
    @for (position of positions; track position.asset + position.side) {
      <div class="close-all-dialog__position-row">
        <span class="close-all-dialog__asset">{{ position.asset }}</span>
        <span class="close-all-dialog__side"
              [class.side--long]="position.side === 'Long'"
              [class.side--short]="position.side === 'Short'">
          {{ position.side }}
        </span>
        <span class="close-all-dialog__size">{{ position.size | number }}</span>
      </div>
    }
  </div>
</mat-dialog-content>

<mat-dialog-actions align="end">
  <button mat-button (click)="onCancel()">Keep Positions</button>
  <button mat-flat-button color="warn" (click)="onConfirm()">Close All</button>
</mat-dialog-actions>
```

```scss
// close-all-dialog.component.scss — new file
.close-all-dialog {
  &__positions-list {
    margin-top: 12px;
    max-height: 240px;
    overflow-y: auto;
    border: 1px solid var(--colour-border-subtle);
    border-radius: 4px;
  }

  &__position-row {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 8px 12px;
    border-bottom: 1px solid var(--colour-border-subtle);

    &:last-child {
      border-bottom: none;
    }
  }

  &__asset {
    font-weight: 500;
    min-width: 60px;
  }

  &__side {
    font-size: 0.85rem;
    min-width: 50px;
    text-align: center;
  }

  &__size {
    color: var(--colour-muted);
    min-width: 80px;
    text-align: right;
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

- `frontend/trading-ui/src/app/features/order-entry/confirm-dialog/confirm-dialog.component.ts` — `MAT_DIALOG_DATA` inject pattern, `MatDialogRef`, dialog template structure
- `frontend/trading-ui/src/app/features/order-entry/confirm-dialog/confirm-dialog.component.html` — dialog layout with title, content, actions
- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — `_dialog.open(ConfirmDialogComponent, { data, width })` usage

### Task 3.2: Add Close All button and output to `PositionsTableComponent` {#task-32-add-close-all-button-and-output}

Add a "Close All Positions" button in the toolbar (visible only when positions exist, disabled during loading) and emit a new `closeAllPositions` output.

- **Complexity**: Low
- **Risk Factors**: Button visibility/disabled logic must account for `globalLoading` state and empty positions
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts` — add `@Output closeAllPositions`, `globalLoading`, `setGlobalLoading()`
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html` — add button in toolbar
- **Success**:
  - "Close All Positions" button visible only when `positions.length > 0`
  - Button disabled when `globalLoading` is true
  - Clicking emits `closeAllPositions` event
  - `setGlobalLoading(loading)` method added (mirrors `OrdersTableComponent` pattern)
  - Button styled consistently with Cancel All button in orders table
- **Dependencies**: None (can be done in parallel with Task 3.1)

#### Implementation Details

```typescript
// positions-table.component.ts — modification
// Add new output and global loading (following orders-table pattern):

@Output() public closeAllPositions = new EventEmitter<void>();

public globalLoading = false;

public setGlobalLoading(loading: boolean): void {
  this.globalLoading = loading;
}
```

```html
<!-- positions-table.component.html — modification -->
<!-- Add to the toolbar (alongside filter input from Phase 2): -->

@if (positions.length > 0) {
  <button mat-flat-button
          color="warn"
          class="positions-table__close-all-btn"
          [disabled]="globalLoading"
          (click)="closeAllPositions.emit()">
    @if (globalLoading) {
      <mat-spinner diameter="18"></mat-spinner>
    } @else {
      Close All Positions
    }
  </button>
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.ts` — `@Output cancelAllOrders`, `globalLoading`, `setGlobalLoading()` pattern
- `frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.html` — Cancel All button template

### Task 3.3: Add `closeAllPositions()` method to `OrderService` {#task-33-add-close-all-to-order-service}

Add a method that sequentially closes a list of positions, emitting progress for each step.

- **Complexity**: High
- **Risk Factors**: RxJS `concat` + `scan` pattern for sequential dispatch with progress; partial failure accumulation; proper side-flip logic
- **Files**:
  - `frontend/trading-ui/src/app/core/services/order.service.ts` — modification
  - `frontend/trading-ui/src/app/core/models/place-order.model.ts` — add `CloseAllProgress` interface (if not using inline type)
- **Success**:
  - `closeAllPositions(positions: Position[])` returns `Observable<CloseAllProgress>`
  - Emits progress after each position close attempt: `{ completed: number, succeeded: number, failed: number, total: number }`
  - Close side: `"Long"` → `"sell"`, `"Short"` → `"buy"`
  - Size: `Math.abs(position.size)`
  - Sequential dispatch (concat) — no parallel calls
  - Each individual failure is caught and counted — does not abort remaining positions
  - Final emission has `completed === total`
- **Dependencies**: None

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/models/place-order.model.ts — modification
// Add this interface:

export interface CloseAllProgress {
  readonly completed: number;
  readonly succeeded: number;
  readonly failed: number;
  readonly total: number;
}
```

```typescript
// frontend/trading-ui/src/app/core/services/order.service.ts — modification
import { concat, map, scan, catchError, of, Observable } from "rxjs";
import { Position } from "../models/position.model";
import { CloseAllProgress } from "../models/place-order.model";

// Add method to OrderService class:

public closeAllPositions(positions: Position[]): Observable<CloseAllProgress> {
  const closeRequests$ = positions.map((position) => {
    const closeSide = position.side === "Long" ? "sell" : "buy";
    const request: PlaceOrderRequest = {
      asset: position.asset,
      side: closeSide,
      orderType: "market",
      price: null,
      size: Math.abs(position.size),
    };
    return this.placeOrder(request).pipe(
      map(() => true as const),
      catchError(() => of(false as const))
    );
  });

  return concat(...closeRequests$).pipe(
    scan(
      (progress, success) => ({
        completed: progress.completed + 1,
        succeeded: progress.succeeded + (success ? 1 : 0),
        failed: progress.failed + (success ? 0 : 1),
        total: positions.length,
      }),
      { completed: 0, succeeded: 0, failed: 0, total: positions.length } as CloseAllProgress
    )
  );
}
```

##### Pattern References

- `frontend/trading-ui/src/app/core/services/order.service.ts` — existing `placeOrder()` method
- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — `onClosePosition()` side-flip logic: `position.side === 'Long' ? 'sell' : 'buy'`

### Task 3.4: Implement Close All handler in `DashboardComponent` {#task-34-implement-close-all-in-dashboard}

Wire up the Close All flow in the dashboard: open dialog → on confirm → call `OrderService.closeAllPositions()` → track progress → show summary toast.

- **Complexity**: High
- **Risk Factors**: Optimistic UI removal + rollback on partial failure; progress subscription management; proper cleanup with `DestroyRef`
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — add `onCloseAllPositions()` handler
  - `frontend/trading-ui/src/app/features/dashboard/dashboard.component.html` — bind `(closeAllPositions)` output
- **Success**:
  - Dialog opens with current positions list when `closeAllPositions` emitted
  - On confirmed: sets `positionsTable.setGlobalLoading(true)`, optimistically clears positions array
  - Subscribes to `OrderService.closeAllPositions()` for progress updates
  - On each progress emission: could update notification (for POC, final summary is sufficient)
  - On complete: show success toast "Closed N positions" or "Closed X/N positions (Y failed)"
  - On complete: `setGlobalLoading(false)`, trigger `_refresh$.next()`
  - On all-failed: restore positions, show error toast
  - Failed positions remain in the table after refresh
- **Dependencies**: Tasks 3.1, 3.2, 3.3

#### Implementation Details

```typescript
// dashboard.component.ts — modification
import { CloseAllDialogComponent, CloseAllResult } from "./positions-table/close-all-dialog/close-all-dialog.component";
import { CloseAllProgress } from "../../core/models/place-order.model";
import { last } from "rxjs";

// Add method to DashboardComponent class:

public onCloseAllPositions(): void {
  const currentPositions = [...this.positions];
  if (currentPositions.length === 0) return;

  const dialogRef = this._dialog.open(CloseAllDialogComponent, {
    data: { positions: currentPositions },
    width: "450px",
  });

  dialogRef.afterClosed().subscribe((result: CloseAllResult | undefined) => {
    if (!result?.confirmed) return;

    // Optimistic UI: clear positions and show loading
    this.positionsTable?.setGlobalLoading(true);
    const savedPositions = [...this.positions];
    currentPositions.forEach((p) => this._pendingPositionKeys.add(p.asset + p.side));
    this.positions = [];

    this._orderService.closeAllPositions(currentPositions).pipe(
      last() // Only care about the final progress emission
    ).subscribe({
      next: (progress: CloseAllProgress) => {
        currentPositions.forEach((p) => this._pendingPositionKeys.delete(p.asset + p.side));
        this.positionsTable?.setGlobalLoading(false);
        if (progress.failed === 0) {
          this._notifications.success(`Closed ${progress.succeeded} positions`);
        } else if (progress.succeeded === 0) {
          this._notifications.error("Failed to close positions");
          this.positions = savedPositions;
        } else {
          this._notifications.warning(
            `Closed ${progress.succeeded}/${progress.total} positions (${progress.failed} failed)`
          );
        }
        this._refresh$.next();
      },
      error: () => {
        currentPositions.forEach((p) => this._pendingPositionKeys.delete(p.asset + p.side));
        this.positionsTable?.setGlobalLoading(false);
        this._notifications.error("Failed to close positions");
        this.positions = savedPositions;
        this._refresh$.next();
      },
    });
  });
}
```

```html
<!-- dashboard.component.html — modification -->
<!-- Add (closeAllPositions) binding to positions table: -->
<!-- Find the existing <app-positions-table> and add the output: -->
<!-- (closeAllPositions)="onCloseAllPositions()" -->
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — `onCancelAllOrders()` — dialog → optimistic clear → API call → restore on error pattern; `onClosePosition()` — close order flow

### Task 3.5: Write unit tests for Close All flow {#task-35-write-unit-tests}

Write unit tests for the `CloseAllDialogComponent`, `OrderService.closeAllPositions()`, and `DashboardComponent.onCloseAllPositions()` flow.

- **Complexity**: Medium
- **Risk Factors**: Mocking sequential RxJS emissions; testing dialog interaction
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/close-all-dialog/close-all-dialog.component.spec.ts` — new file
  - `frontend/trading-ui/src/app/core/services/order.service.spec.ts` — new file (or add to existing)
  - Update `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.spec.ts` — add Close All button tests
- **Success**:
  - CloseAllDialog tests:
    - Renders position list with correct asset, side, size
    - Cancel returns `{ confirmed: false }`
    - Confirm returns `{ confirmed: true }`
  - OrderService.closeAllPositions tests:
    - Emits progress for each position closed
    - Handles partial failures (some succeed, some fail)
    - Final emission has `completed === total`
    - All succeed: `failed === 0`
    - All fail: `succeeded === 0`
  - PositionsTable Close All button tests:
    - Button visible when positions exist
    - Button hidden when no positions
    - Button disabled when globalLoading is true
    - Click emits closeAllPositions event
  - All tests pass
- **Dependencies**: Tasks 3.1–3.4

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/positions-table/close-all-dialog/close-all-dialog.component.spec.ts — new file
import { ComponentFixture, TestBed } from "@angular/core/testing";
import { MAT_DIALOG_DATA, MatDialogRef } from "@angular/material/dialog";
import { NoopAnimationsModule } from "@angular/platform-browser/animations";
import { CloseAllDialogComponent, CloseAllDialogData } from "./close-all-dialog.component";
import { Position } from "../../../../core/models/position.model";

const mockPositions: Position[] = [
  { asset: "BTC", side: "Long", size: 0.001, entryPrice: 50000, markPrice: 51000, unrealisedPnl: 64.13, unrealisedPnlPercent: 12.8, liquidationPrice: 40000, leverage: 10, marginMode: "cross" },
  { asset: "ETH", side: "Short", size: 0.5, entryPrice: 3000, markPrice: 3050, unrealisedPnl: -22.19, unrealisedPnlPercent: -1.5, liquidationPrice: 3500, leverage: 5, marginMode: "cross" },
];

describe("CloseAllDialogComponent", () => {
  let component: CloseAllDialogComponent;
  let fixture: ComponentFixture<CloseAllDialogComponent>;
  let dialogRefSpy: jasmine.SpyObj<MatDialogRef<CloseAllDialogComponent>>;

  beforeEach(async () => {
    dialogRefSpy = jasmine.createSpyObj("MatDialogRef", ["close"]);

    await TestBed.configureTestingModule({
      imports: [CloseAllDialogComponent, NoopAnimationsModule],
      providers: [
        { provide: MatDialogRef, useValue: dialogRefSpy },
        { provide: MAT_DIALOG_DATA, useValue: { positions: mockPositions } as CloseAllDialogData },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CloseAllDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it("should display all positions", () => {
    const rows = fixture.nativeElement.querySelectorAll(".close-all-dialog__position-row");
    expect(rows.length).toBe(2);
  });

  it("should close with confirmed false on cancel", () => {
    component.onCancel();
    expect(dialogRefSpy.close).toHaveBeenCalledWith(
      jasmine.objectContaining({ confirmed: false })
    );
  });

  it("should close with confirmed true on confirm", () => {
    component.onConfirm();
    expect(dialogRefSpy.close).toHaveBeenCalledWith(
      jasmine.objectContaining({ confirmed: true })
    );
  });
});
```

```typescript
// frontend/trading-ui/src/app/core/services/order.service.spec.ts — new file
import { TestBed } from "@angular/core/testing";
import { provideHttpClient } from "@angular/common/http";
import { provideHttpClientTesting, HttpTestingController } from "@angular/common/http/testing";
import { OrderService } from "./order.service";
import { Position } from "../models/position.model";
import { CloseAllProgress } from "../models/place-order.model";

const mockPositions: Position[] = [
  { asset: "BTC", side: "Long", size: 0.001, entryPrice: 50000, markPrice: 51000, unrealisedPnl: 64.13, unrealisedPnlPercent: 12.8, liquidationPrice: 40000, leverage: 10, marginMode: "cross" },
  { asset: "ETH", side: "Short", size: 0.5, entryPrice: 3000, markPrice: 3050, unrealisedPnl: -22.19, unrealisedPnlPercent: -1.5, liquidationPrice: 3500, leverage: 5, marginMode: "cross" },
];

describe("OrderService", () => {
  let service: OrderService;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(OrderService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  describe("closeAllPositions", () => {
    it("should emit progress for each position", () => {
      const emissions: CloseAllProgress[] = [];
      service.closeAllPositions(mockPositions).subscribe((p) => emissions.push(p));

      // First request (BTC Long → sell)
      const req1 = httpTesting.expectOne((req) => req.url.endsWith("/orders") && req.method === "POST");
      expect(req1.request.body.asset).toBe("BTC");
      expect(req1.request.body.side).toBe("sell");
      req1.flush({ success: true, orderId: "1", status: "filled", detail: null });

      // Second request (ETH Short → buy)
      const req2 = httpTesting.expectOne((req) => req.url.endsWith("/orders") && req.method === "POST");
      expect(req2.request.body.asset).toBe("ETH");
      expect(req2.request.body.side).toBe("buy");
      req2.flush({ success: true, orderId: "2", status: "filled", detail: null });

      expect(emissions.length).toBe(2);
      expect(emissions[1]).toEqual({ completed: 2, succeeded: 2, failed: 0, total: 2 });
    });

    it("should handle partial failures", () => {
      const emissions: CloseAllProgress[] = [];
      service.closeAllPositions(mockPositions).subscribe((p) => emissions.push(p));

      // First succeeds
      httpTesting.expectOne((req) => req.url.endsWith("/orders") && req.method === "POST").flush({ success: true, orderId: "1", status: "filled", detail: null });

      // Second fails
      httpTesting.expectOne((req) => req.url.endsWith("/orders") && req.method === "POST").flush("error", { status: 400, statusText: "Bad Request" });

      expect(emissions.length).toBe(2);
      expect(emissions[1]).toEqual({ completed: 2, succeeded: 1, failed: 1, total: 2 });
    });
  });
});
```

##### Pattern References

- `frontend/trading-ui/src/app/app.component.spec.ts` — TestBed mock service pattern
- `frontend/trading-ui/src/app/features/order-entry/confirm-dialog/confirm-dialog.component.ts` — `MAT_DIALOG_DATA` / `MatDialogRef` injection for test mocking

### Task 3.6: Run frontend build and lint {#task-36-run-frontend-build-and-lint}

Verify no build or lint errors were introduced.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification step)
- **Success**:
  - `npx ng build` completes without errors
  - `npx ng lint` completes without errors
  - All existing and new tests pass (`npx ng test --watch=false`)
- **Dependencies**: Tasks 3.1–3.5

## Phase Success Criteria

- "Close All Positions" button is visible when positions exist, hidden when empty
- Button is disabled during global loading
- Clicking opens confirmation dialog listing all positions with asset, side, size
- On cancel, no action taken
- On confirm, positions are closed sequentially via `POST /api/orders`
- Progress tracked: succeeded/failed counts accumulated
- On all succeed: toast "Closed N positions"
- On partial failure: toast "Closed X/N positions (Y failed)"; failed positions remain after refresh
- On all fail: toast "Failed to close positions"; positions restored
- All unit tests pass; frontend builds and lints cleanly
