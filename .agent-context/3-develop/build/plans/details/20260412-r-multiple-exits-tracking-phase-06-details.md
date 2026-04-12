<!-- markdownlint-disable-file -->

# Task Details: R-Multiple Exit Types & Trade Tracking

## Phase 6: Frontend — Backtest Results Display

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — standalone components, `inject()`, DDX design system, SCSS, control flow syntax
- `frontend/trading-ui/src/app/features/backtesting/backtest-result/` — existing KPI card pattern
- `frontend/trading-ui/src/app/features/backtesting/trade-log-table/` — existing sortable column table
- `frontend/trading-ui/src/app/core/models/backtest.model.ts` — TypeScript models

### Task 6.1: Update backtest TypeScript models {#task-61-update-backtest-typescript-models}

Add R-multiple fields to `BacktestResult` and `BacktestTrade` interfaces in the TypeScript models.

- **Complexity**: Low
- **Risk Factors**: None — additive nullable fields
- **Files**:
  - `frontend/trading-ui/src/app/core/models/backtest.model.ts` — add R fields to interfaces
- **Success**:
  - `BacktestResult` has expectancy, profitFactor, sqn, avgWinR, avgLossR, rWinRate, rDistribution
  - `BacktestTrade` has initialRDollars, rMultipleResult, mfe, mae
- **Dependencies**: Phase 4 (API)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/models/backtest.model.ts — modification

// Add to BacktestResult interface:
export interface BacktestResult {
  // ... existing fields ...
  expectancy?: number | null;
  profitFactor?: number | null;
  sqn?: number | null;
  avgWinR?: number | null;
  avgLossR?: number | null;
  rWinRate?: number | null;
  rDistribution?: number[] | null;
}

// Add to BacktestTrade interface:
export interface BacktestTrade {
  // ... existing fields ...
  initialRDollars?: number | null;
  rMultipleResult?: number | null;
  mfe?: number | null;
  mae?: number | null;
}
```

##### Pattern References

- `frontend/trading-ui/src/app/core/models/backtest.model.ts` — existing `BacktestResult` and `BacktestTrade` interfaces

### Task 6.2: Add R-metric KPI cards to backtest-result component {#task-62-add-r-metric-kpi-cards}

Add a conditional section of KPI cards for R-multiple metrics (Expectancy, Profit Factor, SQN, Win Rate, Avg Winner, Avg Loser) to the backtest result component. Only shown when the backtest used RiskBased mode and has R data.

- **Complexity**: Medium
- **Risk Factors**: Must conditionally hide for non-RiskBased backtests
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.html` — add R-metric card section
  - `frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.ts` — add computed properties
  - `frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.scss` — style if needed
- **Success**:
  - R-metric cards show Expectancy, Profit Factor, SQN, Win Rate, Avg Winner R, Avg Loser R
  - Cards only appear when `expectancy` is not null
  - Follows existing KPI card styling
- **Dependencies**: Task 6.1

#### Implementation Details

```typescript
// backtest-result.component.ts — modification
// Add computed property:
public get hasRMetrics(): boolean {
  return this.result?.expectancy != null;
}
```

```html
<!-- backtest-result.component.html — modification -->
<!-- Add after existing KPI cards section: -->
@if (hasRMetrics) {
  <h3 class="backtest-result__section-title">R-Multiple Metrics</h3>
  <div class="backtest-result__cards">
    <mat-card>
      <mat-card-header><mat-card-title>Expectancy</mat-card-title></mat-card-header>
      <mat-card-content>{{ result.expectancy | number:'1.2-2' }}R</mat-card-content>
    </mat-card>
    <mat-card>
      <mat-card-header><mat-card-title>Profit Factor</mat-card-title></mat-card-header>
      <mat-card-content>{{ result.profitFactor | number:'1.2-2' }}</mat-card-content>
    </mat-card>
    <mat-card>
      <mat-card-header><mat-card-title>SQN</mat-card-title></mat-card-header>
      <mat-card-content>{{ result.sqn | number:'1.2-2' }}</mat-card-content>
    </mat-card>
    <mat-card>
      <mat-card-header><mat-card-title>R Win Rate</mat-card-title></mat-card-header>
      <mat-card-content>{{ result.rWinRate | number:'1.1-1' }}%</mat-card-content>
    </mat-card>
    <mat-card>
      <mat-card-header><mat-card-title>Avg Winner</mat-card-title></mat-card-header>
      <mat-card-content>{{ result.avgWinR | number:'1.2-2' }}R</mat-card-content>
    </mat-card>
    <mat-card>
      <mat-card-header><mat-card-title>Avg Loser</mat-card-title></mat-card-header>
      <mat-card-content>{{ result.avgLossR | number:'1.2-2' }}R</mat-card-content>
    </mat-card>
  </div>
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.html` — existing `mat-card` KPI grid pattern
- `frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.ts` — existing component

### Task 6.3: Add R columns to trade-log-table {#task-63-add-r-columns-to-trade-log-table}

Add `InitialR`, `R-Multiple`, `MFE`, `MAE` columns to the trade log table. Only show these columns when R data is available.

- **Complexity**: Medium
- **Risk Factors**: Column visibility must be dynamic
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.html` — add columns
  - `frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.ts` — update SortableColumn type, add hasRData check
- **Success**:
  - R columns appear when at least one trade has rMultipleResult
  - R columns are hidden for non-RiskBased backtests
  - Columns are sortable
- **Dependencies**: Task 6.1

#### Implementation Details

```typescript
// trade-log-table.component.ts — modification
// Update SortableColumn type:
type SortableColumn = "entryTime" | "exitTime" | "entryPrice" | "exitPrice"
  | "side" | "size" | "pnl" | "fees" | "exitReason"
  | "rMultipleResult" | "mfe" | "mae";

// Add computed property:
public get hasRData(): boolean {
  return this.trades?.some(t => t.rMultipleResult != null) ?? false;
}
```

```html
<!-- trade-log-table.component.html — modification -->
<!-- Add columns in thead and tbody, conditionally: -->
@if (hasRData) {
  <th (click)="sort('rMultipleResult')">R-Multiple</th>
  <th (click)="sort('mfe')">MFE</th>
  <th (click)="sort('mae')">MAE</th>
}

<!-- In tbody row: -->
@if (hasRData) {
  <td>{{ trade.rMultipleResult != null ? (trade.rMultipleResult | number:'1.2-2') + 'R' : '—' }}</td>
  <td>{{ trade.mfe != null ? (trade.mfe | number:'1.2-2') + 'R' : '—' }}</td>
  <td>{{ trade.mae != null ? (trade.mae | number:'1.2-2') + 'R' : '—' }}</td>
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.ts` — existing `SortableColumn` union and sort logic
- `frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.html` — existing column pattern

### Task 6.4: Add R-distribution histogram component {#task-64-add-r-distribution-histogram-component}

Create a simple R-distribution histogram component using CSS bar chart (not lightweight-charts, which is time-keyed and unsuitable for value-keyed distributions). Buckets: <-1R, -1R to 0, 0 to 1R, 1R to 2R, 2R to 3R, >3R.

- **Complexity**: Medium
- **Risk Factors**: Custom bar chart rendering
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/r-distribution-chart/r-distribution-chart.component.ts` — new component
  - `frontend/trading-ui/src/app/features/backtesting/r-distribution-chart/r-distribution-chart.component.html` — template
  - `frontend/trading-ui/src/app/features/backtesting/r-distribution-chart/r-distribution-chart.component.scss` — styles
  - `frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.html` — embed histogram
- **Success**:
  - Histogram shows 6 buckets with trade count per bucket
  - Color-coded: red for negative R, green for positive R
  - Only shown when rDistribution has data
- **Dependencies**: Task 6.1

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/backtesting/r-distribution-chart/r-distribution-chart.component.ts — new file
import { Component, Input, OnChanges } from "@angular/core";
import { CommonModule } from "@angular/common";

interface RBucket {
  label: string;
  count: number;
  percent: number;
  isPositive: boolean;
}

@Component({
  selector: "app-r-distribution-chart",
  standalone: true,
  imports: [CommonModule],
  templateUrl: "./r-distribution-chart.component.html",
  styleUrl: "./r-distribution-chart.component.scss",
})
export class RDistributionChartComponent implements OnChanges {
  @Input() public rDistribution: number[] | null = null;

  public buckets: RBucket[] = [];

  public ngOnChanges(): void {
    if (!this.rDistribution || this.rDistribution.length === 0) {
      this.buckets = [];
      return;
    }

    const ranges = [
      { label: "< -1R", test: (r: number) => r < -1, isPositive: false },
      { label: "-1R to 0", test: (r: number) => r >= -1 && r < 0, isPositive: false },
      { label: "0 to 1R", test: (r: number) => r >= 0 && r < 1, isPositive: true },
      { label: "1R to 2R", test: (r: number) => r >= 1 && r < 2, isPositive: true },
      { label: "2R to 3R", test: (r: number) => r >= 2 && r < 3, isPositive: true },
      { label: "> 3R", test: (r: number) => r >= 3, isPositive: true },
    ];

    const total = this.rDistribution.length;
    this.buckets = ranges.map(range => {
      const count = this.rDistribution!.filter(range.test).length;
      return {
        label: range.label,
        count,
        percent: total > 0 ? Math.round((count / total) * 100) : 0,
        isPositive: range.isPositive,
      };
    });
  }
}
```

```html
<!-- r-distribution-chart.component.html — new file -->
<div class="r-distribution">
  <h4>R-Multiple Distribution</h4>
  <div class="r-distribution__chart">
    @for (bucket of buckets; track bucket.label) {
      <div class="r-distribution__bar-group">
        <span class="r-distribution__label">{{ bucket.label }}</span>
        <div class="r-distribution__bar-track">
          <div
            class="r-distribution__bar-fill"
            [class.r-distribution__bar-fill--positive]="bucket.isPositive"
            [class.r-distribution__bar-fill--negative]="!bucket.isPositive"
            [style.width.%]="bucket.percent">
          </div>
        </div>
        <span class="r-distribution__count">{{ bucket.count }}</span>
      </div>
    }
  </div>
</div>
```

```scss
// r-distribution-chart.component.scss — new file
.r-distribution {
  &__chart {
    display: flex;
    flex-direction: column;
    gap: 4px;
  }

  &__bar-group {
    display: flex;
    align-items: center;
    gap: 8px;
  }

  &__label {
    width: 80px;
    font-size: 12px;
    text-align: right;
  }

  &__bar-track {
    flex: 1;
    height: 20px;
    background: rgba(255, 255, 255, 0.05);
    border-radius: 4px;
    overflow: hidden;
  }

  &__bar-fill {
    height: 100%;
    border-radius: 4px;
    transition: width 0.3s ease;

    &--positive {
      background-color: #4caf50;
    }

    &--negative {
      background-color: #f44336;
    }
  }

  &__count {
    width: 30px;
    font-size: 12px;
    text-align: left;
  }
}
```

Embed in backtest-result component:

```html
<!-- backtest-result.component.html — modification -->
<!-- Inside the @if (hasRMetrics) section, after KPI cards: -->
@if (result.rDistribution?.length) {
  <app-r-distribution-chart [rDistribution]="result.rDistribution" />
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/backtesting/equity-chart/equity-chart.component.ts` — standalone component lifecycle pattern
- `frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.html` — embedding child components

### Task 6.5: Conditional display based on RiskBased mode {#task-65-conditional-display-based-on-riskbased-mode}

Ensure R-metric sections are only shown when the backtest used RiskBased position sizing.

- **Complexity**: Low
- **Risk Factors**: None — the `hasRMetrics` check already handles this via null expectancy
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.ts` — verify guard
- **Success**:
  - PercentWallet/FixedNotional backtests show no R section
  - RiskBased backtests with R data show the R section
- **Dependencies**: Tasks 6.2, 6.3, 6.4

### Task 6.6: Frontend build and lint {#task-66-frontend-build-and-lint}

Run frontend build and lint to verify no errors.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None
- **Success**:
  - `npm run build` succeeds
  - `npm run lint` reports no errors
- **Dependencies**: Task 6.5

## Phase Success Criteria

- R-metric KPI cards display in backtest results for RiskBased mode
- Trade log table shows R-Multiple, MFE, MAE columns when R data exists
- R-distribution histogram renders with correct bucketing and color coding
- R sections hidden for non-RiskBased backtests
- Frontend builds and lints cleanly
