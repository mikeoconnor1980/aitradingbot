# UI Foundation Baseline

Recorded from `main` at `80965c4` before Milestone 1 changes.

## Repository and frontend

- Angular 19, Angular Material 19, TypeScript 5.7 and SCSS.
- Jasmine/Karma tests; no Playwright, visual-regression or automated accessibility dependency.
- Existing UI uses a dark teal theme with a decorative body radial gradient and translucent/blurred shell surfaces.
- Dashboard stale state reduces opacity for the whole surface.
- Mobile dashboard hides market context and adds custom horizontal swipe handling to the tab group.
- Desktop navigation is separated into base, Pro and Admin sections rather than task intent.

## Styling inventory

- 101 SCSS files.
- Approximately 437 literal colour occurrences.
- 62 `::ng-deep` occurrences.
- 15 `!important` occurrences.
- Multiple radius and typography values.

These are diagnostic indicators, not blanket replacement targets. Milestone 1 migrates the shell and dashboard incrementally.

## Baseline validation

- `npm ci`: blocked in this environment before completion because cached npm registry tarballs repeatedly failed integrity checks and were retried as corrupted.
- `npm run lint`: not reached because dependency installation did not complete.
- `npm run build`: not reached because dependency installation did not complete.
- Baseline screenshots: blocked because the dependency failure prevented a local Angular server from starting; authenticated account/dashboard data also requires the configured backend.

The implementation must retry validation after changes and report any remaining environment blocker without describing unexecuted checks as passing.
