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

## Charts

Use TradingView Lightweight Charts only.