<!-- markdownlint-disable-file -->

# Task Details: F11 — Position Data Enrichment

## Phase 3: Frontend — Funding Rate Indicator Component

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — standalone components, `inject()`, `@if`/`@for` control flow, explicit `public`/`private`, double quotes, SCSS, BEM
- `.agent-context/0-knowledge/07-ui-design.md` — Dashboard component patterns

## Design References

- Funding rate from Hyperliquid is a per-hour rate (e.g., `0.0001` = 0.01% hourly)
- Color logic: For Long positions: positive funding = paying (red/loss), negative funding = receiving (green/profit). For Short positions: positive funding = receiving (green/profit), negative funding = paying (red/loss).
- Tooltip shows: hourly rate as percentage + estimated daily USD cost/income based on notional
- Compact display: rate as percentage with 4 decimal places
- Existing badge/indicator patterns from `activity-feed.component` and `leverage-badge` in positions table

### Task 3.1: Create FundingIndicatorComponent {#task-31-create-fundingindicatorcomponent}

Create a new standalone Angular component that displays a color-coded funding rate with a tooltip showing the hourly rate and estimated daily USD cost/income.

- **Complexity**: Medium
- **Risk Factors**: Funding rate semantics differ by position side. Must correctly invert the favorable/unfavorable logic for short vs long positions. Daily estimate calculation: `fundingRate × 24 × notionalValue`.
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/funding-indicator/funding-indicator.component.ts` — new component
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/funding-indicator/funding-indicator.component.html` — new template
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/funding-indicator/funding-indicator.component.scss` — new styles
- **Success**:
  - Component renders funding rate as percentage (e.g., "0.0100%")
  - Green when receiving funding (favorable), red when paying (unfavorable)
  - Tooltip shows "Hourly: X.XXXX% | Est. daily: +$X.XX" or "Est. daily: -$X.XX"
  - Shows "—" when funding rate is 0
- **Dependencies**: Phase 2 complete

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/positions-table/funding-indicator/funding-indicator.component.ts — new file
import { DecimalPipe } from "@angular/common";
import { Component, Input } from "@angular/core";
import { MatTooltipModule } from "@angular/material/tooltip";

@Component({
  selector: "app-funding-indicator",
  standalone: true,
  imports: [DecimalPipe, MatTooltipModule],
  templateUrl: "./funding-indicator.component.html",
  styleUrl: "./funding-indicator.component.scss"
})
export class FundingIndicatorComponent {
  @Input()
  public fundingRate: number = 0;

  @Input()
  public side: string = "";

  @Input()
  public notional: number = 0;

  public get isFavorable(): boolean {
    if (this.fundingRate === 0) {
      return false;
    }

    // Long: negative funding = receiving = favorable
    // Short: positive funding = receiving = favorable
    return this.side === "Long"
      ? this.fundingRate < 0
      : this.fundingRate > 0;
  }

  public get fundingClass(): string {
    if (this.fundingRate === 0) {
      return "";
    }

    return this.isFavorable ? "funding--receiving" : "funding--paying";
  }

  public get ratePercent(): number {
    return this.fundingRate * 100;
  }

  public get tooltipText(): string {
    if (this.fundingRate === 0) {
      return "";
    }

    const hourlyPercent = (this.fundingRate * 100).toFixed(4);
    const dailyCost = this.fundingRate * 24 * this.notional;
    const sign = this.isFavorable ? "+" : "-";
    const absDaily = Math.abs(dailyCost).toFixed(2);

    return `Hourly: ${hourlyPercent}% | Est. daily: ${sign}$${absDaily}`;
  }
}
```

```html
<!-- frontend/trading-ui/src/app/features/dashboard/positions-table/funding-indicator/funding-indicator.component.html — new file -->
@if (fundingRate === 0) {
  <span class="funding__na">—</span>
} @else {
  <span [class]="fundingClass" [matTooltip]="tooltipText">
    {{ ratePercent | number: "1.4-4" }}%
  </span>
}
```

```scss
// frontend/trading-ui/src/app/features/dashboard/positions-table/funding-indicator/funding-indicator.component.scss — new file
:host {
  display: inline-block;
}

.funding--receiving {
  color: var(--colour-profit);
}

.funding--paying {
  color: var(--colour-loss);
}

.funding__na {
  color: var(--colour-muted);
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts` — sibling component in same feature directory, same patterns (standalone, Input, DecimalPipe)
- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.scss` — color variable usage (`--colour-profit`, `--colour-loss`, `--colour-muted`)
- `frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.ts` — `MatTooltipModule` import pattern

### Task 3.2: Add Funding column to positions table {#task-32-add-funding-column-to-positions-table}

Import `FundingIndicatorComponent` into `PositionsTableComponent` and add a "Funding" column that uses it.

- **Complexity**: Low
- **Risk Factors**: None — straightforward component composition. Notional value needs to be computed for the tooltip (same calculation as the Notional column).
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts` — import `FundingIndicatorComponent`
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html` — add Funding column
- **Success**:
  - Funding column appears after Margin and before Liquidation Price
  - `FundingIndicatorComponent` correctly receives `fundingRate`, `side`, and `notional` from the position
  - Color coding and tooltip work correctly
- **Dependencies**: Task 3.1

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts — modification
import { FundingIndicatorComponent } from "./funding-indicator/funding-indicator.component";

@Component({
  // ... existing
  imports: [DecimalPipe, MatButtonModule, MatProgressSpinnerModule, MatTooltipModule, FundingIndicatorComponent],
})
```

```html
<!-- frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html — modification -->
<!-- In <thead>, before Liquidation Price: -->
<th>Funding</th>

<!-- In <tbody> row, before Liquidation Price: -->
<td>
  <app-funding-indicator
    [fundingRate]="position.fundingRate"
    [side]="position.side"
    [notional]="getNotional(position)">
  </app-funding-indicator>
</td>
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — component composition pattern (importing child components in `imports` array)

### Task 3.3: Add responsive column handling {#task-33-add-responsive-column-handling}

Add CSS to handle the wider table on smaller viewports. The table wrapper already has `overflow-x: auto` for horizontal scrolling. Add media query to hide the Notional and Margin columns on narrow screens (≤900px) to keep the table readable.

- **Complexity**: Low
- **Risk Factors**: Using `nth-child` selectors may be fragile if columns are reordered later. Consider using a CSS class on the columns to hide instead. However, the table uses standard `<th>`/`<td>` without classes on individual columns. The simplest approach is adding a CSS class to the hideable columns.
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html` — add `class="positions-table__hide-narrow"` to Notional and Margin th/td
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.scss` — add media query
- **Success**:
  - On viewports ≤900px, Notional and Margin columns are hidden
  - All other columns remain visible
  - Table still scrolls horizontally if needed
- **Dependencies**: Task 3.2

#### Implementation Details

```scss
// frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.scss — addition

.positions-table__hide-narrow {
  @media (max-width: 900px) {
    display: none;
  }
}
```

```html
<!-- Apply to Notional and Margin header and data cells -->
<th class="positions-table__hide-narrow">Notional</th>
<!-- ... -->
<td class="positions-table__hide-narrow">...</td>

<th class="positions-table__hide-narrow">Margin</th>
<!-- ... -->
<td class="positions-table__hide-narrow">...</td>
```

##### Pattern References

- `frontend/trading-ui/src/app/features/market-data/price-ticker/price-ticker.component.scss` — existing `@media (max-width: 900px)` breakpoint in the project

### Task 3.4: Run frontend build and lint {#task-34-run-frontend-build-and-lint}

Run `ng build` and `ng lint` to verify the frontend compiles and passes lint checks with all changes from Phase 2 and Phase 3.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification step)
- **Success**:
  - `ng build` completes with zero errors
  - `ng lint` passes with zero violations
- **Dependencies**: All previous tasks in Phase 3

## Phase Success Criteria

- `FundingIndicatorComponent` created as standalone component in `positions-table/funding-indicator/`
- Funding column integrated into positions table using the new component
- Color coding: green when receiving funding (favorable), red when paying (unfavorable)
- Tooltip shows: hourly rate as percentage + estimated daily USD cost/income
- Notional and Margin columns hidden on narrow viewports (≤900px)
- Frontend builds and lints without errors
- All 10 PBI acceptance criteria verifiable against the running application
