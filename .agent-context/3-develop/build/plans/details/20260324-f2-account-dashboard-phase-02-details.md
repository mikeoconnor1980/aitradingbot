<!-- markdownlint-disable-file -->

# Task Details: F2 — Account Dashboard

## Phase 2: Frontend — Angular Material Dashboard with Polling

## Standards and Knowledge References

**Angular Standards** (`.github/instructions/angular.instructions.md` + `.agent-context/0-knowledge/11-angular-instructions.md`):
- **Standalone components** (knowledge file + PRD override instruction template) — use `standalone: true`
- Explicit accessibility on all members (`public`, `private`)
- Explicit return types on all methods
- Double quotes for strings
- SCSS only for styling
- Use newer Angular control flow syntax (`@if`, `@for`, `@switch`)
- Infinite observable variables suffixed with `$` (e.g., `poll$`)
- Infinite observables must use `takeUntilDestroyed(this._destroyRef)` for cleanup
- Finite observables (HTTP calls) do NOT need `$` suffix or teardown
- Feature folder structure under `features/`
- Services with `@Injectable({ providedIn: "root" })`
- Component member order: private fields → constructor → @Input/@Output → public props → public methods → private methods

**Hyperliquid API Service Pattern** (from F1):
- Service injected with `HttpClient` directly (no `ApiRestClient` — that's from DTS project)
- Base URL configured in Angular environment or proxied through .NET API

**PRD UI Layout** (`.agent-context/prd-approved/hyperliquid-poc-prd.md` §7):
- Tabbed layout: account summary always visible at top; positions and orders in separate tabs below
- Functional prototype — navigable and clear, not polished
- Inline error banner for persistent errors, toast for transient errors

## Design References

**Angular Material Components Used:**
- `MatTabsModule` — for tabbed positions/orders layout
- `MatTableModule` — for positions and orders data tables (or plain HTML tables if simpler)
- `MatButtonModule` — for manual refresh button
- `MatSnackBarModule` — for toast notifications on transient errors
- `MatProgressSpinnerModule` — for loading indicator
- `MatIconModule` — for refresh icon

---

### Task 2.1: Install Angular Material and configure theming {#task-21-install-angular-material}

Install Angular Material and Angular CDK. Configure a basic theme.

- **Complexity**: Low
- **Risk Factors**: None — standard Angular Material setup
- **Files**:
  - `frontend/hyperliquid-poc/package.json` — modification (new dependencies)
  - `frontend/hyperliquid-poc/src/styles.scss` — modification (import Material theme)
  - `frontend/hyperliquid-poc/angular.json` — may need style entry if not already present
- **Success**:
  - Angular Material packages installed
  - Material theme imported in global styles
  - `ng build` succeeds

#### Implementation Details

```bash
# Run from frontend/hyperliquid-poc/
ng add @angular/material --theme=custom --typography=true --animations=true
```

```scss
// frontend/hyperliquid-poc/src/styles.scss — modification
// Add Angular Material theme (if not added by ng add)
@use "@angular/material" as mat;

// Custom dark theme for trading dashboard
$dark-theme: mat.define-theme((
  color: (
    theme-type: dark,
    primary: mat.$green-palette,
    tertiary: mat.$red-palette,
  ),
));

html {
  @include mat.all-component-themes($dark-theme);
}

body {
  margin: 0;
  font-family: Roboto, "Helvetica Neue", sans-serif;
  background-color: #1a1a2e;
  color: #e0e0e0;
}
```

> **Note:** A dark theme is appropriate for a trading dashboard. The green primary aligns with profit indicators; red tertiary aligns with loss indicators.

##### Pattern References

- Angular Material installation guide — `ng add @angular/material`

---

### Task 2.2: Create TypeScript models and DTOs {#task-22-create-typescript-models-and-dtos}

Create TypeScript interfaces mirroring the backend DTOs.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/hyperliquid-poc/src/app/core/models/account-summary.model.ts` — new file
  - `frontend/hyperliquid-poc/src/app/core/models/position.model.ts` — new file
  - `frontend/hyperliquid-poc/src/app/core/models/open-order.model.ts` — new file
- **Success**:
  - All three interfaces match the backend DTO property names exactly (camelCase in JSON)
  - TypeScript compilation succeeds

#### Implementation Details

```typescript
// frontend/hyperliquid-poc/src/app/core/models/account-summary.model.ts — new file
export interface AccountSummary {
  equity: number;
  availableMargin: number;
  crossMarginRatio: number;
  maintenanceMargin: number;
  unrealisedPnl: number;
}
```

```typescript
// frontend/hyperliquid-poc/src/app/core/models/position.model.ts — new file
export interface Position {
  asset: string;
  size: number;
  side: string;
  entryPrice: number;
  markPrice: number;
  unrealisedPnl: number;
  unrealisedPnlPercent: number;
  liquidationPrice: number;
}
```

```typescript
// frontend/hyperliquid-poc/src/app/core/models/open-order.model.ts — new file
export interface OpenOrder {
  orderId: string;
  asset: string;
  side: string;
  price: number;
  size: number;
  orderType: string;
  status: string;
}
```

##### Pattern References

- F1's model files (assumed in `core/models/`) — same interface pattern
- `.github/instructions/angular.instructions.md` — model files in `models/` folder

---

### Task 2.3: Extend hyperliquid-api.service.ts with account endpoints {#task-23-extend-api-service}

Add three new methods to the existing `HyperliquidApiService` (from F1) for fetching account summary, positions, and orders.

- **Complexity**: Low
- **Risk Factors**: None — simple HTTP GET calls
- **Files**:
  - `frontend/hyperliquid-poc/src/app/core/services/hyperliquid-api.service.ts` — modification
- **Success**:
  - Three new methods: `getAccountSummary()`, `getPositions()`, `getOpenOrders()`
  - Each returns an `Observable<T>` of the correct type
  - HTTP calls target the backend endpoints (`/api/account`, `/api/positions`, `/api/orders`)
- **Dependencies**:
  - Task 2.2 (models must exist)

#### Implementation Details

```typescript
// frontend/hyperliquid-poc/src/app/core/services/hyperliquid-api.service.ts — modification
// Add these methods to the existing HyperliquidApiService class:

import { AccountSummary } from "../models/account-summary.model";
import { Position } from "../models/position.model";
import { OpenOrder } from "../models/open-order.model";

// ... existing code ...

public getAccountSummary(): Observable<AccountSummary> {
  return this._http.get<AccountSummary>(`${this._baseUrl}/api/account`);
}

public getPositions(): Observable<Position[]> {
  return this._http.get<Position[]>(`${this._baseUrl}/api/positions`);
}

public getOpenOrders(): Observable<OpenOrder[]> {
  return this._http.get<OpenOrder[]>(`${this._baseUrl}/api/orders`);
}
```

##### Pattern References

- F1's `hyperliquid-api.service.ts` — existing `getHealth()` method pattern
- `.github/instructions/angular.instructions.md` — service pattern with `HttpClient`

---

### Task 2.4: Create DashboardComponent with polling and staleness logic {#task-24-create-dashboard-component}

Create the main dashboard component that orchestrates data fetching with a 2-second polling interval, manages staleness detection (10s threshold), and handles error state (toast for transient, inline banner for persistent).

- **Complexity**: High
- **Risk Factors**: RxJS polling logic must correctly handle concurrent requests, error recovery, and cleanup
- **Files**:
  - `frontend/hyperliquid-poc/src/app/features/dashboard/dashboard.component.ts` — new file
  - `frontend/hyperliquid-poc/src/app/features/dashboard/dashboard.component.html` — new file
  - `frontend/hyperliquid-poc/src/app/features/dashboard/dashboard.component.scss` — new file
- **Success**:
  - Component polls all three endpoints every 2 seconds using `interval()` + `switchMap` + `forkJoin`
  - "Last updated: X seconds ago" text updates in real-time (every second via a separate timer)
  - Data visually dims when last successful fetch > 10 seconds ago
  - Manual refresh button triggers immediate re-fetch and resets the polling timer
  - Toast notification on single API failure; inline error banner on 3+ consecutive failures
  - Polling observable uses `takeUntilDestroyed` for cleanup
  - Account summary always visible at top; positions and orders in Material tabs below
- **Dependencies**:
  - Tasks 2.1–2.3

#### Implementation Details

```typescript
// frontend/hyperliquid-poc/src/app/features/dashboard/dashboard.component.ts — new file
import { Component, DestroyRef, OnInit } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { CommonModule } from "@angular/common";
import { MatTabsModule } from "@angular/material/tabs";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { Observable, Subject, forkJoin, interval, of, timer } from "rxjs";
import { switchMap, catchError, tap, startWith } from "rxjs/operators";

import { HyperliquidApiService } from "../../core/services/hyperliquid-api.service";
import { AccountSummary } from "../../core/models/account-summary.model";
import { Position } from "../../core/models/position.model";
import { OpenOrder } from "../../core/models/open-order.model";
import { AccountSummaryComponent } from "./account-summary/account-summary.component";
import { PositionsTableComponent } from "./positions-table/positions-table.component";
import { OrdersTableComponent } from "./orders-table/orders-table.component";

@Component({
  selector: "app-dashboard",
  standalone: true,
  imports: [
    CommonModule,
    MatTabsModule,
    MatButtonModule,
    MatIconModule,
    MatSnackBarModule,
    MatProgressSpinnerModule,
    AccountSummaryComponent,
    PositionsTableComponent,
    OrdersTableComponent,
  ],
  templateUrl: "./dashboard.component.html",
  styleUrls: ["./dashboard.component.scss"],
})
export class DashboardComponent implements OnInit {
  private readonly _destroyRef: DestroyRef;
  private readonly _apiService: HyperliquidApiService;
  private readonly _snackBar: MatSnackBar;

  private readonly _refresh$: Subject<void> = new Subject<void>();
  private _consecutiveErrors: number = 0;

  public accountSummary: AccountSummary | null = null;
  public positions: Position[] = [];
  public orders: OpenOrder[] = [];
  public isLoading: boolean = true;
  public isStale: boolean = false;
  public showErrorBanner: boolean = false;
  public errorMessage: string = "";
  public lastUpdated: Date | null = null;
  public secondsAgo: number = 0;

  public constructor(destroyRef: DestroyRef, apiService: HyperliquidApiService, snackBar: MatSnackBar) {
    this._destroyRef = destroyRef;
    this._apiService = apiService;
    this._snackBar = snackBar;
  }

  public ngOnInit(): void {
    this._startPolling();
    this._startStalenessTimer();
  }

  public onManualRefresh(): void {
    this._refresh$.next();
  }

  private _startPolling(): void {
    // Merge manual refresh triggers with 2-second interval
    const poll$ = this._refresh$.pipe(
      startWith(void 0),
      switchMap(() =>
        interval(2000).pipe(
          startWith(0),
          switchMap(() => this._fetchAllData())
        )
      )
    );

    poll$
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe();
  }

  private _startStalenessTimer(): void {
    // Update "X seconds ago" display every second
    timer(0, 1000)
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe(() => {
        if (this.lastUpdated) {
          this.secondsAgo = Math.floor(
            (Date.now() - this.lastUpdated.getTime()) / 1000
          );
          this.isStale = this.secondsAgo > 10;
        }
      });
  }

  private _fetchAllData(): Observable<unknown> {
    return forkJoin({
      account: this._apiService.getAccountSummary().pipe(
        catchError(() => of(null))
      ),
      positions: this._apiService.getPositions().pipe(
        catchError(() => of(null))
      ),
      orders: this._apiService.getOpenOrders().pipe(
        catchError(() => of(null))
      ),
    }).pipe(
      tap((results) => {
        const hasError =
          results.account === null ||
          results.positions === null ||
          results.orders === null;

        if (hasError) {
          this._consecutiveErrors++;
          if (this._consecutiveErrors >= 3) {
            this.showErrorBanner = true;
            this.errorMessage = "Unable to reach Hyperliquid API. Retrying...";
          } else {
            this._snackBar.open(
              "Failed to refresh dashboard data",
              "Dismiss",
              { duration: 3000 }
            );
          }
        } else {
          this._consecutiveErrors = 0;
          this.showErrorBanner = false;
          this.accountSummary = results.account;
          this.positions = results.positions ?? [];
          this.orders = results.orders ?? [];
          this.lastUpdated = new Date();
          this.isStale = false;
        }

        this.isLoading = false;
      })
    );
  }
}
```

```html
<!-- frontend/hyperliquid-poc/src/app/features/dashboard/dashboard.component.html — new file -->
<div class="dashboard" [class.dashboard--stale]="isStale">
  <!-- Error Banner -->
  @if (showErrorBanner) {
    <div class="dashboard__error-banner">
      <mat-icon>error</mat-icon>
      <span>{{ errorMessage }}</span>
    </div>
  }

  <!-- Header with refresh controls -->
  <div class="dashboard__header">
    <h1>Account Dashboard</h1>
    <div class="dashboard__controls">
      @if (lastUpdated) {
        <span class="dashboard__timestamp">
          Last updated: {{ secondsAgo }}s ago
        </span>
      }
      <button
        mat-icon-button
        (click)="onManualRefresh()"
        [disabled]="isLoading"
        aria-label="Refresh dashboard data"
      >
        <mat-icon>refresh</mat-icon>
      </button>
    </div>
  </div>

  <!-- Loading spinner -->
  @if (isLoading && !accountSummary) {
    <div class="dashboard__loading">
      <mat-spinner diameter="48"></mat-spinner>
    </div>
  }

  <!-- Account Summary (always visible at top) -->
  @if (accountSummary) {
    <app-account-summary
      [summary]="accountSummary"
    ></app-account-summary>
  }

  <!-- Tabbed positions and orders -->
  <mat-tab-group class="dashboard__tabs">
    <mat-tab label="Positions">
      <app-positions-table
        [positions]="positions"
      ></app-positions-table>
    </mat-tab>
    <mat-tab label="Orders">
      <app-orders-table
        [orders]="orders"
      ></app-orders-table>
    </mat-tab>
  </mat-tab-group>
</div>
```

```scss
// frontend/hyperliquid-poc/src/app/features/dashboard/dashboard.component.scss — new file
.dashboard {
  max-width: 1200px;
  margin: 0 auto;
  padding: 24px;

  &--stale {
    opacity: 0.5;
    transition: opacity 0.3s ease;
  }

  &__error-banner {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 12px 16px;
    background-color: #d32f2f;
    color: #fff;
    border-radius: 4px;
    margin-bottom: 16px;
  }

  &__header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 24px;

    h1 {
      margin: 0;
      font-size: 24px;
    }
  }

  &__controls {
    display: flex;
    align-items: center;
    gap: 12px;
  }

  &__timestamp {
    font-size: 14px;
    opacity: 0.7;
  }

  &__loading {
    display: flex;
    justify-content: center;
    padding: 48px 0;
  }

  &__tabs {
    margin-top: 24px;
  }
}
```

##### Pattern References

- `.github/instructions/angular.instructions.md` — `takeUntilDestroyed`, `$` suffix on infinite observables, member ordering, SCSS BEM nesting
- F2 PBI — 2s polling, 10s staleness, toast for transient errors, inline banner for persistent errors

---

### Task 2.5: Create AccountSummaryComponent {#task-25-create-account-summary-component}

Create a card component that displays the account summary metrics (equity, available margin, cross-margin ratio, maintenance margin, unrealised PnL).

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/hyperliquid-poc/src/app/features/dashboard/account-summary/account-summary.component.ts` — new file
  - `frontend/hyperliquid-poc/src/app/features/dashboard/account-summary/account-summary.component.html` — new file
  - `frontend/hyperliquid-poc/src/app/features/dashboard/account-summary/account-summary.component.scss` — new file
- **Success**:
  - Component displays all 5 metrics from `AccountSummary`
  - Unrealised PnL is color-coded: green for positive, red for negative
  - Component receives data via `@Input()`
- **Dependencies**:
  - Task 2.2 (model must exist)

#### Implementation Details

```typescript
// frontend/hyperliquid-poc/src/app/features/dashboard/account-summary/account-summary.component.ts — new file
import { Component, Input } from "@angular/core";
import { CommonModule, DecimalPipe } from "@angular/common";
import { MatCardModule } from "@angular/material/card";
import { AccountSummary } from "../../../core/models/account-summary.model";

@Component({
  selector: "app-account-summary",
  standalone: true,
  imports: [CommonModule, DecimalPipe, MatCardModule],
  templateUrl: "./account-summary.component.html",
  styleUrls: ["./account-summary.component.scss"],
})
export class AccountSummaryComponent {
  @Input()
  public summary!: AccountSummary;

  public get pnlClass(): string {
    if (!this.summary) return "";
    return this.summary.unrealisedPnl >= 0 ? "pnl--profit" : "pnl--loss";
  }
}
```

```html
<!-- frontend/hyperliquid-poc/src/app/features/dashboard/account-summary/account-summary.component.html — new file -->
<mat-card class="account-summary">
  <mat-card-header>
    <mat-card-title>Account Summary</mat-card-title>
  </mat-card-header>
  <mat-card-content>
    <div class="account-summary__metrics">
      <div class="account-summary__metric">
        <span class="account-summary__label">Equity</span>
        <span class="account-summary__value">{{ summary.equity | number:"1.2-2" }}</span>
      </div>
      <div class="account-summary__metric">
        <span class="account-summary__label">Available Margin</span>
        <span class="account-summary__value">{{ summary.availableMargin | number:"1.2-2" }}</span>
      </div>
      <div class="account-summary__metric">
        <span class="account-summary__label">Cross Margin Ratio</span>
        <span class="account-summary__value">{{ summary.crossMarginRatio | number:"1.4-4" }}</span>
      </div>
      <div class="account-summary__metric">
        <span class="account-summary__label">Maintenance Margin</span>
        <span class="account-summary__value">{{ summary.maintenanceMargin | number:"1.2-2" }}</span>
      </div>
      <div class="account-summary__metric">
        <span class="account-summary__label">Unrealised PnL</span>
        <span class="account-summary__value" [ngClass]="pnlClass">
          {{ summary.unrealisedPnl | number:"1.2-2" }}
        </span>
      </div>
    </div>
  </mat-card-content>
</mat-card>
```

```scss
// frontend/hyperliquid-poc/src/app/features/dashboard/account-summary/account-summary.component.scss — new file
.account-summary {
  margin-bottom: 16px;

  &__metrics {
    display: flex;
    flex-wrap: wrap;
    gap: 24px;
    padding: 16px 0;
  }

  &__metric {
    display: flex;
    flex-direction: column;
    min-width: 150px;
  }

  &__label {
    font-size: 12px;
    opacity: 0.7;
    text-transform: uppercase;
    letter-spacing: 0.5px;
  }

  &__value {
    font-size: 20px;
    font-weight: 500;
    margin-top: 4px;
  }
}

.pnl--profit {
  color: #4caf50;
}

.pnl--loss {
  color: #f44336;
}
```

##### Pattern References

- `.github/instructions/angular.instructions.md` — standalone component, `@Input()`, SCSS BEM, explicit access modifiers
- F2 PBI — unrealised PnL color-coded green/red

---

### Task 2.6: Create PositionsTableComponent {#task-26-create-positions-table-component}

Create a table component displaying open positions with PnL color-coding.

- **Complexity**: Medium
- **Risk Factors**: PnL percentage calculation and formatting need to match PBI spec
- **Files**:
  - `frontend/hyperliquid-poc/src/app/features/dashboard/positions-table/positions-table.component.ts` — new file
  - `frontend/hyperliquid-poc/src/app/features/dashboard/positions-table/positions-table.component.html` — new file
  - `frontend/hyperliquid-poc/src/app/features/dashboard/positions-table/positions-table.component.scss` — new file
- **Success**:
  - Table columns: Asset, Size, Entry Price, Unrealised PnL, Liquidation Price
  - PnL displayed in green (profit) / red (loss) with absolute value and percentage
  - Empty state shows "No open positions" text
  - Component receives data via `@Input()`
- **Dependencies**:
  - Task 2.2 (model must exist)

#### Implementation Details

```typescript
// frontend/hyperliquid-poc/src/app/features/dashboard/positions-table/positions-table.component.ts — new file
import { Component, Input } from "@angular/core";
import { CommonModule, DecimalPipe } from "@angular/common";
import { Position } from "../../../core/models/position.model";

@Component({
  selector: "app-positions-table",
  standalone: true,
  imports: [CommonModule, DecimalPipe],
  templateUrl: "./positions-table.component.html",
  styleUrls: ["./positions-table.component.scss"],
})
export class PositionsTableComponent {
  @Input()
  public positions: Position[] = [];

  public getPnlClass(pnl: number): string {
    return pnl >= 0 ? "pnl--profit" : "pnl--loss";
  }
}
```

```html
<!-- frontend/hyperliquid-poc/src/app/features/dashboard/positions-table/positions-table.component.html — new file -->
@if (positions.length === 0) {
  <div class="positions-table__empty">No open positions</div>
} @else {
  <div class="positions-table__wrapper">
    <table class="positions-table">
      <thead>
        <tr>
          <th>Asset</th>
          <th>Size</th>
          <th>Entry Price</th>
          <th>Unrealised PnL</th>
          <th>Liquidation Price</th>
        </tr>
      </thead>
      <tbody>
        @for (position of positions; track position.asset) {
          <tr>
            <td>{{ position.asset }}</td>
            <td>
              <span [class]="position.side === 'Long' ? 'side--long' : 'side--short'">
                {{ position.size | number:"1.4-4" }} ({{ position.side }})
              </span>
            </td>
            <td>{{ position.entryPrice | number:"1.2-2" }}</td>
            <td [ngClass]="getPnlClass(position.unrealisedPnl)">
              {{ position.unrealisedPnl | number:"1.2-2" }}
              ({{ position.unrealisedPnlPercent | number:"1.2-2" }}%)
            </td>
            <td>{{ position.liquidationPrice | number:"1.2-2" }}</td>
          </tr>
        }
      </tbody>
    </table>
  </div>
}
```

```scss
// frontend/hyperliquid-poc/src/app/features/dashboard/positions-table/positions-table.component.scss — new file
.positions-table {
  width: 100%;
  border-collapse: collapse;

  th, td {
    padding: 12px 16px;
    text-align: left;
    border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  }

  th {
    font-size: 12px;
    text-transform: uppercase;
    letter-spacing: 0.5px;
    opacity: 0.7;
  }

  &__wrapper {
    overflow-x: auto;
    padding: 16px 0;
  }

  &__empty {
    padding: 48px;
    text-align: center;
    opacity: 0.5;
    font-size: 16px;
  }
}

.pnl--profit {
  color: #4caf50;
}

.pnl--loss {
  color: #f44336;
}

.side--long {
  color: #4caf50;
}

.side--short {
  color: #f44336;
}
```

##### Pattern References

- `.github/instructions/angular.instructions.md` — standalone component, `@Input()`, `@for`/`@if` control flow, SCSS BEM
- F2 PBI — PnL color-coded green/red with absolute value and percentage, "No open positions" empty state

---

### Task 2.7: Create OrdersTableComponent {#task-27-create-orders-table-component}

Create a table component displaying open orders.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/hyperliquid-poc/src/app/features/dashboard/orders-table/orders-table.component.ts` — new file
  - `frontend/hyperliquid-poc/src/app/features/dashboard/orders-table/orders-table.component.html` — new file
  - `frontend/hyperliquid-poc/src/app/features/dashboard/orders-table/orders-table.component.scss` — new file
- **Success**:
  - Table columns: Asset, Side, Price, Size, Order Type, Status
  - Empty state shows "No open orders" text
  - Component receives data via `@Input()`
- **Dependencies**:
  - Task 2.2 (model must exist)

#### Implementation Details

```typescript
// frontend/hyperliquid-poc/src/app/features/dashboard/orders-table/orders-table.component.ts — new file
import { Component, Input } from "@angular/core";
import { CommonModule, DecimalPipe } from "@angular/common";
import { OpenOrder } from "../../../core/models/open-order.model";

@Component({
  selector: "app-orders-table",
  standalone: true,
  imports: [CommonModule, DecimalPipe],
  templateUrl: "./orders-table.component.html",
  styleUrls: ["./orders-table.component.scss"],
})
export class OrdersTableComponent {
  @Input()
  public orders: OpenOrder[] = [];

  public getSideClass(side: string): string {
    return side.toLowerCase() === "buy" ? "side--buy" : "side--sell";
  }
}
```

```html
<!-- frontend/hyperliquid-poc/src/app/features/dashboard/orders-table/orders-table.component.html — new file -->
@if (orders.length === 0) {
  <div class="orders-table__empty">No open orders</div>
} @else {
  <div class="orders-table__wrapper">
    <table class="orders-table">
      <thead>
        <tr>
          <th>Asset</th>
          <th>Side</th>
          <th>Price</th>
          <th>Size</th>
          <th>Order Type</th>
          <th>Status</th>
        </tr>
      </thead>
      <tbody>
        @for (order of orders; track order.orderId) {
          <tr>
            <td>{{ order.asset }}</td>
            <td [ngClass]="getSideClass(order.side)">{{ order.side }}</td>
            <td>{{ order.price | number:"1.2-2" }}</td>
            <td>{{ order.size | number:"1.4-4" }}</td>
            <td>{{ order.orderType }}</td>
            <td>{{ order.status }}</td>
          </tr>
        }
      </tbody>
    </table>
  </div>
}
```

```scss
// frontend/hyperliquid-poc/src/app/features/dashboard/orders-table/orders-table.component.scss — new file
.orders-table {
  width: 100%;
  border-collapse: collapse;

  th, td {
    padding: 12px 16px;
    text-align: left;
    border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  }

  th {
    font-size: 12px;
    text-transform: uppercase;
    letter-spacing: 0.5px;
    opacity: 0.7;
  }

  &__wrapper {
    overflow-x: auto;
    padding: 16px 0;
  }

  &__empty {
    padding: 48px;
    text-align: center;
    opacity: 0.5;
    font-size: 16px;
  }
}

.side--buy {
  color: #4caf50;
}

.side--sell {
  color: #f44336;
}
```

##### Pattern References

- `.github/instructions/angular.instructions.md` — standalone component, `@Input()`, `@for`/`@if` control flow, SCSS BEM
- F2 PBI — "No open orders" empty state, columns: Asset, Side, Price, Size, Order Type, Status

---

### Task 2.8: Add dashboard route and navigation {#task-28-add-dashboard-route-and-navigation}

Add the dashboard route to the Angular routing configuration and update navigation to include a dashboard link.

- **Complexity**: Low
- **Risk Factors**: None — standard Angular routing
- **Files**:
  - `frontend/hyperliquid-poc/src/app/app.routes.ts` — modification
  - `frontend/hyperliquid-poc/src/app/app.component.ts` — modification (add navigation link)
  - `frontend/hyperliquid-poc/src/app/app.component.html` — modification (add navigation)
- **Success**:
  - `/dashboard` route loads `DashboardComponent`
  - Navigation includes a link to the dashboard
  - Default route redirects to `/dashboard` (or dashboard is the landing page)
- **Dependencies**:
  - Task 2.4 (DashboardComponent must exist)

#### Implementation Details

```typescript
// frontend/hyperliquid-poc/src/app/app.routes.ts — modification
import { Routes } from "@angular/router";

export const routes: Routes = [
  // ... existing routes from F1 (e.g. health/status) ...
  {
    path: "dashboard",
    loadComponent: () =>
      import("./features/dashboard/dashboard.component").then(
        (m) => m.DashboardComponent
      ),
  },
  { path: "", redirectTo: "dashboard", pathMatch: "full" },
  // ... existing code ...
];
```

```html
<!-- frontend/hyperliquid-poc/src/app/app.component.html — modification -->
<!-- Add dashboard navigation link to existing nav structure -->
<!-- ... existing code ... -->
<nav>
  <!-- ... existing links from F1 ... -->
  <a routerLink="/dashboard" routerLinkActive="active">Dashboard</a>
</nav>
<!-- ... existing code ... -->
```

##### Pattern References

- F1's `app.routes.ts` — existing routing scaffold
- Angular lazy loading with `loadComponent` — standard standalone component routing

---

### Task 2.9: Build and lint the frontend {#task-29-build-and-lint-frontend}

Build the Angular application and verify there are no lint or TypeScript errors.

- **Complexity**: Low
- **Risk Factors**: Material module imports may cause build issues if not imported correctly
- **Files**: None — verification step only
- **Success**:
  - `ng build` succeeds without errors
  - `ng lint` passes (if linting is configured)
  - No TypeScript compilation errors
- **Dependencies**:
  - Tasks 2.1–2.8 (all Phase 2 tasks)

#### Implementation Details

```bash
# Run from frontend/hyperliquid-poc/
ng build --configuration=development
ng lint  # if eslint/tslint is configured
```

> **Note:** If lint tooling is not yet configured by F1, skip the lint step but ensure `ng build` succeeds.

##### Pattern References

- Angular CLI build commands

---

## Phase Success Criteria

- `ng build` succeeds without errors or warnings
- Dashboard route (`/dashboard`) is accessible and renders correctly
- Account summary card displays all 5 metrics
- Positions tab renders table with correct columns and empty state
- Orders tab renders table with correct columns and empty state
- PnL values are green for profit, red for loss (with absolute + percentage)
- Data auto-refreshes every 2 seconds
- "Last updated: Xs ago" updates every second
- Manual refresh button triggers immediate re-fetch
- Data dims visually when stale (> 10 seconds since last successful fetch)
- Toast notification appears on single API failure
- Inline error banner appears after 3+ consecutive failures
