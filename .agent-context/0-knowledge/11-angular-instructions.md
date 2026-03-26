# Angular Development Instructions

## Principles

- Angular 19 with standalone components (`standalone: true`) throughout
- Strict TypeScript typing
- All API calls go through injectable services — never call `HttpClient` from components
- Never store sensitive data in browser storage

## Folder Structure

```
src/app/
├── core/
│   ├── models/     # TypeScript interfaces matching API response shapes
│   └── services/   # Root-scoped services (providedIn: 'root')
├── features/       # Feature components, one folder per domain area
└── app.config.ts   # Root providers configured here (provideHttpClient, etc.)
```

## Service Pattern — Polling with Observable State

Services that poll an API use `BehaviorSubject` for state and `merge(timer, refresh$)` to combine timed polling with manual refresh triggers:

```typescript
merge(timer(0, 10_000), this._refresh$).pipe(
  switchMap(() => this._http.get<T>(url).pipe(catchError(...))),
  takeUntilDestroyed(this._destroyRef)
).subscribe({ next: (r) => this._state$.next(r) });
```

Reference: `frontend/trading-ui/src/app/core/services/health.service.ts`

## Component Pattern

Components use `inject()` for DI and consume service observables via `AsyncPipe`:

```typescript
@Component({ standalone: true, imports: [AsyncPipe], ... })
export class MyComponent {
  private readonly _service = inject(MyService);
  public readonly data$ = this._service.data$;
}
```

## Multi-Endpoint Polling Pattern

When a component polls multiple endpoints in parallel, use `forkJoin` inside a `switchMap`:

```typescript
interval(POLL_INTERVAL_MS).pipe(
  startWith(0),
  switchMap(() => forkJoin({
    summary: this._api.getAccountSummary().pipe(catchError(() => of(null))),
    positions: this._api.getPositions().pipe(catchError(() => of([]))),
  })),
  takeUntilDestroyed(this._destroyRef)
).subscribe(({ summary, positions }) => { ... });
```

Use `catchError` per-request inside `forkJoin` so a partial failure returns partial data rather than killing the stream.

Reference: `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts`

## Routing

Routes use lazy `loadComponent` for all feature components. No eager loading of feature modules.

```typescript
{ path: 'dashboard', loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent) }
```

Default route redirects to `dashboard`. Wildcard also redirects to `dashboard`.

## CSS Theming Tokens

Shared colour tokens are defined as CSS custom properties on `body` in `styles.scss` and referenced via `var(--colour-*)` in component stylesheets.

| Token | Purpose |
|---|---|
| `--colour-profit` | Positive PnL / green values |
| `--colour-loss` | Negative PnL / red values |
| `--colour-label` | Field labels and secondary text |
| `--colour-muted` | Placeholder/disabled text |
| `--colour-border-subtle` | Subtle dividers |
| `--colour-surface-dark` | Dark panel backgrounds |
| `--colour-error-bg` / `--colour-error-text` | Error state surfaces |

Always use tokens from this list in component styles; do not hardcode colour values in component `.scss` files.

## Angular Material

Angular Material (dark theme, green primary) is configured in `styles.scss`. Import specific Material modules per component — never import a blanket `MaterialModule`. Do not mix Material components with non-Material components without token alignment.

## Standalone Component Imports

Import only what a component uses. Prefer specific pipes (`DecimalPipe`, `CurrencyPipe`) over `CommonModule` for standalone components; `CommonModule` is a large NgModule barrel and is unnecessary in the standalone model.

## Charts

Use TradingView Lightweight Charts only.

## Row-Level Loading and Optimistic UI Pattern

When a table row has an action (cancel, modify, close), the parent/child architecture is:

**Child component** (`PositionsTableComponent`, `OrdersTableComponent`):
- Holds `loadingKeys = new Set<string>()` — keyed by a composite string identity (e.g., `asset + side`)
- Exposes `isLoading(item)`, `setLoading(key, bool)`, and a `@Output()` event emitter for the action
- Disables the button and shows a spinner while the key is in the set

**Parent component** (`DashboardComponent`):
- Uses `@ViewChild` to access the child's loading state and call `setLoading()`
- Tracks in-flight keys in a `_pendingXKeys = new Set<string>()` field
- Applies optimistic removal: removes the item from the list immediately, restores it on error
- Guards the polling update: skips overwriting items whose keys are still in `_pendingXKeys`

Reference implementations:
- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts` (Close Position)
- `frontend/trading-ui/src/app/features/dashboard/orders-table/orders-table.component.ts` (Cancel, Modify)
- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` (orchestrates both)