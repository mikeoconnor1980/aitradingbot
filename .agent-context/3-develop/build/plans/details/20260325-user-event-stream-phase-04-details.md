<!-- markdownlint-disable-file -->

# Task Details: F7 — User Event Stream

## Phase 4: Frontend — Activity Feed & Dashboard Integration

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — standalone components, inject(), takeUntilDestroyed, SCSS with kebab-casing, Flexbox layout
- `.agent-context/0-knowledge/11-angular-instructions.md` — BehaviorSubject for service state, CSS custom properties
- `.agent-context/0-knowledge/07-ui-design.md` — Dashboard layout: chart, positions, orders, signals

## Design References

- Activity feed is a new tab in the existing dashboard `mat-tab-group`
- Feed shows newest events at top, capped at 100 events
- Each event row: timestamp, type badge ("Fill" or "Order Update"), asset, key details
- Dashboard component refactored to consume `AccountStateService` for positions and orders (reactive via SignalR), while retaining polling as fallback
- Positions and orders tables remain pure `@Input()` display components

### Task 4.1: Create ActivityFeedComponent {#task-41-create-activityfeedcomponent}

Create a standalone Angular component that displays the live activity feed. Receives events from `AccountStateService.events$`.

- **Complexity**: Low
- **Risk Factors**: None — straightforward display component
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/activity-feed/activity-feed.component.ts` - new file
  - `frontend/trading-ui/src/app/features/dashboard/activity-feed/activity-feed.component.html` - new file
  - `frontend/trading-ui/src/app/features/dashboard/activity-feed/activity-feed.component.scss` - new file
- **Success**:
  - Component renders a list of user events, newest first
  - Each event shows timestamp, type, asset, and relevant details
  - Empty state message shown when no events exist
  - Styled consistently with existing dashboard tables
- **Dependencies**: Phase 3 (AccountStateService, models)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/activity-feed/activity-feed.component.ts — new file
import { Component, DestroyRef, inject, OnInit } from "@angular/core";
import { CommonModule } from "@angular/common";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { AccountStateService } from "../../../core/services/account-state.service";
import { UserEvent } from "../../../core/models/user-event.model";
import { FillEvent } from "../../../core/models/fill-event.model";
import { OrderUpdate } from "../../../core/models/order-update.model";

@Component({
  selector: "app-activity-feed",
  standalone: true,
  imports: [CommonModule],
  templateUrl: "./activity-feed.component.html",
  styleUrls: ["./activity-feed.component.scss"]
})
export class ActivityFeedComponent implements OnInit {
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _accountState = inject(AccountStateService);

  public events: UserEvent[] = [];

  public ngOnInit(): void {
    this._accountState.events$
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((events: UserEvent[]) => {
        this.events = events;
      });
  }

  public isFill(event: UserEvent): event is UserEvent & { data: FillEvent } {
    return event.type === "Fill";
  }

  public isOrderUpdate(event: UserEvent): event is UserEvent & { data: OrderUpdate } {
    return event.type === "OrderUpdate";
  }

  public getEventDescription(event: UserEvent): string {
    if (this.isFill(event)) {
      const fill = event.data as FillEvent;
      return `${fill.side} ${fill.size} ${fill.asset} @ ${fill.price}`;
    }
    const order = event.data as OrderUpdate;
    return `${order.asset} — ${order.status} (filled: ${order.filledSize}, remaining: ${order.remainingSize})`;
  }
}
```

```html
<!-- frontend/trading-ui/src/app/features/dashboard/activity-feed/activity-feed.component.html — new file -->
<div class="activity-feed">
  @if (events.length === 0) {
    <div class="activity-feed__empty">
      No events yet. Events will appear here as orders fill and update.
    </div>
  } @else {
    <table class="activity-feed__table">
      <thead>
        <tr>
          <th>Time</th>
          <th>Type</th>
          <th>Asset</th>
          <th>Details</th>
        </tr>
      </thead>
      <tbody>
        @for (event of events; track event.timestamp.getTime() + event.type) {
          <tr class="activity-feed__row">
            <td class="activity-feed__time">{{ event.timestamp | date:'HH:mm:ss' }}</td>
            <td>
              <span class="activity-feed__badge"
                    [class.activity-feed__badge--fill]="isFill(event)"
                    [class.activity-feed__badge--order]="isOrderUpdate(event)">
                {{ event.type === 'Fill' ? 'Fill' : 'Order Update' }}
              </span>
            </td>
            <td class="activity-feed__asset">
              @if (isFill(event)) { {{ event.data.asset }} }
              @if (isOrderUpdate(event)) { {{ event.data.asset }} }
            </td>
            <td class="activity-feed__details">{{ getEventDescription(event) }}</td>
          </tr>
        }
      </tbody>
    </table>
  }
</div>
```

```scss
// frontend/trading-ui/src/app/features/dashboard/activity-feed/activity-feed.component.scss — new file
.activity-feed {
  &__empty {
    padding: 2rem;
    text-align: center;
    color: var(--colour-muted);
    font-size: 0.875rem;
  }

  &__table {
    width: 100%;
    border-collapse: collapse;
    font-size: 0.8125rem;

    th {
      text-align: left;
      padding: 0.5rem 0.75rem;
      color: var(--colour-label);
      font-weight: 500;
      border-bottom: 1px solid var(--colour-border-subtle);
    }

    td {
      padding: 0.5rem 0.75rem;
      border-bottom: 1px solid var(--colour-border-subtle);
    }
  }

  &__time {
    color: var(--colour-muted);
    white-space: nowrap;
  }

  &__asset {
    font-weight: 500;
  }

  &__details {
    color: var(--colour-label);
  }

  &__badge {
    display: inline-block;
    padding: 0.125rem 0.5rem;
    border-radius: 4px;
    font-size: 0.75rem;
    font-weight: 500;

    &--fill {
      background-color: rgba(var(--colour-profit-rgb, 46, 204, 113), 0.15);
      color: var(--colour-profit);
    }

    &--order {
      background-color: rgba(var(--colour-surface-alt-rgb, 100, 149, 237), 0.15);
      color: var(--colour-label);
    }
  }

  &__row {
    &:hover {
      background-color: var(--colour-surface-alt);
    }
  }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts` — pure display component pattern with `@for` loop
- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.html` — table layout with BEM classes
- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.scss` — BEM naming and CSS custom properties

---

### Task 4.2: Integrate activity feed and shared state into dashboard {#task-42-integrate-activity-feed-and-shared-state-into-dashboard}

Add the Activity tab to the dashboard's `mat-tab-group` and refactor the dashboard to consume `AccountStateService` for positions and orders, while retaining polling as a fallback that also feeds the shared state.

- **Complexity**: Medium
- **Risk Factors**: Must not break existing polling-based data flow; positions and orders must continue to update even before any SignalR events arrive
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` - modification
  - `frontend/trading-ui/src/app/features/dashboard/dashboard.component.html` - modification
- **Success**:
  - Activity tab appears as third tab in the dashboard
  - Positions and orders populated initially from polling, updated reactively from SignalR
  - Polling writes to `AccountStateService`, which components subscribe to
  - All existing dashboard functionality preserved
- **Dependencies**: Tasks 3.2, 4.1

#### Implementation Details

**Template modification** — add Activity tab and switch to shared state:

```html
<!-- frontend/trading-ui/src/app/features/dashboard/dashboard.component.html — modification -->
<!-- Add inside mat-tab-group, after Orders tab: -->
<mat-tab label="Activity">
  <app-activity-feed></app-activity-feed>
</mat-tab>
```

**Component modification** — inject `AccountStateService`, update polling to write to shared state, subscribe to shared state for display:

```typescript
// frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts — modification

// Add imports:
import { AccountStateService } from "../../core/services/account-state.service";
import { ActivityFeedComponent } from "./activity-feed/activity-feed.component";

// Add to imports array in @Component:
// ActivityFeedComponent

// Add injection:
private readonly _accountState = inject(AccountStateService);

// In the polling subscription, update shared state:
// After fetching positions/orders from REST, push to AccountStateService:
// this._accountState.updatePositions(result.positions);
// this._accountState.updateOrders(result.orders);

// Subscribe to shared state for reactive updates:
// this._accountState.positions$.pipe(takeUntilDestroyed(this._destroyRef)).subscribe(p => this.positions = p);
// this._accountState.orders$.pipe(takeUntilDestroyed(this._destroyRef)).subscribe(o => this.orders = o);
```

The key change: polling results flow through `AccountStateService` (which deduplicates with SignalR-pushed data), and the component subscribes to the service rather than directly holding polled data. This means:
1. On startup, polling populates positions/orders (immediate data)
2. When SignalR events arrive, they update the same shared state
3. Components see a single source of truth

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — existing polling logic with forkJoin
- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.html` — existing mat-tab-group structure

---

### Task 4.3: Run frontend build and lint {#task-43-run-frontend-build-and-lint}

Run the Angular build and lint to verify the complete frontend compiles and conforms to project standards.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: No new files
- **Success**:
  - `npx ng build` succeeds with no errors
  - `npm run lint` passes with no violations
- **Dependencies**: Tasks 4.1, 4.2

## Phase Success Criteria

- Activity feed component renders user events, newest first, with empty state message
- Activity tab appears as third tab in dashboard mat-tab-group
- Dashboard positions and orders update reactively from SignalR events via AccountStateService
- Polling fallback continues to work, feeding data through the same shared state
- All BEM styling consistent with existing dashboard conventions
- Frontend builds and lints cleanly with no errors or warnings
