<!-- markdownlint-disable-file -->

# Task Details: Backtest UI Dashboard (F5)

## Phase 4: Frontend — Results Dashboard

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — standalone components, `inject()`, `@ViewChild`, lifecycle hooks, BEM SCSS
- `.agent-context/0-knowledge/09-charting-library.md` — lightweight-charts v5: `createChart()`, `LineSeries`, `AreaSeries`, `ResizeObserver`, `ngOnDestroy` cleanup, dark theme colours, `ISeriesApi`, marker API
- `.agent-context/0-knowledge/18-backtesting-architecture.md` — BacktestResult fields, EquitySnapshot shape, BacktestTrade fields

## Design References

- Metric cards: Total PnL, Win Rate, Max Drawdown, Total Trades, Winning Trades, Losing Trades, Average Trade PnL, Average Hold Time, Hedges Opened, Total Fees Paid
- Equity chart: line/area chart from `EquitySnapshot[]` with trade entry/exit markers
- Trade log table: sortable mat-table with columns: Entry Time, Exit Time, Entry Price, Exit Price, Side, Size, PnL, Fees
- PnL colour-coding: `--colour-profit` (green), `--colour-loss` (red) from CSS custom properties
- Empty state when zero trades returned
- Chart dark theme matching existing price-chart: background `#1a1a2e`, grid `#2a2a3e`, text `#a0a0b0`

### Task 4.1: Create BacktestResultComponent with metric cards {#task-41-create-backtestresultcomponent-with-metric-cards}

Create a component that displays summary metric cards for a backtest result in a 5-column, 2-row layout (primary row: Total PnL, Win Rate, Max Drawdown, Total Trades, Avg Trade PnL; secondary row: Winning Trades, Losing Trades, Avg Hold Time, Hedges Opened, Total Fees). Also includes a "Configuration Used" echo section showing the backtest config params, candles replayed, and elapsed time. Layout per wireframe `mockup_backtest_run_form.html`.

- **Complexity**: Medium
- **Risk Factors**: Must render varied metric types (percentage, currency, count, duration) with correct formatting; must parse `strategyConfigJson` for the config echo section
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.ts` — new file
  - `frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.html` — new file
  - `frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.scss` — new file
- **Success**:
  - Displays 10 metric cards in two explicit rows of 5 columns (not auto-fill)
  - Primary row: Total PnL, Win Rate, Max Drawdown, Total Trades, Avg Trade PnL
  - Secondary row: Winning Trades, Losing Trades, Avg Hold Time, Hedges Opened, Total Fees
  - PnL and drawdown values are colour-coded (green/red)
  - "Configuration Used" echo section shows symbol, intervals, grid levels, grid spacing, take profit, leverage, position size, stop loss, candles replayed, elapsed time
  - Accepts `BacktestResult` as `@Input()`
  - Shows empty state when result has zero trades
- **Dependencies**:
  - Phase 2 (models)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.ts — new file
import { Component, Input } from "@angular/core";
import { DecimalPipe, PercentPipe } from "@angular/common";
import { MatCardModule } from "@angular/material/card";
import { MatIconModule } from "@angular/material/icon";
import { BacktestResult } from "../../../core/models/backtest.model";

@Component({
  selector: "app-backtest-result",
  standalone: true,
  imports: [DecimalPipe, PercentPipe, MatCardModule, MatIconModule],
  templateUrl: "./backtest-result.component.html",
  styleUrl: "./backtest-result.component.scss"
})
export class BacktestResultComponent {
  @Input({ required: true }) public result!: BacktestResult;

  public get pnlClass(): string {
    return this.result.totalPnL >= 0 ? "backtest-result__value--profit" : "backtest-result__value--loss";
  }

  public get drawdownClass(): string {
    return "backtest-result__value--loss";
  }

  /** Parsed strategy config for the "Configuration Used" echo section (per wireframe) */
  public get configValues(): { gridLevels: number; gridSpacing: number; takeProfitPercent: number; stopLossPercent: number; leverage: number; positionSize: number } {
    try {
      const c = JSON.parse(this.result.config?.strategyConfigJson ?? "{}");
      return {
        gridLevels: c.grid?.levels ?? 0,
        gridSpacing: c.grid?.spacing ?? 0,
        takeProfitPercent: c.exit?.takeProfitPercent ?? 0,
        stopLossPercent: c.exit?.stopLossPercent ?? 0,
        leverage: c.risk?.leverage ?? 0,
        positionSize: c.risk?.positionSize ?? 0
      };
    } catch {
      return { gridLevels: 0, gridSpacing: 0, takeProfitPercent: 0, stopLossPercent: 0, leverage: 0, positionSize: 0 };
    }
  }
}
```

```html
<!-- frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.html — new file -->
@if (result.totalTrades === 0) {
  <div class="backtest-result__empty">
    <mat-icon>info</mat-icon>
    <p>Strategy did not generate any trades in this date range.</p>
  </div>
} @else {
  <!-- Primary metrics row (5 columns) — per wireframe mockup_backtest_run_form.html -->
  <div class="backtest-result__cards backtest-result__cards--primary">
    <mat-card class="backtest-result__card">
      <mat-card-header><mat-card-title>Total PnL</mat-card-title></mat-card-header>
      <mat-card-content>
        <span [class]="pnlClass">${{ result.totalPnL | number:'1.2-2' }}</span>
      </mat-card-content>
    </mat-card>

    <mat-card class="backtest-result__card">
      <mat-card-header><mat-card-title>Win Rate</mat-card-title></mat-card-header>
      <mat-card-content>
        <span>{{ result.winRate | number:'1.1-1' }}%</span>
      </mat-card-content>
    </mat-card>

    <mat-card class="backtest-result__card">
      <mat-card-header><mat-card-title>Max Drawdown</mat-card-title></mat-card-header>
      <mat-card-content>
        <span [class]="drawdownClass">${{ result.maxDrawdownAbsolute | number:'1.2-2' }}</span>
        <span class="backtest-result__subtitle">({{ result.maxDrawdownPercent | number:'1.1-1' }}%)</span>
      </mat-card-content>
    </mat-card>

    <mat-card class="backtest-result__card">
      <mat-card-header><mat-card-title>Total Trades</mat-card-title></mat-card-header>
      <mat-card-content>
        <span>{{ result.totalTrades }}</span>
      </mat-card-content>
    </mat-card>

    <mat-card class="backtest-result__card">
      <mat-card-header><mat-card-title>Avg Trade PnL</mat-card-title></mat-card-header>
      <mat-card-content>
        <span [class]="result.averageTradePnL >= 0 ? 'backtest-result__value--profit' : 'backtest-result__value--loss'">
          ${{ result.averageTradePnL | number:'1.2-2' }}
        </span>
      </mat-card-content>
    </mat-card>
  </div>

  <!-- Secondary metrics row (5 columns) — per wireframe mockup_backtest_run_form.html -->
  <div class="backtest-result__cards backtest-result__cards--secondary">
    <mat-card class="backtest-result__card">
      <mat-card-header><mat-card-title>Winning Trades</mat-card-title></mat-card-header>
      <mat-card-content>
        <span class="backtest-result__value--profit">{{ result.winningTrades }}</span>
      </mat-card-content>
    </mat-card>

    <mat-card class="backtest-result__card">
      <mat-card-header><mat-card-title>Losing Trades</mat-card-title></mat-card-header>
      <mat-card-content>
        <span class="backtest-result__value--loss">{{ result.losingTrades }}</span>
      </mat-card-content>
    </mat-card>

    <mat-card class="backtest-result__card">
      <mat-card-header><mat-card-title>Avg Hold Time</mat-card-title></mat-card-header>
      <mat-card-content>
        <span>{{ result.averageHoldTime }}</span>
      </mat-card-content>
    </mat-card>

    <mat-card class="backtest-result__card">
      <mat-card-header><mat-card-title>Hedges Opened</mat-card-title></mat-card-header>
      <mat-card-content>
        <span>{{ result.hedgesOpened }}</span>
      </mat-card-content>
    </mat-card>

    <mat-card class="backtest-result__card">
      <mat-card-header><mat-card-title>Total Fees</mat-card-title></mat-card-header>
      <mat-card-content>
        <span class="backtest-result__value--loss">${{ result.totalFeesPaid | number:'1.2-2' }}</span>
      </mat-card-content>
    </mat-card>
  </div>

  <!-- Configuration Used echo — per wireframe mockup_backtest_run_form.html -->
  @if (result.config) {
    <mat-card class="backtest-result__config">
      <mat-card-header><mat-card-title>Configuration Used</mat-card-title></mat-card-header>
      <mat-card-content>
        <div class="backtest-result__config-grid">
          <div class="backtest-result__config-item">
            <span class="backtest-result__config-label">Symbol</span>
            <span class="backtest-result__config-value">{{ result.config.symbol }}</span>
          </div>
          <div class="backtest-result__config-item">
            <span class="backtest-result__config-label">Intervals</span>
            <span class="backtest-result__config-value">{{ result.config.intervals.join(', ') }}</span>
          </div>
          <div class="backtest-result__config-item">
            <span class="backtest-result__config-label">Grid Levels</span>
            <span class="backtest-result__config-value">{{ configValues.gridLevels }}</span>
          </div>
          <div class="backtest-result__config-item">
            <span class="backtest-result__config-label">Grid Spacing</span>
            <span class="backtest-result__config-value">{{ configValues.gridSpacing }}%</span>
          </div>
          <div class="backtest-result__config-item">
            <span class="backtest-result__config-label">Take Profit</span>
            <span class="backtest-result__config-value">{{ configValues.takeProfitPercent }}%</span>
          </div>
          <div class="backtest-result__config-item">
            <span class="backtest-result__config-label">Leverage</span>
            <span class="backtest-result__config-value">{{ configValues.leverage }}x</span>
          </div>
          <div class="backtest-result__config-item">
            <span class="backtest-result__config-label">Position Size</span>
            <span class="backtest-result__config-value">${{ configValues.positionSize }}</span>
          </div>
          <div class="backtest-result__config-item">
            <span class="backtest-result__config-label">Stop Loss</span>
            <span class="backtest-result__config-value">{{ configValues.stopLossPercent }}%</span>
          </div>
          <div class="backtest-result__config-item">
            <span class="backtest-result__config-label">Candles Replayed</span>
            <span class="backtest-result__config-value">{{ result.equityTimeSeries.length | number }}</span>
          </div>
          @if (result.elapsedSeconds) {
            <div class="backtest-result__config-item">
              <span class="backtest-result__config-label">Elapsed</span>
              <span class="backtest-result__config-value">{{ result.elapsedSeconds }}s</span>
            </div>
          }
        </div>
      </mat-card-content>
    </mat-card>
  }
}
```

```scss
// frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.scss — new file
.backtest-result {
  &__cards {
    display: grid;
    grid-template-columns: repeat(5, 1fr);
    gap: 1rem;
    margin-bottom: 1.5rem;
  }

  &__card {
    text-align: center;

    mat-card-content {
      font-size: 1.5rem;
      font-weight: 600;
    }
  }

  &__value--profit {
    color: var(--colour-profit);
  }

  &__value--loss {
    color: var(--colour-loss);
  }

  &__subtitle {
    display: block;
    font-size: 0.75rem;
    color: var(--colour-muted);
  }

  &__config {
    margin-bottom: 1.5rem;
  }

  &__config-grid {
    display: grid;
    grid-template-columns: repeat(5, 1fr);
    gap: 0.5rem;
  }

  &__config-item {
    font-size: 0.75rem;
  }

  &__config-label {
    color: var(--colour-muted);
  }

  &__config-value {
    color: var(--colour-text-primary);
    font-weight: 600;
    margin-left: 0.25rem;
  }

  &__empty {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    padding: 2rem;
    text-align: center;
    color: var(--colour-muted);
    justify-content: center;
  }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/account-summary/` — card-based metric display with colour-coding
- `frontend/trading-ui/src/styles.scss` — `--colour-profit`, `--colour-loss`, `--colour-muted` custom properties

---

### Task 4.2: Create EquityChartComponent {#task-42-create-equitychartcomponent}

Create a reusable equity curve chart component using `lightweight-charts` v5. Supports single series and optionally two overlaid series (for comparison mode).

- **Complexity**: High
- **Risk Factors**: lightweight-charts v5 API differences from v4; marker API for trade entry/exit; ResizeObserver cleanup; comparison mode dual-series handling
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/equity-chart/equity-chart.component.ts` — new file
  - `frontend/trading-ui/src/app/features/backtesting/equity-chart/equity-chart.component.html` — new file
  - `frontend/trading-ui/src/app/features/backtesting/equity-chart/equity-chart.component.scss` — new file
- **Success**:
  - Renders an area chart from `EquitySnapshot[]` data
  - Trade markers plotted on the chart (up arrow for entry, down arrow for exit, coloured by PnL)
  - Supports optional second series with different colour (for comparison mode in Phase 5)
  - Chart resizes responsively (ResizeObserver)
  - Proper cleanup in ngOnDestroy
  - Renders within 1 second for up to 35K data points
- **Dependencies**:
  - Phase 2 (models)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/backtesting/equity-chart/equity-chart.component.ts — new file
import {
  AfterViewInit,
  Component,
  ElementRef,
  Input,
  OnChanges,
  OnDestroy,
  SimpleChanges,
  ViewChild
} from "@angular/core";
import {
  AreaSeries,
  createChart,
  IChartApi,
  ISeriesApi,
  SeriesMarker,
  UTCTimestamp
} from "lightweight-charts";
import { BacktestTrade, EquitySnapshot } from "../../../core/models/backtest.model";

interface EquityDataPoint {
  time: UTCTimestamp;
  value: number;
}

@Component({
  selector: "app-equity-chart",
  standalone: true,
  imports: [],
  templateUrl: "./equity-chart.component.html",
  styleUrl: "./equity-chart.component.scss"
})
export class EquityChartComponent implements AfterViewInit, OnChanges, OnDestroy {
  @Input() public equityData: EquitySnapshot[] = [];
  @Input() public trades: BacktestTrade[] = [];
  @Input() public comparisonData: EquitySnapshot[] | null = null;
  @Input() public comparisonLabel = "Run B";
  @Input() public primaryLabel = "Equity";

  @ViewChild("chartContainer", { static: true })
  private readonly _chartContainer!: ElementRef<HTMLDivElement>;

  private _chart: IChartApi | null = null;
  private _primarySeries: ISeriesApi<"Area"> | null = null;
  private _comparisonSeries: ISeriesApi<"Area"> | null = null;
  private _resizeObserver: ResizeObserver | null = null;

  public ngAfterViewInit(): void {
    this._initChart();
    this._updateData();
  }

  public ngOnChanges(changes: SimpleChanges): void {
    if (this._chart && (changes['equityData'] || changes['trades'] || changes['comparisonData'])) {
      this._updateData();
    }
  }

  public ngOnDestroy(): void {
    this._resizeObserver?.disconnect();
    this._chart?.remove();
  }

  private _initChart(): void {
    const container = this._chartContainer.nativeElement;

    this._chart = createChart(container, {
      width: container.clientWidth,
      height: 400,
      layout: {
        background: { color: "#1a1a2e" },
        textColor: "#a0a0b0"
      },
      grid: {
        vertLines: { color: "#2a2a3e" },
        horzLines: { color: "#2a2a3e" }
      },
      crosshair: { mode: 0 },
      timeScale: {
        borderColor: "#2a2a3e",
        timeVisible: true,
        secondsVisible: false
      },
      rightPriceScale: { borderColor: "#2a2a3e" }
    });

    this._primarySeries = this._chart.addSeries(AreaSeries, {
      lineColor: "#26a69a",
      topColor: "rgba(38, 166, 154, 0.4)",
      bottomColor: "rgba(38, 166, 154, 0.0)",
      lineWidth: 2,
      title: this.primaryLabel
    });

    this._resizeObserver = new ResizeObserver((entries) => {
      if (entries.length > 0) {
        const { width } = entries[0].contentRect;
        this._chart?.applyOptions({ width });
      }
    });
    this._resizeObserver.observe(container);
  }

  private _updateData(): void {
    if (!this._chart || !this._primarySeries) return;

    // Set primary series data
    const primaryData: EquityDataPoint[] = this.equityData.map(s => ({
      time: Math.floor(s.timestampUtc / 1000) as UTCTimestamp,
      value: s.equity
    }));
    this._primarySeries.setData(primaryData);

    // Add trade markers
    if (this.trades.length > 0) {
      const markers: SeriesMarker<UTCTimestamp>[] = this.trades
        .filter(t => t.entryTimeUtc)
        .flatMap(t => {
          const result: SeriesMarker<UTCTimestamp>[] = [];
          result.push({
            time: Math.floor(t.entryTimeUtc / 1000) as UTCTimestamp,
            position: "belowBar",
            color: "#26a69a",
            shape: "arrowUp",
            text: `${t.side} Entry`
          });
          if (t.exitTimeUtc) {
            result.push({
              time: Math.floor(t.exitTimeUtc / 1000) as UTCTimestamp,
              position: "aboveBar",
              color: t.pnL != null && t.pnL >= 0 ? "#26a69a" : "#ef5350",
              shape: "arrowDown",
              text: `Exit ${t.pnL != null ? (t.pnL >= 0 ? "+" : "") + t.pnL.toFixed(2) : ""}`
            });
          }
          return result;
        })
        .sort((a, b) => (a.time as number) - (b.time as number));

      this._primarySeries.setMarkers(markers);
    }

    // Comparison series
    if (this.comparisonData && this.comparisonData.length > 0) {
      if (!this._comparisonSeries) {
        this._comparisonSeries = this._chart.addSeries(AreaSeries, {
          lineColor: "#ff9800",
          topColor: "rgba(255, 152, 0, 0.2)",
          bottomColor: "rgba(255, 152, 0, 0.0)",
          lineWidth: 2,
          title: this.comparisonLabel
        });
      }
      const compData: EquityDataPoint[] = this.comparisonData.map(s => ({
        time: Math.floor(s.timestampUtc / 1000) as UTCTimestamp,
        value: s.equity
      }));
      this._comparisonSeries.setData(compData);
    } else if (this._comparisonSeries) {
      this._chart.removeSeries(this._comparisonSeries);
      this._comparisonSeries = null;
    }

    this._chart.timeScale().fitContent();
  }
}
```

```html
<!-- frontend/trading-ui/src/app/features/backtesting/equity-chart/equity-chart.component.html — new file -->
<div class="equity-chart">
  <div #chartContainer class="equity-chart__container"></div>
</div>
```

```scss
// frontend/trading-ui/src/app/features/backtesting/equity-chart/equity-chart.component.scss — new file
.equity-chart {
  width: 100%;
  margin: 1rem 0;

  &__container {
    width: 100%;
    height: 400px;
    border-radius: 4px;
    overflow: hidden;
  }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/market-data/price-chart/price-chart.component.ts` — `createChart()`, `@ViewChild("chartContainer")`, `ResizeObserver`, `ngOnDestroy` cleanup, dark theme colours, `ISeriesApi`, `UTCTimestamp`
- `.agent-context/0-knowledge/09-charting-library.md` — `LineSeries`/`AreaSeries`, multi-series pattern, marker API

---

### Task 4.3: Create TradeLogTableComponent {#task-43-create-tradelogtablecomponent}

Create a sortable table component for the trade log using Angular Material's mat-table with mat-sort.

- **Complexity**: Medium
- **Risk Factors**: Must configure MatSort correctly; PnL colour-coding; timestamp formatting from epoch milliseconds
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.ts` — new file
  - `frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.html` — new file
  - `frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.scss` — new file
- **Success**:
  - Table displays all trade columns: Entry Time, Exit Time, Entry Price, Exit Price, Side, Size, PnL, Fees
  - All columns are sortable via mat-sort
  - PnL values are colour-coded (green positive, red negative)
  - Timestamps formatted from epoch milliseconds to readable dates
  - Accepts `BacktestTrade[]` as `@Input()`
- **Dependencies**:
  - Phase 2 (models)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.ts — new file
import { Component, Input, OnChanges, SimpleChanges, ViewChild, AfterViewInit } from "@angular/core";
import { DatePipe, DecimalPipe } from "@angular/common";
import { MatSort, MatSortModule } from "@angular/material/sort";
import { MatTableDataSource, MatTableModule } from "@angular/material/table";
import { BacktestTrade } from "../../../core/models/backtest.model";

@Component({
  selector: "app-trade-log-table",
  standalone: true,
  imports: [DatePipe, DecimalPipe, MatTableModule, MatSortModule],
  templateUrl: "./trade-log-table.component.html",
  styleUrl: "./trade-log-table.component.scss"
})
export class TradeLogTableComponent implements OnChanges, AfterViewInit {
  @Input() public trades: BacktestTrade[] = [];

  @ViewChild(MatSort) public sort!: MatSort;

  public dataSource = new MatTableDataSource<BacktestTrade>();
  public readonly displayedColumns = [
    "entryTime", "exitTime", "entryPrice", "exitPrice", "side", "size", "pnL", "fees"
  ];

  public ngOnChanges(changes: SimpleChanges): void {
    if (changes['trades']) {
      this.dataSource.data = this.trades;
    }
  }

  public ngAfterViewInit(): void {
    this.dataSource.sort = this.sort;
    this.dataSource.sortingDataAccessor = (item: BacktestTrade, property: string): string | number => {
      switch (property) {
        case "entryTime": return item.entryTimeUtc;
        case "exitTime": return item.exitTimeUtc ?? 0;
        case "entryPrice": return item.entryPrice;
        case "exitPrice": return item.exitPrice ?? 0;
        case "pnL": return item.pnL ?? 0;
        case "fees": return item.fees;
        case "size": return item.size;
        default: return "";
      }
    };
  }

  public getPnlClass(pnl: number | null): string {
    if (pnl == null) return "";
    return pnl >= 0 ? "trade-log__pnl--profit" : "trade-log__pnl--loss";
  }

  public formatTimestamp(epochMs: number | null): Date | null {
    return epochMs != null ? new Date(epochMs) : null;
  }
}
```

```html
<!-- frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.html — new file -->
<div class="trade-log">
  <table mat-table [dataSource]="dataSource" matSort class="trade-log__table">
    <ng-container matColumnDef="entryTime">
      <th mat-header-cell *matHeaderCellDef mat-sort-header>Entry Time</th>
      <td mat-cell *matCellDef="let trade">{{ formatTimestamp(trade.entryTimeUtc) | date:'short' }}</td>
    </ng-container>

    <ng-container matColumnDef="exitTime">
      <th mat-header-cell *matHeaderCellDef mat-sort-header>Exit Time</th>
      <td mat-cell *matCellDef="let trade">{{ formatTimestamp(trade.exitTimeUtc) | date:'short' }}</td>
    </ng-container>

    <ng-container matColumnDef="entryPrice">
      <th mat-header-cell *matHeaderCellDef mat-sort-header>Entry Price</th>
      <td mat-cell *matCellDef="let trade">{{ trade.entryPrice | number:'1.2-2' }}</td>
    </ng-container>

    <ng-container matColumnDef="exitPrice">
      <th mat-header-cell *matHeaderCellDef mat-sort-header>Exit Price</th>
      <td mat-cell *matCellDef="let trade">{{ trade.exitPrice != null ? (trade.exitPrice | number:'1.2-2') : '—' }}</td>
    </ng-container>

    <ng-container matColumnDef="side">
      <th mat-header-cell *matHeaderCellDef mat-sort-header>Side</th>
      <td mat-cell *matCellDef="let trade">{{ trade.side }}</td>
    </ng-container>

    <ng-container matColumnDef="size">
      <th mat-header-cell *matHeaderCellDef mat-sort-header>Size</th>
      <td mat-cell *matCellDef="let trade">{{ trade.size | number:'1.4-4' }}</td>
    </ng-container>

    <ng-container matColumnDef="pnL">
      <th mat-header-cell *matHeaderCellDef mat-sort-header>PnL</th>
      <td mat-cell *matCellDef="let trade" [class]="getPnlClass(trade.pnL)">
        {{ trade.pnL != null ? '$' + (trade.pnL | number:'1.2-2') : '—' }}
      </td>
    </ng-container>

    <ng-container matColumnDef="fees">
      <th mat-header-cell *matHeaderCellDef mat-sort-header>Fees</th>
      <td mat-cell *matCellDef="let trade">${{ trade.fees | number:'1.4-4' }}</td>
    </ng-container>

    <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
    <tr mat-row *matRowDef="let row; columns: displayedColumns"></tr>
  </table>
</div>
```

```scss
// frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.scss — new file
.trade-log {
  overflow-x: auto;
  margin: 1rem 0;

  &__table {
    width: 100%;
  }

  &__pnl--profit {
    color: var(--colour-profit);
    font-weight: 500;
  }

  &__pnl--loss {
    color: var(--colour-loss);
    font-weight: 500;
  }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/market-data/market-data.component.html` — `mat-table [dataSource]`, `matColumnDef`, `*matCellDef`, `*matHeaderRowDef`, `*matRowDef` directives
- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts` — sort logic, colour-coding pattern

---

### Task 4.4: Integrate results into BacktestPageComponent {#task-44-integrate-results-into-backtestpagecomponent}

Wire `BacktestResultComponent`, `EquityChartComponent`, and `TradeLogTableComponent` into the "Run Backtest" tab so they display after a backtest completes.

- **Complexity**: Low
- **Risk Factors**: None — wiring existing components together
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/backtest-page.component.ts` — modification (add imports)
  - `frontend/trading-ui/src/app/features/backtesting/backtest-page.component.html` — modification
- **Success**:
  - After backtest completes, metrics cards + equity chart + trade log display below the form
  - Results section hidden when no result
- **Dependencies**:
  - Task 4.1, 4.2, 4.3

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/backtesting/backtest-page.component.ts — modification
// Add to component imports array:
import { BacktestResultComponent } from "./backtest-result/backtest-result.component";
import { EquityChartComponent } from "./equity-chart/equity-chart.component";
import { TradeLogTableComponent } from "./trade-log-table/trade-log-table.component";
// Add these to the @Component imports: BacktestResultComponent, EquityChartComponent, TradeLogTableComponent
```

```html
<!-- frontend/trading-ui/src/app/features/backtesting/backtest-page.component.html — modification -->
<!-- Add after the coverage report in the "Run Backtest" tab: -->

        @if (latestResult) {
          <h3 class="backtest-page__results-title">Results</h3>
          <app-backtest-result [result]="latestResult"></app-backtest-result>

          @if (latestResult.totalTrades > 0) {
            <app-equity-chart
              [equityData]="latestResult.equityTimeSeries"
              [trades]="latestResult.tradeLog">
            </app-equity-chart>

            <h3 class="backtest-page__results-title">Trade Log</h3>
            <app-trade-log-table [trades]="latestResult.tradeLog"></app-trade-log-table>
          }
        }
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.html` — conditional rendering of sub-components with @if

---

### Task 4.5: Add unit tests for result components {#task-45-add-unit-tests-for-result-components}

Add unit tests for BacktestResultComponent, and TradeLogTableComponent. EquityChartComponent tests are limited to lifecycle (chart creation requires a DOM container which may have limitations in headless test environments).

- **Complexity**: Medium
- **Risk Factors**: lightweight-charts may not render in headless Karma; test should verify component creation and data binding, not pixel-level chart output
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.spec.ts` — new file
  - `frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.spec.ts` — new file
  - `frontend/trading-ui/src/app/features/backtesting/equity-chart/equity-chart.component.spec.ts` — new file
- **Success**:
  - BacktestResultComponent tests: renders metric cards, colour-codes PnL, shows empty state
  - TradeLogTableComponent tests: renders rows, sorts by column, colour-codes PnL
  - EquityChartComponent tests: creates without error, cleans up on destroy
  - All tests pass

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.spec.ts — new file
import { ComponentFixture, TestBed } from "@angular/core/testing";
import { BacktestResultComponent } from "./backtest-result.component";
import { BacktestResult } from "../../../core/models/backtest.model";

describe("BacktestResultComponent", () => {
  let component: BacktestResultComponent;
  let fixture: ComponentFixture<BacktestResultComponent>;

  const mockResult: BacktestResult = {
    id: "test-1",
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
    config: {} as any
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BacktestResultComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(BacktestResultComponent);
    component = fixture.componentInstance;
    component.result = mockResult;
    fixture.detectChanges();
  });

  it("should display metric cards", () => {
    const cards = fixture.nativeElement.querySelectorAll(".backtest-result__card");
    expect(cards.length).toBe(10);
  });

  it("should apply profit class for positive PnL", () => {
    expect(component.pnlClass).toBe("backtest-result__value--profit");
  });

  it("should show empty state for zero trades", () => {
    component.result = { ...mockResult, totalTrades: 0 };
    fixture.detectChanges();
    const empty = fixture.nativeElement.querySelector(".backtest-result__empty");
    expect(empty).toBeTruthy();
  });
});
```

```typescript
// frontend/trading-ui/src/app/features/backtesting/equity-chart/equity-chart.component.spec.ts — new file
import { ComponentFixture, TestBed } from "@angular/core/testing";
import { EquityChartComponent } from "./equity-chart.component";

describe("EquityChartComponent", () => {
  let component: EquityChartComponent;
  let fixture: ComponentFixture<EquityChartComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EquityChartComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(EquityChartComponent);
    component = fixture.componentInstance;
    component.equityData = [
      { timestampUtc: 1704067200000, equity: 10000 },
      { timestampUtc: 1704153600000, equity: 10100 }
    ];
    component.trades = [];
    fixture.detectChanges();
  });

  it("should create", () => {
    expect(component).toBeTruthy();
  });

  it("should clean up on destroy", () => {
    component.ngOnDestroy();
    // No error thrown indicates cleanup succeeded
    expect(component).toBeTruthy();
  });
});
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.spec.ts` — component test with @Input, fixture.detectChanges, DOM assertions

---

### Task 4.6: Frontend build and lint {#task-46-frontend-build-and-lint}

Run Angular build, lint, and test to verify results integration.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None — verification step
- **Success**:
  - `npx ng build` succeeds
  - `npx ng lint` passes
  - `npx ng test --watch=false` passes all tests

## Phase Success Criteria

- After a backtest completes, 10 metric cards display with correct formatting and colour-coding
- Equity curve chart renders from EquitySnapshot data with trade entry/exit markers
- Trade log table displays all trades with sortable columns and PnL colour-coding
- Zero-trade results show empty state message
- Chart supports optional comparison overlay (for Phase 5)
- Frontend builds, lints, and all tests pass
