<!-- markdownlint-disable-file -->

# Task Details: Backtest UI Dashboard (F5)

## Phase 3: Frontend — Run Form & Coverage Validation

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — `inject()`, reactive forms, `FormBuilder`, typed `FormGroup<T>`, `Validators`, `takeUntilDestroyed`, `mat-form-field appearance="outline"`, inline error messages
- `.agent-context/0-knowledge/13-strategy-config-schema.md` — full strategy config schema (grid levels, spacing, TP, hedge, risk)
- `.agent-context/0-knowledge/18-backtesting-architecture.md` — BacktestConfig fields: Symbol, Intervals, StartDateUtc, EndDateUtc, StrategyConfigJson, FeeModel

## Design References

- Form fields from PBI: symbol selector, date range picker, interval checkboxes, gridLevels, gridSpacing, takeProfitPercent, breakdownThreshold, makerFee, takerFee, slippage, positionSize, leverage, stopLossPercent
- Validation rules: gridLevels > 0, leverage > 0, startDate before endDate
- Default values should match Hyperliquid standard fees (maker: 0.01%, taker: 0.035%)
- "Validate Data" button calls GET /api/backtests/validate
- "Run Backtest" button calls POST /api/backtests with spinner/disabled state

### Task 3.1: Create BacktestFormComponent with reactive form {#task-31-create-backtestformcomponent-with-reactive-form}

Create the form component with all configuration fields, organised into logical sections (symbol/date, intervals, strategy config, fees).

- **Complexity**: High
- **Risk Factors**: Large number of form fields requires careful typing; date range picker needs `MatNativeDateModule` provider; interval checkboxes are multi-select (not standard form control)
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.ts` — new file
  - `frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.html` — new file
  - `frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.scss` — new file
  - `frontend/trading-ui/src/app/app.config.ts` — modification (add `provideNativeDateAdapter` for date picker)
- **Success**:
  - Form renders with all fields grouped logically
  - Typed `FormGroup` with explicit `FormControl` generics
  - Default values pre-populated (maker: 0.0001, taker: 0.00035, slippage: 0, gridLevels: 10, etc.)
  - Date picker allows start/end date selection
  - Interval checkboxes for 15m, 1h, 4h
  - Form emits `runBacktest` and `validateData` events
- **Dependencies**:
  - Phase 2 (models, service)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.ts — new file
import { Component, DestroyRef, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject } from "@angular/core";
import { FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { MatDatepickerModule } from "@angular/material/datepicker";
import { MatDividerModule } from "@angular/material/divider";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatIconModule } from "@angular/material/icon";
import { MatInputModule } from "@angular/material/input";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatSelectModule } from "@angular/material/select";
import { BacktestRequest, BacktestResult } from "../../../core/models/backtest.model";

interface BacktestFormModel {
  symbol: FormControl<string>;
  startDate: FormControl<Date | null>;
  endDate: FormControl<Date | null>;
  interval15m: FormControl<boolean>;
  interval1h: FormControl<boolean>;
  interval4h: FormControl<boolean>;
  gridLevels: FormControl<number>;
  gridSpacing: FormControl<number>;
  takeProfitPercent: FormControl<number>;
  breakdownThreshold: FormControl<number>;
  makerFee: FormControl<number>;
  takerFee: FormControl<number>;
  slippage: FormControl<number>;
  positionSize: FormControl<number>;
  leverage: FormControl<number>;
  stopLossPercent: FormControl<number>;
  initialCapital: FormControl<number>;
}

@Component({
  selector: "app-backtest-form",
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatDatepickerModule,
    MatDividerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule
  ],
  templateUrl: "./backtest-form.component.html",
  styleUrl: "./backtest-form.component.scss"
})
export class BacktestFormComponent implements OnChanges {
  private readonly _fb = inject(FormBuilder);

  @Input() public isRunning = false;
  @Input() public isValidating = false;
  @Input() public prefillConfig: BacktestResult | null = null;
  @Output() public runBacktest = new EventEmitter<BacktestRequest>();
  @Output() public validateData = new EventEmitter<{ symbol: string; intervals: string[]; startDate: string; endDate: string }>();

  public readonly symbols = ["BTC", "ETH", "SOL", "DOGE", "ARB", "OP"];

  public form: FormGroup<BacktestFormModel> = this._fb.group<BacktestFormModel>({
    symbol: this._fb.nonNullable.control("BTC"),
    startDate: this._fb.control<Date | null>(null, [Validators.required]),
    endDate: this._fb.control<Date | null>(null, [Validators.required]),
    interval15m: this._fb.nonNullable.control(true),
    interval1h: this._fb.nonNullable.control(true),
    interval4h: this._fb.nonNullable.control(true),
    gridLevels: this._fb.nonNullable.control(10, [Validators.required, Validators.min(1), Validators.max(50)]),
    gridSpacing: this._fb.nonNullable.control(0.5, [Validators.required, Validators.min(0.01)]),
    takeProfitPercent: this._fb.nonNullable.control(1.0, [Validators.required, Validators.min(0.01)]),
    breakdownThreshold: this._fb.nonNullable.control(2.0, [Validators.required, Validators.min(0)]),
    makerFee: this._fb.nonNullable.control(0.0001, [Validators.required, Validators.min(0)]),
    takerFee: this._fb.nonNullable.control(0.00035, [Validators.required, Validators.min(0)]),
    slippage: this._fb.nonNullable.control(0, [Validators.required, Validators.min(0)]),
    positionSize: this._fb.nonNullable.control(100, [Validators.required, Validators.min(1)]),
    leverage: this._fb.nonNullable.control(3, [Validators.required, Validators.min(1), Validators.max(50)]),
    stopLossPercent: this._fb.nonNullable.control(5.0, [Validators.required, Validators.min(0.1)]),
    initialCapital: this._fb.nonNullable.control(10000, [Validators.required, Validators.min(100)])
  });

  public ngOnChanges(changes: SimpleChanges): void {
    if (changes['prefillConfig'] && this.prefillConfig) {
      this._prefillFromResult(this.prefillConfig);
    }
  }

  public get isFormValid(): boolean {
    return this.form.valid && this.hasAtLeastOneInterval() && this.isDateRangeValid();
  }

  public onRunBacktest(): void {
    if (!this.isFormValid) return;
    const v = this.form.getRawValue();

    const request: BacktestRequest = {
      symbol: v.symbol,
      intervals: this.getSelectedIntervals(),
      startDateUtc: v.startDate!.getTime(),
      endDateUtc: v.endDate!.getTime(),
      initialCapital: v.initialCapital,
      feeModel: { makerFeeRate: v.makerFee, takerFeeRate: v.takerFee, slippageRate: v.slippage },
      warmupPeriod: 200,
      strategyConfigJson: JSON.stringify({
        grid: { levels: v.gridLevels, spacing: v.gridSpacing },
        exit: { takeProfitPercent: v.takeProfitPercent, stopLossPercent: v.stopLossPercent },
        entry: { breakdownThreshold: v.breakdownThreshold },
        risk: { leverage: v.leverage, positionSize: v.positionSize }
      })
    };

    this.runBacktest.emit(request);
  }

  public onValidateData(): void {
    const v = this.form.getRawValue();
    if (!v.startDate || !v.endDate) return;

    this.validateData.emit({
      symbol: v.symbol,
      intervals: this.getSelectedIntervals(),
      startDate: v.startDate.toISOString(),
      endDate: v.endDate.toISOString()
    });
  }

  public getSelectedIntervals(): string[] {
    const v = this.form.getRawValue();
    const intervals: string[] = [];
    if (v.interval15m) intervals.push("15m");
    if (v.interval1h) intervals.push("1h");
    if (v.interval4h) intervals.push("4h");
    return intervals;
  }

  public hasAtLeastOneInterval(): boolean {
    return this.getSelectedIntervals().length > 0;
  }

  private isDateRangeValid(): boolean {
    const v = this.form.getRawValue();
    return v.startDate != null && v.endDate != null && v.startDate < v.endDate;
  }

  private _prefillFromResult(result: BacktestResult): void {
    // Parse strategy config from the result to pre-fill form
    if (result.config) {
      const c = result.config;
      this.form.patchValue({
        symbol: c.symbol,
        startDate: new Date(c.startDateUtc),
        endDate: new Date(c.endDateUtc),
        interval15m: c.intervals.includes("15m"),
        interval1h: c.intervals.includes("1h"),
        interval4h: c.intervals.includes("4h"),
        makerFee: c.feeModel.makerFeeRate,
        takerFee: c.feeModel.takerFeeRate,
        slippage: c.feeModel.slippageRate,
        initialCapital: c.initialCapital
      });

      try {
        const stratConfig = JSON.parse(c.strategyConfigJson);
        this.form.patchValue({
          gridLevels: stratConfig.grid?.levels ?? 10,
          gridSpacing: stratConfig.grid?.spacing ?? 0.5,
          takeProfitPercent: stratConfig.exit?.takeProfitPercent ?? 1.0,
          stopLossPercent: stratConfig.exit?.stopLossPercent ?? 5.0,
          breakdownThreshold: stratConfig.entry?.breakdownThreshold ?? 2.0,
          leverage: stratConfig.risk?.leverage ?? 3,
          positionSize: stratConfig.risk?.positionSize ?? 100
        });
      } catch { /* ignore invalid JSON */ }
    }
  }
}
```

```html
<!-- frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.html — new file -->
<mat-card class="backtest-form">
  <mat-card-header>
    <mat-card-title>Configure Backtest</mat-card-title>
  </mat-card-header>

  <mat-card-content>
    <form [formGroup]="form" class="backtest-form__grid">
      <!-- Symbol & Date Range -->
      <section class="backtest-form__section">
        <h3 class="backtest-form__section-title">Symbol & Date Range</h3>

        <mat-form-field appearance="outline">
          <mat-label>Symbol</mat-label>
          <mat-select formControlName="symbol">
            @for (s of symbols; track s) {
              <mat-option [value]="s">{{ s }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Start Date</mat-label>
          <input matInput [matDatepicker]="startPicker" formControlName="startDate">
          <mat-datepicker-toggle matIconSuffix [for]="startPicker"></mat-datepicker-toggle>
          <mat-datepicker #startPicker></mat-datepicker>
          @if (form.controls.startDate.hasError('required')) {
            <mat-error>Start date is required</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>End Date</mat-label>
          <input matInput [matDatepicker]="endPicker" formControlName="endDate">
          <mat-datepicker-toggle matIconSuffix [for]="endPicker"></mat-datepicker-toggle>
          <mat-datepicker #endPicker></mat-datepicker>
          @if (form.controls.endDate.hasError('required')) {
            <mat-error>End date is required</mat-error>
          }
        </mat-form-field>
      </section>

      <!-- Intervals -->
      <section class="backtest-form__section">
        <h3 class="backtest-form__section-title">Intervals</h3>
        <div class="backtest-form__checkboxes">
          <mat-checkbox formControlName="interval15m">15m</mat-checkbox>
          <mat-checkbox formControlName="interval1h">1h</mat-checkbox>
          <mat-checkbox formControlName="interval4h">4h</mat-checkbox>
        </div>
        @if (!hasAtLeastOneInterval()) {
          <p class="backtest-form__error">Select at least one interval</p>
        }
      </section>

      <mat-divider></mat-divider>

      <!-- Grid Strategy -->
      <section class="backtest-form__section">
        <h3 class="backtest-form__section-title">Grid Strategy</h3>

        <mat-form-field appearance="outline">
          <mat-label>Grid Levels</mat-label>
          <input matInput type="number" formControlName="gridLevels">
          @if (form.controls.gridLevels.hasError('min')) {
            <mat-error>Must be at least 1</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Grid Spacing (%)</mat-label>
          <input matInput type="number" formControlName="gridSpacing" step="0.1">
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Take Profit (%)</mat-label>
          <input matInput type="number" formControlName="takeProfitPercent" step="0.1">
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Stop Loss (%)</mat-label>
          <input matInput type="number" formControlName="stopLossPercent" step="0.1">
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Breakdown Threshold (%)</mat-label>
          <input matInput type="number" formControlName="breakdownThreshold" step="0.1">
        </mat-form-field>
      </section>

      <mat-divider></mat-divider>

      <!-- Position & Risk -->
      <section class="backtest-form__section">
        <h3 class="backtest-form__section-title">Position & Risk</h3>

        <mat-form-field appearance="outline">
          <mat-label>Initial Capital ($)</mat-label>
          <input matInput type="number" formControlName="initialCapital">
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Position Size ($)</mat-label>
          <input matInput type="number" formControlName="positionSize">
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Leverage</mat-label>
          <input matInput type="number" formControlName="leverage">
          @if (form.controls.leverage.hasError('min')) {
            <mat-error>Must be at least 1</mat-error>
          }
        </mat-form-field>
      </section>

      <mat-divider></mat-divider>

      <!-- Fees -->
      <section class="backtest-form__section">
        <h3 class="backtest-form__section-title">Fees & Slippage</h3>

        <mat-form-field appearance="outline">
          <mat-label>Maker Fee Rate</mat-label>
          <input matInput type="number" formControlName="makerFee" step="0.0001">
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Taker Fee Rate</mat-label>
          <input matInput type="number" formControlName="takerFee" step="0.0001">
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Slippage Rate</mat-label>
          <input matInput type="number" formControlName="slippage" step="0.0001">
        </mat-form-field>
      </section>
    </form>
  </mat-card-content>

  <mat-card-actions class="backtest-form__actions">
    <button mat-stroked-button
            (click)="onValidateData()"
            [disabled]="isValidating || !form.controls.startDate.valid || !form.controls.endDate.valid">
      @if (isValidating) {
        <mat-spinner diameter="20"></mat-spinner>
      } @else {
        Validate Data
      }
    </button>

    <button mat-flat-button color="primary"
            (click)="onRunBacktest()"
            [disabled]="isRunning || !isFormValid">
      @if (isRunning) {
        <mat-spinner diameter="20"></mat-spinner>
      } @else {
        Run Backtest
      }
    </button>
  </mat-card-actions>
</mat-card>
```

```scss
// frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.scss — new file
.backtest-form {
  &__grid {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
  }

  &__section {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
    gap: 0.75rem;
    padding: 0.5rem 0;
  }

  &__section-title {
    grid-column: 1 / -1;
    margin: 0;
    color: var(--colour-label);
    font-size: 0.875rem;
    font-weight: 500;
    text-transform: uppercase;
    letter-spacing: 0.05em;
  }

  &__checkboxes {
    grid-column: 1 / -1;
    display: flex;
    gap: 1rem;
  }

  &__error {
    grid-column: 1 / -1;
    color: var(--colour-error-text);
    font-size: 0.75rem;
    margin: 0;
  }

  &__actions {
    display: flex;
    gap: 1rem;
    justify-content: flex-end;
    padding: 1rem 1rem 0.5rem;
  }
}
```

Add `provideNativeDateAdapter` to `app.config.ts`:

```typescript
// frontend/trading-ui/src/app/app.config.ts — modification
// Add to providers array:
import { provideNativeDateAdapter } from "@angular/material/core";
// ... in providers:
provideNativeDateAdapter(),
```

##### Pattern References

- `frontend/trading-ui/src/app/features/order-entry/order-entry.component.ts` — reactive form pattern with typed FormGroup, FormBuilder.inject(), Validators, EventEmitter outputs
- `frontend/trading-ui/src/app/features/order-entry/order-entry.component.html` — mat-form-field appearance="outline", mat-select, mat-error, button spinner

---

### Task 3.2: Create CoverageReportComponent {#task-32-create-coveragereportcomponent}

Create a component to display the data coverage validation results.

- **Complexity**: Low
- **Risk Factors**: None — simple display component
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/coverage-report/coverage-report.component.ts` — new file
  - `frontend/trading-ui/src/app/features/backtesting/coverage-report/coverage-report.component.html` — new file
  - `frontend/trading-ui/src/app/features/backtesting/coverage-report/coverage-report.component.scss` — new file
- **Success**:
  - Displays coverage status per interval (full/partial/none) with colour indicators
  - Shows candle count and date range per interval
  - Accepts `CoverageReport` as `@Input()`

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/backtesting/coverage-report/coverage-report.component.ts — new file
import { Component, Input } from "@angular/core";
import { DatePipe, DecimalPipe } from "@angular/common";
import { MatCardModule } from "@angular/material/card";
import { MatIconModule } from "@angular/material/icon";
import { CoverageReport, IntervalCoverage } from "../../../core/models/backtest.model";

@Component({
  selector: "app-coverage-report",
  standalone: true,
  imports: [MatCardModule, MatIconModule, DecimalPipe, DatePipe],
  templateUrl: "./coverage-report.component.html",
  styleUrl: "./coverage-report.component.scss"
})
export class CoverageReportComponent {
  @Input() public report: CoverageReport | null = null;

  public getStatusIcon(status: IntervalCoverage["status"]): string {
    switch (status) {
      case "full": return "check_circle";
      case "partial": return "warning";
      case "none": return "cancel";
    }
  }

  public getStatusClass(status: IntervalCoverage["status"]): string {
    return `coverage-report__status--${status}`;
  }
}
```

```html
<!-- frontend/trading-ui/src/app/features/backtesting/coverage-report/coverage-report.component.html — new file -->
@if (report) {
  <mat-card class="coverage-report">
    <mat-card-header>
      <mat-card-title>Data Coverage — {{ report.symbol }}</mat-card-title>
    </mat-card-header>
    <mat-card-content>
      <table class="coverage-report__table">
        <thead>
          <tr>
            <th>Interval</th>
            <th>Status</th>
            <th>Candles</th>
            <th>Coverage</th>
            <th>Available Range</th>
          </tr>
        </thead>
        <tbody>
          @for (interval of report.intervals; track interval.interval) {
            <tr>
              <td>{{ interval.interval }}</td>
              <td [class]="getStatusClass(interval.status)">
                <mat-icon>{{ getStatusIcon(interval.status) }}</mat-icon>
                {{ interval.status }}
              </td>
              <td>{{ interval.candleCount | number }}</td>
              <td>{{ interval.coveragePercent | number:'1.0-1' }}%</td>
              <td>{{ interval.earliestDate | date:'short' }} — {{ interval.latestDate | date:'short' }}</td>
            </tr>
          }
        </tbody>
      </table>
    </mat-card-content>
  </mat-card>
}
```

Note: Since this project uses standalone components without `CommonModule`, you'll need to import `DecimalPipe` and `DatePipe` individually in the component's `imports` array.

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts` — table display pattern with @for loop
- `frontend/trading-ui/src/app/features/dashboard/account-summary/margin-ratio-indicator/margin-ratio-indicator.component.ts` — status class pattern

---

### Task 3.3: Wire form to BacktestService and handle responses {#task-33-wire-form-to-backtestservice-and-handle-responses}

Update `BacktestPageComponent` to integrate `BacktestFormComponent`, handle form events, and call `BacktestService`.

- **Complexity**: Medium
- **Risk Factors**: Must handle the POST request lifecycle (loading state, success, error) and coordinate state between form and results
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/backtest-page.component.ts` — modification
  - `frontend/trading-ui/src/app/features/backtesting/backtest-page.component.html` — modification
- **Success**:
  - "Run Backtest" triggers POST call with loading spinner
  - "Validate Data" triggers GET call with loading state
  - Successful backtest stores result in `latestResult`
  - Coverage report displays after validation
- **Dependencies**:
  - Task 3.1 (BacktestFormComponent)
  - Task 3.2 (CoverageReportComponent)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/backtesting/backtest-page.component.ts — modification
// Add to imports and update class:

import { BacktestFormComponent } from "./backtest-form/backtest-form.component";
import { CoverageReportComponent } from "./coverage-report/coverage-report.component";
import { BacktestService } from "../../core/services/backtest.service";
import { NotificationService } from "../../core/services/notification.service";
import { BacktestRequest, BacktestResult, BacktestSummary, CoverageReport } from "../../core/models/backtest.model";

// In @Component imports array, add: BacktestFormComponent, CoverageReportComponent
// Update class:

export class BacktestPageComponent {
  private readonly _backtestService = inject(BacktestService);
  private readonly _notifications = inject(NotificationService);

  public latestResult: BacktestResult | null = null;
  public coverageReport: CoverageReport | null = null;
  public isRunning = false;
  public isValidating = false;
  public selectedCompareIds: string[] = [];
  public prefillConfig: BacktestResult | null = null;
  public apiError: string | null = null;

  public onRunBacktest(request: BacktestRequest): void {
    this.isRunning = true;
    this.apiError = null;
    this._backtestService.runBacktest(request).subscribe({
      next: (result) => {
        this.latestResult = result;
        this.isRunning = false;
        this._notifications.success("Backtest completed successfully");
      },
      error: (err) => {
        this.isRunning = false;
        this._handleApiError(err);
      }
    });
  }

  public onValidateData(params: { symbol: string; intervals: string[]; startDate: string; endDate: string }): void {
    this.isValidating = true;
    this._backtestService.validateCoverage(params.symbol, params.intervals, params.startDate, params.endDate).subscribe({
      next: (report) => {
        this.coverageReport = report;
        this.isValidating = false;
      },
      error: (err) => {
        this.isValidating = false;
        this._handleApiError(err);
      }
    });
  }

  private _handleApiError(err: unknown): void {
    // Error interceptor handles global notification; store for inline display
    const httpErr = err as { status?: number; error?: { errorMessage?: string } };
    if (httpErr.status === 408) {
      this.apiError = "Backtest timed out. Try a shorter date range.";
    } else if (httpErr.status === 404) {
      this.apiError = "No candle data found. Use Validate Data to check coverage.";
    } else if (httpErr.status === 400) {
      this.apiError = httpErr.error?.errorMessage ?? "Invalid request. Check form values.";
    } else {
      this.apiError = "Unable to reach API. Check connection.";
    }
  }
}
```

```html
<!-- In backtest-page.component.html, replace the Run Backtest tab content: -->
    <mat-tab label="Run Backtest">
      <div class="backtest-page__tab-content">
        <app-backtest-form
          [isRunning]="isRunning"
          [isValidating]="isValidating"
          [prefillConfig]="prefillConfig"
          (runBacktest)="onRunBacktest($event)"
          (validateData)="onValidateData($event)">
        </app-backtest-form>

        @if (apiError) {
          <div class="backtest-page__error-banner">
            <p>{{ apiError }}</p>
            <button mat-button (click)="apiError = null">Dismiss</button>
          </div>
        }

        <app-coverage-report [report]="coverageReport"></app-coverage-report>

        <!-- BacktestResultComponent will be added in Phase 4 -->
      </div>
    </mat-tab>
```

##### Pattern References

- `frontend/trading-ui/src/app/features/order-entry/order-entry.component.ts` — component event handling, loading state, error handling with formatErrorPayload
- `frontend/trading-ui/src/app/core/services/notification.service.ts` — success/error notification pattern

---

### Task 3.4: Add form validation and error handling {#task-34-add-form-validation-and-error-handling}

Ensure comprehensive form validation including cross-field validation (date range) and API error mapping to form fields.

- **Complexity**: Medium
- **Risk Factors**: Must handle 400 validation errors from the API and map them to specific form fields
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.ts` — modification
  - `frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.html` — modification
  - `frontend/trading-ui/src/app/features/backtesting/backtest-page.component.scss` — modification
- **Success**:
  - All fields show inline error messages when invalid
  - Date range cross-validation (start < end) shows error
  - "Run Backtest" disabled when form invalid
  - API 400 errors mapped to relevant fields

#### Implementation Details

Add error banner styles:

```scss
// frontend/trading-ui/src/app/features/backtesting/backtest-page.component.scss — modification
// Add:
  &__error-banner {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 0.75rem 1rem;
    margin: 1rem 0;
    background: var(--colour-error-bg);
    color: var(--colour-error-text);
    border-radius: 4px;

    p {
      margin: 0;
    }
  }
```

##### Pattern References

- `frontend/trading-ui/src/app/features/order-entry/order-entry.component.html` — mat-error inline validation pattern
- `frontend/trading-ui/src/app/core/utils/error-utils.ts` — formatErrorPayload for API error extraction

---

### Task 3.5: Add unit tests for BacktestFormComponent {#task-35-add-unit-tests-for-backtestformcomponent}

Add tests covering form validation, default values, event emission, and prefill logic.

- **Complexity**: Medium
- **Risk Factors**: Must configure Angular Material and date adapter in test setup
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.spec.ts` — new file
- **Success**:
  - Tests cover: default values, validation rules, form submit emission, interval selection, prefill from result
  - All tests pass

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.spec.ts — new file
import { ComponentFixture, TestBed } from "@angular/core/testing";
import { NoopAnimationsModule } from "@angular/platform-browser/animations";
import { provideNativeDateAdapter } from "@angular/material/core";
import { BacktestFormComponent } from "./backtest-form.component";
import { BacktestRequest } from "../../../core/models/backtest.model";

describe("BacktestFormComponent", () => {
  let component: BacktestFormComponent;
  let fixture: ComponentFixture<BacktestFormComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BacktestFormComponent, NoopAnimationsModule],
      providers: [provideNativeDateAdapter()]
    }).compileComponents();

    fixture = TestBed.createComponent(BacktestFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it("should create with default values", () => {
    expect(component).toBeTruthy();
    expect(component.form.controls.symbol.value).toBe("BTC");
    expect(component.form.controls.gridLevels.value).toBe(10);
    expect(component.form.controls.makerFee.value).toBe(0.0001);
  });

  it("should be invalid without dates", () => {
    expect(component.isFormValid).toBeFalse();
  });

  it("should be valid with all required fields", () => {
    component.form.patchValue({
      startDate: new Date(2024, 0, 1),
      endDate: new Date(2024, 11, 31)
    });
    expect(component.isFormValid).toBeTrue();
  });

  it("should emit runBacktest event with correct request", () => {
    spyOn(component.runBacktest, "emit");
    component.form.patchValue({
      startDate: new Date(2024, 0, 1),
      endDate: new Date(2024, 11, 31)
    });

    component.onRunBacktest();

    expect(component.runBacktest.emit).toHaveBeenCalledWith(
      jasmine.objectContaining({ symbol: "BTC" })
    );
  });

  it("should not emit if form is invalid", () => {
    spyOn(component.runBacktest, "emit");
    component.onRunBacktest();
    expect(component.runBacktest.emit).not.toHaveBeenCalled();
  });

  it("should require gridLevels >= 1", () => {
    component.form.controls.gridLevels.setValue(0);
    expect(component.form.controls.gridLevels.hasError("min")).toBeTrue();
  });

  it("should require leverage >= 1", () => {
    component.form.controls.leverage.setValue(0);
    expect(component.form.controls.leverage.hasError("min")).toBeTrue();
  });
});
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.spec.ts` — component test with TestBed, @Input, fixture.detectChanges

---

### Task 3.6: Frontend build and lint {#task-36-frontend-build-and-lint}

Run Angular build, lint, and test to verify the form integration works correctly.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None — verification step
- **Success**:
  - `npx ng build` succeeds
  - `npx ng lint` passes
  - `npx ng test --watch=false` passes all tests

## Phase Success Criteria

- Backtest form renders with all configuration fields grouped logically
- Form validation prevents submission with invalid values
- "Validate Data" calls the coverage endpoint and displays results
- "Run Backtest" triggers the API call with loading spinner
- API errors display as inline messages
- Pre-fill from a previous result populates the form
- Frontend builds, lints, and all tests pass
