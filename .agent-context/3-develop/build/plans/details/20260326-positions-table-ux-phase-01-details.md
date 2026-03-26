<!-- markdownlint-disable-file -->

# Task Details: F10 — Positions Table UX Enhancements

## Phase 1: Cross Margin Ratio Visual Indicator

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — standalone components, `inject()` DI, explicit access modifiers and return types, `@if`/`@for` control flow, BEM SCSS naming, double-quoted strings
- `.agent-context/0-knowledge/11-angular-instructions.md` — CSS token system, design token usage
- `frontend/trading-ui/src/styles.scss` — existing CSS custom properties: `--colour-profit`, `--colour-loss`, `--colour-muted`, snackbar warning color `#fbbf24`

## Design References

- Angular Material `MatProgressBarModule` is already used in `market-data.component.ts` — proven in the codebase
- `MatTooltipModule` for accessible threshold labels
- `@keyframes pulse` animation pattern from `app.component.scss` (reconnecting status dot)
- `crossMarginRatio` is a decimal ratio (0–1) computed server-side as `maintenanceMargin / equity`

### Task 1.1: Create the `MarginRatioIndicatorComponent` {#task-11-create-margin-ratio-indicator-component}

Create a new standalone component that renders a progress bar with color-coded thresholds for the Cross Margin Ratio.

- **Complexity**: Medium
- **Risk Factors**: Threshold color logic, progress bar fill calculation, pulsing animation at critical levels
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/account-summary/margin-ratio-indicator/margin-ratio-indicator.component.ts` — new file
  - `frontend/trading-ui/src/app/features/dashboard/account-summary/margin-ratio-indicator/margin-ratio-indicator.component.html` — new file
  - `frontend/trading-ui/src/app/features/dashboard/account-summary/margin-ratio-indicator/margin-ratio-indicator.component.scss` — new file
- **Success**:
  - Component accepts a `ratio` input (decimal, 0–1)
  - Renders `<mat-progress-bar>` with percentage fill (ratio × 100, capped at 100)
  - Color coding: green (0–0.30), yellow (0.30–0.60), orange (0.60–0.80), red (0.80–1.00)
  - Tooltip displays threshold label: "Low risk", "Moderate", "Elevated", "Critical — near liquidation"
  - At ratio ≥ 0.80, pulsing animation is applied
  - Numeric value displayed alongside the bar
- **Dependencies**: None

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/account-summary/margin-ratio-indicator/margin-ratio-indicator.component.ts — new file
import { Component, Input } from "@angular/core";
import { DecimalPipe } from "@angular/common";
import { MatProgressBarModule } from "@angular/material/progress-bar";
import { MatTooltipModule } from "@angular/material/tooltip";
import { MatIconModule } from "@angular/material/icon";

interface ThresholdConfig {
  readonly cssClass: string;
  readonly label: string;
}

@Component({
  selector: "app-margin-ratio-indicator",
  standalone: true,
  imports: [DecimalPipe, MatProgressBarModule, MatTooltipModule, MatIconModule],
  templateUrl: "./margin-ratio-indicator.component.html",
  styleUrl: "./margin-ratio-indicator.component.scss",
})
export class MarginRatioIndicatorComponent {
  @Input({ required: true }) public ratio!: number;

  public get percentage(): number {
    return Math.min(this.ratio * 100, 100);
  }

  public get threshold(): ThresholdConfig {
    if (this.ratio >= 0.80) {
      return { cssClass: "critical", label: "Critical — near liquidation" };
    }
    if (this.ratio >= 0.60) {
      return { cssClass: "elevated", label: "Elevated" };
    }
    if (this.ratio >= 0.30) {
      return { cssClass: "moderate", label: "Moderate" };
    }
    return { cssClass: "low", label: "Low risk" };
  }

  public get isCritical(): boolean {
    return this.ratio >= 0.80;
  }
}
```

```html
<!-- frontend/trading-ui/src/app/features/dashboard/account-summary/margin-ratio-indicator/margin-ratio-indicator.component.html — new file -->
<div class="margin-ratio"
     [class.margin-ratio--critical]="isCritical"
     [matTooltip]="threshold.label">
  <div class="margin-ratio__bar-container">
    <mat-progress-bar
      mode="determinate"
      [value]="percentage"
      [class]="'margin-ratio__bar margin-ratio__bar--' + threshold.cssClass">
    </mat-progress-bar>
  </div>
  <span class="margin-ratio__value">{{ ratio | number: "1.4-4" }}</span>
  @if (isCritical) {
    <mat-icon class="margin-ratio__warning-icon">warning</mat-icon>
  }
</div>
```

```scss
// frontend/trading-ui/src/app/features/dashboard/account-summary/margin-ratio-indicator/margin-ratio-indicator.component.scss — new file
@keyframes pulse-warning {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.5; }
}

.margin-ratio {
  display: flex;
  align-items: center;
  gap: 8px;

  &__bar-container {
    flex: 1;
    min-width: 80px;
  }

  &__bar {
    &--low ::ng-deep .mdc-linear-progress__bar-inner {
      border-color: var(--colour-profit);
    }

    &--moderate ::ng-deep .mdc-linear-progress__bar-inner {
      border-color: var(--colour-warning, #fbbf24);
    }

    &--elevated ::ng-deep .mdc-linear-progress__bar-inner {
      border-color: var(--colour-warning-elevated, #f97316);
    }

    &--critical ::ng-deep .mdc-linear-progress__bar-inner {
      border-color: var(--colour-loss);
    }
  }

  &__value {
    font-size: 0.85rem;
    color: var(--colour-muted);
    white-space: nowrap;
  }

  &__warning-icon {
    color: var(--colour-loss);
    font-size: 18px;
    width: 18px;
    height: 18px;
  }

  &--critical {
    .margin-ratio__bar-container,
    .margin-ratio__warning-icon {
      animation: pulse-warning 1.5s ease-in-out infinite;
    }
  }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/account-summary/account-summary.component.ts` — existing account summary component structure
- `frontend/trading-ui/src/app/features/market-data/market-data.component.ts` — `MatProgressBarModule` import pattern
- `frontend/trading-ui/src/styles.scss` — CSS custom properties for colours

### Task 1.2: Add CSS custom properties for warning and critical thresholds {#task-12-add-css-custom-properties-for-thresholds}

Add `--colour-warning` and `--colour-warning-elevated` CSS custom properties to the global styles.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/styles.scss` — modification (add 2 custom properties to `:root` / `body`)
- **Success**:
  - `--colour-warning: #fbbf24` (yellow/amber — matches existing `.snackbar--warning` color)
  - `--colour-warning-elevated: #f97316` (orange)
  - Properties available globally for all components
- **Dependencies**: None

### Task 1.3: Integrate `MarginRatioIndicatorComponent` into `AccountSummaryComponent` {#task-13-integrate-into-account-summary-component}

Replace the plain Cross Margin Ratio number in the account summary card with the new visual indicator component.

- **Complexity**: Low
- **Risk Factors**: Layout may need minor adjustment to accommodate the progress bar width
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/account-summary/account-summary.component.ts` — add import
  - `frontend/trading-ui/src/app/features/dashboard/account-summary/account-summary.component.html` — replace CMR `<span>` with `<app-margin-ratio-indicator>`
- **Success**:
  - `MarginRatioIndicatorComponent` imported in `AccountSummaryComponent.imports[]`
  - Template uses `<app-margin-ratio-indicator [ratio]="summary.crossMarginRatio" />` instead of `{{ summary.crossMarginRatio | number: "1.4-4" }}`
  - Existing layout and spacing preserved
- **Dependencies**: Task 1.1

#### Implementation Details

In `account-summary.component.html`, find the Cross Margin Ratio metric item and replace the value `<span>`:

```html
<!-- account-summary.component.html — modification -->
<!-- Replace this: -->
<!-- <span class="account-summary__value">{{ summary.crossMarginRatio | number: "1.4-4" }}</span> -->

<!-- With: -->
<app-margin-ratio-indicator [ratio]="summary.crossMarginRatio" />
```

In `account-summary.component.ts`, add the import:

```typescript
// account-summary.component.ts — modification
// Add to imports array:
import { MarginRatioIndicatorComponent } from "./margin-ratio-indicator/margin-ratio-indicator.component";

// In @Component.imports:
imports: [/* ...existing */, MarginRatioIndicatorComponent]
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/account-summary/account-summary.component.html` — existing CMR rendering location
- `frontend/trading-ui/src/app/features/dashboard/account-summary/account-summary.component.ts` — standalone component imports pattern

### Task 1.4: Write unit tests for `MarginRatioIndicatorComponent` {#task-14-write-unit-tests}

Create unit tests covering all threshold levels, percentage calculation, pulsing animation, and tooltip text.

- **Complexity**: Medium
- **Risk Factors**: Testing CSS class application and Material tooltip content
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/account-summary/margin-ratio-indicator/margin-ratio-indicator.component.spec.ts` — new file
- **Success**:
  - Test: ratio 0.15 → green bar, "Low risk" tooltip, no warning icon
  - Test: ratio 0.45 → yellow bar, "Moderate" tooltip, no warning icon
  - Test: ratio 0.70 → orange bar, "Elevated" tooltip, no warning icon
  - Test: ratio 0.90 → red bar, "Critical — near liquidation" tooltip, warning icon visible, pulsing class applied
  - Test: ratio 0.0 → 0% fill, green
  - Test: ratio 1.5 → capped at 100% fill
  - All tests pass
- **Dependencies**: Task 1.1

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/dashboard/account-summary/margin-ratio-indicator/margin-ratio-indicator.component.spec.ts — new file
import { ComponentFixture, TestBed } from "@angular/core/testing";
import { MarginRatioIndicatorComponent } from "./margin-ratio-indicator.component";
import { NoopAnimationsModule } from "@angular/platform-browser/animations";

describe("MarginRatioIndicatorComponent", () => {
  let component: MarginRatioIndicatorComponent;
  let fixture: ComponentFixture<MarginRatioIndicatorComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MarginRatioIndicatorComponent, NoopAnimationsModule],
    }).compileComponents();

    fixture = TestBed.createComponent(MarginRatioIndicatorComponent);
    component = fixture.componentInstance;
  });

  it("should create", () => {
    component.ratio = 0;
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it("should return percentage capped at 100", () => {
    component.ratio = 1.5;
    expect(component.percentage).toBe(100);
  });

  it("should return 'Low risk' threshold for ratio 0.15", () => {
    component.ratio = 0.15;
    expect(component.threshold.cssClass).toBe("low");
    expect(component.threshold.label).toBe("Low risk");
    expect(component.isCritical).toBeFalse();
  });

  it("should return 'Moderate' threshold for ratio 0.45", () => {
    component.ratio = 0.45;
    expect(component.threshold.cssClass).toBe("moderate");
    expect(component.threshold.label).toBe("Moderate");
  });

  it("should return 'Elevated' threshold for ratio 0.70", () => {
    component.ratio = 0.70;
    expect(component.threshold.cssClass).toBe("elevated");
    expect(component.threshold.label).toBe("Elevated");
  });

  it("should return 'Critical' threshold for ratio 0.90", () => {
    component.ratio = 0.90;
    expect(component.threshold.cssClass).toBe("critical");
    expect(component.threshold.label).toBe("Critical — near liquidation");
    expect(component.isCritical).toBeTrue();
  });

  it("should show warning icon when critical", () => {
    component.ratio = 0.85;
    fixture.detectChanges();
    const icon = fixture.nativeElement.querySelector(".margin-ratio__warning-icon");
    expect(icon).toBeTruthy();
  });

  it("should not show warning icon when not critical", () => {
    component.ratio = 0.15;
    fixture.detectChanges();
    const icon = fixture.nativeElement.querySelector(".margin-ratio__warning-icon");
    expect(icon).toBeFalsy();
  });

  it("should apply pulsing class when critical", () => {
    component.ratio = 0.85;
    fixture.detectChanges();
    const container = fixture.nativeElement.querySelector(".margin-ratio");
    expect(container.classList).toContain("margin-ratio--critical");
  });
});
```

##### Pattern References

- `frontend/trading-ui/src/app/app.component.spec.ts` — TestBed standalone component test pattern
- `frontend/trading-ui/src/app/features/connection/status-card.component.spec.ts` — minimal component test

### Task 1.5: Run frontend build and lint {#task-15-run-frontend-build-and-lint}

Verify no build or lint errors were introduced.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification step)
- **Success**:
  - `npx ng build` completes without errors
  - `npx ng lint` completes without errors
  - All existing and new tests pass (`npx ng test --watch=false`)
- **Dependencies**: Tasks 1.1–1.4

## Phase Success Criteria

- Cross Margin Ratio in the account summary card displays a color-coded progress bar with correct threshold colors
- Tooltip shows the appropriate risk label at each threshold level
- At ratio ≥ 0.80, a pulsing animation and warning icon draw attention
- The numeric ratio value remains visible alongside the bar
- The indicator updates reactively on 2s polling refresh
- All unit tests pass; frontend builds and lints cleanly
