<!-- markdownlint-disable-file -->

# Task Details: F3.5 — Strategy–Backtest Integration

## Phase 4: Frontend — Navigation & Backtest History

## Standards and Knowledge References

- **angular.instructions.md**: `standalone: true`, `inject()` for DI, explicit visibility modifiers, `@if`/`@for` control flow, kebab-case CSS classes, `Router.navigate()` for navigation
- **18-backtesting-architecture.md**: Backtesting UI lives under `features/backtesting/`, strategy UI under `features/strategy-builder/`

### Task 4.1: Add Backtest Action to Strategy List {#task-41-add-backtest-action-to-strategy-list}

Add a "Backtest" action button to each row in the strategy list table. Clicking navigates to `/backtesting?strategyId={id}`.

- **Complexity**: Low
- **Risk Factors**: None — follows existing action button pattern (Edit, Delete)
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/strategy-list-page.component.ts` — Add navigation method
  - `frontend/trading-ui/src/app/features/strategy-builder/strategy-list-page.component.html` — Add button to actions column
- **Success**:
  - Each strategy row has a "Backtest" icon button
  - Clicking navigates to `/backtesting?strategyId={strategy.id}`
- **Dependencies**: Phase 3

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/strategy-list-page.component.ts — modification
// Add new navigation method:

  public onBacktestStrategy(strategyId: string): void {
    this._router.navigate(["/backtesting"], {
      queryParams: { strategyId },
    });
  }
```

```html
<!-- frontend/trading-ui/src/app/features/strategy-builder/strategy-list-page.component.html — modification -->
<!-- Add to the actions column, after existing Edit and Delete buttons: -->

<button mat-icon-button
        color="primary"
        matTooltip="Backtest"
        (click)="onBacktestStrategy(strategy.id); $event.stopPropagation()">
  <mat-icon>science</mat-icon>
</button>
```

##### Pattern References

Based on `frontend/trading-ui/src/app/features/strategy-builder/strategy-list-page.component.ts` — existing Edit/Delete action patterns.

### Task 4.2: Add Backtest Button to Builder {#task-42-add-backtest-button-to-builder}

Add a "Backtest this strategy" button to the strategy builder page when editing an existing strategy (not when creating a new one).

- **Complexity**: Low
- **Risk Factors**: Button only visible in edit mode (when `strategyId` is set from route param)
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` — Add navigation method
  - `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.html` — Add "Backtest" button in header area
- **Success**:
  - "Backtest this strategy" button visible only in edit mode
  - Clicking navigates to `/backtesting?strategyId={id}`
- **Dependencies**: Phase 3

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts — modification
// Add navigation method:

  public onBacktestStrategy(): void {
    if (this.strategyId) {
      this._router.navigate(["/backtesting"], {
        queryParams: { strategyId: this.strategyId },
      });
    }
  }
```

```html
<!-- frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.html — modification -->
<!-- Add button in header area, conditionally shown in edit mode: -->

@if (strategyId) {
  <button mat-stroked-button color="primary" (click)="onBacktestStrategy()">
    <mat-icon>science</mat-icon>
    Backtest this strategy
  </button>
}
```

##### Pattern References

Based on `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` — existing `strategyId` property from route param used for edit mode detection.

### Task 4.3: Add Strategy Column to Backtest List {#task-43-add-strategy-column-to-backtest-list}

Add a "Strategy" column to the backtest results list table. Display the strategy name as a clickable link that navigates to `/strategies/{id}/edit`. Show "—" for backtests without a linked strategy.

- **Complexity**: Medium
- **Risk Factors**: Must handle null `strategyId` gracefully; deleted strategies should show name with "(deleted)" indicator
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/backtest-list/backtest-list.component.ts` — Add strategy name column to displayed columns, add navigation method
  - `frontend/trading-ui/src/app/features/backtesting/backtest-list/backtest-list.component.html` — Add strategy column definition with link
- **Success**:
  - Strategy column shows strategy name for linked backtests
  - Clicking strategy name navigates to strategy edit page
  - Backtests without strategy show "—"
- **Dependencies**: Phase 3 (BacktestSummary model update)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/backtesting/backtest-list/backtest-list.component.ts — modification
// Add "strategyName" to displayedColumns array (insert after "symbol", before date columns)
// Add navigation method:

  public onNavigateToStrategy(strategyId: string): void {
    this._router.navigate(["/strategies", strategyId, "edit"]);
  }
```

```html
<!-- frontend/trading-ui/src/app/features/backtesting/backtest-list/backtest-list.component.html — modification -->
<!-- Add strategy column definition: -->

<ng-container matColumnDef="strategyName">
  <th mat-header-cell *matHeaderCellDef>Strategy</th>
  <td mat-cell *matCellDef="let row">
    @if (row.strategyName) {
      <a class="strategy-link" (click)="onNavigateToStrategy(row.strategyId); $event.stopPropagation()">
        {{ row.strategyName }}
      </a>
    } @else {
      <span class="no-strategy">—</span>
    }
  </td>
</ng-container>
```

##### Pattern References

Based on `frontend/trading-ui/src/app/features/backtesting/backtest-list/backtest-list.component.ts` — existing column pattern.

### Task 4.4: Add Strategy Link to Result Detail {#task-44-add-strategy-link-to-result-detail}

Add strategy name and revision number to the backtest result detail view. The strategy name is a clickable link to the strategy edit page. Show "(deleted)" if the strategy has been soft-deleted.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.ts` — Add navigation method
  - `frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.html` — Add strategy info row in config summary section
- **Success**:
  - Strategy name + revision number displayed in result detail
  - Clicking strategy name navigates to edit page
  - If no strategy linked, section hidden
- **Dependencies**: Phase 3 (BacktestResult model update)

#### Implementation Details

```html
<!-- frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.html — modification -->
<!-- Add strategy info in the config summary section: -->

@if (result.strategyName) {
  <div class="config-row">
    <span class="config-label">Strategy</span>
    <span class="config-value">
      <a class="strategy-link" (click)="onNavigateToStrategy(result.strategyId!)">
        {{ result.strategyName }}
      </a>
      @if (result.strategyRevisionId) {
        <span class="revision-badge">v{{ result.strategyRevisionId }}</span>
      }
    </span>
  </div>
}
```

```typescript
// frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.ts — modification
// Add Router injection and navigation method:

  private readonly _router = inject(Router);

  public onNavigateToStrategy(strategyId: string): void {
    this._router.navigate(["/strategies", strategyId, "edit"]);
  }
```

##### Pattern References

Based on `frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.html` — existing config summary display pattern.

### Task 4.5: Add Backtest History Panel {#task-45-add-backtest-history-panel}

Add a backtest history panel to the strategy builder page (in edit mode) showing all backtests for the current strategy, grouped by revision.

- **Complexity**: High
- **Risk Factors**: New component creation; must handle empty state; must handle paging; must be visible only in edit mode
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/components/strategy-backtest-history/strategy-backtest-history.component.ts` — New standalone component
  - `frontend/trading-ui/src/app/features/strategy-builder/components/strategy-backtest-history/strategy-backtest-history.component.html` — Template
  - `frontend/trading-ui/src/app/features/strategy-builder/components/strategy-backtest-history/strategy-backtest-history.component.scss` — Styles
  - `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` — Import `StrategyBacktestHistoryComponent` in component `imports` array and use history component
  - `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.html` — Add history panel in layout
- **Success**:
  - History panel shows when editing an existing strategy
  - Displays all backtests grouped by revision number
  - Each row shows key metrics (total PnL, win rate, max drawdown, date)
  - Clicking a row navigates to the full backtest result
  - Empty state shown when no backtests exist for the strategy
- **Dependencies**: Tasks 3.2, 4.2

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/components/strategy-backtest-history/strategy-backtest-history.component.ts — new file

import { Component, DestroyRef, Input, OnChanges, SimpleChanges, inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import { Router } from "@angular/router";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { MatTableModule } from "@angular/material/table";
import { MatCardModule } from "@angular/material/card";
import { MatIconModule } from "@angular/material/icon";
import { MatButtonModule } from "@angular/material/button";
import { BacktestService } from "../../../../core/services/backtest.service";
import { BacktestSummary } from "../../../../core/models/backtest.model";

interface RevisionGroup {
  revisionNumber: number | null;
  backtests: BacktestSummary[];
}

@Component({
  selector: "app-strategy-backtest-history",
  standalone: true,
  imports: [CommonModule, MatTableModule, MatCardModule, MatIconModule, MatButtonModule],
  templateUrl: "./strategy-backtest-history.component.html",
  styleUrls: ["./strategy-backtest-history.component.scss"],
})
export class StrategyBacktestHistoryComponent implements OnChanges {
  @Input({ required: true }) public strategyId!: string;

  private readonly _backtestService = inject(BacktestService);
  private readonly _router = inject(Router);
  private readonly _destroyRef = inject(DestroyRef);

  public revisionGroups: RevisionGroup[] = [];
  public isLoading = false;
  public isEmpty = false;

  public ngOnChanges(changes: SimpleChanges): void {
    if (changes["strategyId"] && this.strategyId) {
      this._loadHistory();
    }
  }

  public onViewBacktest(backtestId: string): void {
    this._router.navigate(["/backtesting"], {
      queryParams: { viewResult: backtestId },
    });
  }

  private _loadHistory(): void {
    this.isLoading = true;
    this._backtestService
      .getBacktestsByStrategy(this.strategyId, 1, 50)
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe({
        next: (result) => {
          this.isLoading = false;
          this.isEmpty = result.items.length === 0;
          this.revisionGroups = this._groupByRevision(result.items);
        },
        error: () => {
          this.isLoading = false;
          this.isEmpty = true;
        },
      });
  }

  private _groupByRevision(backtests: BacktestSummary[]): RevisionGroup[] {
    const groups = new Map<number | null, BacktestSummary[]>();
    for (const bt of backtests) {
      const rev = bt.strategyRevisionId ?? null;
      const group = groups.get(rev) ?? [];
      group.push(bt);
      groups.set(rev, group);
    }
    return Array.from(groups.entries())
      .map(([revisionNumber, items]) => ({ revisionNumber, backtests: items }))
      .sort((a, b) => (b.revisionNumber ?? 0) - (a.revisionNumber ?? 0));
  }
}
```

```html
<!-- strategy-backtest-history.component.html — new file -->

<mat-card>
  <mat-card-header>
    <mat-card-title>Backtest History</mat-card-title>
  </mat-card-header>
  <mat-card-content>
    @if (isLoading) {
      <p>Loading backtest history...</p>
    } @else if (isEmpty) {
      <p class="empty-state">No backtests have been run for this strategy yet.</p>
    } @else {
      @for (group of revisionGroups; track group.revisionNumber) {
        <div class="revision-group">
          <h4 class="revision-header">
            @if (group.revisionNumber !== null) {
              Revision {{ group.revisionNumber }}
            } @else {
              Unversioned
            }
          </h4>
          <table mat-table [dataSource]="group.backtests" class="backtest-history-table">
            <ng-container matColumnDef="createdAt">
              <th mat-header-cell *matHeaderCellDef>Date</th>
              <td mat-cell *matCellDef="let row">{{ row.createdAt | date:"short" }}</td>
            </ng-container>
            <ng-container matColumnDef="totalPnl">
              <th mat-header-cell *matHeaderCellDef>PnL</th>
              <td mat-cell *matCellDef="let row"
                  [class.positive]="row.totalPnl > 0"
                  [class.negative]="row.totalPnl < 0">
                {{ row.totalPnl | number:"1.2-2" }}
              </td>
            </ng-container>
            <ng-container matColumnDef="winRate">
              <th mat-header-cell *matHeaderCellDef>Win Rate</th>
              <td mat-cell *matCellDef="let row">{{ row.winRate | percent:"1.1-1" }}</td>
            </ng-container>
            <ng-container matColumnDef="maxDrawdown">
              <th mat-header-cell *matHeaderCellDef>Max DD</th>
              <td mat-cell *matCellDef="let row">{{ row.maxDrawdown | number:"1.2-2" }}</td>
            </ng-container>
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef></th>
              <td mat-cell *matCellDef="let row">
                <button mat-icon-button matTooltip="View Details" (click)="onViewBacktest(row.id)">
                  <mat-icon>visibility</mat-icon>
                </button>
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="['createdAt', 'totalPnl', 'winRate', 'maxDrawdown', 'actions']"></tr>
            <tr mat-row *matRowDef="let row; columns: ['createdAt', 'totalPnl', 'winRate', 'maxDrawdown', 'actions']"
                (click)="onViewBacktest(row.id)"
                class="clickable-row"></tr>
          </table>
        </div>
      }
    }
  </mat-card-content>
</mat-card>
```

```html
<!-- frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.html — modification -->
<!-- Add backtest history panel in the side column (visible in edit mode): -->

@if (strategyId) {
  <app-strategy-backtest-history [strategyId]="strategyId"></app-strategy-backtest-history>
}
```

##### Pattern References

- Component structure based on `frontend/trading-ui/src/app/features/backtesting/backtest-list/backtest-list.component.ts` — existing table component
- Service call pattern from `frontend/trading-ui/src/app/core/services/backtest.service.ts`
- Layout integration based on `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.html` — existing side column

### Task 4.6: Update Re-run Action {#task-46-update-rerun-action}

Update the existing re-run action on backtest results to navigate to `/backtesting?strategyId={id}` with backtest-specific params pre-filled when the backtest has a linked strategy.

- **Complexity**: Medium
- **Risk Factors**: Must handle deleted strategies (fall back to displaying config as read-only without picker selection)
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/backtest-page.component.ts` — Update `onRerunConfig` to include strategyId
  - `frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.ts` — Handle prefill with strategyId + backtest params
- **Success**:
  - Re-run with strategy-linked backtest navigates to `/backtesting?strategyId={id}` with date/capital/fees pre-filled
  - Re-run without strategy falls back to existing behavior (prefill from config snapshot)
- **Dependencies**: Tasks 3.3, 3.4

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/backtesting/backtest-page.component.ts — modification
// Update onRerunConfig to route with strategyId when available:

  public onRerunConfig(backtestId: string): void {
    this._backtestService.getBacktest(backtestId)
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((result) => {
        if (result.strategyId) {
          // Navigate with strategy pre-selected
          this._router.navigate(["/backtesting"], {
            queryParams: { strategyId: result.strategyId },
          });
          // Pre-fill backtest params
          this.prefillConfig = result;
        } else {
          // Fall back to existing config-based prefill
          this.prefillConfig = result;
        }
      });
  }
```

```typescript
// frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.ts — modification
// In ngOnChanges, handle prefillConfig to fill backtest-specific fields:

  public ngOnChanges(changes: SimpleChanges): void {
    if (changes["strategyId"] && this.strategyId) {
      this.form.controls.strategyId.setValue(this.strategyId);
      this._loadSelectedStrategy(this.strategyId);
    }
    if (changes["prefillConfig"] && this.prefillConfig) {
      this._prefillBacktestParams(this.prefillConfig);
    }
  }

  private _prefillBacktestParams(result: BacktestResult): void {
    this.form.patchValue({
      startDate: result.startDate,
      endDate: result.endDate,
      initialCapital: result.initialCapital,
      makerFee: result.executionConfig.feeModel.makerFeeRate,
      takerFee: result.executionConfig.feeModel.takerFeeRate,
      slippage: result.executionConfig.feeModel.slippageRate,
    });
  }
```

##### Pattern References

Based on `frontend/trading-ui/src/app/features/backtesting/backtest-page.component.ts` — existing `onRerunConfig` method and `prefillConfig` pattern.

### Task 4.7: Build and Lint {#task-47-build-and-lint}

Verify the frontend builds and passes linting after all navigation and history changes.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None — verification step only
- **Success**:
  - `npx ng build` succeeds
  - `npx ng lint` passes
- **Dependencies**: Tasks 4.1–4.6

## Phase Success Criteria

- Strategy list has "Backtest" action per row navigating to `/backtesting?strategyId={id}`
- Strategy builder has "Backtest this strategy" button in edit mode
- Backtest list shows strategy name column with links
- Backtest result detail shows strategy name + revision with navigation
- Strategy builder shows backtest history panel grouped by revision
- Re-run action navigates with `strategyId` when available
- Frontend builds and lints cleanly
