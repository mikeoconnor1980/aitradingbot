<!-- markdownlint-disable-file -->

# Task Details: F10 — Positions Table UX Enhancements

## Phase 2: Column Sorting & Asset Filter

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — standalone components, `inject()` DI, explicit access modifiers and return types, `@if`/`@for` control flow, BEM SCSS naming, double-quoted strings
- `.agent-context/0-knowledge/11-angular-instructions.md` — CSS token system, `BehaviorSubject` polling pattern, row-level loading pattern
- `frontend/trading-ui/src/styles.scss` — CSS custom properties, Material dark theme

## Design References

- Sorting implemented as pure client-side array sort on a computed getter — no `MatSortModule` needed since the table uses `@for` loops (not `mat-table`)
- Filter uses `MatFormFieldModule` + `MatInputModule` for the search input field
- Sort state cycles: none → descending → ascending → none (descending first for numeric columns per PBI)
- `MatIconModule` for sort arrow indicators (`arrow_upward` / `arrow_downward`)

### Task 2.1: Add sort state and sort logic to `PositionsTableComponent` {#task-21-add-sort-state-and-logic}

Add sorting properties and a computed getter that returns positions sorted by the active column/direction.

- **Complexity**: Medium
- **Risk Factors**: Sort comparator must handle string (asset) and numeric columns correctly; the `none` → `desc` → `asc` → `none` cycle must be exact for the "three clicks" acceptance criteria
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts` — modification
- **Success**:
  - `sortColumn` and `sortDirection` properties added (`null`/`'asc'`/`'desc'`)
  - `onSort(column)` method cycles through desc → asc → none
  - Sortable columns: `asset`, `size`, `unrealisedPnl`, `entryPrice`, `liquidationPrice`
  - `sortedFilteredPositions` getter returns positions sorted by active column
  - Sort by `asset` uses `localeCompare`; all others use numeric comparison
  - When no sort active, positions retain API order
- **Dependencies**: None

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts — modification
// Add these types and members to the existing component:

type SortableColumn = "asset" | "size" | "unrealisedPnl" | "entryPrice" | "liquidationPrice";
type SortDirection = "asc" | "desc" | null;

// New public properties (add after existing loadingPositionKeys):
public sortColumn: SortableColumn | null = null;
public sortDirection: SortDirection = null;
public filterText = "";

// Sort cycling method:
public onSort(column: SortableColumn): void {
  if (this.sortColumn !== column) {
    this.sortColumn = column;
    this.sortDirection = "desc";
    return;
  }
  // Cycle: desc → asc → null
  if (this.sortDirection === "desc") {
    this.sortDirection = "asc";
  } else if (this.sortDirection === "asc") {
    this.sortColumn = null;
    this.sortDirection = null;
  }
}

// Computed getter — apply filter then sort:
public get sortedFilteredPositions(): Position[] {
  let result = this.positions;

  // Apply filter
  if (this.filterText) {
    const term = this.filterText.toLowerCase();
    result = result.filter((p) => p.asset.toLowerCase().includes(term));
  }

  // Apply sort
  if (!this.sortColumn || !this.sortDirection) {
    return result;
  }

  const col = this.sortColumn;
  const mult = this.sortDirection === "asc" ? 1 : -1;

  return [...result].sort((a, b) => {
    if (col === "asset") {
      return a.asset.localeCompare(b.asset) * mult;
    }
    return ((a[col] as number) - (b[col] as number)) * mult;
  });
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts` — existing component structure with `@Input positions`, `loadingPositionKeys`

### Task 2.2: Update positions table template with sortable headers and sort indicators {#task-22-update-template-with-sortable-headers}

Make column headers clickable with sort direction arrows. Replace `@for` iteration source with `sortedFilteredPositions`.

- **Complexity**: Medium
- **Risk Factors**: Maintaining existing row-level loading and close button functionality while adding sortable headers
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts` — add `MatIconModule` import
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html` — modification
- **Success**:
  - Column headers for Asset, Size, Unrealised PnL, Entry Price, Liquidation Price are clickable
  - Clicking a header calls `onSort(column)` and shows arrow_upward/arrow_downward icon
  - Non-sortable columns (Leverage, Actions) have no click handler or icon
  - `@for` iterates over `sortedFilteredPositions` instead of `positions`
  - All existing row rendering, loading states, and close button functionality preserved
- **Dependencies**: Task 2.1

#### Implementation Details

```html
<!-- positions-table.component.html — modification -->
<!-- Replace static <th> headers with sortable versions. Example for one column: -->

<th class="positions-table__header positions-table__header--sortable"
    (click)="onSort('unrealisedPnl')">
  Unrealised PnL
  @if (sortColumn === 'unrealisedPnl' && sortDirection) {
    <mat-icon class="positions-table__sort-icon">
      {{ sortDirection === 'asc' ? 'arrow_upward' : 'arrow_downward' }}
    </mat-icon>
  }
</th>

<!-- Change @for to iterate sortedFilteredPositions: -->
<!-- @for (position of sortedFilteredPositions; track position.asset + position.side) { -->
```

Add `MatIconModule` to the component imports array in `positions-table.component.ts`.

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html` — existing table header and `@for` loop
- `frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.html` — header row structure pattern

### Task 2.3: Add filter input with result count and clear button {#task-23-add-filter-input}

Add a header bar above the positions table with a search input field, result count, and clear button.

- **Complexity**: Medium
- **Risk Factors**: Integration with sort getter, empty state message, maintaining layout consistency with orders table header
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts` — add Material imports
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html` — add filter bar and empty state
- **Success**:
  - Filter input above the table uses `MatFormField` + `MatInput`
  - Real-time filtering on `(input)` event updates `filterText`
  - "X results" count shown when filter is active
  - `×` clear button resets filter
  - "No positions matching 'X'" empty state shown when filter yields no results
  - Filter works in combination with sort
- **Dependencies**: Task 2.1

#### Implementation Details

```typescript
// positions-table.component.ts — modification
// Add to imports array:
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatButtonModule } from "@angular/material/button"; // may already be imported

// Add onFilterChange and clearFilter methods:
public onFilterChange(event: Event): void {
  this.filterText = (event.target as HTMLInputElement).value;
}

public clearFilter(): void {
  this.filterText = "";
}

public get isFiltered(): boolean {
  return this.filterText.length > 0;
}

public get filteredCount(): number {
  return this.sortedFilteredPositions.length;
}
```

```html
<!-- positions-table.component.html — modification -->
<!-- Add filter bar above the table, inside the component wrapper: -->

<div class="positions-table__toolbar">
  <mat-form-field class="positions-table__filter" appearance="outline" subscriptSizing="dynamic">
    <mat-label>Filter by asset</mat-label>
    <input matInput
           [value]="filterText"
           (input)="onFilterChange($event)"
           placeholder="e.g. BTC" />
    @if (isFiltered) {
      <button matSuffix mat-icon-button (click)="clearFilter()">
        <mat-icon>close</mat-icon>
      </button>
    }
  </mat-form-field>
  @if (isFiltered) {
    <span class="positions-table__filter-count">{{ filteredCount }} result{{ filteredCount !== 1 ? 's' : '' }}</span>
  }
</div>

<!-- After the table, add empty state for filter: -->
@if (isFiltered && filteredCount === 0) {
  <div class="positions-table__empty-filter">
    No positions matching '{{ filterText }}'
  </div>
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.html` — header/toolbar layout pattern above table
- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html` — existing empty state rendering pattern

### Task 2.4: Add SCSS for sort indicators, filter bar, and empty state {#task-24-add-scss-styles}

Add BEM-style SCSS for the new sort, filter, and empty state elements.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.scss` — modification
- **Success**:
  - Sort icon is inline with header text, sized appropriately
  - Sortable headers have `cursor: pointer` and hover effect
  - Filter bar is properly spaced above the table
  - Filter count text uses `--colour-muted`
  - Empty filter state is styled consistently with existing empty states
- **Dependencies**: Tasks 2.2, 2.3

#### Implementation Details

```scss
// positions-table.component.scss — modification (append these rules)

.positions-table {
  &__toolbar {
    display: flex;
    align-items: center;
    gap: 12px;
    margin-bottom: 12px;
  }

  &__filter {
    max-width: 240px;
  }

  &__filter-count {
    font-size: 0.85rem;
    color: var(--colour-muted);
  }

  &__header--sortable {
    cursor: pointer;
    user-select: none;

    &:hover {
      color: var(--colour-label);
    }
  }

  &__sort-icon {
    font-size: 16px;
    width: 16px;
    height: 16px;
    vertical-align: middle;
    margin-left: 4px;
  }

  &__empty-filter {
    text-align: center;
    padding: 24px;
    color: var(--colour-muted);
    font-style: italic;
  }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.scss` — existing BEM class structure (`.positions-table__*`)
- `frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.scss` — toolbar/header styling pattern

### Task 2.5: Write unit tests for sorting and filtering {#task-25-write-unit-tests}

Create unit tests for the new sort and filter functionality in `PositionsTableComponent`.

- **Complexity**: Medium
- **Risk Factors**: Need mock position data; testing computed getter behavior with multiple states
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.spec.ts` — new file
- **Success**:
  - Test: default state — `sortedFilteredPositions` returns positions in original order
  - Test: sort by PnL descending — first click sorts descending
  - Test: sort by PnL ascending — second click sorts ascending
  - Test: sort removed — third click returns original order
  - Test: sort by asset — alphabetical comparison
  - Test: sort by different column — resets to descending on new column
  - Test: filter by asset — case-insensitive substring match
  - Test: filter + sort combined — filter applied first, then sort
  - Test: filter clear — `clearFilter()` resets `filterText` and shows all positions
  - Test: `isFiltered` returns true when `filterText` is non-empty
  - Test: empty filter result — `filteredCount` returns 0
  - All tests pass
- **Dependencies**: Tasks 2.1–2.3

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.spec.ts — new file
import { ComponentFixture, TestBed } from "@angular/core/testing";
import { PositionsTableComponent } from "./positions-table.component";
import { NoopAnimationsModule } from "@angular/platform-browser/animations";
import { Position } from "../../../core/models/position.model";

const mockPositions: Position[] = [
  { asset: "BTC", side: "Long", size: 0.001, entryPrice: 50000, markPrice: 51000, unrealisedPnl: 64.13, unrealisedPnlPercent: 12.8, liquidationPrice: 40000, leverage: 10, marginMode: "cross" },
  { asset: "ETH", side: "Short", size: 0.5, entryPrice: 3000, markPrice: 3050, unrealisedPnl: -22.19, unrealisedPnlPercent: -1.5, liquidationPrice: 3500, leverage: 5, marginMode: "cross" },
  { asset: "SUI", side: "Long", size: 100, entryPrice: 1.5, markPrice: 1.52, unrealisedPnl: 2.15, unrealisedPnlPercent: 1.4, liquidationPrice: 1.0, leverage: 3, marginMode: "cross" },
];

describe("PositionsTableComponent", () => {
  let component: PositionsTableComponent;
  let fixture: ComponentFixture<PositionsTableComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PositionsTableComponent, NoopAnimationsModule],
    }).compileComponents();

    fixture = TestBed.createComponent(PositionsTableComponent);
    component = fixture.componentInstance;
    component.positions = [...mockPositions];
    fixture.detectChanges();
  });

  describe("sorting", () => {
    it("should return positions in original order by default", () => {
      expect(component.sortedFilteredPositions.map(p => p.asset))
        .toEqual(["BTC", "ETH", "SUI"]);
    });

    it("should sort by PnL descending on first click", () => {
      component.onSort("unrealisedPnl");
      expect(component.sortedFilteredPositions.map(p => p.asset))
        .toEqual(["BTC", "SUI", "ETH"]);
    });

    it("should sort by PnL ascending on second click", () => {
      component.onSort("unrealisedPnl");
      component.onSort("unrealisedPnl");
      expect(component.sortedFilteredPositions.map(p => p.asset))
        .toEqual(["ETH", "SUI", "BTC"]);
    });

    it("should remove sort on third click", () => {
      component.onSort("unrealisedPnl");
      component.onSort("unrealisedPnl");
      component.onSort("unrealisedPnl");
      expect(component.sortColumn).toBeNull();
      expect(component.sortDirection).toBeNull();
      expect(component.sortedFilteredPositions.map(p => p.asset))
        .toEqual(["BTC", "ETH", "SUI"]);
    });

    it("should reset to descending when switching columns", () => {
      component.onSort("unrealisedPnl");
      component.onSort("unrealisedPnl"); // ascending
      component.onSort("asset"); // new column → descending
      expect(component.sortDirection).toBe("desc");
    });

    it("should sort asset alphabetically", () => {
      component.onSort("asset");
      // desc alphabetical
      expect(component.sortedFilteredPositions.map(p => p.asset))
        .toEqual(["SUI", "ETH", "BTC"]);
    });
  });

  describe("filtering", () => {
    it("should filter positions by asset name (case-insensitive)", () => {
      component.filterText = "btc";
      expect(component.sortedFilteredPositions.length).toBe(1);
      expect(component.sortedFilteredPositions[0].asset).toBe("BTC");
    });

    it("should show all positions when filter is empty", () => {
      component.filterText = "";
      expect(component.sortedFilteredPositions.length).toBe(3);
    });

    it("should return 0 results for non-matching filter", () => {
      component.filterText = "XYZ";
      expect(component.filteredCount).toBe(0);
    });

    it("should clear filter", () => {
      component.filterText = "BTC";
      component.clearFilter();
      expect(component.filterText).toBe("");
      expect(component.sortedFilteredPositions.length).toBe(3);
    });

    it("should apply filter and sort together", () => {
      component.filterText = "s"; // matches SUI
      component.onSort("unrealisedPnl");
      expect(component.sortedFilteredPositions.length).toBe(1);
      expect(component.sortedFilteredPositions[0].asset).toBe("SUI");
    });

    it("should report isFiltered correctly", () => {
      expect(component.isFiltered).toBeFalse();
      component.filterText = "E";
      expect(component.isFiltered).toBeTrue();
    });
  });
});
```

##### Pattern References

- `frontend/trading-ui/src/app/app.component.spec.ts` — TestBed standalone component test pattern
- `frontend/trading-ui/src/app/features/connection/status-card.component.spec.ts` — minimal component test

### Task 2.6: Run frontend build and lint {#task-26-run-frontend-build-and-lint}

Verify no build or lint errors were introduced.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification step)
- **Success**:
  - `npx ng build` completes without errors
  - `npx ng lint` completes without errors
  - All existing and new tests pass (`npx ng test --watch=false`)
- **Dependencies**: Tasks 2.1–2.5

## Phase Success Criteria

- All 5 sortable columns (Asset, Size, Unrealised PnL, Entry Price, Liquidation Price) are clickable with sort direction arrows
- Sort cycles correctly: desc → asc → none on repeated clicks of the same column
- Switching to a new column starts at descending
- Filter input instantly filters positions by asset name (case-insensitive substring)
- Result count shown when filter is active
- Clear button resets the filter
- "No positions matching 'X'" empty state shown when no results
- Sorting and filtering work together (filter first, then sort)
- All unit tests pass; frontend builds and lints cleanly
