<!-- markdownlint-disable-file -->

# Task Details: F3.5 — Strategy–Backtest Integration

## Phase 3: Frontend — Strategy Picker & Backtest Form Refactor

## Standards and Knowledge References

- **angular.instructions.md**: `standalone: true`, `inject()` for DI, explicit `public`/`private`/`protected`, double quotes, SCSS only, new control flow (`@if`, `@for`), `takeUntilDestroyed`, `ApiRestClient` for HTTP calls, DTO interfaces must match C# DTO names
- **18-backtesting-architecture.md**: Backtest form is the entry point for running backtests
- **13-strategy-config-schema.md**: StrategyConfig is the canonical schema for strategy configuration

## Design References

- `EntryMode` casing mismatch between strategy models (snake_case) and backtest models (PascalCase) is resolved at the backend — when `StrategyId` is supplied, the backend retrieves and uses `Strategy.ConfigJson` directly, so the frontend does not need to map between the two config types
- Strategy picker uses existing `StrategyApiService.getStrategies()` which returns `StrategySummaryDto[]`
- When a strategy is selected, `StrategyApiService.getStrategy(id)` fetches the full `StrategyDto` with config for preview

### Task 3.1: Update Frontend Models {#task-31-update-frontend-models}

Update backtest models to include strategy fields matching the updated C# DTOs.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/core/models/backtest.model.ts` — Add strategy fields to `BacktestRequest`, `BacktestResult`, `BacktestSummary`
- **Success**:
  - `BacktestRequest` has optional `strategyId: string`
  - `BacktestResult` has optional `strategyId`, `strategyRevisionId`, `strategyName`
  - `BacktestSummary` has optional `strategyId`, `strategyRevisionId`, `strategyName`
- **Dependencies**: Phase 2 (API contract)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/models/backtest.model.ts — modification

// Update BacktestRequest — add optional strategyId, make strategyConfig optional:
export interface BacktestRequest {
  symbol?: string;
  intervals?: string[];
  startDate: string;
  endDate: string;
  initialCapital: number;
  strategyConfig?: BacktestStrategyConfig;
  executionConfig: BacktestExecutionConfigRequest;
  enableAuditLog?: boolean;
  strategyId?: string;
}

// Update BacktestResult — add strategy fields after existing fields:
export interface BacktestResult {
  // ... existing fields ...
  strategyId?: string | null;
  strategyRevisionId?: number | null;
  strategyName?: string | null;
}

// Update BacktestSummary — add strategy fields:
export interface BacktestSummary {
  // ... existing fields ...
  strategyId?: string | null;
  strategyRevisionId?: number | null;
  strategyName?: string | null;
}
```

##### Pattern References

Based on `frontend/trading-ui/src/app/core/models/backtest.model.ts` — existing interface definitions.

### Task 3.2: Update BacktestService {#task-32-update-backtest-service}

Add strategy-scoped backtest list method to `BacktestService`.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/core/services/backtest.service.ts` — Add `getBacktestsByStrategy` method
- **Success**:
  - New method calls `GET /api/strategies/{id}/backtests` with pagination params
  - Returns `Observable<PagedResult<BacktestSummary>>`
- **Dependencies**: Task 3.1

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/services/backtest.service.ts — modification
// Add new method:

  public getBacktestsByStrategy(
    strategyId: string,
    page = 1,
    pageSize = 20,
    context?: HttpContext
  ): Observable<PagedResult<BacktestSummary>> {
    const encodedId = encodeURIComponent(strategyId);
    return this._apiClient.get<PagedResult<BacktestSummary>>(
      `strategies/${encodedId}/backtests?page=${page}&pageSize=${pageSize}`,
      context
    );
  }
```

##### Pattern References

Based on `frontend/trading-ui/src/app/core/services/backtest.service.ts` — existing `getBacktestList` method.

### Task 3.3: Refactor Backtest Form {#task-33-refactor-backtest-form}

Replace the manual strategy configuration fields with a strategy picker (mat-select dropdown). When a strategy is selected, display its configuration as read-only and only retain backtest-specific editable fields (date range, initial capital, fees).

- **Complexity**: High
- **Risk Factors**: Significant restructuring of the form component; must handle both strategy-loaded and deep-link scenarios; must handle loading/error states for strategy fetch
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.ts` — Major refactor
  - `frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.html` — Replace strategy fields with picker + read-only preview
  - `frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.scss` — Style read-only preview section
- **Success**:
  - Strategy picker dropdown loads strategies from `StrategyApiService.getStrategies()`
  - Selecting a strategy fetches full config via `getStrategy(id)` and displays it read-only
  - Symbol and intervals are derived from strategy config (not editable)
  - Only date range, initial capital, maker fee, taker fee, slippage remain editable
  - Form emits `BacktestRequest` with `strategyId` (no `strategyConfig` or `symbol`)
  - `@Input strategyId` pre-selects a strategy (for deep-link / navigation)
  - Loading indicator while strategies load
  - Error state if strategy fetch fails
- **Dependencies**: Tasks 3.1, 3.2

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.ts — major refactor

// New imports needed:
import { StrategyApiService } from "../../strategy-builder/services/strategy-api.service";
import { StrategyDto, StrategySummaryDto } from "../../strategy-builder/models/strategy.model";

// Component class changes:
export class BacktestFormComponent implements OnInit, OnChanges {
  private readonly _strategyApi = inject(StrategyApiService);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _snackBar = inject(MatSnackBar);

  // New inputs
  @Input() public strategyId: string | null = null;

  // New state
  public strategies: StrategySummaryDto[] = [];
  public selectedStrategy: StrategyDto | null = null;
  public isLoadingStrategies = false;
  public isLoadingStrategy = false;

  // Simplified form — only backtest-specific fields remain
  public form = new FormGroup({
    strategyId: new FormControl<string>("", { nonNullable: true, validators: [Validators.required] }),
    startDate: new FormControl<string>("", { nonNullable: true, validators: [Validators.required] }),
    endDate: new FormControl<string>("", { nonNullable: true, validators: [Validators.required] }),
    initialCapital: new FormControl<number>(10000, { nonNullable: true, validators: [Validators.required, Validators.min(1)] }),
    makerFee: new FormControl<number>(0.02, { nonNullable: true }),
    takerFee: new FormControl<number>(0.05, { nonNullable: true }),
    slippage: new FormControl<number>(0.01, { nonNullable: true }),
    enableAuditLog: new FormControl<boolean>(true, { nonNullable: true }),
  });

  public ngOnInit(): void {
    this._loadStrategies();
  }

  public ngOnChanges(changes: SimpleChanges): void {
    if (changes["strategyId"] && this.strategyId) {
      this.form.controls.strategyId.setValue(this.strategyId);
      this._loadSelectedStrategy(this.strategyId);
    }
  }

  public onStrategySelected(strategyId: string): void {
    this._loadSelectedStrategy(strategyId);
  }

  public onRunBacktest(): void {
    if (!this.form.valid || !this.selectedStrategy) return;

    const value = this.form.getRawValue();
    const request: BacktestRequest = {
      strategyId: value.strategyId,
      startDate: value.startDate,
      endDate: value.endDate,
      initialCapital: value.initialCapital,
      executionConfig: {
        makerFee: value.makerFee,
        takerFee: value.takerFee,
        slippage: value.slippage,
      },
      enableAuditLog: value.enableAuditLog,
    };
    this.runBacktest.emit(request);
  }

  private _loadStrategies(): void {
    this.isLoadingStrategies = true;
    this._strategyApi.getStrategies()
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe({
        next: (strategies) => {
          this.strategies = strategies;
          this.isLoadingStrategies = false;
          // If strategyId was provided via input, auto-select
          if (this.strategyId) {
            this.form.controls.strategyId.setValue(this.strategyId);
            this._loadSelectedStrategy(this.strategyId);
          }
        },
        error: () => {
          this.isLoadingStrategies = false;
        },
      });
  }

  private _loadSelectedStrategy(strategyId: string): void {
    this.isLoadingStrategy = true;
    this._strategyApi.getStrategy(strategyId)
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe({
        next: (strategy) => {
          this.selectedStrategy = strategy;
          this.isLoadingStrategy = false;
        },
        error: () => {
          this.selectedStrategy = null;
          this.isLoadingStrategy = false;
          this._snackBar.open("Strategy not found. Please select a different strategy.", "Close", { duration: 5000 });
          this.form.controls.strategyId.reset();
        },
      });
  }
}
```

```html
<!-- frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.html — major refactor -->

<mat-card>
  <mat-card-header>
    <mat-card-title>Run Backtest</mat-card-title>
  </mat-card-header>
  <mat-card-content>
    <form [formGroup]="form" (ngSubmit)="onRunBacktest()">

      <!-- Strategy Picker -->
      <mat-form-field appearance="outline" class="full-width">
        <mat-label>Strategy</mat-label>
        <mat-select formControlName="strategyId" (selectionChange)="onStrategySelected($event.value)">
          @for (strategy of strategies; track strategy.id) {
            <mat-option [value]="strategy.id">
              {{ strategy.name }} ({{ strategy.market }} / {{ strategy.timeframe }})
            </mat-option>
          }
        </mat-select>
        @if (isLoadingStrategies) {
          <mat-hint>Loading strategies...</mat-hint>
        }
      </mat-form-field>

      <!-- Read-only Strategy Preview -->
      @if (selectedStrategy) {
        <div class="strategy-preview">
          <h4>Strategy Configuration</h4>
          <div class="preview-grid">
            <div class="preview-item">
              <span class="label">Name</span>
              <span class="value">{{ selectedStrategy.config.strategyName }}</span>
            </div>
            <div class="preview-item">
              <span class="label">Market</span>
              <span class="value">{{ selectedStrategy.config.market }}</span>
            </div>
            <div class="preview-item">
              <span class="label">Timeframe</span>
              <span class="value">{{ selectedStrategy.config.timeframe }}</span>
            </div>
            <div class="preview-item">
              <span class="label">Direction</span>
              <span class="value">{{ selectedStrategy.config.direction }}</span>
            </div>
            @if (selectedStrategy.config.grid) {
              <div class="preview-item">
                <span class="label">Grid Levels</span>
                <span class="value">{{ selectedStrategy.config.grid.levels }}</span>
              </div>
              <div class="preview-item">
                <span class="label">Grid Spacing</span>
                <span class="value">{{ selectedStrategy.config.grid.spacing }}%</span>
              </div>
            }
            <div class="preview-item">
              <span class="label">Leverage</span>
              <span class="value">{{ selectedStrategy.config.risk.leverage }}x</span>
            </div>
            <div class="preview-item">
              <span class="label">Position Size</span>
              <span class="value">{{ selectedStrategy.config.risk.positionSizeValue }}%</span>
            </div>
          </div>
        </div>
      }

      <!-- Backtest-specific fields -->
      <div class="backtest-params">
        <h4>Backtest Parameters</h4>
        <!-- Date range -->
        <mat-form-field appearance="outline">
          <mat-label>Start Date</mat-label>
          <input matInput [matDatepicker]="startPicker" formControlName="startDate" />
          <mat-datepicker-toggle matSuffix [for]="startPicker"></mat-datepicker-toggle>
          <mat-datepicker #startPicker></mat-datepicker>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>End Date</mat-label>
          <input matInput [matDatepicker]="endPicker" formControlName="endDate" />
          <mat-datepicker-toggle matSuffix [for]="endPicker"></mat-datepicker-toggle>
          <mat-datepicker #endPicker></mat-datepicker>
        </mat-form-field>

        <!-- Capital & Fees -->
        <mat-form-field appearance="outline">
          <mat-label>Initial Capital</mat-label>
          <input matInput type="number" formControlName="initialCapital" />
          <span matTextPrefix>$&nbsp;</span>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Maker Fee (%)</mat-label>
          <input matInput type="number" formControlName="makerFee" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Taker Fee (%)</mat-label>
          <input matInput type="number" formControlName="takerFee" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Slippage (%)</mat-label>
          <input matInput type="number" formControlName="slippage" />
        </mat-form-field>

        <mat-checkbox formControlName="enableAuditLog">Enable Audit Log</mat-checkbox>
      </div>

      <button mat-raised-button color="primary" type="submit"
              [disabled]="!form.valid || !selectedStrategy || isLoadingStrategy">
        Run Backtest
      </button>
    </form>
  </mat-card-content>
</mat-card>
```

##### Pattern References

- Component structure based on `frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.ts` — existing form component
- `mat-select` pattern from existing symbol dropdown in same component
- `StrategyApiService` pattern from `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-api.service.ts`

### Task 3.4: Update Backtest Page {#task-34-update-backtest-page}

Update `BacktestPageComponent` to read `strategyId` from query parameters and pass it to the backtest form for pre-selection.

- **Complexity**: Medium
- **Risk Factors**: Must handle invalid/missing strategyId gracefully (toast notification)
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/backtest-page.component.ts` — Read `strategyId` from `ActivatedRoute.queryParamMap`
  - `frontend/trading-ui/src/app/features/backtesting/backtest-page.component.html` — Pass `[strategyId]` to form component
- **Success**:
  - Navigating to `/backtesting?strategyId=xxx` pre-selects the strategy in the picker
  - Invalid UUID shows a snackbar/toast notification
  - Missing query param shows empty picker (existing behavior)
- **Dependencies**: Task 3.3

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/backtesting/backtest-page.component.ts — modification
// Add ActivatedRoute injection and strategyId reading:

  private readonly _route = inject(ActivatedRoute);
  public strategyId: string | null = null;

  public ngOnInit(): void {
    this.strategyId = this._route.snapshot.queryParamMap.get("strategyId");
    // ... existing initialization ...
  }
```

```html
<!-- frontend/trading-ui/src/app/features/backtesting/backtest-page.component.html — modification -->
<!-- Update backtest form usage to pass strategyId: -->

<app-backtest-form
  [strategyId]="strategyId"
  [prefillConfig]="prefillConfig"
  (runBacktest)="onRunBacktest($event)">
</app-backtest-form>
```

##### Pattern References

Based on `frontend/trading-ui/src/app/features/backtesting/backtest-page.component.ts` — existing component pattern. Query param reading follows `ActivatedRoute.snapshot.queryParamMap` pattern used in strategy builder for route params.

### Task 3.5: Build and Lint {#task-35-build-and-lint}

Verify the frontend builds and passes linting.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None — verification step only
- **Success**:
  - `npx ng build` succeeds
  - `npx ng lint` passes
- **Dependencies**: Tasks 3.1–3.4

## Phase Success Criteria

- Strategy picker dropdown loads saved strategies
- Selecting a strategy shows read-only configuration preview
- Only backtest-specific fields (date range, capital, fees) are editable
- Deep-link via `?strategyId` pre-selects the strategy
- Frontend builds and lints cleanly
