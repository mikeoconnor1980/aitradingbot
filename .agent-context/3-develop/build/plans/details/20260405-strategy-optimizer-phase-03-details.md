<!-- markdownlint-disable-file -->

# Task Details: Strategy Optimizer — Phase 3: Frontend — Optimizer Tab & Configuration

## Phase 3: Frontend — Optimizer Tab & Configuration

## Standards and Knowledge References

- **angular.instructions.md**: Standalone components, `inject()` DI, explicit accessibility, double quotes, SCSS, `takeUntilDestroyed`
- **app.component.html**: Nav link pattern with `routerLink` + `routerLinkActive`
- **app.routes.ts**: Lazy-loaded route pattern with `loadComponent`
- **backtest.service.ts**: API service pattern with `ApiRestClient`
- **backtest-page.component.ts**: Feature page pattern with internal `MatTabGroup`
- **SignalRService**: `backtestProgress$` observable pattern for real-time updates

---

### Task 3.1: Create `optimizer.service.ts` API service {#task-31-create-optimizer-service}

Create the Angular service for optimizer API calls.

- **Complexity**: Low
- **Risk Factors**: None — follows `backtest.service.ts` pattern
- **Files**:
  - `frontend/trading-ui/src/app/core/services/optimizer.service.ts` — new file
- **Success**:
  - Service injectable in root
  - Methods: `runOptimization(request)`, `getOptimization(id)`, `getOptimizationList(page, pageSize)`

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/services/optimizer.service.ts
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { ApiRestClient } from "./api-rest-client.service";
import { OptimizationRunResponse, RunOptimizationRequest, OptimizationListResponse } from "../models/optimizer.model";

@Injectable({ providedIn: "root" })
export class OptimizerService {

    private readonly _api = inject(ApiRestClient);

    public runOptimization(request: RunOptimizationRequest): Observable<OptimizationRunResponse> {
        return this._api.post<OptimizationRunResponse>("optimizations", request);
    }

    public getOptimization(id: string): Observable<OptimizationRunResponse> {
        return this._api.get<OptimizationRunResponse>(`optimizations/${id}`);
    }

    public getOptimizationList(page: number = 1, pageSize: number = 10): Observable<OptimizationListResponse> {
        return this._api.get<OptimizationListResponse>(`optimizations?page=${page}&pageSize=${pageSize}`);
    }
}
```

---

### Task 3.2: Create optimizer TypeScript models {#task-32-create-optimizer-models}

Create the TypeScript interfaces for optimization requests, responses, and results.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/core/models/optimizer.model.ts` — new file
- **Success**: All interfaces match backend DTOs

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/models/optimizer.model.ts

export interface RunOptimizationRequest {
    symbol: string;
    startDateUtc: number;
    endDateUtc: number;
    initialCapital: number;
    sampleSize: number;
    stopLossMin?: number;
    stopLossMax?: number;
    takeProfitMin?: number;
    takeProfitMax?: number;
    leverageMin?: number;
    leverageMax?: number;
    minWinRate?: number;
    minTotalTrades?: number;
    maxDrawdownPercent?: number;
}

export interface OptimizationRunResponse {
    id: string;
    symbol: string;
    startDate: string;
    endDate: string;
    initialCapital: number;
    status: "Queued" | "Running" | "Completed" | "Failed";
    totalCombinations: number;
    completedCount: number;
    qualifiedCount: number;
    elapsedMs: number;
    errorMessage?: string;
    createdAt: string;
    results: OptimizationResultResponse[];
}

export interface OptimizationResultResponse {
    rank: number;
    fitnessScore: number;
    signalDescription: string;
    strategyConfigJson: string;
    totalPnl: number;
    winRate: number;
    maxDrawdown: number;
    totalTrades: number;
    winningTrades: number;
    losingTrades: number;
    totalFeesPaid: number;
    averageTradePnl: number;
    averageHoldTimeMinutes: number;
}

export interface OptimizationListResponse {
    items: OptimizationRunSummary[];
    totalCount: number;
    page: number;
    pageSize: number;
}

export interface OptimizationRunSummary {
    id: string;
    symbol: string;
    status: string;
    totalCombinations: number;
    qualifiedCount: number;
    elapsedMs: number;
    createdAt: string;
}

export interface OptimizationProgress {
    id: string;
    status: string;
    completed: number;
    total: number;
}
```

---

### Task 3.3: Create `optimizer-page.component` — feature shell {#task-33-create-optimizer-page-component}

Create the top-level optimizer page component with internal tab switching between Configure, Results, and History views.

- **Complexity**: Medium
- **Risk Factors**: Must handle 3 states: configuring, running (with progress), and viewing results
- **Files**:
  - `frontend/trading-ui/src/app/features/optimizer/optimizer-page.component.ts` — new file
  - `frontend/trading-ui/src/app/features/optimizer/optimizer-page.component.html` — new file
  - `frontend/trading-ui/src/app/features/optimizer/optimizer-page.component.scss` — new file
- **Success**:
  - Page renders with header and MatTabGroup (Configure, Results, History tabs)
  - Manages state: `isRunning`, `optimizationProgress`, `currentResult`, `apiError`
  - Subscribes to SignalR `ReceiveOptimizationProgress` for live updates
  - On completion, switches to Results tab and fetches full result
- **Dependencies**: `OptimizerService`, `SignalRService`

#### Implementation Details

```typescript
// Follow backtest-page.component.ts pattern exactly
@Component({
    selector: "app-optimizer-page",
    standalone: true,
    imports: [
        MatTabGroup, MatTab, MatProgressBar, MatButton,
        OptimizerConfigFormComponent,
        OptimizerResultsTableComponent,
        OptimizerHistoryListComponent
    ],
    templateUrl: "./optimizer-page.component.html",
    styleUrl: "./optimizer-page.component.scss"
})
export class OptimizerPageComponent {
    // State management
    public selectedTabIndex = 0;
    public isRunning = false;
    public optimizationProgress: OptimizationProgress | null = null;
    public currentResult: OptimizationRunResponse | null = null;
    public apiError: string | null = null;
    public pendingOptimizationId: string | null = null;

    // SignalR subscription for ReceiveOptimizationProgress
    // On "Completed" status → fetch full result → switch to Results tab
}
```

Template structure:
```html
<section class="optimizer-page">
  <header>
    <h2>The Optimizer</h2>
    <p>Discover optimal signal strategy parameters through automated parameter sweeps.</p>
  </header>

  <mat-tab-group [(selectedIndex)]="selectedTabIndex">
    <mat-tab label="Configure">
      <!-- Config form + progress bar when running -->
    </mat-tab>
    <mat-tab label="Results" [disabled]="!currentResult">
      <!-- Results table when completed -->
    </mat-tab>
    <mat-tab label="History">
      <!-- History list of previous runs -->
    </mat-tab>
  </mat-tab-group>
</section>
```

---

### Task 3.4: Create `optimizer-config-form.component` — parameter bounds form {#task-34-create-optimizer-config-form}

Create the form component for configuring and launching an optimization run.

- **Complexity**: Medium
- **Risk Factors**: Many form fields — must be organized into clear sections
- **Files**:
  - `frontend/trading-ui/src/app/features/optimizer/optimizer-config-form/optimizer-config-form.component.ts` — new file
  - `frontend/trading-ui/src/app/features/optimizer/optimizer-config-form/optimizer-config-form.component.html` — new file
  - `frontend/trading-ui/src/app/features/optimizer/optimizer-config-form/optimizer-config-form.component.scss` — new file
- **Success**:
  - Form emits `runOptimization` event with complete `RunOptimizationRequest`
  - Form sections: Market (symbol, dates, capital), Parameter Bounds (SL, TP, Leverage ranges), Fitness Thresholds (min win rate, min trades, max drawdown), Sweep Settings (sample size)
  - Validation: symbol required, dates valid, capital > 0, min < max for all ranges
  - Defaults pre-populated from model defaults
- **Dependencies**: Angular Material form controls, reactive forms

#### Implementation Details

Form layout in 3 card sections:

**Section 1: Market Settings**
- Symbol (text input, required)
- Start Date / End Date (date pickers → converted to Unix ms)
- Initial Capital (number input, min 100)

**Section 2: Parameter Bounds**
- Stop Loss: Min (1%) — Max (5%) with step
- Take Profit: Min (2%) — Max (10%) with step
- Leverage: Min (3x) — Max (10x) with step

**Section 3: Optimization Settings**
- Sample Size (number input, default 500, range 10-5000)
- Min Win Rate % (default 40)
- Min Total Trades (default 10)
- Max Drawdown % (default 30)

**Action**: "Run Optimization" button (disabled when `isRunning`)

```typescript
@Component({
    selector: "app-optimizer-config-form",
    standalone: true,
    imports: [ReactiveFormsModule, MatFormField, MatInput, MatSelect, MatButton, MatCard, ...],
    templateUrl: "./optimizer-config-form.component.html",
    styleUrl: "./optimizer-config-form.component.scss"
})
export class OptimizerConfigFormComponent {
    @Input() public isRunning = false;
    @Output() public runOptimization = new EventEmitter<RunOptimizationRequest>();

    public form = new FormGroup({
        symbol: new FormControl("BTC", [Validators.required]),
        startDate: new FormControl("", [Validators.required]),
        endDate: new FormControl("", [Validators.required]),
        initialCapital: new FormControl(10000, [Validators.required, Validators.min(100)]),
        sampleSize: new FormControl(500, [Validators.required, Validators.min(10), Validators.max(5000)]),
        // Bounds
        stopLossMin: new FormControl(1, [Validators.required, Validators.min(0.1)]),
        stopLossMax: new FormControl(5, [Validators.required]),
        takeProfitMin: new FormControl(2, [Validators.required, Validators.min(0.1)]),
        takeProfitMax: new FormControl(10, [Validators.required]),
        leverageMin: new FormControl(3, [Validators.required, Validators.min(1)]),
        leverageMax: new FormControl(10, [Validators.required]),
        // Thresholds
        minWinRate: new FormControl(40, [Validators.required, Validators.min(0), Validators.max(100)]),
        minTotalTrades: new FormControl(10, [Validators.required, Validators.min(1)]),
        maxDrawdownPercent: new FormControl(30, [Validators.required, Validators.min(1), Validators.max(100)])
    });

    public onSubmit(): void {
        if (this.form.valid) {
            // Map form values to RunOptimizationRequest, emit
        }
    }
}
```

---

### Task 3.5: Add `/optimizer` route and navigation link {#task-35-add-route-and-nav-link}

Add the Optimizer route and navigation tab.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/app.routes.ts` — modification (add route)
  - `frontend/trading-ui/src/app/app.component.html` — modification (add nav link)
- **Success**:
  - `/optimizer` route lazy-loads `OptimizerPageComponent`
  - "Optimizer" link appears in navigation bar after "Backtesting"

#### Implementation Details

In `app.routes.ts`:
```typescript
{
    path: "optimizer",
    loadComponent: () => import("./features/optimizer/optimizer-page.component")
        .then(m => m.OptimizerPageComponent),
    title: "Optimizer"
}
```

In `app.component.html`, add after the Backtesting link:
```html
<a routerLink="/optimizer" routerLinkActive="app-shell__link--active" class="app-shell__link">Optimizer</a>
```

---

### Task 3.6: Wire SignalR progress for optimization runs {#task-36-wire-signalr-progress}

Add SignalR message handling for `ReceiveOptimizationProgress` events.

- **Complexity**: Low
- **Risk Factors**: Must not break existing `ReceiveBacktestProgress` handling
- **Files**:
  - `frontend/trading-ui/src/app/core/services/signalr.service.ts` — modification
- **Success**:
  - New observable: `optimizationProgress$` of type `OptimizationProgress`
  - Emits whenever `ReceiveOptimizationProgress` message received from hub

#### Implementation Details

Follow existing `backtestProgress$` pattern:

```typescript
// Add to SignalRService
public optimizationProgress$ = new Subject<OptimizationProgress>();

// In connection setup:
this._connection.on("ReceiveOptimizationProgress", (data: OptimizationProgress) => {
    this.optimizationProgress$.next(data);
});
```

---

### Task 3.7: Build frontend and lint {#task-37-build-and-lint}

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None — verification only
- **Success**:
  - `npx ng build` succeeds with zero errors
  - `npx ng lint` succeeds with zero errors
  - Optimizer tab navigable and renders config form
