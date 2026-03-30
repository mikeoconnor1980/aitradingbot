<!-- markdownlint-disable-file -->

# Task Details: Consolidate Connection Indicator into Connection Pill

## Phase 1: Consolidate Header Connection Elements

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — Standalone components, `inject()` DI, double quotes, SCSS styling, explicit return types
- `.agent-context/0-knowledge/02-hyperliquid-integration.md` — Connection state model (Connected/Reconnecting/Disconnected)

## Design References

N/A — no external libraries or APIs introduced.

### Task 1.1: Remove "Connection" nav link from header template {#task-11-remove-connection-nav-link}

Remove the `<a routerLink="/connection">Connection</a>` element from the header navigation bar.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/app.component.html` - Remove the Connection nav link
- **Success**:
  - Header nav contains only Dashboard, Market Data, Order Entry, Backtesting links
  - No "Connection" text in the nav area
- **Dependencies**: None

#### Implementation Details

```html
<!-- frontend/trading-ui/src/app/app.component.html — modification -->
<!-- Remove this line from the <nav> element: -->
<!--   <a routerLink="/connection" routerLinkActive="app-shell__link--active" class="app-shell__link">Connection</a> -->

<!-- The nav should contain only: -->
<nav class="app-shell__nav" aria-label="Primary navigation">
  <a routerLink="/dashboard" routerLinkActive="app-shell__link--active" [routerLinkActiveOptions]="{ exact: true }" class="app-shell__link">Dashboard</a>
  <a routerLink="/market-data" routerLinkActive="app-shell__link--active" class="app-shell__link">Market Data</a>
  <a routerLink="/order-entry" routerLinkActive="app-shell__link--active" class="app-shell__link">Order Entry</a>
  <a routerLink="/backtesting" routerLinkActive="app-shell__link--active" class="app-shell__link">Backtesting</a>
</nav>
```

##### Pattern References

- `frontend/trading-ui/src/app/app.component.html` — existing header template structure

---

### Task 1.2: Make status pill clickable with navigation to /connection {#task-12-make-status-pill-clickable}

Wrap the existing `app-shell__status` div in an `<a>` tag with `routerLink="/connection"` so clicking the pill navigates to the connection page. Alternatively, change the `<div>` to an `<a>` element.

- **Complexity**: Low
- **Risk Factors**: Ensure `aria-label` and `ngClass` are preserved; ensure `RouterLink` is imported in standalone component
- **Files**:
  - `frontend/trading-ui/src/app/app.component.html` - Change pill from `<div>` to `<a>` with `routerLink`
  - `frontend/trading-ui/src/app/app.component.ts` - Ensure `RouterLink` is in `imports` array (may already be there via `RouterModule`)
- **Success**:
  - Clicking the status pill navigates to `/connection`
  - Status pill still displays correct status text and colour coding
  - Accessibility: `aria-label` preserved, element is keyboard-navigable as a link
- **Dependencies**: Task 1.1

#### Implementation Details

```html
<!-- frontend/trading-ui/src/app/app.component.html — modification -->
<!-- Change the status pill from <div> to <a> with routerLink -->
<a class="app-shell__status" [ngClass]="statusClass"
   routerLink="/connection"
   [attr.aria-label]="'Connection status: ' + connectionStatus.status + '. Click to view details.'">
  <span class="app-shell__status-dot"></span>
  <span class="app-shell__status-label">{{ connectionStatus.status }}</span>
</a>
```

```typescript
// frontend/trading-ui/src/app/app.component.ts — modification
// Ensure RouterLink is in imports array (check if already present via RouterModule or RouterLink)
// ... existing code ...
imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive, /* ...existing imports... */],
// ... existing code ...
```

##### Pattern References

- `frontend/trading-ui/src/app/app.component.html` — existing pill markup
- `frontend/trading-ui/src/app/app.component.ts` — existing component imports

---

### Task 1.3: Add hover/cursor styles for clickable pill {#task-13-add-hover-cursor-styles}

Add cursor and hover styles so the pill looks interactive.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/app.component.scss` - Add hover/cursor styles to `.app-shell__status`
- **Success**:
  - Pill shows pointer cursor on hover
  - Subtle hover effect (e.g. slight brightness or opacity change) provides affordance
  - No text-decoration underline (since it's an `<a>` tag now)
- **Dependencies**: Task 1.2

#### Implementation Details

```scss
// frontend/trading-ui/src/app/app.component.scss — modification
// Add to existing .app-shell__status block:
.app-shell__status {
  // ... existing styles ...
  cursor: pointer;
  text-decoration: none;
  color: inherit;
  transition: opacity 0.2s ease;

  &:hover {
    opacity: 0.85;
  }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/app.component.scss` — existing pill styling with `.status--connected/reconnecting/disconnected` modifiers

---

### Task 1.4: Update AppComponent tests {#task-14-update-appcomponent-tests}

Update the existing `app.component.spec.ts` to:
1. Remove any assertion about a "Connection" nav link (if one exists)
2. Add a test that the status pill is rendered as an `<a>` element with `routerLink="/connection"`
3. Verify the nav only contains 4 links (Dashboard, Market Data, Order Entry, Backtesting)

- **Complexity**: Low
- **Risk Factors**: Existing test mocks `HealthService` but `AppComponent` uses `SignalRService` — may need to fix the mock setup
- **Files**:
  - `frontend/trading-ui/src/app/app.component.spec.ts` - Update tests
- **Success**:
  - All tests pass
  - Nav link count test asserts 4 links (no "Connection")
  - Status pill renders as an `<a>` element
- **Dependencies**: Tasks 1.1–1.3

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/app.component.spec.ts — modification
// Replace the HealthService mock with a SignalRService mock (the component depends on SignalRService, not HealthService):

const signalRServiceMock: Partial<SignalRService> = {
  connectionStatus$: of({ source: "SignalR", status: "Connected", detail: null, retryCount: 0 })
};

// Update providers in TestBed:
// Replace: { provide: HealthService, useValue: healthServiceMock }
// With:    { provide: SignalRService, useValue: signalRServiceMock }

// Add these tests to the existing describe block:

it("should not have a Connection nav link", () => {
  fixture.detectChanges();
  const navLinks = fixture.nativeElement.querySelectorAll(".app-shell__link");
  const linkTexts = Array.from(navLinks).map((el: any) => el.textContent.trim());
  expect(linkTexts).not.toContain("Connection");
});

it("should have exactly 4 nav links", () => {
  fixture.detectChanges();
  const navLinks = fixture.nativeElement.querySelectorAll(".app-shell__link");
  expect(navLinks.length).toBe(4);
});

it("should render status pill as a link to /connection", () => {
  fixture.detectChanges();
  const pill = fixture.nativeElement.querySelector(".app-shell__status");
  expect(pill.tagName).toBe("A");
  expect(pill.getAttribute("href")).toBe("/connection");
});
```

##### Pattern References

- `frontend/trading-ui/src/app/app.component.spec.ts` — existing test structure using `TestBed.configureTestingModule`, `provideRouter([])`, standalone component imports

---

### Task 1.5: Run frontend build and lint {#task-15-run-frontend-build-and-lint}

Run the Angular build and lint to verify no compilation or lint errors.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification only)
- **Success**:
  - `ng build` completes without errors
  - `npm run lint` completes without errors
  - All existing `ng test` specs pass
- **Dependencies**: Tasks 1.1–1.4

## Phase Success Criteria

- Header shows 4 nav links (no "Connection" link)
- Status pill is a clickable `<a>` that navigates to `/connection`
- Pill retains green/amber/red colour coding and status text
- `/connection` route and `StatusCardComponent` continue to work unchanged
- Frontend build, lint, and tests all pass
