<!-- markdownlint-disable-file -->

# Task Details: Strategy Optimizer — Phase 4: Frontend — Results Display & Promote to Strategy

## Phase 4: Frontend — Results Display & Promote to Strategy

## Standards and Knowledge References

- **angular.instructions.md**: Standalone components, `inject()` DI, double quotes, SCSS
- **strategy-builder**: `StrategyConfig` JSON structure used in strategy builder for pre-fill
- **backtest-list.component.ts**: Paginated table pattern reference
- **backtest-result.component.ts**: Results card display pattern

---

### Task 4.1: Create `optimizer-results-table.component` — ranked results display {#task-41-create-results-table}

Create the component that displays the top 10 ranked optimization results in a table.

- **Complexity**: Medium
- **Risk Factors**: Table must be visually clear with many metric columns; expandable rows for detail
- **Files**:
  - `frontend/trading-ui/src/app/features/optimizer/optimizer-results-table/optimizer-results-table.component.ts` — new file
  - `frontend/trading-ui/src/app/features/optimizer/optimizer-results-table/optimizer-results-table.component.html` — new file
  - `frontend/trading-ui/src/app/features/optimizer/optimizer-results-table/optimizer-results-table.component.scss` — new file
- **Success**:
  - Renders a `mat-table` with columns: Rank, Fitness Score, Signal Description, PnL, Win Rate, Max Drawdown, Trades, Actions
  - Supports row expansion for detail view
  - "Create Strategy" button on each row emits event
  - Conditional coloring: positive PnL green, negative red; high fitness highlighted
- **Dependencies**: Angular Material Table, `OptimizationResultResponse` model

#### Implementation Details

```typescript
@Component({
    selector: "app-optimizer-results-table",
    standalone: true,
    imports: [MatTable, MatHeaderRow, MatRow, MatCell, MatButton, MatIcon, DecimalPipe, PercentPipe, ...],
    templateUrl: "./optimizer-results-table.component.html",
    styleUrl: "./optimizer-results-table.component.scss"
})
export class OptimizerResultsTableComponent {

    @Input() public results: OptimizationResultResponse[] = [];
    @Input() public symbol = "";
    @Input() public elapsedMs = 0;
    @Input() public qualifiedCount = 0;
    @Input() public totalCombinations = 0;

    @Output() public createStrategy = new EventEmitter<OptimizationResultResponse>();
    @Output() public viewDetail = new EventEmitter<OptimizationResultResponse>();

    public displayedColumns = ["rank", "fitnessScore", "signalDescription", "totalPnl", "winRate", "maxDrawdown", "totalTrades", "actions"];
    public expandedRow: OptimizationResultResponse | null = null;

    public onToggleExpand(row: OptimizationResultResponse): void {
        this.expandedRow = this.expandedRow === row ? null : row;
    }

    public onCreateStrategy(result: OptimizationResultResponse): void {
        this.createStrategy.emit(result);
    }
}
```

Table template includes a summary header above the table:
```html
<div class="optimizer-results__summary">
  <span>{{ qualifiedCount }} qualified out of {{ totalCombinations }} combinations</span>
  <span>Completed in {{ elapsedMs / 1000 | number:'1.1-1' }}s</span>
</div>

<table mat-table [dataSource]="results">
  <ng-container matColumnDef="rank">
    <th mat-header-cell *matHeaderCellDef>#</th>
    <td mat-cell *matCellDef="let row">{{ row.rank }}</td>
  </ng-container>
  <!-- ... other columns ... -->
  <ng-container matColumnDef="actions">
    <th mat-header-cell *matHeaderCellDef></th>
    <td mat-cell *matCellDef="let row">
      <button mat-stroked-button (click)="onCreateStrategy(row)">Create Strategy</button>
    </td>
  </ng-container>
</table>
```

---

### Task 4.2: Create `optimizer-result-detail.component` — expandable row detail {#task-42-create-result-detail}

Create the detail view shown when a result row is expanded, showing full strategy configuration in human-readable form.

- **Complexity**: Low
- **Risk Factors**: Must parse `strategyConfigJson` and display it readably
- **Files**:
  - `frontend/trading-ui/src/app/features/optimizer/optimizer-result-detail/optimizer-result-detail.component.ts` — new file
  - `frontend/trading-ui/src/app/features/optimizer/optimizer-result-detail/optimizer-result-detail.component.html` — new file
  - `frontend/trading-ui/src/app/features/optimizer/optimizer-result-detail/optimizer-result-detail.component.scss` — new file
- **Success**:
  - Displays parsed strategy config in sections: Entry Conditions, Trend Filter, Exit Rules, Risk Settings
  - Shows all metric values in a compact card layout
  - Raw JSON toggleable for debugging

#### Implementation Details

```typescript
@Component({
    selector: "app-optimizer-result-detail",
    standalone: true,
    imports: [MatCard, MatChipSet, MatChip, KeyValuePipe, JsonPipe, ...],
    templateUrl: "./optimizer-result-detail.component.html",
    styleUrl: "./optimizer-result-detail.component.scss"
})
export class OptimizerResultDetailComponent {

    @Input() public result!: OptimizationResultResponse;

    public showRawJson = false;

    public get parsedConfig(): any {
        try {
            return JSON.parse(this.result.strategyConfigJson);
        } catch {
            return null;
        }
    }
}
```

Template shows:
- **Entry Conditions** as chips (e.g., "RSI(14) < 40", "MACD(12,26,9) cross_above_signal")
- **Entry Logic**: "All conditions must pass" or "Any condition passes"
- **Trend Filter**: Enabled/disabled, type, periods
- **Exit Rules**: SL % / TP % with enabled states
- **Risk**: Leverage, Position Size, Max Open Trades
- **Metrics grid**: 2-column layout with all numeric metrics
- **Toggle**: "Show Raw JSON" collapsible section

---

### Task 4.3: Create `optimizer-history-list.component` — previous runs {#task-43-create-history-list}

Create the component showing a paginated list of previous optimization runs.

- **Complexity**: Low
- **Risk Factors**: None — follows `backtest-list.component.ts` pattern
- **Files**:
  - `frontend/trading-ui/src/app/features/optimizer/optimizer-history-list/optimizer-history-list.component.ts` — new file
  - `frontend/trading-ui/src/app/features/optimizer/optimizer-history-list/optimizer-history-list.component.html` — new file
  - `frontend/trading-ui/src/app/features/optimizer/optimizer-history-list/optimizer-history-list.component.scss` — new file
- **Success**:
  - Renders a `mat-table` with columns: Date, Symbol, Status, Combinations, Qualified, Duration
  - Clickable rows emit `viewRun` event
  - Paginator for navigation
- **Dependencies**: `OptimizerService.getOptimizationList()`

#### Implementation Details

```typescript
@Component({
    selector: "app-optimizer-history-list",
    standalone: true,
    imports: [MatTable, MatPaginator, MatButton, DatePipe, ...],
    templateUrl: "./optimizer-history-list.component.html",
    styleUrl: "./optimizer-history-list.component.scss"
})
export class OptimizerHistoryListComponent implements OnInit {

    private readonly _optimizerService = inject(OptimizerService);
    private readonly _destroyRef = inject(DestroyRef);

    @Output() public viewRun = new EventEmitter<string>();

    public runs: OptimizationRunSummary[] = [];
    public totalCount = 0;
    public page = 1;
    public pageSize = 10;
    public displayedColumns = ["createdAt", "symbol", "status", "totalCombinations", "qualifiedCount", "elapsedMs"];

    public ngOnInit(): void {
        this._loadRuns();
    }

    public onPageChange(event: PageEvent): void {
        this.page = event.pageIndex + 1;
        this.pageSize = event.pageSize;
        this._loadRuns();
    }

    public onRowClick(run: OptimizationRunSummary): void {
        this.viewRun.emit(run.id);
    }

    private _loadRuns(): void {
        this._optimizerService.getOptimizationList(this.page, this.pageSize)
            .pipe(takeUntilDestroyed(this._destroyRef))
            .subscribe(response => {
                this.runs = response.items;
                this.totalCount = response.totalCount;
            });
    }
}
```

---

### Task 4.4: Implement "Create Strategy" promotion flow {#task-44-create-strategy-promotion}

Wire the "Create Strategy" button to navigate to the strategy builder with the optimization result's config pre-filled.

- **Complexity**: Medium
- **Risk Factors**: Must match the strategy builder's expected input format for pre-fill. The `strategyConfigJson` stored in optimization results IS the exact `StrategyConfig` format used by the builder.
- **Files**:
  - `frontend/trading-ui/src/app/features/optimizer/optimizer-page.component.ts` — modification
  - `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` — modification (accept query param or route state)
- **Success**:
  - Clicking "Create Strategy" on optimization result navigates to `/strategies/new` with the strategy config
  - Strategy builder form pre-fills with all parameter values from the optimization result
  - User can review, modify, and save as a new strategy

#### Implementation Details

**Navigation approach**: Use Angular Router `state` to pass the strategy config:

```typescript
// In optimizer-page.component.ts
public onCreateStrategy(result: OptimizationResultResponse): void {
    const config = JSON.parse(result.strategyConfigJson);
    config.strategyName = `Optimizer Winner #${result.rank}`;

    this._router.navigate(["/strategies/new"], {
        state: { prefillConfig: config }
    });
}
```

```typescript
// In strategy-builder-page.component.ts — check for router state on init
// Already has prefillConfig pattern from backtest → strategy flow
// Verify it handles the state and pre-fills the form
```

The strategy builder already has a `prefillConfig` mechanism (used when launching a backtest from a strategy). The optimizer result's `strategyConfigJson` is the exact same `StrategyConfig` shape. The builder should detect the state and auto-populate all form sections:
- Mode = Signal
- Direction = Long
- Entry conditions (RSI, MACD, PriceVsEma with their params)
- Entry logic (All/Any)
- Trend filter (if present)
- Exit rules (SL/TP percentages)
- Risk settings (leverage, position size)

If the builder already accepts `prefillConfig` from route state, this should work with zero changes to the builder. Verify and test.

---

### Task 4.5: Build frontend, lint, and run unit tests {#task-45-build-lint-test}

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None — verification only
- **Success**:
  - `npx ng build` succeeds with zero errors
  - `npx ng lint` succeeds with zero errors
  - `npx ng test --watch=false --browsers=ChromeHeadless` — all tests pass
  - Full end-to-end smoke test: navigate to Optimizer tab → configure → confirm form renders
