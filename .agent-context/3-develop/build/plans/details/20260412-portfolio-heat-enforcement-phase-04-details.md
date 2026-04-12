<!-- markdownlint-disable-file -->

# Task Details: Portfolio Heat Enforcement

## Phase 4: Frontend Dashboard

## Standards and Knowledge References

- **angular.instructions.md**: Standalone components, `inject()` function (never constructor injection), `@Input`/`@Output`, explicit return types, double quotes for strings, SCSS only, kebab-case CSS class names, `@if`/`@for` template syntax, `takeUntilDestroyed`, DTO names must match C# exactly
- **angular.instructions.md**: Services in `_shared/services/`, DTOs in `_shared/dtos/` with `.dto.ts` suffix, models in `models/`
- CSS custom properties from `styles.scss`: `--colour-profit` (green), `--colour-warning` (amber), `--colour-loss` (red)

## Design References

- **Heat thresholds**: green (< 50% of max), amber (50–80% of max), red (> 80% of max)
- **MarginRatioIndicatorComponent**: Direct template — same `mat-progress-bar` + threshold CSS class + pulse animation pattern
- **Data source**: `GET /api/risk/portfolio-heat` via `HyperliquidApiService`
- **Placement**: New metric row in `AccountSummaryComponent`, inline with existing metrics
- **Update strategy**: Poll alongside existing dashboard refresh (every 2s via `DashboardComponent._refresh$`)

---

### Task 4.1: Create `PortfolioHeatDto` TypeScript interface {#task-41-create-portfolioheatdto-typescript-interface}

Create TypeScript interfaces matching the C# `PortfolioHeatResponse` DTOs.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/core/models/portfolio-heat.model.ts` — New file
- **Success**:
  - `PortfolioHeat` interface with `heatPercent`, `maxHeatPercent`, `equity`, `positions`
  - `PortfolioHeatPosition` interface with `symbol`, `riskUsd`, `riskPercent`
  - Property names use camelCase (matching C# serialization)
- **Dependencies**: Phase 3

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/models/portfolio-heat.model.ts — new file
export interface PortfolioHeat {
  heatPercent: number;
  maxHeatPercent: number;
  equity: number;
  positions: PortfolioHeatPosition[];
}

export interface PortfolioHeatPosition {
  symbol: string;
  riskUsd: number;
  riskPercent: number;
}
```

##### Pattern References

- `frontend/trading-ui/src/app/core/models/account-summary.model.ts` — interface naming and structure
- `frontend/trading-ui/src/app/core/models/position.model.ts` — property naming

---

### Task 4.2: Add `getPortfolioHeat()` to `HyperliquidApiService` {#task-42-add-getportfolioheat-to-hyperliquidapiservice}

Add a new API method to fetch portfolio heat data.

- **Complexity**: Low
- **Risk Factors**: None — follows exact existing pattern
- **Files**:
  - `frontend/trading-ui/src/app/core/services/hyperliquid-api.service.ts` — Add method
- **Success**:
  - `getPortfolioHeat()` method returns `Observable<PortfolioHeat>`
  - Calls `GET ${baseUrl}/risk/portfolio-heat`
  - Follows existing `getAccountSummary` pattern
- **Dependencies**: Task 4.1

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/services/hyperliquid-api.service.ts — modification
// Add import at top:
import { PortfolioHeat } from "../models/portfolio-heat.model";

// Add method:
  public getPortfolioHeat(context?: HttpContext): Observable<PortfolioHeat> {
    return this._http.get<PortfolioHeat>(`${this._baseUrl}/risk/portfolio-heat`, context ? { context } : undefined);
  }
```

##### Pattern References

- `frontend/trading-ui/src/app/core/services/hyperliquid-api.service.ts` — `getAccountSummary()`, `getPositions()` method patterns

---

### Task 4.3: Create `PortfolioHeatIndicatorComponent` {#task-43-create-portfolioheatindicatorcomponent}

Create the heat indicator component with progress bar, percentage display, threshold colouring, and position breakdown tooltip.

- **Complexity**: Medium
- **Risk Factors**: Threshold calculation relative to max heat (not absolute %)
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/account-summary/portfolio-heat-indicator/portfolio-heat-indicator.component.ts` — New file
  - `frontend/trading-ui/src/app/features/dashboard/account-summary/portfolio-heat-indicator/portfolio-heat-indicator.component.html` — New file
  - `frontend/trading-ui/src/app/features/dashboard/account-summary/portfolio-heat-indicator/portfolio-heat-indicator.component.scss` — New file
- **Success**:
  - Receives `heatPercent`, `maxHeatPercent`, and `positions` as inputs
  - Computes threshold: green (< 50% of max), amber (50–80% of max), red (> 80% of max)
  - Shows `mat-progress-bar` with colour matching threshold
  - Shows percentage value next to bar
  - Warning icon and pulse animation when red (> 80% of max)
  - Tooltip shows position breakdown
  - Handles `maxHeatPercent = 0` (disabled) gracefully
- **Dependencies**: Tasks 4.1

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/account-summary/portfolio-heat-indicator/portfolio-heat-indicator.component.ts — new file
import { DecimalPipe } from "@angular/common";
import { Component, Input } from "@angular/core";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressBarModule } from "@angular/material/progress-bar";
import { MatTooltipModule } from "@angular/material/tooltip";
import { PortfolioHeatPosition } from "../../../../core/models/portfolio-heat.model";

interface HeatThresholdConfig {
  readonly cssClass: "low" | "moderate" | "elevated" | "critical";
  readonly label: string;
}

@Component({
  selector: "app-portfolio-heat-indicator",
  standalone: true,
  imports: [DecimalPipe, MatIconModule, MatProgressBarModule, MatTooltipModule],
  templateUrl: "./portfolio-heat-indicator.component.html",
  styleUrl: "./portfolio-heat-indicator.component.scss"
})
export class PortfolioHeatIndicatorComponent {
  @Input({ required: true })
  public heatPercent!: number;

  @Input({ required: true })
  public maxHeatPercent!: number;

  @Input()
  public positions: PortfolioHeatPosition[] = [];

  public get barValue(): number {
    if (this.maxHeatPercent <= 0) {
      return 0;
    }
    return Math.min(Math.max((this.heatPercent / this.maxHeatPercent) * 100, 0), 100);
  }

  public get threshold(): HeatThresholdConfig {
    if (this.maxHeatPercent <= 0) {
      return { cssClass: "low", label: "Heat limit disabled" };
    }

    const ratio = this.heatPercent / this.maxHeatPercent;

    if (ratio > 0.8) {
      return { cssClass: "critical", label: "Critical — near heat limit" };
    }

    if (ratio > 0.5) {
      return { cssClass: "elevated", label: "Elevated" };
    }

    return { cssClass: "low", label: "Low heat" };
  }

  public get isCritical(): boolean {
    return this.maxHeatPercent > 0 && (this.heatPercent / this.maxHeatPercent) > 0.8;
  }

  public get tooltipText(): string {
    if (this.positions.length === 0) {
      return "No open positions";
    }

    return this.positions
      .map(p => `${p.symbol}: $${p.riskUsd.toFixed(2)} (${p.riskPercent.toFixed(1)}%)`)
      .join("\n");
  }
}
```

```html
<!-- frontend/trading-ui/src/app/features/dashboard/account-summary/portfolio-heat-indicator/portfolio-heat-indicator.component.html — new file -->
<div class="portfolio-heat" [class.portfolio-heat--critical]="isCritical" [matTooltip]="tooltipText" matTooltipClass="portfolio-heat__tooltip">
  <div class="portfolio-heat__bar-container">
    <mat-progress-bar mode="determinate" [value]="barValue" [class]="'portfolio-heat__bar portfolio-heat__bar--' + threshold.cssClass">
    </mat-progress-bar>
  </div>
  <span class="portfolio-heat__value">{{ heatPercent | number: "1.1-1" }}%</span>
  @if (isCritical) {
    <mat-icon class="portfolio-heat__warning-icon">local_fire_department</mat-icon>
  }
</div>
```

```scss
// frontend/trading-ui/src/app/features/dashboard/account-summary/portfolio-heat-indicator/portfolio-heat-indicator.component.scss — new file
@keyframes pulse-warning {
  0%,
  100% {
    opacity: 1;
  }

  50% {
    opacity: 0.5;
  }
}

.portfolio-heat {
  display: flex;
  align-items: center;
  gap: 0.5rem;

  &__bar-container {
    flex: 1;
    min-width: 5rem;
  }

  &__bar {
    &--low {
      --mdc-linear-progress-active-indicator-color: var(--colour-profit);
    }

    &--moderate {
      --mdc-linear-progress-active-indicator-color: var(--colour-warning);
    }

    &--elevated {
      --mdc-linear-progress-active-indicator-color: var(--colour-warning);
    }

    &--critical {
      --mdc-linear-progress-active-indicator-color: var(--colour-loss);
    }
  }

  &__value {
    white-space: nowrap;
    color: var(--colour-muted);
    font-size: 0.85rem;
    font-weight: 500;
  }

  &__warning-icon {
    width: 1.125rem;
    height: 1.125rem;
    color: var(--colour-loss);
    font-size: 1.125rem;
  }

  &__tooltip {
    white-space: pre-line;
  }

  &--critical {
    .portfolio-heat__bar-container,
    .portfolio-heat__warning-icon {
      animation: pulse-warning 1.5s ease-in-out infinite;
    }
  }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/account-summary/margin-ratio-indicator/margin-ratio-indicator.component.ts` — threshold logic, `mat-progress-bar`, pulse animation
- `frontend/trading-ui/src/app/features/dashboard/account-summary/margin-ratio-indicator/margin-ratio-indicator.component.html` — template structure
- `frontend/trading-ui/src/app/features/dashboard/account-summary/margin-ratio-indicator/margin-ratio-indicator.component.scss` — CSS custom property overrides

---

### Task 4.4: Integrate into `AccountSummaryComponent` {#task-44-integrate-into-accountsummarycomponent}

Add the portfolio heat indicator to the account summary card and wire up data fetching.

- **Complexity**: Medium
- **Risk Factors**: Need to integrate data fetching into the existing dashboard polling cycle
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/account-summary/account-summary.component.ts` — Add import and input
  - `frontend/trading-ui/src/app/features/dashboard/account-summary/account-summary.component.html` — Add heat metric row
  - `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — Add heat data fetching
  - `frontend/trading-ui/src/app/features/dashboard/dashboard.component.html` — Pass heat data to account-summary
- **Success**:
  - `AccountSummaryComponent` receives optional `portfolioHeat` input
  - Heat indicator displays as a new metric row alongside Cross Margin Ratio
  - `DashboardComponent` fetches heat data alongside existing account/positions data
  - Heat data updates on each poll cycle
  - Gracefully handles undefined heat data (loading state)
- **Dependencies**: Tasks 4.1, 4.2, 4.3

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/account-summary/account-summary.component.ts — modification
// Add import:
import { PortfolioHeatIndicatorComponent } from "./portfolio-heat-indicator/portfolio-heat-indicator.component";
import { PortfolioHeat } from "../../../core/models/portfolio-heat.model";

// Add to imports array in @Component:
// imports: [... existing ..., PortfolioHeatIndicatorComponent]

// Add input property:
  @Input()
  public portfolioHeat: PortfolioHeat | null = null;
```

```html
<!-- frontend/trading-ui/src/app/features/dashboard/account-summary/account-summary.component.html — modification -->
<!-- Add after the "Cross Margin Ratio" metric div, within the @if (!isMobile() || expanded()) block: -->
        @if (portfolioHeat) {
          <div class="account-summary__metric">
            <span class="account-summary__label">Portfolio Heat</span>
            <app-portfolio-heat-indicator
              [heatPercent]="portfolioHeat.heatPercent"
              [maxHeatPercent]="portfolioHeat.maxHeatPercent"
              [positions]="portfolioHeat.positions" />
          </div>
        }
```

```typescript
// frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts — modification
// Add import:
import { PortfolioHeat } from "../../core/models/portfolio-heat.model";

// Add property:
  public portfolioHeat: PortfolioHeat | null = null;

// In the polling/refresh pipeline (inside the switchMap that fetches account + positions),
// add a call to fetch portfolio heat:
// Look for where getAccountSummary / getPositions are called and add:
  this._api.getPortfolioHeat().subscribe(heat => this.portfolioHeat = heat);
// Or use forkJoin/combineLatest to fetch in parallel with existing calls.
```

```html
<!-- frontend/trading-ui/src/app/features/dashboard/dashboard.component.html — modification -->
<!-- Pass portfolioHeat to account-summary component: -->
<!-- Find <app-account-summary [summary]="..."> and add [portfolioHeat]="portfolioHeat" -->
```

> **Note**: The exact integration into the polling pipeline depends on the current structure of `DashboardComponent.ngOnInit()`. Read the file to find the `switchMap` or `forkJoin` pattern and add the `getPortfolioHeat()` call alongside existing API calls.

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts` — polling pattern with `_refresh$`
- `frontend/trading-ui/src/app/features/dashboard/account-summary/account-summary.component.ts` — `@Input` pattern
- `frontend/trading-ui/src/app/features/dashboard/account-summary/account-summary.component.html` — metric row template

---

### Task 4.5: Frontend build and lint verification {#task-45-frontend-build-and-lint-verification}

Build the Angular app and run lint to verify no errors.

- **Complexity**: Low
- **Risk Factors**: Potential import or template errors
- **Files**: None (verification only)
- **Success**:
  - `cd frontend/trading-ui && npx ng build` succeeds
  - `cd frontend/trading-ui && npm run lint` passes
- **Dependencies**: Tasks 4.1–4.4

## Phase Success Criteria

- Portfolio heat percentage displays in the account summary card
- Colour coding: green (< 50% of max), amber (50–80% of max), red (> 80% of max)
- Position breakdown visible via tooltip
- Heat data updates with dashboard polling
- Angular build and lint pass
