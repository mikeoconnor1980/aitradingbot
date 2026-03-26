<!-- markdownlint-disable-file -->

# Task Details: F9 — Position Actions

## Phase 4: Frontend — Position Detail Panel

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — Standalone components, `inject()` only, `@if`/`@for` control flow, double quotes, SCSS Flexbox
- `.agent-context/0-knowledge/11-angular-instructions.md` — Row-level loading pattern, `@ViewChild` access, CSS colour variables
- `.agent-context/0-knowledge/07-ui-design.md` — Dashboard layout, colour scheme

## Design References

- No existing expandable row pattern — this is net-new. Uses `@if` with a tracked `expandedPositionKey` and an inline `<tr class="detail-row">` toggled by key match.
- Position detail data comes from the extended `Position` model (Phase 1 enrichment: `marginUsed`, `positionValue`).
- Funding rate sourced from market data (SignalR stream or existing market info endpoint).
- Associated TP/SL orders identified from open orders by matching asset + opposite side + trigger order type.

---

### Task 4.1: Create PositionDetailPanelComponent {#task-41-create-positiondetailpanelcomponent}

Create a standalone inline component that displays detailed position information. This component receives a `Position` and optionally an array of associated TP/SL `OpenOrder`s. It renders a grid of detail fields: entry price, mark price, liquidation price, margin used, notional value (positionValue), leverage, margin type, and associated TP/SL orders.

- **Complexity**: Medium
- **Risk Factors**: Funding rate may not be available if market data is not loaded; TP/SL order matching must be resilient to missing data
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/position-detail-panel/position-detail-panel.component.ts` — New file
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/position-detail-panel/position-detail-panel.component.html` — New file
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/position-detail-panel/position-detail-panel.component.scss` — New file
- **Success**:
  - Component displays all detail fields in a structured grid
  - Shows associated TP/SL orders (if any) with order type, price, and size
  - Shows "—" placeholder for unavailable data (e.g. funding rate if not loaded)
  - Component is visually consistent with the positions table styling
- **Dependencies**: Task 2.1 (Position model with `marginUsed`, `positionValue`)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/positions-table/position-detail-panel/position-detail-panel.component.ts — new file
import { DecimalPipe } from "@angular/common";
import { Component, Input } from "@angular/core";
import { Position } from "../../../../core/models/position.model";
import { OpenOrder } from "../../../../core/models/open-order.model";

@Component({
  selector: "app-position-detail-panel",
  standalone: true,
  imports: [DecimalPipe],
  templateUrl: "./position-detail-panel.component.html",
  styleUrl: "./position-detail-panel.component.scss"
})
export class PositionDetailPanelComponent {
  @Input({ required: true })
  public position!: Position;

  @Input()
  public associatedOrders: OpenOrder[] = [];

  @Input()
  public fundingRate: number | null = null;


}
```

```html
<!-- frontend/trading-ui/src/app/features/dashboard/positions-table/position-detail-panel/position-detail-panel.component.html — new file -->
<div class="position-detail">
  <div class="position-detail__grid">
    <div class="position-detail__item">
      <span class="position-detail__label">Entry Price</span>
      <span class="position-detail__value">{{ position.entryPrice | number: "1.2-6" }}</span>
    </div>
    <div class="position-detail__item">
      <span class="position-detail__label">Mark Price</span>
      <span class="position-detail__value">{{ position.markPrice | number: "1.2-6" }}</span>
    </div>
    <div class="position-detail__item">
      <span class="position-detail__label">Liquidation Price</span>
      <span class="position-detail__value">{{ position.liquidationPrice | number: "1.2-6" }}</span>
    </div>
    <div class="position-detail__item">
      <span class="position-detail__label">Margin Used</span>
      <span class="position-detail__value">{{ position.marginUsed | number: "1.2-2" }}</span>
    </div>
    <div class="position-detail__item">
      <span class="position-detail__label">Notional Value</span>
      <span class="position-detail__value">{{ position.positionValue | number: "1.2-2" }}</span>
    </div>
    <div class="position-detail__item">
      <span class="position-detail__label">Leverage</span>
      <span class="position-detail__value">{{ position.leverage }}x {{ position.marginMode }}</span>
    </div>
    <div class="position-detail__item">
      <span class="position-detail__label">Funding Rate</span>
      <span class="position-detail__value">
        @if (fundingRate !== null) {
          {{ fundingRate | number: "1.6-6" }}%
        } @else {
          —
        }
      </span>
    </div>
  </div>

  @if (associatedOrders.length > 0) {
    <div class="position-detail__orders">
      <span class="position-detail__orders-title">TP/SL Orders</span>
      <table class="position-detail__orders-table">
        <thead>
          <tr>
            <th>Type</th>
            <th>Side</th>
            <th>Price</th>
            <th>Size</th>
          </tr>
        </thead>
        <tbody>
          @for (order of associatedOrders; track order.orderId) {
            <tr>
              <td>{{ order.orderType }}</td>
              <td>{{ order.side }}</td>
              <td>{{ order.price | number: "1.2-6" }}</td>
              <td>{{ order.size | number: "1.4-4" }}</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  } @else {
    <div class="position-detail__no-orders">No TP/SL orders set</div>
  }
</div>
```

```scss
// frontend/trading-ui/src/app/features/dashboard/positions-table/position-detail-panel/position-detail-panel.component.scss — new file
.position-detail {
  padding: 1rem 1.5rem;
  background-color: var(--colour-surface-dark);
  border-bottom: 1px solid var(--colour-border-subtle);

  &__grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
    gap: 0.75rem 2rem;
    margin-bottom: 1rem;
  }

  &__item {
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
  }

  &__label {
    color: var(--colour-label);
    font-size: 0.7rem;
    text-transform: uppercase;
    letter-spacing: 0.08em;
  }

  &__value {
    font-size: 0.9rem;
  }

  &__orders {
    margin-top: 0.5rem;
  }

  &__orders-title {
    display: block;
    color: var(--colour-label);
    font-size: 0.75rem;
    text-transform: uppercase;
    letter-spacing: 0.08em;
    margin-bottom: 0.5rem;
  }

  &__orders-table {
    width: 100%;
    border-collapse: collapse;

    th,
    td {
      padding: 0.4rem 0.75rem;
      text-align: left;
      font-size: 0.85rem;
    }

    th {
      color: var(--colour-muted);
      font-size: 0.7rem;
      text-transform: uppercase;
    }
  }

  &__no-orders {
    color: var(--colour-muted);
    font-size: 0.85rem;
    font-style: italic;
  }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html` — table styling, CSS variable usage
- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.scss` — BEM naming, colour variables

---

### Task 4.2: Add expandable row behavior to positions table {#task-42-add-expandable-row-behavior-to-positions-table}

Add expand/collapse logic to `PositionsTableComponent`. Clicking a position row (outside of action buttons) toggles an inline detail row below it. Only one detail panel can be expanded at a time.

- **Complexity**: Medium
- **Risk Factors**: Click handler must not trigger on action button clicks (use `$event.target` check or a separate clickable area); colspan must match column count; expand/collapse animation should be smooth
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts` — Add `expandedPositionKey`, `toggleExpand`, import `PositionDetailPanelComponent`
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html` — Add detail row after each position row
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.scss` — Style detail row and clickable rows
- **Success**:
  - Clicking a position row expands the detail panel below it
  - Clicking the same row again collapses it
  - Clicking a different row collapses the previous and expands the new one
  - Action button clicks do NOT trigger expand/collapse
  - Detail row spans the full table width
- **Dependencies**: Task 4.1

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts — modifications

// Add to imports array:
import { PositionDetailPanelComponent } from "./position-detail-panel/position-detail-panel.component";

// Add to component imports:
imports: [DecimalPipe, MatButtonModule, MatIconModule, MatMenuModule, MatProgressSpinnerModule, PositionDetailPanelComponent],

// Add to class:
@Input()
public orders: OpenOrder[] = [];

@Input()
public fundingRates: Record<string, number> = {};

public expandedPositionKey: string | null = null;

public toggleExpand(position: Position): void {
  const key = this.getPositionKey(position);
  this.expandedPositionKey = this.expandedPositionKey === key ? null : key;
}

public isExpanded(position: Position): boolean {
  return this.expandedPositionKey === this.getPositionKey(position);
}

public getAssociatedOrders(position: Position): OpenOrder[] {
  const oppositeSide = position.side === "Long" ? "Sell" : "Buy";
  return this.orders.filter(
    (order) => order.asset === position.asset && order.side === oppositeSide
  );
}
```

```html
<!-- frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html — modification -->
<!-- Inside the @for loop, after the main <tr>, add the detail row: -->
@for (position of positions; track position.asset + position.side) {
  <tr [class.positions-table__row--loading]="isLoading(position)"
      [class.positions-table__row--expanded]="isExpanded(position)"
      (click)="toggleExpand(position)">
    <!-- ... existing <td> cells unchanged ... -->
    <td class="positions-table__actions" (click)="$event.stopPropagation()">
      <!-- ... action buttons unchanged, stopPropagation prevents row toggle ... -->
    </td>
  </tr>
  @if (isExpanded(position)) {
    <tr class="positions-table__detail-row">
      <td [attr.colspan]="7">
        <app-position-detail-panel
          [position]="position"
          [associatedOrders]="getAssociatedOrders(position)"
          [fundingRate]="fundingRates[position.asset] ?? null">
        </app-position-detail-panel>
      </td>
    </tr>
  }
}
```

```scss
// frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.scss — add

.positions-table {
  // ... existing styles ...

  tbody tr:not(.positions-table__detail-row) {
    cursor: pointer;

    &:hover {
      background-color: rgba(255, 255, 255, 0.03);
    }
  }

  &__row--expanded {
    background-color: rgba(255, 255, 255, 0.04);
  }

  &__detail-row {
    td {
      padding: 0;
      border-bottom: 1px solid var(--colour-border-subtle);
    }
  }
}

.positions-table__row--loading {
  opacity: 0.6;
  pointer-events: none;
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html` — existing `@for` loop with row tracking
- `frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.html` — `(click)` + `$event.stopPropagation()` pattern for action cells

---

### Task 4.3: Wire position detail data and TP/SL order display {#task-43-wire-position-detail-data-and-tpsl-order-display}

Pass the `orders` array and per-asset funding rate map from `DashboardComponent` to `PositionsTableComponent` so the detail panel can display associated TP/SL orders and funding rates (required by acceptance criterion #8).

- **Complexity**: Low
- **Risk Factors**: Funding rate data must be sourced from market data service; if not yet loaded, detail panel shows "—" placeholder
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/dashboard.component.html` — Pass `[orders]` and `[fundingRates]` to `app-positions-table`
  - `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — Compute `fundingRates` map from market data service
- **Success**:
  - `PositionsTableComponent` receives the open orders array and funding rate map
  - Detail panel shows associated TP/SL orders for each position
  - Detail panel shows "No TP/SL orders set" when none exist
  - Detail panel shows per-asset funding rate (or "—" if not loaded)
- **Dependencies**: Task 4.2

#### Implementation Details

```html
<!-- frontend/trading-ui/src/app/features/dashboard/dashboard.component.html — modification -->
<app-positions-table
  [positions]="positions"
  [orders]="orders"
  [fundingRates]="fundingRates"
  (closePosition)="onClosePosition($event)"
  (setTpSl)="onSetTpSl($event)"
  (partialClose)="onPartialClose($event)"
  (reversePosition)="onReversePosition($event)">
</app-positions-table>
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.html` — existing `[positions]` binding

---

### Task 4.4: Frontend build and lint {#task-44-frontend-build-and-lint}

Run the Angular build and lint to verify no compilation or style errors.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (build/lint only)
- **Success**:
  - `npx ng build` succeeds with no errors
  - `npx ng lint` passes with no errors or warnings
- **Dependencies**: Tasks 4.1–4.3

## Phase Success Criteria

- Clicking a position row expands an inline detail panel showing all required fields
- Detail panel displays entry price, mark price, liquidation price, margin used, notional value, leverage, and associated TP/SL orders
- Only one detail panel is expanded at a time; clicking another row collapses the previous
- Action buttons do not trigger expand/collapse
- Frontend compiles and lints without errors
