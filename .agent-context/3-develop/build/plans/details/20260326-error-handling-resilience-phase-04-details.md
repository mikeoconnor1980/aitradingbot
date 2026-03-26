<!-- markdownlint-disable-file -->

# Task Details: F8 — Error Handling & Resilience

## Phase 4: Frontend Error Infrastructure & Component Refactoring

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — Standalone components, injectable services, signals pattern, CSS custom properties for theming
- `.agent-context/0-knowledge/06-project-structure.md` — Angular project structure: `core/services/`, `core/models/`, `features/`
- Global design tokens in `frontend/trading-ui/src/styles.scss` — `--colour-error-bg`, `--colour-error-text`, `--colour-loss`

## Design References

- Angular `HttpInterceptorFn` (functional interceptor) — Angular 17+ pattern using `provideHttpClient(withInterceptors([...]))`
- `MatSnackBar` from `@angular/material ^19.2.19` — Already used in `DashboardComponent` and `OrderEntryComponent`
- `MatSnackBarConfig.panelClass` — Allows applying custom CSS classes for error/success/warning severities

### Task 4.1: Create NotificationService wrapping MatSnackBar {#task-41-create-notificationservice-wrapping-matsnackbar}

Create a centralized `NotificationService` that wraps `MatSnackBar` with typed methods for success, error, warning, and info notifications. This replaces direct `MatSnackBar` usage scattered across components.

- **Complexity**: Medium
- **Risk Factors**: Must match the existing `MatSnackBar` behaviour (duration, action button) so the refactored components behave identically
- **Files**:
  - `frontend/trading-ui/src/app/core/services/notification.service.ts` — New: centralized notification service
  - `frontend/trading-ui/src/styles.scss` — Modify: add snackbar severity CSS classes
- **Success**:
  - `NotificationService` is injectable with `error()`, `success()`, `warning()`, `info()` methods
  - Each severity has distinct visual styling via `panelClass`
  - Error notifications default to 5s duration, success to 3s
  - Service is a singleton (root-provided)
- **Dependencies**: None (frontend-only)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/services/notification.service.ts — new file
import { Injectable } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';

export type NotificationSeverity = 'success' | 'error' | 'warning' | 'info';

@Injectable({ providedIn: 'root' })
export class NotificationService {
  constructor(private readonly _snackBar: MatSnackBar) {}

  success(message: string, duration = 3000): void {
    this._show(message, 'success', duration);
  }

  error(message: string, duration = 5000): void {
    this._show(message, 'error', duration);
  }

  warning(message: string, duration = 4000): void {
    this._show(message, 'warning', duration);
  }

  info(message: string, duration = 3000): void {
    this._show(message, 'info', duration);
  }

  private _show(message: string, severity: NotificationSeverity, duration: number): void {
    this._snackBar.open(message, 'Dismiss', {
      duration,
      panelClass: [`snackbar--${severity}`],
      horizontalPosition: 'right',
      verticalPosition: 'top',
    });
  }
}
```

```scss
// frontend/trading-ui/src/styles.scss — add at end of file
// Snackbar severity styles
.snackbar--error {
  --mdc-snackbar-container-color: var(--colour-error-bg);
  --mdc-snackbar-supporting-text-color: var(--colour-error-text);
  --mat-snack-bar-button-color: var(--colour-error-text);
}

.snackbar--success {
  --mdc-snackbar-container-color: #1b3a2a;
  --mdc-snackbar-supporting-text-color: #4ade80;
  --mat-snack-bar-button-color: #4ade80;
}

.snackbar--warning {
  --mdc-snackbar-container-color: #3a2e1b;
  --mdc-snackbar-supporting-text-color: #fbbf24;
  --mat-snack-bar-button-color: #fbbf24;
}

.snackbar--info {
  --mdc-snackbar-container-color: #1b2e3a;
  --mdc-snackbar-supporting-text-color: #60a5fa;
  --mat-snack-bar-button-color: #60a5fa;
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — Current `MatSnackBar.open()` calls with `"Dismiss"` action and `duration: 3000`
- `frontend/trading-ui/src/styles.scss` — Global design tokens pattern

---

### Task 4.2: Create ErrorDto model and shared error utility {#task-42-create-errordto-model-and-shared-error-utility}

Create an `ErrorDto` model matching the enhanced backend `Envelope` shape and extract the duplicated `_formatErrorPayload` logic into a shared utility function.

- **Complexity**: Low
- **Risk Factors**: None — straightforward extraction of existing code
- **Files**:
  - `frontend/trading-ui/src/app/core/models/error.model.ts` — New: `ErrorDto` interface matching backend `Envelope`
  - `frontend/trading-ui/src/app/core/utils/error-utils.ts` — New: `formatErrorPayload` utility function
- **Success**:
  - `ErrorDto` interface has `errorMessage`, `errorCode`, `correlationId`, `timestamp` fields
  - `formatErrorPayload(error: HttpErrorResponse): string` extracts messages from `Envelope` and fallback shapes
  - Utility is stateless (pure function, no service needed)
- **Dependencies**: Phase 1 (backend Envelope updated)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/models/error.model.ts — new file
export interface ErrorDto {
  errorMessage: string;
  errorCode: string | null;
  correlationId: string;
  timestamp: string;
}
```

```typescript
// frontend/trading-ui/src/app/core/utils/error-utils.ts — new file
import { HttpErrorResponse } from '@angular/common/http';

/**
 * Extracts a human-readable error message from an HttpErrorResponse.
 * Handles the backend Envelope shape (errorMessage), ProblemDetails (detail/title),
 * and plain string error bodies.
 */
export function formatErrorPayload(errorResponse: HttpErrorResponse): string {
  if (typeof errorResponse.error === 'string' && errorResponse.error.length > 0) {
    return errorResponse.error;
  }

  if (errorResponse.error !== null && errorResponse.error !== undefined) {
    if (typeof errorResponse.error === 'object' && errorResponse.error.errorMessage) {
      return String(errorResponse.error.errorMessage);
    }
    if (typeof errorResponse.error === 'object' && errorResponse.error.detail) {
      return String(errorResponse.error.detail);
    }
    if (typeof errorResponse.error === 'object' && errorResponse.error.title) {
      return String(errorResponse.error.title);
    }
    return 'An unexpected error occurred';
  }

  return errorResponse.message || 'Unknown error';
}

/**
 * Extracts the error code from an HttpErrorResponse if it carries an Envelope body.
 */
export function extractErrorCode(errorResponse: HttpErrorResponse): string | null {
  if (
    errorResponse.error !== null &&
    typeof errorResponse.error === 'object' &&
    errorResponse.error.errorCode
  ) {
    return String(errorResponse.error.errorCode);
  }
  return null;
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — Current `_formatErrorPayload` implementation (duplicated)
- `frontend/trading-ui/src/app/features/order-entry/order-entry.component.ts` — Second copy of `_formatErrorPayload`

---

### Task 4.3: Create HTTP error interceptor {#task-43-create-http-error-interceptor}

Create a functional HTTP interceptor that catches error responses and displays them via `NotificationService`. Register it in `app.config.ts`.

- **Complexity**: Medium
- **Risk Factors**: The interceptor should handle errors but still re-throw them so component-level error handling (e.g., error banners, form-level messages) can also react. Must not double-notify — components that handle errors explicitly should use `catchError` before the interceptor's notification is relevant, but since interceptors run first, the pattern is: interceptor shows toast, component optionally handles further.
- **Files**:
  - `frontend/trading-ui/src/app/core/interceptors/error.interceptor.ts` — New: functional HTTP interceptor
  - `frontend/trading-ui/src/app/app.config.ts` — Modify: register interceptor via `provideHttpClient(withInterceptors([...]))`
- **Success**:
  - All HTTP errors (4xx, 5xx) trigger a toast notification automatically
  - Error responses are re-thrown so component-level handlers still work
  - Interceptor uses `NotificationService.error()` for server errors, `.warning()` for client errors
  - Specific error codes (e.g., `rate_limit`, `signing_error`) get tailored messages
- **Dependencies**: Task 4.1 (NotificationService), Task 4.2 (error utils)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/interceptors/error.interceptor.ts — new file
import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { NotificationService } from '../services/notification.service';
import { formatErrorPayload, extractErrorCode } from '../utils/error-utils';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const notifications = inject(NotificationService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      const message = formatErrorPayload(error);
      const errorCode = extractErrorCode(error);

      if (errorCode === 'rate_limit') {
        notifications.warning('Rate limited — please try again later');
      } else if (errorCode === 'signing_error') {
        notifications.error('Signature rejected — check signing configuration');
      } else if (error.status >= 500) {
        notifications.error(message);
      } else if (error.status >= 400) {
        notifications.warning(message);
      } else if (error.status === 0) {
        notifications.error('Cannot reach server — check your connection');
      }

      return throwError(() => error);
    }),
  );
};
```

```typescript
// frontend/trading-ui/src/app/app.config.ts — modification
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { errorInterceptor } from './core/interceptors/error.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideHttpClient(withInterceptors([errorInterceptor])),
    provideAnimationsAsync(),
    provideRouter(routes),
  ],
};
```

##### Pattern References

- `frontend/trading-ui/src/app/app.config.ts` — Current `provideHttpClient()` without interceptors
- `frontend/trading-ui/src/app/core/services/api-rest-client.service.ts` — HTTP wrapper that will benefit from interceptor

---

### Task 4.4: Refactor components to use NotificationService {#task-44-refactor-components-to-use-notificationservice}

Replace direct `MatSnackBar` usage with `NotificationService` in `DashboardComponent` and `OrderEntryComponent`. Remove the duplicated `_formatErrorPayload` methods and replace with the shared utility.

- **Complexity**: Medium
- **Risk Factors**: Must preserve existing behaviour — the interceptor now handles toast display for HTTP errors, so some component-level snackbar calls may become redundant. Review each snackbar call: keep explicit notifications for user actions (e.g., "Order placed successfully"), remove error-path snackbar calls now covered by interceptor.
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — Modify: replace `MatSnackBar` with `NotificationService`, remove `_formatErrorPayload`
  - `frontend/trading-ui/src/app/features/order-entry/order-entry.component.ts` — Modify: replace `MatSnackBar` with `NotificationService`, remove `_formatErrorPayload`
  - `frontend/trading-ui/src/app/features/market-data/market-data.component.ts` — Modify: import `formatErrorPayload` from shared utility (if applicable)
- **Success**:
  - No component imports `MatSnackBar` directly for error display
  - `_formatErrorPayload` method removed from both components
  - Success notifications (e.g., "Order placed", "Order cancelled") use `NotificationService.success()`
  - Manual error snackbar calls removed where the HTTP interceptor covers them
  - Error banner logic in `DashboardComponent` preserved (consecutive failures escalation)
- **Dependencies**: Tasks 4.1, 4.2, 4.3

#### Implementation Details

**DashboardComponent changes:**

```typescript
// frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts — modification
// Replace:
//   private readonly _snackBar = inject(MatSnackBar);
// With:
//   private readonly _notifications = inject(NotificationService);

// Remove the entire _formatErrorPayload method

// Replace success notifications like:
//   this._snackBar.open('Order cancelled successfully', 'Dismiss', { duration: 3000 });
// With:
//   this._notifications.success('Order cancelled successfully');

// Remove error snackbar calls that are now handled by the interceptor:
//   this._snackBar.open('Failed to cancel order: ...', 'Dismiss', { duration: 5000 });
// These are now covered by the HTTP error interceptor

// Keep the error banner escalation logic (consecutive failures) as-is
// but use the shared formatErrorPayload for the banner message
```

**OrderEntryComponent changes:**

```typescript
// frontend/trading-ui/src/app/features/order-entry/order-entry.component.ts — modification
// Replace:
//   private readonly _snackBar = inject(MatSnackBar);
// With:
//   private readonly _notifications = inject(NotificationService);

// Remove the entire _formatErrorPayload method

// Replace success notifications like:
//   this._snackBar.open('Order placed successfully', 'Dismiss', { duration: 3000 });
// With:
//   this._notifications.success('Order placed successfully');

// Remove error snackbar calls covered by interceptor
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — Current `MatSnackBar` and `_formatErrorPayload` usage
- `frontend/trading-ui/src/app/features/order-entry/order-entry.component.ts` — Current duplicate `_formatErrorPayload` usage

---

### Task 4.5: Fix inconsistent error styling and run build/lint {#task-45-fix-inconsistent-error-styling-and-run-buildlint}

Fix the `MarketDataComponent` error styling to use global design tokens instead of local SCSS variables. Verify the entire frontend builds and lints cleanly.

- **Complexity**: Low
- **Risk Factors**: Minimal — CSS-only change
- **Files**:
  - `frontend/trading-ui/src/app/features/market-data/market-data.component.scss` — Modify: replace local SCSS vars with global CSS custom properties
- **Success**:
  - `MarketDataComponent` error styling uses `var(--colour-error-bg)`, `var(--colour-error-text)` instead of local variables
  - `npx ng build --no-progress` succeeds with no errors
  - `npx ng lint` passes with no errors
  - All error displays are visually consistent across the application
- **Dependencies**: Task 4.4

#### Implementation Details

```scss
// frontend/trading-ui/src/app/features/market-data/market-data.component.scss — modification
// Replace:
//   $color-error-bg: #3a1e1e;
//   $color-error-border: #7f1d1d;
//   $color-error-text: #f87171;
// 
// And references like:
//   background-color: $color-error-bg;
//   border: 1px solid $color-error-border;
//   color: $color-error-text;

// With:
//   background-color: var(--colour-error-bg);
//   border: 1px solid var(--colour-error-border, #7f1d1d);
//   color: var(--colour-error-text);
```

After making all changes, run:
```bash
cd frontend/trading-ui
npx ng build --no-progress
npx ng lint
```

##### Pattern References

- `frontend/trading-ui/src/styles.scss` — Global design tokens: `--colour-error-bg`, `--colour-error-text`
- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.scss` — Correct usage of global CSS custom properties

## Phase Success Criteria

- `NotificationService` exists and is injectable with `error()`, `success()`, `warning()`, `info()` methods
- `ErrorDto` model matches backend `Envelope` shape
- `formatErrorPayload` exists as a shared utility function (not duplicated in components)
- HTTP error interceptor catches all error responses and shows toast notifications
- No component directly imports `MatSnackBar` for error display
- `MarketDataComponent` uses global CSS custom properties for error styling
- `npx ng build --no-progress` succeeds
- `npx ng lint` passes
