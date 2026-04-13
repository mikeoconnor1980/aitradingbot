# Angular Development Instructions

This document captures the implemented Angular frontend conventions in `frontend/trading-ui/`. It complements the repository-wide Angular instruction file by recording codebase-specific architecture, providers, services, guards, and known deviations from earlier guidance.

## Principles

- Angular 19 with standalone components throughout
- strict TypeScript-first component and service design
- all API calls go through injectable services; feature components do not own raw endpoint URLs
- routes are lazy and component-based through `loadComponent`
- Material components are imported per component, not through a blanket module
- current-state note: authentication tokens are intentionally stored in `localStorage` by `AuthService`; this is an implemented tradeoff, not an aspirational rule violation

## Application Structure

```
src/app/
├── app.component.*
├── app.config.ts
├── app.routes.ts
├── core/
│   ├── components/
│   ├── guards/
│   ├── interceptors/
│   ├── models/
│   ├── pipes/
│   ├── services/
│   └── utils/
└── features/
    ├── auth/
    ├── dashboard/
    ├── market-data/
    ├── strategy-builder/
    ├── backtesting/
    ├── optimizer/
    ├── agents/
    └── ...
```

Important `core/` contents:

| Folder | Purpose |
|---|---|
| `components/` | Shared shell UI such as `HelpPanelComponent`, `SidebarNavComponent`, and `MobileNavComponent` |
| `guards/` | Route guards for auth, subscription gating, and mobile redirection |
| `interceptors/` | Auth header/refresh handling plus global error notification policy |
| `pipes/` | Reusable pipes including `DurationPipe` and `HelpMarkdownPipe` |
| `services/` | Root-scoped integration, state, and orchestration services |
| `utils/` | Shared formatting and error helper functions |

## Root Providers

`app.config.ts` is the single root provider composition point.

| Provider | Purpose |
|---|---|
| `provideZoneChangeDetection({ eventCoalescing: true })` | Reduces change-detection noise |
| `provideHttpClient(withInterceptors([authInterceptor, errorInterceptor]))` | Registers both HTTP interceptors globally |
| `provideAnimationsAsync()` | Async animation provider for Angular Material |
| `MAT_DATE_LOCALE = "en-GB"` | UK date locale |
| `DateAdapter = NativeDateAdapter` | Native date adapter for Material date controls |
| `MAT_DATE_FORMATS = APP_DATE_FORMATS` | Shared date parse/display config |
| `provideRouter(routes)` | Root routing configuration |

## Routing and Guards

All feature routes live in `app.routes.ts` and are lazy loaded with `loadComponent()`.

### Guard Set

| Guard | Purpose |
|---|---|
| `authGuard` | Allows access only when `AuthService.isAuthenticated` is true; otherwise redirects to `/login` |
| `subscriptionGuard` | Checks `SubscriptionService.currentStatus`, falls back to `/subscriptions/status`, and redirects inactive users to `/dashboard?needsSubscription=true` |
| `mobileRedirectGuard` | Blocks desktop-only pages on small screens and shows a snackbar before redirecting to `/dashboard` |
| `unsavedChangesGuard` | `CanDeactivateFn` for the strategy builder |

### Route Pattern

```typescript
{
  path: "strategies/wizard",
  loadComponent: () => import("./features/strategy-builder/wizard/strategy-wizard-page.component").then((m) => m.StrategyWizardPageComponent),
  canActivate: [authGuard, subscriptionGuard, mobileRedirectGuard]
}
```

Default route and wildcard both redirect to `dashboard`.

## Service Patterns

### Polling with Observable State

Long-lived polling services typically use `BehaviorSubject` state plus a merged timer/manual refresh trigger.

```typescript
merge(timer(0, 10_000), this._refresh$).pipe(
  switchMap(() => this._http.get<T>(url).pipe(catchError(...))),
  takeUntilDestroyed(this._destroyRef)
).subscribe((value) => this._state$.next(value));
```

Reference pattern: `core/services/health.service.ts`.

### Parallel Polling from Components

Dashboard-style pages use `forkJoin` inside a timed `switchMap` so multiple endpoints refresh together while allowing per-request fallbacks.

Reference pattern: `features/dashboard/dashboard.component.ts`.

### DI Pattern

Components and services use `inject()` rather than constructor injection unless Angular APIs require otherwise.

## Interceptors

| Interceptor | Behaviour |
|---|---|
| `authInterceptor` | Adds `Authorization: Bearer <token>` except on login/register/refresh endpoints; on `401`, attempts refresh and retries once; logs out if refresh fails |
| `errorInterceptor` | Converts transport and API failures into snackbar notifications; honours `SKIP_ERROR_NOTIFICATION` `HttpContext` token |

`SKIP_ERROR_NOTIFICATION` is widely used for flows that want local error handling, such as strategy interpretation, validation, and guard-based subscription checks.

## Core Services to Know

| Service | Purpose |
|---|---|
| `AuthService` | Login, register, Google sign-in, refresh, logout, and persisted auth state |
| `GoogleAuthService` | Wraps Google Identity Services button initialisation/rendering |
| `NotificationService` | Snackbar wrapper with `success`, `error`, `warning`, and `info` helpers |
| `ResponsiveDialogService` | Opens bottom-anchored full-width dialogs on mobile |
| `LayoutService` | Exposes `isMobile` as an Angular `Signal<boolean>` via `BreakpointObserver` |
| `StrategyDraftService` | Saves strategy-wizard drafts to `sessionStorage` under `strategy_draft` |
| `SignalRService` | Realtime price/fill and connection state orchestration |
| `HelpService` | Toggles the contextual help drawer |

## Storage Model

`AuthService` persists the following keys in `localStorage`:

| Key | Purpose |
|---|---|
| `auth_token` | Access token |
| `auth_refresh_token` | Refresh token |
| `auth_user` | Serialised authenticated user |

This is a deliberate current-state choice and should be treated as part of the implemented architecture when planning frontend changes.

## Component Architecture Patterns

| Pattern | Current Practice |
|---|---|
| Page components | Compose feature cards/tables and orchestrate services |
| Shared shell components | Live under `core/components/` |
| Feature-specific components | Live under each feature folder and are imported directly by the parent page |
| Dialogs | Angular Material dialogs; use `ResponsiveDialogService` when the same dialog must work on mobile |
| Charts | Lightweight Charts only |
| Signals | Used selectively for local UI state such as auth-page submit flags and layout state |

Examples:

- `DashboardComponent` composes tables, summary cards, and dialog flows.
- `StrategyBuilderPageComponent` coordinates interpretation, validation, revision history, and AI review.
- `AppComponent` owns shell-level connection/auth/help state.

## Angular Material Theme

Angular Material theming is defined in `src/styles.scss`.

Implemented theme settings:

| Setting | Value |
|---|---|
| Theme type | dark |
| Primary palette | `mat.$cyan-palette` |
| Tertiary palette | `mat.$orange-palette` |
| Background style | radial dark teal gradient |

Earlier notes calling the primary theme green are incorrect. The current UI palette is cyan/teal-forward, centred around `#79cfc3`.

## CSS Tokens

`styles.scss` defines the shared colour tokens on `body`. These are the current source-of-truth variables used across the UI.

| Token | Purpose |
|---|---|
| `--colour-profit` | Positive PnL and profitable actions |
| `--colour-loss` | Negative PnL and loss states |
| `--colour-warning` | Warning foreground |
| `--colour-warning-elevated` | Stronger warning accent |
| `--colour-label` | Labels and highlighted metadata text |
| `--colour-muted` | Secondary and muted text |
| `--colour-border-subtle` | Standard subtle borders |
| `--colour-border-light` | Stronger border variant |
| `--colour-surface-dark` | Primary dark surface |
| `--colour-surface-alt` | Alternate elevated surface |
| `--colour-surface-soft` | Very low-contrast surface fill |
| `--colour-surface-strong` | Higher-contrast dark surface |
| `--colour-accent` | Primary accent colour |
| `--colour-accent-strong` | Strong accent highlight |
| `--colour-accent-soft` | Soft accent background |
| `--colour-accent-text` | Text intended for accent-filled surfaces |
| `--colour-info` | Informational accent |
| `--colour-info-soft` | Informational soft background |
| `--colour-profit-soft` | Positive soft background |
| `--colour-loss-soft` | Negative soft background |
| `--colour-warning-soft` | Warning soft background |
| `--colour-error-bg` | Snackbar and error surface background |
| `--colour-error-text` | Error foreground text |
| `--colour-text-primary` | Main body text |
| `--colour-on-profit` | Text on profit-coloured surfaces |
| `--colour-on-loss` | Text on loss-coloured surfaces |

Snackbar severity classes in the same file map these tokens to Material snackbar CSS variables:

- `snackbar--error`
- `snackbar--success`
- `snackbar--warning`
- `snackbar--info`

## Third-Party Libraries

| Library | Purpose |
|---|---|
| `@angular/material` | Main component library |
| `lightweight-charts` | Market-data and equity charts |
| `marked` | Markdown rendering for AI review and help content |

`marked` is used both in strategy review UI (`AiReviewCardComponent`, `AiReviewModalComponent`) and help rendering (`HelpMarkdownPipe`).

## Standalone Imports and `CommonModule`

The intended standalone pattern is to import only the exact pipes/directives/components needed. In practice, there are still components that import `CommonModule` directly.

Notable current examples:

- `features/dashboard/market-context-card/`
- `features/strategy-builder/components/nl-input-card/`
- `features/strategy-builder/components/assumptions-panel/`
- `features/dashboard/grid-state/`
- `features/market-data/market-data.component.ts`

Additional components also still import `CommonModule`, so the earlier "never import `CommonModule`" rule should be interpreted as a direction rather than a hard description of the current codebase.

## Deployment and Local Development Config

| File | Purpose |
|---|---|
| `proxy.conf.json` | Local Angular dev proxy for API calls |
| `angular.json` asset entry for `staticwebapp.config.json` | Indicates the app is prepared to ship Azure Static Web Apps routing config when that file is present |

## Future Recommendations

- Standardise or remove undefined fallback token names such as `--colour-primary`, `--colour-success`, and `--colour-text` that still appear in some feature styles.
- Finish the move away from `CommonModule` imports where specific standalone imports are sufficient.
- Revisit `localStorage` token persistence if a more secure browser auth model is introduced later.
- Consolidate repeated polling patterns into more reusable view-model services where pages are becoming orchestration-heavy.