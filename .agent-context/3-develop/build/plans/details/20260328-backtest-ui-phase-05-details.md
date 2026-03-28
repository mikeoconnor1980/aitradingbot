<!-- markdownlint-disable-file -->

# Task Details: Backtest UI Dashboard (F5)

## Phase 5: Frontend — Past Results & Comparison

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — standalone components, `inject()`, EventEmitter, `@Input`/`@Output`, BEM SCSS, `takeUntilDestroyed`
- `.agent-context/0-knowledge/09-charting-library.md` — multi-series on same chart with different colours (comparison mode)
- `.agent-context/0-knowledge/11-angular-instructions.md` — Angular 19 standalone, mat-table, mat-paginator

## Design References

- Past results list: paginated table of BacktestSummary items with click-to-view and re-run-with-changes
- Comparison: two-column metrics table with delta values + overlaid equity curves
- Selection mechanism: checkboxes in the past results list to select up to two runs for comparison
- Error handling: 400 (inline), 404 (no data), 408 (timeout), network errors (banner with retry)

### Task 5.1: Create BacktestListComponent {#task-51-create-backtestlistcomponent}

Create a paginated list component that displays past backtest runs with key metrics. Supports selection of rows for comparison and a "Re-run with changes" action.

- **Complexity**: High
- **Risk Factors**: Pagination state management; checkbox selection limited to 2; row click navigation to detail view; coordinating with parent component for comparison and re-run workflows
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/backtest-list/backtest-list.component.ts` — new file
  - `frontend/trading-ui/src/app/features/backtesting/backtest-list/backtest-list.component.html` — new file
  - `frontend/trading-ui/src/app/features/backtesting/backtest-list/backtest-list.component.scss` — new file
- **Success**:
  - Paginated table loads from `GET /api/backtests`
  - Each row shows: run date, symbol, date range, intervals (badge-styled), total trades, win rate, total PnL, max drawdown
  - Clicking a row emits `viewResult` event with the backtest ID
  - "Re-run with changes" button emits `rerunConfig` event with the backtest ID
  - Checkboxes allow selecting up to 2 results; emits `compareSelected` event
  - Pagination controls (next/previous/page size)
  - Empty state when no past results exist
- **Dependencies**:
  - Phase 2 (BacktestService, models)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/backtesting/backtest-list/backtest-list.component.ts — new file
import { Component, EventEmitter, OnInit, Output, inject } from "@angular/core";
import { DatePipe, DecimalPipe } from "@angular/common";
import { MatButtonModule } from "@angular/material/button";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { MatIconModule } from "@angular/material/icon";
import { MatPaginatorModule, PageEvent } from "@angular/material/paginator";
import { MatTableModule } from "@angular/material/table";
import { MatTooltipModule } from "@angular/material/tooltip";
import { BacktestService } from "../../../core/services/backtest.service";
import { BacktestSummary, PagedResult } from "../../../core/models/backtest.model";

@Component({
  selector: "app-backtest-list",
  standalone: true,
  imports: [
    DatePipe,
    DecimalPipe,
    MatButtonModule,
    MatCheckboxModule,
    MatIconModule,
    MatPaginatorModule,
    MatTableModule,
    MatTooltipModule
  ],
  templateUrl: "./backtest-list.component.html",
  styleUrl: "./backtest-list.component.scss"
})
export class BacktestListComponent implements OnInit {
  private readonly _backtestService = inject(BacktestService);

  @Output() public viewResult = new EventEmitter<string>();
  @Output() public rerunConfig = new EventEmitter<string>();
  @Output() public compareSelected = new EventEmitter<string[]>();

  public results: BacktestSummary[] = [];
  public totalCount = 0;
  public page = 1;
  public pageSize = 20;
  public selectedIds = new Set<string>();
  public isLoading = false;

  public readonly displayedColumns = [
    "select", "createdAt", "symbol", "dateRange", "intervals", "totalTrades", "winRate", "totalPnl", "maxDrawdown", "actions"
  ];

  public ngOnInit(): void {
    this.loadPage();
  }

  public loadPage(): void {
    this.isLoading = true;
    this._backtestService.getBacktestList(this.page, this.pageSize)
      .subscribe({
        next: (result) => {
          this.results = result.items;
          this.totalCount = result.totalCount;
          this.isLoading = false;
        },
        error: () => {
          this.isLoading = false;
        }
      });
  }

  public onPageChange(event: PageEvent): void {
    this.page = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.loadPage();
  }

  public toggleSelection(id: string): void {
    if (this.selectedIds.has(id)) {
      this.selectedIds.delete(id);
    } else if (this.selectedIds.size < 2) {
      this.selectedIds.add(id);
    }
  }

  public isSelected(id: string): boolean {
    return this.selectedIds.has(id);
  }

  public canSelect(id: string): boolean {
    return this.selectedIds.has(id) || this.selectedIds.size < 2;
  }

  public onCompare(): void {
    this.compareSelected.emit([...this.selectedIds]);
  }

  public onViewResult(id: string): void {
    this.viewResult.emit(id);
  }

  public onRerun(id: string, event: Event): void {
    event.stopPropagation();
    this.rerunConfig.emit(id);
  }

  public getPnlClass(pnl: number): string {
    return pnl >= 0 ? "backtest-list__pnl--profit" : "backtest-list__pnl--loss";
  }
}
```

```html
<!-- frontend/trading-ui/src/app/features/backtesting/backtest-list/backtest-list.component.html — new file -->
<div class="backtest-list">
  @if (results.length === 0 && !isLoading) {
    <div class="backtest-list__empty">
      <mat-icon>science</mat-icon>
      <p>No backtests run yet. Configure and run your first backtest above.</p>
    </div>
  } @else {
    <div class="backtest-list__actions">
      <button mat-stroked-button
              [disabled]="selectedIds.size !== 2"
              (click)="onCompare()">
        <mat-icon>compare_arrows</mat-icon>
        Compare Selected ({{ selectedIds.size }}/2)
      </button>
    </div>

    <table mat-table [dataSource]="results" class="backtest-list__table">
      <ng-container matColumnDef="select">
        <th mat-header-cell *matHeaderCellDef></th>
        <td mat-cell *matCellDef="let row">
          <mat-checkbox
            [checked]="isSelected(row.id)"
            [disabled]="!canSelect(row.id)"
            (change)="toggleSelection(row.id)">
          </mat-checkbox>
        </td>
      </ng-container>

      <ng-container matColumnDef="createdAt">
        <th mat-header-cell *matHeaderCellDef>Run Date</th>
        <td mat-cell *matCellDef="let row">{{ row.createdAt | date:'short' }}</td>
      </ng-container>

      <ng-container matColumnDef="symbol">
        <th mat-header-cell *matHeaderCellDef>Symbol</th>
        <td mat-cell *matCellDef="let row">{{ row.symbol }}</td>
      </ng-container>

      <ng-container matColumnDef="dateRange">
        <th mat-header-cell *matHeaderCellDef>Date Range</th>
        <td mat-cell *matCellDef="let row">{{ row.startDate | date:'mediumDate' }} — {{ row.endDate | date:'mediumDate' }}</td>
      </ng-container>

      <ng-container matColumnDef="intervals">
        <th mat-header-cell *matHeaderCellDef>Intervals</th>
        <td mat-cell *matCellDef="let row">
          <span class="backtest-list__intervals-badge">{{ row.intervals.join(' ') }}</span>
        </td>
      </ng-container>

      <ng-container matColumnDef="totalTrades">
        <th mat-header-cell *matHeaderCellDef>Trades</th>
        <td mat-cell *matCellDef="let row">{{ row.totalTrades }}</td>
      </ng-container>

      <ng-container matColumnDef="winRate">
        <th mat-header-cell *matHeaderCellDef>Win Rate</th>
        <td mat-cell *matCellDef="let row">{{ row.winRate | number:'1.1-1' }}%</td>
      </ng-container>

      <ng-container matColumnDef="totalPnl">
        <th mat-header-cell *matHeaderCellDef>Total PnL</th>
        <td mat-cell *matCellDef="let row" [class]="getPnlClass(row.totalPnl)">
          ${{ row.totalPnl | number:'1.2-2' }}
        </td>
      </ng-container>

      <ng-container matColumnDef="maxDrawdown">
        <th mat-header-cell *matHeaderCellDef>Max Drawdown</th>
        <td mat-cell *matCellDef="let row" class="backtest-list__pnl--loss">
          ${{ row.maxDrawdown | number:'1.2-2' }}
        </td>
      </ng-container>

      <ng-container matColumnDef="actions">
        <th mat-header-cell *matHeaderCellDef></th>
        <td mat-cell *matCellDef="let row">
          <button mat-icon-button matTooltip="Re-run with changes" (click)="onRerun(row.id, $event)">
            <mat-icon>replay</mat-icon>
          </button>
        </td>
      </ng-container>

      <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
      <tr mat-row *matRowDef="let row; columns: displayedColumns"
          class="backtest-list__row"
          (click)="onViewResult(row.id)"></tr>
    </table>

    <mat-paginator
      [length]="totalCount"
      [pageSize]="pageSize"
      [pageSizeOptions]="[10, 20, 50]"
      (page)="onPageChange($event)">
    </mat-paginator>
  }
</div>
```

```scss
// frontend/trading-ui/src/app/features/backtesting/backtest-list/backtest-list.component.scss — new file
.backtest-list {
  &__actions {
    display: flex;
    justify-content: flex-end;
    margin-bottom: 0.75rem;
  }

  &__table {
    width: 100%;
  }

  &__row {
    cursor: pointer;

    &:hover {
      background: var(--colour-surface-alt);
    }
  }

  &__intervals-badge {
    display: inline-block;
    padding: 2px 8px;
    border-radius: 10px;
    font-size: 0.6875rem;
    font-weight: 600;
    background: rgba(var(--colour-profit-rgb, 0, 200, 151), 0.12);
    color: var(--colour-profit);
  }

  &__pnl--profit {
    color: var(--colour-profit);
    font-weight: 500;
  }

  &__pnl--loss {
    color: var(--colour-loss);
    font-weight: 500;
  }

  &__empty {
    display: flex;
    flex-direction: column;
    align-items: center;
    padding: 3rem;
    color: var(--colour-muted);
    text-align: center;

    mat-icon {
      font-size: 2rem;
      height: 2rem;
      width: 2rem;
      margin-bottom: 1rem;
    }
  }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/market-data/market-data.component.html` — mat-table with `matColumnDef`, `*matCellDef`
- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts` — row click handling, colour-coding
- `frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.ts` — action buttons in table rows

---

### Task 5.2: Create BacktestCompareComponent {#task-52-create-backtestcomparecomponent}

Create a comparison view showing run labels, configuration differences, overlaid equity curves, and side-by-side metrics with delta values. Layout and section order follows wireframe `mockup_backtest_compare.html`: Run Labels → Config Differences → Equity Chart → Metrics Table.

- **Complexity**: High
- **Risk Factors**: Calculating deltas between two results; determining "better/worse" for different metrics (higher PnL = green, deeper drawdown = red); building config diff by parsing `strategyConfigJson`; coordinating with EquityChartComponent for dual-series mode
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/backtest-compare/backtest-compare.component.ts` — new file
  - `frontend/trading-ui/src/app/features/backtesting/backtest-compare/backtest-compare.component.html` — new file
  - `frontend/trading-ui/src/app/features/backtesting/backtest-compare/backtest-compare.component.scss` — new file
- **Success**:
  - Run label cards show at top with colour-coded dots (green for A, blue for B) and run descriptions
  - Configuration Differences grid highlights changed parameters (red border) vs same parameters (muted text)
  - Equity curves from both runs overlaid on same chart in different colours
  - Metrics table shows Run A, Run B, and Delta columns with ▲/▼ arrows
  - Delta values highlighted (green for improvement, red for degradation)
  - Accepts two `BacktestResult` objects as `@Input()`
- **Dependencies**:
  - Phase 4 (EquityChartComponent)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/backtesting/backtest-compare/backtest-compare.component.ts — new file
import { Component, Input, OnChanges, SimpleChanges } from "@angular/core";
import { DatePipe, DecimalPipe } from "@angular/common";
import { MatCardModule } from "@angular/material/card";
import { MatTableModule } from "@angular/material/table";
import { BacktestResult } from "../../../core/models/backtest.model";
import { EquityChartComponent } from "../equity-chart/equity-chart.component";

interface ComparisonRow {
  metric: string;
  valueA: string;
  valueB: string;
  delta: string;
  deltaClass: string;
}

interface ConfigDiffItem {
  label: string;
  valueA: string;
  valueB: string;
  changed: boolean;
}

@Component({
  selector: "app-backtest-compare",
  standalone: true,
  imports: [DatePipe, DecimalPipe, MatCardModule, MatTableModule, EquityChartComponent],
  templateUrl: "./backtest-compare.component.html",
  styleUrl: "./backtest-compare.component.scss"
})
export class BacktestCompareComponent implements OnChanges {
  @Input({ required: true }) public resultA!: BacktestResult;
  @Input({ required: true }) public resultB!: BacktestResult;

  public readonly displayedColumns = ["metric", "valueA", "valueB", "delta"];
  public configDiffs: ConfigDiffItem[] = [];

  /** Run label description (e.g. "Grid Levels: 10") based on the most impactful config difference */
  public runALabel = "Run A";
  public runBLabel = "Run B";
  public runADetail = "";
  public runBDetail = "";

  public ngOnChanges(changes: SimpleChanges): void {
    if (changes["resultA"] || changes["resultB"]) {
      this._buildConfigDiff();
      this._buildRunLabels();
    }
  }

  public get comparisonRows(): ComparisonRow[] {
    return [
      this._row("Total PnL", this.resultA.totalPnL, this.resultB.totalPnL, "$", "higher"),
      this._row("Win Rate", this.resultA.winRate, this.resultB.winRate, "%", "higher"),
      this._row("Max Drawdown", this.resultA.maxDrawdownAbsolute, this.resultB.maxDrawdownAbsolute, "$", "closer-to-zero"),
      this._row("Total Trades", this.resultA.totalTrades, this.resultB.totalTrades, "", "neutral"),
      this._row("Winning Trades", this.resultA.winningTrades, this.resultB.winningTrades, "", "higher"),
      this._row("Losing Trades", this.resultA.losingTrades, this.resultB.losingTrades, "", "lower"),
      this._row("Avg Trade PnL", this.resultA.averageTradePnL, this.resultB.averageTradePnL, "$", "higher"),
      this._row("Avg Hold Time", 0, 0, "", "neutral", this.resultA.averageHoldTime, this.resultB.averageHoldTime),
      this._row("Hedges Opened", this.resultA.hedgesOpened, this.resultB.hedgesOpened, "", "neutral"),
      this._row("Total Fees", this.resultA.totalFeesPaid, this.resultB.totalFeesPaid, "$", "lower")
    ];
  }

  private _buildConfigDiff(): void {
    const configA = this._parseStrategyConfig(this.resultA.config?.strategyConfigJson);
    const configB = this._parseStrategyConfig(this.resultB.config?.strategyConfigJson);

    const params: { label: string; keyPath: (c: any) => string }[] = [
      { label: "Grid Levels", keyPath: c => c.grid?.levels ?? "—" },
      { label: "Grid Spacing", keyPath: c => c.grid?.spacing != null ? `${c.grid.spacing}%` : "—" },
      { label: "Take Profit", keyPath: c => c.exit?.takeProfitPercent != null ? `${c.exit.takeProfitPercent}%` : "—" },
      { label: "Leverage", keyPath: c => c.risk?.leverage != null ? `${c.risk.leverage}x` : "—" },
      { label: "Position Size", keyPath: c => c.risk?.positionSize != null ? `$${c.risk.positionSize}` : "—" },
      { label: "Stop Loss", keyPath: c => c.exit?.stopLossPercent != null ? `${c.exit.stopLossPercent}%` : "—" },
      { label: "Maker Fee", keyPath: c => this.resultA.config?.feeModel?.makerFeeRate != null ? `${(this.resultA.config.feeModel.makerFeeRate * 100).toFixed(3)}%` : "—" },
      { label: "Taker Fee", keyPath: c => this.resultA.config?.feeModel?.takerFeeRate != null ? `${(this.resultA.config.feeModel.takerFeeRate * 100).toFixed(3)}%` : "—" }
    ];

    this.configDiffs = params.map(p => {
      const vA = String(p.keyPath(configA));
      const vB = String(p.keyPath(configB));
      return { label: p.label, valueA: vA, valueB: vB, changed: vA !== vB };
    });
  }

  private _buildRunLabels(): void {
    const firstDiff = this.configDiffs.find(d => d.changed);
    if (firstDiff) {
      this.runALabel = `Run A — ${firstDiff.label}: ${firstDiff.valueA}`;
      this.runBLabel = `Run B — ${firstDiff.label}: ${firstDiff.valueB}`;
    }
    const cA = this.resultA.config;
    const cB = this.resultB.config;
    if (cA) {
      this.runADetail = `${cA.symbol} | ${new Date(cA.startDateUtc).toISOString().slice(0, 10)} — ${new Date(cA.endDateUtc).toISOString().slice(0, 10)}`;
    }
    if (cB) {
      this.runBDetail = `${cB.symbol} | ${new Date(cB.startDateUtc).toISOString().slice(0, 10)} — ${new Date(cB.endDateUtc).toISOString().slice(0, 10)}`;
    }
  }

  private _parseStrategyConfig(json: string | undefined): any {
    try { return JSON.parse(json ?? "{}"); } catch { return {}; }
  }

  private _row(
    metric: string,
    a: number,
    b: number,
    suffix: string,
    preference: "higher" | "lower" | "closer-to-zero" | "neutral",
    rawA?: string,
    rawB?: string
  ): ComparisonRow {
    // For string-based metrics like "Avg Hold Time", use raw values
    if (rawA != null && rawB != null) {
      return { metric, valueA: rawA, valueB: rawB, delta: "—", deltaClass: "compare__delta--neutral" };
    }

    const delta = a - b; // A minus B: positive means A is larger
    const absDelta = Math.abs(delta);
    const sign = delta > 0 ? "+" : delta < 0 ? "-" : "";
    const formatted = suffix === "$"
      ? `${sign}$${absDelta.toFixed(2)}`
      : suffix === "%"
        ? `${sign}${absDelta.toFixed(1)}%`
        : `${sign}${absDelta}`;

    let deltaClass = "compare__delta--neutral";
    let arrow = "";
    if (preference === "higher") {
      deltaClass = delta > 0 ? "compare__delta--better" : delta < 0 ? "compare__delta--worse" : "compare__delta--neutral";
      arrow = delta > 0 ? " ▲" : delta < 0 ? " ▼" : "";
    } else if (preference === "lower") {
      deltaClass = delta < 0 ? "compare__delta--better" : delta > 0 ? "compare__delta--worse" : "compare__delta--neutral";
      arrow = delta < 0 ? " ▲" : delta > 0 ? " ▼" : "";
    } else if (preference === "closer-to-zero") {
      deltaClass = Math.abs(a) < Math.abs(b) ? "compare__delta--better" : Math.abs(a) > Math.abs(b) ? "compare__delta--worse" : "compare__delta--neutral";
      arrow = Math.abs(a) < Math.abs(b) ? " ▲" : Math.abs(a) > Math.abs(b) ? " ▼" : "";
    }

    const fmt = (v: number) => suffix === "$" ? `$${v.toFixed(2)}` : suffix === "%" ? `${v.toFixed(1)}%` : `${v}`;

    return { metric, valueA: fmt(a), valueB: fmt(b), delta: `${formatted}${arrow}`, deltaClass };
  }
}
```

```html
<!-- frontend/trading-ui/src/app/features/backtesting/backtest-compare/backtest-compare.component.html — new file -->
<!-- Layout order per wireframe mockup_backtest_compare.html:
     Run Labels → Config Differences → Equity Chart → Metrics Table -->
<div class="compare">
  <!-- Run Labels (per wireframe) -->
  <div class="compare__run-labels">
    <div class="compare__run-label">
      <div class="compare__run-label-dot compare__run-label-dot--a"></div>
      <div>
        <div class="compare__run-label-name">{{ runALabel }}</div>
        <div class="compare__run-label-detail">{{ runADetail }}</div>
      </div>
    </div>
    <div class="compare__run-label">
      <div class="compare__run-label-dot compare__run-label-dot--b"></div>
      <div>
        <div class="compare__run-label-name">{{ runBLabel }}</div>
        <div class="compare__run-label-detail">{{ runBDetail }}</div>
      </div>
    </div>
  </div>

  <!-- Configuration Differences (per wireframe) -->
  <mat-card class="compare__config-diff">
    <mat-card-header>
      <mat-card-title>Configuration Differences</mat-card-title>
    </mat-card-header>
    <mat-card-content>
      <div class="compare__config-grid">
        @for (item of configDiffs; track item.label) {
          <div class="compare__config-item" [class.compare__config-item--changed]="item.changed">
            <div class="compare__config-item-label">{{ item.label }}</div>
            @if (item.changed) {
              <div class="compare__config-item-values">
                <span class="compare__config-value--a">{{ item.valueA }}</span>
                <span class="compare__config-value--vs">vs</span>
                <span class="compare__config-value--b">{{ item.valueB }}</span>
              </div>
            } @else {
              <div class="compare__config-item-values">
                <span class="compare__config-value--same">{{ item.valueA }} (same)</span>
              </div>
            }
          </div>
        }
      </div>
    </mat-card-content>
  </mat-card>

  <!-- Equity Curve Comparison (before metrics per wireframe) -->
  <mat-card class="compare__chart-card">
    <mat-card-header>
      <mat-card-title>Equity Curve Comparison</mat-card-title>
    </mat-card-header>
    <mat-card-content>
      <app-equity-chart
        [equityData]="resultA.equityTimeSeries"
        [trades]="resultA.tradeLog"
        [comparisonData]="resultB.equityTimeSeries"
        primaryLabel="Run A"
        comparisonLabel="Run B">
      </app-equity-chart>
    </mat-card-content>
  </mat-card>

  <!-- Metrics Comparison Table -->
  <mat-card class="compare__metrics">
    <mat-card-header>
      <mat-card-title>Metrics Comparison</mat-card-title>
    </mat-card-header>
    <mat-card-content>
      <table mat-table [dataSource]="comparisonRows" class="compare__table">
        <ng-container matColumnDef="metric">
          <th mat-header-cell *matHeaderCellDef>Metric</th>
          <td mat-cell *matCellDef="let row" class="compare__metric-name">{{ row.metric }}</td>
        </ng-container>

        <ng-container matColumnDef="valueA">
          <th mat-header-cell *matHeaderCellDef>
            Run A <span class="compare__header-dot compare__header-dot--a">●</span>
          </th>
          <td mat-cell *matCellDef="let row" class="compare__value--a">{{ row.valueA }}</td>
        </ng-container>

        <ng-container matColumnDef="valueB">
          <th mat-header-cell *matHeaderCellDef>
            Run B <span class="compare__header-dot compare__header-dot--b">●</span>
          </th>
          <td mat-cell *matCellDef="let row" class="compare__value--b">{{ row.valueB }}</td>
        </ng-container>

        <ng-container matColumnDef="delta">
          <th mat-header-cell *matHeaderCellDef>Delta</th>
          <td mat-cell *matCellDef="let row" [class]="row.deltaClass">{{ row.delta }}</td>
        </ng-container>

        <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
        <tr mat-row *matRowDef="let row; columns: displayedColumns"></tr>
      </table>
    </mat-card-content>
  </mat-card>
</div>
```

```scss
// frontend/trading-ui/src/app/features/backtesting/backtest-compare/backtest-compare.component.scss — new file
.compare {
  // Run labels (per wireframe mockup_backtest_compare.html)
  &__run-labels {
    display: flex;
    gap: 1.5rem;
    margin-bottom: 1.25rem;
  }

  &__run-label {
    display: flex;
    align-items: center;
    gap: 0.625rem;
    padding: 0.625rem 1rem;
    background: var(--colour-surface);
    border: 1px solid var(--colour-border);
    border-radius: 8px;
    flex: 1;
  }

  &__run-label-dot {
    width: 14px;
    height: 14px;
    border-radius: 3px;

    &--a { background: var(--colour-profit); }
    &--b { background: var(--colour-accent-blue, #4fc3f7); }
  }

  &__run-label-name {
    font-weight: 600;
    font-size: 0.875rem;
  }

  &__run-label-detail {
    font-size: 0.75rem;
    color: var(--colour-muted);
  }

  // Config differences (per wireframe)
  &__config-diff {
    margin-bottom: 1.25rem;
  }

  &__config-grid {
    display: grid;
    grid-template-columns: repeat(4, 1fr);
    gap: 0.5rem;
  }

  &__config-item {
    font-size: 0.75rem;
    padding: 0.375rem 0.625rem;
    border-radius: 4px;

    &--changed {
      background: rgba(233, 69, 96, 0.08);
      border: 1px solid rgba(233, 69, 96, 0.2);
    }
  }

  &__config-item-label {
    color: var(--colour-muted);
    font-size: 0.6875rem;
  }

  &__config-item-values {
    display: flex;
    gap: 0.5rem;
    margin-top: 0.125rem;
  }

  &__config-value--a { color: var(--colour-profit); }
  &__config-value--b { color: var(--colour-accent-blue, #4fc3f7); }
  &__config-value--vs { color: var(--colour-muted); }
  &__config-value--same { color: var(--colour-muted); }

  // Chart card
  &__chart-card {
    margin-bottom: 1.25rem;
  }

  // Metrics table
  &__metrics {
    margin-bottom: 1.5rem;
  }

  &__table {
    width: 100%;
  }

  &__metric-name {
    font-weight: 600;
    color: var(--colour-muted);
  }

  &__header-dot {
    &--a { color: var(--colour-profit); }
    &--b { color: var(--colour-accent-blue, #4fc3f7); }
  }

  &__value--a { color: var(--colour-profit); }
  &__value--b { color: var(--colour-accent-blue, #4fc3f7); }

  &__delta--better {
    color: var(--colour-profit);
    font-weight: 500;
  }

  &__delta--worse {
    color: var(--colour-loss);
    font-weight: 500;
  }

  &__delta--neutral {
    color: var(--colour-muted);
  }
}
```

##### Pattern References

- Wireframe: `.agent-context/1-discover/wireframes/mockup_backtest_compare.html` — run labels, config diff grid, section ordering, delta arrows
- `frontend/trading-ui/src/app/features/backtesting/equity-chart/equity-chart.component.ts` — `comparisonData` and `comparisonLabel` inputs for dual-series mode (from Phase 4)
- `frontend/trading-ui/src/app/features/market-data/market-data.component.html` — mat-table data source pattern

---

### Task 5.3: Implement "Re-run with changes" workflow {#task-53-implement-re-run-with-changes-workflow}

Wire the "Re-run with changes" action from BacktestListComponent through BacktestPageComponent to BacktestFormComponent. When triggered, fetch the full result detail and pre-fill the form.

- **Complexity**: Medium
- **Risk Factors**: Must fetch full result (with config) from GET /api/backtests/{id} before pre-filling; must switch to the "Run Backtest" tab after pre-fill
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/backtest-page.component.ts` — modification
  - `frontend/trading-ui/src/app/features/backtesting/backtest-page.component.html` — modification
- **Success**:
  - Clicking "Re-run with changes" fetches the full result
  - Form pre-fills with the result's config and date range
  - Active tab switches to "Run Backtest"
  - Operator can modify parameters and re-run
- **Dependencies**:
  - Task 5.1 (BacktestListComponent)
  - Phase 3 (BacktestFormComponent with prefill support)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/backtesting/backtest-page.component.ts — modification
// Add to class:
import { ViewChild } from "@angular/core";
import { forkJoin } from "rxjs";
import { MatTabGroup } from "@angular/material/tab";
import { BacktestListComponent } from "./backtest-list/backtest-list.component";
import { BacktestCompareComponent } from "./backtest-compare/backtest-compare.component";

// In component imports: add BacktestListComponent, BacktestCompareComponent

// Add to class body:
  @ViewChild(MatTabGroup) public tabGroup!: MatTabGroup;

  public viewedResult: BacktestResult | null = null;
  public compareResultA: BacktestResult | null = null;
  public compareResultB: BacktestResult | null = null;

  public onRerunConfig(id: string): void {
    this._backtestService.getBacktest(id).subscribe({
      next: (result) => {
        this.prefillConfig = result;
        if (this.tabGroup) {
          this.tabGroup.selectedIndex = 0; // Switch to "Run Backtest" tab
        }
      },
      error: () => this._notifications.error("Failed to load backtest config")
    });
  }

  public onViewResult(id: string): void {
    this._backtestService.getBacktest(id).subscribe({
      next: (result) => {
        this.latestResult = result;
        if (this.tabGroup) {
          this.tabGroup.selectedIndex = 0; // Show result in Run tab
        }
      },
      error: () => this._notifications.error("Failed to load backtest result")
    });
  }

  public onCompareSelected(ids: string[]): void {
    if (ids.length !== 2) return;

    forkJoin([
      this._backtestService.getBacktest(ids[0]),
      this._backtestService.getBacktest(ids[1])
    ]).subscribe({
      next: ([a, b]) => {
        this.compareResultA = a;
        this.compareResultB = b;
        if (this.tabGroup) {
          this.tabGroup.selectedIndex = 2; // Switch to Compare tab
        }
      },
      error: () => this._notifications.error("Failed to load comparison data")
    });
  }
```

Note: For the `forkJoin` import, prefer a static import at the top of the file instead of the dynamic import shown. The implementer should add `import { forkJoin } from "rxjs";` at the file top.

```html
<!-- frontend/trading-ui/src/app/features/backtesting/backtest-page.component.html — modification -->
<!-- Replace the "Past Results" tab content: -->
    <mat-tab label="Past Results">
      <div class="backtest-page__tab-content">
        <app-backtest-list
          (viewResult)="onViewResult($event)"
          (rerunConfig)="onRerunConfig($event)"
          (compareSelected)="onCompareSelected($event)">
        </app-backtest-list>
      </div>
    </mat-tab>

<!-- Replace the "Compare" tab content: -->
    <mat-tab label="Compare">
      <div class="backtest-page__tab-content">
        @if (compareResultA && compareResultB) {
          <app-backtest-compare
            [resultA]="compareResultA"
            [resultB]="compareResultB">
          </app-backtest-compare>
        } @else {
          <div class="backtest-page__empty">
            <p>Select two backtest results from the Past Results tab to compare.</p>
          </div>
        }
      </div>
    </mat-tab>
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — tab group management, event handling between child components
- `frontend/trading-ui/src/app/core/services/notification.service.ts` — error notification pattern

---

### Task 5.4: Implement error handling for all API states {#task-54-implement-error-handling-for-all-api-states}

Ensure comprehensive error handling across all API interactions with user-friendly messages as specified in the PBI.

- **Complexity**: Medium
- **Risk Factors**: Must correctly classify errors by status code; must not duplicate global error interceptor notifications
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/backtest-page.component.ts` — modification (ensure _handleApiError covers all cases)
  - `frontend/trading-ui/src/app/features/backtesting/backtest-page.component.html` — modification (add retry button for network errors)
  - `frontend/trading-ui/src/app/features/backtesting/backtest-page.component.scss` — modification (add empty state styles)
- **Success**:
  - 400 errors show inline form error message
  - 404 errors show "No candle data found" message
  - 408 errors show "Backtest timed out" message
  - Network errors show "Unable to reach API" with retry button
  - Zero-trade results show empty state in results area
  - Error banner can be dismissed

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/backtesting/backtest-page.component.ts — modification
// The _handleApiError method already handles 400/404/408/network errors (from Phase 3).
// Ensure the retry mechanism calls the last action:

  public lastRequest: BacktestRequest | null = null;

  public onRunBacktest(request: BacktestRequest): void {
    this.lastRequest = request;
    this.isRunning = true;
    this.apiError = null;
    // ... existing code
  }

  public onRetry(): void {
    if (this.lastRequest) {
      this.onRunBacktest(this.lastRequest);
    }
  }
```

```html
<!-- Update error banner in backtest-page.component.html: -->
        @if (apiError) {
          <div class="backtest-page__error-banner">
            <p>{{ apiError }}</p>
            <div class="backtest-page__error-actions">
              @if (lastRequest) {
                <button mat-button (click)="onRetry()">Retry</button>
              }
              <button mat-button (click)="apiError = null">Dismiss</button>
            </div>
          </div>
        }
```

```scss
// frontend/trading-ui/src/app/features/backtesting/backtest-page.component.scss — modification
// Add:
  &__empty {
    display: flex;
    justify-content: center;
    padding: 3rem;
    color: var(--colour-muted);
  }

  &__error-actions {
    display: flex;
    gap: 0.5rem;
  }
```

##### Pattern References

- `frontend/trading-ui/src/app/core/interceptors/error.interceptor.ts` — global error handling (avoid duplicate notifications for errors already handled by interceptor)
- `frontend/trading-ui/src/app/core/interceptors/http-context-tokens.ts` — `SKIP_ERROR_NOTIFICATION` token (use when handling errors locally in the component)

---

### Task 5.5: Add unit tests for list and comparison components {#task-55-add-unit-tests-for-list-and-comparison-components}

Add tests for BacktestListComponent and BacktestCompareComponent.

- **Complexity**: Medium
- **Risk Factors**: BacktestListComponent needs BacktestService mock; BacktestCompareComponent needs two full BacktestResult objects
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/backtest-list/backtest-list.component.spec.ts` — new file
  - `frontend/trading-ui/src/app/features/backtesting/backtest-compare/backtest-compare.component.spec.ts` — new file
- **Success**:
  - BacktestListComponent tests: loads list, renders rows, handles empty state, checkbox selection limits to 2, emits events
  - BacktestCompareComponent tests: renders comparison table, calculates deltas, applies correct classes
  - All tests pass

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/backtesting/backtest-list/backtest-list.component.spec.ts — new file
import { ComponentFixture, TestBed } from "@angular/core/testing";
import { NoopAnimationsModule } from "@angular/platform-browser/animations";
import { of } from "rxjs";
import { BacktestListComponent } from "./backtest-list.component";
import { BacktestService } from "../../../core/services/backtest.service";
import { PagedResult, BacktestSummary } from "../../../core/models/backtest.model";

describe("BacktestListComponent", () => {
  let component: BacktestListComponent;
  let fixture: ComponentFixture<BacktestListComponent>;
  let mockService: jasmine.SpyObj<BacktestService>;

  const mockPage: PagedResult<BacktestSummary> = {
    items: [{
      id: "1",
      symbol: "BTC",
      intervals: ["15m"],
      startDate: "2024-01-01",
      endDate: "2024-12-31",
      totalTrades: 100,
      winRate: 65.0,
      totalPnl: 1500.50,
      maxDrawdown: -500.25,
      createdAt: "2026-03-28T12:00:00Z"
    }],
    page: 1,
    pageSize: 20,
    totalCount: 1,
    totalPages: 1
  };

  beforeEach(async () => {
    mockService = jasmine.createSpyObj("BacktestService", ["getBacktestList"]);
    mockService.getBacktestList.and.returnValue(of(mockPage));

    await TestBed.configureTestingModule({
      imports: [BacktestListComponent, NoopAnimationsModule],
      providers: [{ provide: BacktestService, useValue: mockService }]
    }).compileComponents();

    fixture = TestBed.createComponent(BacktestListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it("should load results on init", () => {
    expect(mockService.getBacktestList).toHaveBeenCalledWith(1, 20);
    expect(component.results.length).toBe(1);
  });

  it("should allow selecting up to 2 items", () => {
    component.toggleSelection("1");
    component.toggleSelection("2");
    component.toggleSelection("3"); // Should be ignored
    expect(component.selectedIds.size).toBe(2);
    expect(component.selectedIds.has("3")).toBeFalse();
  });

  it("should emit compareSelected when compare clicked", () => {
    spyOn(component.compareSelected, "emit");
    component.toggleSelection("1");
    component.toggleSelection("2");
    component.onCompare();
    expect(component.compareSelected.emit).toHaveBeenCalledWith(["1", "2"]);
  });

  it("should show empty state when no results", () => {
    mockService.getBacktestList.and.returnValue(of({
      items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0
    }));
    component.loadPage();
    fixture.detectChanges();
    const empty = fixture.nativeElement.querySelector(".backtest-list__empty");
    expect(empty).toBeTruthy();
  });
});
```

```typescript
// frontend/trading-ui/src/app/features/backtesting/backtest-compare/backtest-compare.component.spec.ts — new file
import { ComponentFixture, TestBed } from "@angular/core/testing";
import { NoopAnimationsModule } from "@angular/platform-browser/animations";
import { BacktestCompareComponent } from "./backtest-compare.component";
import { BacktestResult } from "../../../core/models/backtest.model";

describe("BacktestCompareComponent", () => {
  let component: BacktestCompareComponent;
  let fixture: ComponentFixture<BacktestCompareComponent>;

  const makeResult = (overrides: Partial<BacktestResult>): BacktestResult => ({
    id: "test",
    totalTrades: 100,
    winningTrades: 65,
    losingTrades: 35,
    winRate: 65.0,
    totalPnL: 1500.50,
    maxDrawdownAbsolute: -500.25,
    maxDrawdownPercent: -5.0,
    averageTradePnL: 15.0,
    averageHoldTime: "2h 30m",
    hedgesOpened: 5,
    totalFeesPaid: 120.50,
    gridCycles: 10,
    finalEquity: 11500.50,
    equityTimeSeries: [],
    tradeLog: [],
    config: {} as any,
    ...overrides
  });

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BacktestCompareComponent, NoopAnimationsModule]
    }).compileComponents();

    fixture = TestBed.createComponent(BacktestCompareComponent);
    component = fixture.componentInstance;
    component.resultA = makeResult({ id: "a", totalPnL: 1000 });
    component.resultB = makeResult({ id: "b", totalPnL: 1500 });
    fixture.detectChanges();
  });

  it("should calculate comparison rows", () => {
    const rows = component.comparisonRows;
    expect(rows.length).toBe(10);
  });

  it("should show positive delta for higher PnL in Run B", () => {
    const pnlRow = component.comparisonRows.find(r => r.metric === "Total PnL")!;
    expect(pnlRow.delta).toContain("+");
    expect(pnlRow.deltaClass).toBe("compare__delta--better");
  });

  it("should show neutral delta when values are equal", () => {
    component.resultB = makeResult({ id: "b", totalPnL: 1000 });
    const pnlRow = component.comparisonRows.find(r => r.metric === "Total PnL")!;
    expect(pnlRow.deltaClass).toBe("compare__delta--neutral");
  });
});
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.spec.ts` — component test with service mock, DOM assertions
- `frontend/trading-ui/src/app/core/services/order.service.spec.ts` — service spy pattern

---

### Task 5.6: Frontend build and lint {#task-56-frontend-build-and-lint}

Run Angular build, lint, and all tests to verify the complete feature.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None — verification step
- **Success**:
  - `npx ng build` succeeds
  - `npx ng lint` passes
  - `npx ng test --watch=false` passes all tests
  - Complete feature is functional end-to-end

## Phase Success Criteria

- Past results list loads with pagination from GET /api/backtests
- Clicking a past result shows its full detail (metrics, chart, trade log)
- "Re-run with changes" pre-fills the form and switches to the Run tab
- Comparison view shows side-by-side metrics with coloured deltas
- Overlaid equity curves render in different colours on the same chart
- Checkbox selection limited to 2 results
- All error states (400, 404, 408, network) display appropriate messages
- Empty states shown when no results exist
- Frontend builds, lints, and all tests pass
