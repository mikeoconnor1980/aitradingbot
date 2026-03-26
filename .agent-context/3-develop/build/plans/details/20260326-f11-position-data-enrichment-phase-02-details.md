<!-- markdownlint-disable-file -->

# Task Details: F11 — Position Data Enrichment

## Phase 2: Frontend — Display Enriched Position Data in Positions Table

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — standalone components, `inject()`, `@if`/`@for` control flow, explicit `public`/`private`, double quotes, SCSS
- `.github/instructions/csharp.instructions.md` — N/A for this phase (frontend only)
- `.github/instructions/testing.instructions.md` — N/A for this phase (no frontend tests exist in the project yet)
- `.agent-context/0-knowledge/07-ui-design.md` — Dashboard layout, positions table structure

## Design References

- Mark price is already in the `Position` model but not rendered in the template
- Notional value is derived client-side: `abs(size) × markPrice`
- Margin used comes from the enriched backend DTO
- Equity is available from `DashboardComponent.accountSummary.equity` for margin % tooltip
- `MatTooltipModule` from `@angular/material` is already used in `OrdersTableComponent`
- Number formatting uses Angular `DecimalPipe` with format strings: `"1.2-2"` for prices

### Task 2.1: Extend Position interface {#task-21-extend-position-interface}

Add `marginUsed` and `fundingRate` fields to the TypeScript `Position` interface to match the enriched backend DTO.

- **Complexity**: Low
- **Risk Factors**: None — additive change, existing fields unchanged
- **Files**:
  - `frontend/trading-ui/src/app/core/models/position.model.ts` — add `marginUsed` and `fundingRate` number properties
- **Success**:
  - `Position` interface includes `marginUsed: number` and `fundingRate: number`
  - No TypeScript compilation errors
- **Dependencies**: Phase 1 complete (backend returns the new fields)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/models/position.model.ts — modification
export interface Position {
  asset: string;
  size: number;
  side: string;
  entryPrice: number;
  markPrice: number;
  unrealisedPnl: number;
  unrealisedPnlPercent: number;
  liquidationPrice: number;
  leverage: number;
  marginMode: string;
  marginUsed: number;
  fundingRate: number;
}
```

##### Pattern References

- `frontend/trading-ui/src/app/core/models/position.model.ts` — existing interface pattern

### Task 2.2: Add Mark Price column {#task-22-add-mark-price-column}

Add a "Mark Price" column to the positions table between "Entry Price" and "Unrealised PnL". Include a color-coded indicator showing whether the price is moving in favor or against the position.

- **Complexity**: Medium
- **Risk Factors**: Color logic depends on position side: for Long, mark > entry = green; for Short, mark < entry = green. Must handle the edge case where mark === entry (neutral, no indicator).
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html` — add column
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts` — add `getMarkPriceClass` method
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.scss` — add mark price indicator styles
- **Success**:
  - Mark Price column appears between Entry Price and Unrealised PnL
  - Green indicator when price moves in position's favor
  - Red indicator when price moves against position
  - No indicator when mark === entry
  - Displays "—" when markPrice is 0 (unavailable)
- **Dependencies**: Task 2.1

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts — modification
// Add method to component class:
public getMarkPriceClass(position: Position): string {
  if (position.markPrice === 0 || position.markPrice === position.entryPrice) {
    return "";
  }

  const isFavorable = position.side === "Long"
    ? position.markPrice > position.entryPrice
    : position.markPrice < position.entryPrice;

  return isFavorable ? "mark-price--favorable" : "mark-price--adverse";
}
```

```html
<!-- frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html — modification -->
<!-- Add between Entry Price and Unrealised PnL columns -->
<!-- In <thead>: -->
<th>Mark Price</th>

<!-- In <tbody> row: -->
<td>
  @if (position.markPrice === 0) {
    <span class="positions-table__na">—</span>
  } @else {
    <span [class]="getMarkPriceClass(position)">
      {{ position.markPrice | number: "1.2-2" }}
    </span>
  }
</td>
```

```scss
// frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.scss — addition

.mark-price--favorable {
  color: var(--colour-profit);
}

.mark-price--adverse {
  color: var(--colour-loss);
}

.positions-table__na {
  color: var(--colour-muted);
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts` — existing `getPnlClass()` method pattern for sign-based color coding
- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.scss` — existing `.pnl--profit`/`.pnl--loss` CSS variable usage

### Task 2.3: Add Notional column {#task-23-add-notional-column}

Add a "Notional" column showing the USD value of the position, calculated client-side as `abs(size) × markPrice`.

- **Complexity**: Low
- **Risk Factors**: When `markPrice` is 0 (unavailable), notional should show "—" instead of "$0.00"
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html` — add column
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts` — add `getNotional` method
- **Success**:
  - Notional column displays `$X,XXX.XX` format
  - Shows "—" when mark price is unavailable
- **Dependencies**: Task 2.2

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts — modification
public getNotional(position: Position): number {
  return Math.abs(position.size) * position.markPrice;
}
```

```html
<!-- frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html — modification -->
<!-- In <thead>: -->
<th>Notional</th>

<!-- In <tbody> row, after Mark Price: -->
<td>
  @if (position.markPrice === 0) {
    <span class="positions-table__na">—</span>
  } @else {
    ${{ getNotional(position) | number: "1.2-2" }}
  }
</td>
```

##### Pattern References

- Existing `DecimalPipe` usage with `"1.2-2"` format throughout the template

### Task 2.4: Add Margin column with tooltip {#task-24-add-margin-column-with-tooltip}

Add a "Margin" column showing `marginUsed` in USD with a `matTooltip` displaying the percentage of total account equity.

- **Complexity**: Medium
- **Risk Factors**: Requires `equity` value from `AccountSummary` to calculate percentage. The `PositionsTableComponent` is a dumb presentational component — it needs a new `@Input()` for equity. Tooltip content must be dynamic (computed per position).
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts` — add `equity` Input, `getMarginPercent` method, import `MatTooltipModule`
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html` — add column with tooltip
- **Success**:
  - Margin column displays `$X,XXX.XX` format
  - Tooltip shows "X.X% of equity" on hover
  - Gracefully handles equity = 0 (no tooltip or shows "—")
- **Dependencies**: Task 2.1

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts — modification

// Add to imports array:
import { MatTooltipModule } from "@angular/material/tooltip";

@Component({
  // ... existing
  imports: [DecimalPipe, MatButtonModule, MatProgressSpinnerModule, MatTooltipModule],
})
export class PositionsTableComponent {
  @Input()
  public positions: Position[] = [];

  @Input()
  public equity: number = 0;

  // ... existing members ...

  public getMarginPercent(position: Position): string {
    if (this.equity <= 0 || position.marginUsed <= 0) {
      return "";
    }

    const percent = (position.marginUsed / this.equity) * 100;
    return `${percent.toFixed(1)}% of equity`;
  }
}
```

```html
<!-- frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html — modification -->
<!-- In <thead>: -->
<th>Margin</th>

<!-- In <tbody> row: -->
<td>
  @if (position.marginUsed > 0) {
    <span [matTooltip]="getMarginPercent(position)">
      ${{ position.marginUsed | number: "1.2-2" }}
    </span>
  } @else {
    <span class="positions-table__na">—</span>
  }
</td>
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.ts` — existing `MatTooltipModule` import and `matTooltip` usage

### Task 2.5: Pass equity to PositionsTableComponent {#task-25-pass-equity-to-positions-table}

Update the `DashboardComponent` template to pass `accountSummary.equity` to the `PositionsTableComponent` via the new `equity` input.

- **Complexity**: Low
- **Risk Factors**: `accountSummary` may be null initially (before first data load). Use optional chaining with fallback to 0.
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/dashboard.component.html` — add `[equity]` binding
- **Success**:
  - `PositionsTableComponent` receives equity value from dashboard
  - Margin % tooltip correctly reflects account equity
- **Dependencies**: Task 2.4

#### Implementation Details

```html
<!-- frontend/trading-ui/src/app/features/dashboard/dashboard.component.html — modification -->
<app-positions-table
  [positions]="positions"
  [equity]="accountSummary?.equity ?? 0"
  (closePosition)="onClosePosition($event)"></app-positions-table>
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.html` — existing `[positions]` binding pattern

### Task 2.6: Run frontend build and lint {#task-26-run-frontend-build-and-lint}

Run `ng build` and `ng lint` to verify the frontend compiles and passes lint checks.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification step)
- **Success**:
  - `ng build` completes with zero errors
  - `ng lint` passes with zero violations
- **Dependencies**: All previous tasks in Phase 2

## Phase Success Criteria

- `Position` TypeScript interface includes `marginUsed` and `fundingRate` fields
- Positions table renders Mark Price, Notional, and Margin columns
- Mark Price shows color-coded favorable/adverse indicator based on position side
- Notional calculated client-side as `abs(size) × markPrice`
- Margin shows USD value with matTooltip displaying "X.X% of equity"
- Missing data (mark price = 0) shows "—" dash
- Frontend builds and lints without errors
