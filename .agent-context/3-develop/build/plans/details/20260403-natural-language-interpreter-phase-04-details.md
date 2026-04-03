<!-- markdownlint-disable-file -->

# Task Details: F9 — Natural Language Strategy Interpreter

## Phase 4: Frontend — NL Interpretation UI

## Standards and Knowledge References

- **angular.instructions.md**: Standalone components, `inject()` for DI (never constructor injection), double quotes for strings, `@if`/`@for` control flow, SCSS with CSS custom properties, `takeUntilDestroyed()` for observable cleanup, `public`/`private` explicit, explicit return types
- **testing.instructions.md**: Jasmine + Angular TestBed for `.spec.ts` files, stub services with `jasmine.createSpy`
- Strategy Builder uses Angular Material components: `mat-card`, `mat-form-field`, `mat-button`, etc.
- CSS design system: `--colour-profit` (green), `--colour-loss` (red), `--colour-warning` (amber), `--colour-surface-dark`, `--colour-border-subtle`, BEM naming

## Design References

- Strategy Builder layout: two-column CSS Grid with `main` (form cards) and `side` (preview/validation) columns
- Form population: `this.form.patchValue(...)` + `ConditionFactoryService` for conditions FormArray
- API calls: `ApiRestClient.post<T>(path, body, context?)` with `SKIP_ERROR_NOTIFICATION` context token for in-component error handling

### Task 4.1: Add NL interpretation models and API service method {#task-41-add-frontend-models-and-service}

Add TypeScript interfaces for the interpretation result and the API service method.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/models/strategy-intent.model.ts` — new model file
  - `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-api.service.ts` — add method
- **Success**:
  - `StrategyIntentDto` interface matches C# DTO shape
  - `interpretStrategy()` method calls `POST strategies/interpret`
- **Dependencies**: Phase 3 (API endpoint)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/models/strategy-intent.model.ts — new file
import { StrategyConfig } from "./strategy.model";

export interface StrategyIntentDto {
  config: StrategyConfig;
  confidence: number;
  assumptions: AssumptionDto[];
  clarificationNeeded: string | null;
}

export interface AssumptionDto {
  fieldName: string;
  assumedValue: string;
  reason: string;
}
```

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/services/strategy-api.service.ts — add method
// Add import:
import { StrategyIntentDto } from "../models/strategy-intent.model";

// Add method to StrategyApiService class:
public interpretStrategy(text: string, context?: HttpContext): Observable<StrategyIntentDto> {
  return this._apiClient.post<StrategyIntentDto>("strategies/interpret", { text }, context);
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts` — DTO interface conventions
- `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-api.service.ts` — API method with `ApiRestClient.post()` pattern

### Task 4.2: Create NL input card component {#task-42-create-nl-input-card}

Create the NL text input component with text area, character counter, generate button, loading state, and error display.

- **Complexity**: Medium
- **Risk Factors**: Character counter must update reactively; loading state prevents double-submit
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/components/nl-input-card/nl-input-card.component.ts` — component
  - `frontend/trading-ui/src/app/features/strategy-builder/components/nl-input-card/nl-input-card.component.html` — template
  - `frontend/trading-ui/src/app/features/strategy-builder/components/nl-input-card/nl-input-card.component.scss` — styles
- **Success**:
  - Text area with placeholder and 500 char limit
  - Character counter shows current/max
  - Generate button disabled when empty or loading
  - Loading spinner during API call
  - Error message displayed on failure
  - Emits `interpreted` event with `StrategyIntentDto`
- **Dependencies**: Task 4.1

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/components/nl-input-card/nl-input-card.component.ts — new file
import { Component, EventEmitter, Input, Output, inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { MatCardModule } from "@angular/material/card";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatButtonModule } from "@angular/material/button";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatIconModule } from "@angular/material/icon";
import { HttpContext } from "@angular/common/http";
import { finalize } from "rxjs";
import { StrategyApiService } from "../../services/strategy-api.service";
import { StrategyIntentDto } from "../../models/strategy-intent.model";
import { SKIP_ERROR_NOTIFICATION } from "../../../../core/interceptors/http-context-tokens";

@Component({
  selector: "app-nl-input-card",
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatIconModule,
  ],
  templateUrl: "./nl-input-card.component.html",
  styleUrl: "./nl-input-card.component.scss",
})
export class NlInputCardComponent {
  @Input() public initialText: string | null = null;
  @Output() public interpreted = new EventEmitter<StrategyIntentDto>();

  private readonly _strategyApi = inject(StrategyApiService);

  public text = "";
  public isLoading = false;
  public errorMessage: string | null = null;
  public readonly maxLength = 500;

  public ngOnInit(): void {
    if (this.initialText) {
      this.text = this.initialText;
    }
  }

  public get charCount(): number {
    return this.text.length;
  }

  public get canGenerate(): boolean {
    return this.text.trim().length > 0 && !this.isLoading;
  }

  public generate(): void {
    if (!this.canGenerate) return;

    this.isLoading = true;
    this.errorMessage = null;

    const context = new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);

    this._strategyApi.interpretStrategy(this.text, context)
      .pipe(finalize(() => this.isLoading = false))
      .subscribe({
        next: (result) => this.interpreted.emit(result),
        error: (err) => {
          if (err.status === 429) {
            this.errorMessage = "Too many requests. Please wait a moment.";
          } else {
            this.errorMessage = "Strategy interpreter is temporarily unavailable. Please try again or use the form builder.";
          }
        }
      });
  }

  public clear(): void {
    this.text = "";
    this.errorMessage = null;
  }
}
```

```html
<!-- frontend/trading-ui/src/app/features/strategy-builder/components/nl-input-card/nl-input-card.component.html — new file -->
<mat-card class="nl-input-card">
  <mat-card-header>
    <mat-card-title>Describe Your Strategy</mat-card-title>
  </mat-card-header>
  <mat-card-content>
    <mat-form-field appearance="outline" class="nl-input-card__text-field">
      <mat-label>Strategy description</mat-label>
      <textarea
        matInput
        [(ngModel)]="text"
        [maxlength]="maxLength"
        placeholder="Describe your strategy in plain English, e.g. 'Buy ETH when RSI drops below 30 with a 2% take profit'"
        rows="3"
        cdkTextareaAutosize
      ></textarea>
      <mat-hint align="end">{{ charCount }} / {{ maxLength }}</mat-hint>
    </mat-form-field>

    @if (errorMessage) {
      <div class="nl-input-card__error">
        <mat-icon>error_outline</mat-icon>
        <span>{{ errorMessage }}</span>
      </div>
    }

    <div class="nl-input-card__actions">
      <button mat-raised-button color="primary" [disabled]="!canGenerate" (click)="generate()">
        @if (isLoading) {
          <mat-spinner diameter="20"></mat-spinner>
        } @else {
          <mat-icon>auto_awesome</mat-icon>
          Generate
        }
      </button>
      <button mat-button (click)="clear()" [disabled]="isLoading">Clear</button>
    </div>
  </mat-card-content>
</mat-card>
```

```scss
// frontend/trading-ui/src/app/features/strategy-builder/components/nl-input-card/nl-input-card.component.scss — new file
.nl-input-card {
  background: var(--colour-surface-dark);
  border: 1px solid var(--colour-border-subtle);

  &__text-field {
    width: 100%;
  }

  &__error {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 8px 12px;
    margin-bottom: 12px;
    background: var(--colour-error-bg);
    color: var(--colour-error-text);
    border-radius: 4px;
    font-size: 0.875rem;
  }

  &__actions {
    display: flex;
    gap: 8px;
    align-items: center;

    mat-spinner {
      display: inline-block;
      margin-right: 8px;
    }
  }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/components/strategy-details-card/` — `mat-card` with `mat-form-field appearance="outline"`, BEM SCSS
- `frontend/trading-ui/src/app/features/strategy-builder/components/trend-filter-card/` — conditional field display with `@if`

### Task 4.3: Create assumptions panel component {#task-43-create-assumptions-panel}

Create a component that displays the list of assumptions made during interpretation, with "Edit" action to scroll to the relevant form field.

- **Complexity**: Medium
- **Risk Factors**: Scroll-to-field requires knowing field element IDs in the form
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/components/assumptions-panel/assumptions-panel.component.ts` — component
  - `frontend/trading-ui/src/app/features/strategy-builder/components/assumptions-panel/assumptions-panel.component.html` — template
  - `frontend/trading-ui/src/app/features/strategy-builder/components/assumptions-panel/assumptions-panel.component.scss` — styles
- **Success**:
  - Displays list of assumptions with field name, assumed value, and reason
  - "Edit" button emits event with field name for parent to handle scrolling
  - Hidden when no assumptions exist
- **Dependencies**: Task 4.1

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/components/assumptions-panel/assumptions-panel.component.ts — new file
import { Component, EventEmitter, Input, Output } from "@angular/core";
import { CommonModule } from "@angular/common";
import { MatCardModule } from "@angular/material/card";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { AssumptionDto } from "../../models/strategy-intent.model";

@Component({
  selector: "app-assumptions-panel",
  standalone: true,
  imports: [CommonModule, MatCardModule, MatButtonModule, MatIconModule],
  templateUrl: "./assumptions-panel.component.html",
  styleUrl: "./assumptions-panel.component.scss",
})
export class AssumptionsPanelComponent {
  @Input({ required: true }) public assumptions: AssumptionDto[] = [];
  @Output() public editField = new EventEmitter<string>();

  public onEdit(fieldName: string): void {
    this.editField.emit(fieldName);
  }
}
```

```html
<!-- frontend/trading-ui/src/app/features/strategy-builder/components/assumptions-panel/assumptions-panel.component.html — new file -->
@if (assumptions.length > 0) {
  <mat-card class="assumptions-panel">
    <mat-card-header>
      <mat-card-title>Assumptions</mat-card-title>
    </mat-card-header>
    <mat-card-content>
      @for (assumption of assumptions; track assumption.fieldName) {
        <div class="assumptions-panel__item">
          <div class="assumptions-panel__info">
            <span class="assumptions-panel__field">{{ assumption.fieldName }}</span>
            <span class="assumptions-panel__value">{{ assumption.assumedValue }}</span>
            <span class="assumptions-panel__reason">{{ assumption.reason }}</span>
          </div>
          <button mat-icon-button (click)="onEdit(assumption.fieldName)" aria-label="Edit field">
            <mat-icon>edit</mat-icon>
          </button>
        </div>
      }
    </mat-card-content>
  </mat-card>
}
```

```scss
// frontend/trading-ui/src/app/features/strategy-builder/components/assumptions-panel/assumptions-panel.component.scss — new file
.assumptions-panel {
  background: var(--colour-surface-dark);
  border: 1px solid var(--colour-border-subtle);

  &__item {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 8px 0;
    border-bottom: 1px solid var(--colour-border-subtle);

    &:last-child {
      border-bottom: none;
    }
  }

  &__info {
    display: flex;
    flex-direction: column;
    gap: 2px;
  }

  &__field {
    font-weight: 500;
    color: var(--colour-label);
    font-size: 0.875rem;
  }

  &__value {
    font-size: 0.875rem;
    color: var(--colour-warning);
  }

  &__reason {
    font-size: 0.75rem;
    color: var(--colour-muted);
  }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/components/validation-card/` — severity badge items with BEM modifier pattern, icon + text layout

### Task 4.4: Create confidence badge component {#task-44-create-confidence-badge}

Create an inline badge component that displays the confidence score with colour coding (green/amber/red).

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/components/confidence-badge/confidence-badge.component.ts` — component
  - `frontend/trading-ui/src/app/features/strategy-builder/components/confidence-badge/confidence-badge.component.html` — template
  - `frontend/trading-ui/src/app/features/strategy-builder/components/confidence-badge/confidence-badge.component.scss` — styles
- **Success**:
  - Confidence ≥ 0.8 shows green (High)
  - Confidence 0.5–0.79 shows amber (Medium)
  - Confidence < 0.5 shows red (Low) with warning message
  - Clarification message displayed when present
- **Dependencies**: None

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/components/confidence-badge/confidence-badge.component.ts — new file
import { Component, Input } from "@angular/core";
import { CommonModule } from "@angular/common";
import { MatIconModule } from "@angular/material/icon";

@Component({
  selector: "app-confidence-badge",
  standalone: true,
  imports: [CommonModule, MatIconModule],
  templateUrl: "./confidence-badge.component.html",
  styleUrl: "./confidence-badge.component.scss",
})
export class ConfidenceBadgeComponent {
  @Input({ required: true }) public confidence: number = 0;
  @Input() public clarificationNeeded: string | null = null;

  public get level(): "high" | "medium" | "low" {
    if (this.confidence >= 0.8) return "high";
    if (this.confidence >= 0.5) return "medium";
    return "low";
  }

  public get label(): string {
    const pct = Math.round(this.confidence * 100);
    return `${this.level.charAt(0).toUpperCase() + this.level.slice(1)}: ${pct}%`;
  }
}
```

```html
<!-- frontend/trading-ui/src/app/features/strategy-builder/components/confidence-badge/confidence-badge.component.html — new file -->
<div class="confidence-badge" [class]="'confidence-badge--' + level">
  <mat-icon>{{ level === 'high' ? 'check_circle' : level === 'medium' ? 'info' : 'warning' }}</mat-icon>
  <span class="confidence-badge__label">{{ label }}</span>
</div>

@if (level === 'low') {
  <div class="confidence-badge__warning">
    The system wasn't confident about this interpretation. Please review carefully.
  </div>
}

@if (clarificationNeeded) {
  <div class="confidence-badge__clarification">
    <mat-icon>help_outline</mat-icon>
    <span>{{ clarificationNeeded }}</span>
  </div>
}
```

```scss
// frontend/trading-ui/src/app/features/strategy-builder/components/confidence-badge/confidence-badge.component.scss — new file
.confidence-badge {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 12px;
  border-radius: 16px;
  font-size: 0.875rem;
  font-weight: 500;

  &--high {
    background: rgba(74, 222, 128, 0.15);
    color: var(--colour-profit);
  }

  &--medium {
    background: rgba(251, 191, 36, 0.15);
    color: var(--colour-warning);
  }

  &--low {
    background: rgba(248, 113, 113, 0.15);
    color: var(--colour-loss);
  }

  &__label {
    white-space: nowrap;
  }

  &__warning {
    margin-top: 8px;
    padding: 8px 12px;
    background: rgba(248, 113, 113, 0.1);
    color: var(--colour-loss);
    border-radius: 4px;
    font-size: 0.875rem;
  }

  &__clarification {
    display: flex;
    align-items: flex-start;
    gap: 8px;
    margin-top: 8px;
    padding: 8px 12px;
    background: rgba(251, 191, 36, 0.1);
    color: var(--colour-warning);
    border-radius: 4px;
    font-size: 0.875rem;
  }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/components/validation-card/` — severity badge items with BEM SCSS modifiers and colour variables

### Task 4.5: Integrate NL components into Strategy Builder page {#task-45-integrate-into-strategy-builder}

Add the NL input card, assumptions panel, and confidence badge to the Strategy Builder page layout.

- **Complexity**: Medium
- **Risk Factors**: Layout integration must not break existing form behaviour; collapsible section for returning users
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` — add imports, state, handler
  - `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.html` — add NL section
  - `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.scss` — minor layout adjustments
- **Success**:
  - NL input card appears at the top of the main column
  - After generation, confidence badge and assumptions panel appear between NL input and the form
  - Existing form layout and behaviour unchanged
- **Dependencies**: Tasks 4.2, 4.3, 4.4

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts — modifications

// Add imports:
import { NlInputCardComponent } from "./components/nl-input-card/nl-input-card.component";
import { AssumptionsPanelComponent } from "./components/assumptions-panel/assumptions-panel.component";
import { ConfidenceBadgeComponent } from "./components/confidence-badge/confidence-badge.component";
import { StrategyIntentDto, AssumptionDto } from "./models/strategy-intent.model";

// Add to imports array in @Component:
// NlInputCardComponent, AssumptionsPanelComponent, ConfidenceBadgeComponent

// Add state properties:
public nlResult: StrategyIntentDto | null = null;
public showNlSection = true;

// Add method:
public onNlInterpreted(result: StrategyIntentDto): void {
  this.nlResult = result;
  this._populateFormFromIntent(result);
}

public onEditAssumptionField(fieldName: string): void {
  // Map assumption field names to form field element IDs for scrolling
  const element = document.querySelector(`[formcontrolname="${fieldName}"]`);
  element?.scrollIntoView({ behavior: "smooth", block: "center" });
}
```

```html
<!-- frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.html — add at top of main column -->
<!-- Insert before the first existing mat-card in the main column -->
<app-nl-input-card
  [initialText]="nlResult?.config?.source?.sourceText ?? null"
  (interpreted)="onNlInterpreted($event)"
></app-nl-input-card>

@if (nlResult) {
  <app-confidence-badge
    [confidence]="nlResult.confidence"
    [clarificationNeeded]="nlResult.clarificationNeeded"
  ></app-confidence-badge>

  <app-assumptions-panel
    [assumptions]="nlResult.assumptions"
    (editField)="onEditAssumptionField($event)"
  ></app-assumptions-panel>
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.html` — existing two-column layout with `main` and `side` columns
- `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` — existing `_loadStrategy()` and `_applyEmaPullbackTemplate()` form population patterns

### Task 4.6: Implement form population from interpreter result {#task-46-implement-form-population}

Implement the method that takes a `StrategyIntentDto` and populates the reactive form using the existing `patchValue` + `ConditionFactoryService` patterns.

- **Complexity**: High
- **Risk Factors**: Must handle all strategy modes, condition types, and optional fields correctly; must use `ConditionFactoryService` for conditions FormArray manipulation; `createMacdCondition()` does not yet exist and must be added to `ConditionFactoryService`
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` — add `_populateFormFromIntent` method
  - `frontend/trading-ui/src/app/features/strategy-builder/services/condition-factory.service.ts` — add `createMacdCondition()` method
- **Success**:
  - Signal mode config populates mode toggle, conditions, exit, risk fields
  - Grid mode config populates grid fields
  - Conditions array correctly uses `ConditionFactoryService`
  - Form validation runs after population
- **Dependencies**: Task 4.5

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts — add method

private _populateFormFromIntent(intent: StrategyIntentDto): void {
  const config = intent.config;

  // Patch scalar fields
  this.form.patchValue({
    strategyName: config.strategyName,
    market: config.market,
    timeframe: config.timeframe,
    direction: config.direction,
  });

  // Set strategy mode
  this.strategyMode = config.strategyMode;

  // Grid config
  if (config.strategyMode === "grid" && config.grid) {
    this.form.get("grid")?.enable();
    this.form.get("grid")?.patchValue({
      levels: config.grid.levels,
      spacing: config.grid.spacing,
      entryMode: config.grid.entryMode ?? "limit",
    });
  }

  // Entry conditions
  if (config.entryConditions?.length) {
    const conditionsArray = this.conditionsFormArray;
    conditionsArray.clear();

    for (const condition of config.entryConditions) {
      switch (condition.type) {
        case "rsi":
          conditionsArray.push(
            this._conditionFactory.createRsiCondition({
              period: condition.params?.period ?? 14,
              operator: condition.params?.operator ?? "lt",
              value: condition.params?.value ?? 30,
            })
          );
          break;
        case "price_vs_ema":
          conditionsArray.push(
            this._conditionFactory.createPriceVsEmaCondition({
              period: condition.params?.period ?? 20,
              operator: condition.params?.operator ?? "lt",
              distanceType: condition.params?.distanceType ?? "percent",
              distanceValue: condition.params?.distanceValue ?? 0,
            })
          );
          break;
        case "macd":
          conditionsArray.push(
            this._conditionFactory.createMacdCondition({
              fastPeriod: condition.params?.fastPeriod ?? 12,
              slowPeriod: condition.params?.slowPeriod ?? 26,
              signalPeriod: condition.params?.signalPeriod ?? 9,
              operator: condition.params?.operator ?? "cross_above",
            })
          );
          break;
      }
    }
  }

  // Exit config
  if (config.exit) {
    this.form.get("exit")?.patchValue({
      takeProfit: {
        enabled: config.exit.takeProfit?.enabled ?? false,
        type: config.exit.takeProfit?.type ?? "fixed_percent",
        value: config.exit.takeProfit?.value ?? 2,
      },
      stopLoss: {
        enabled: config.exit.stopLoss?.enabled ?? false,
        type: config.exit.stopLoss?.type ?? "fixed_percent",
        value: config.exit.stopLoss?.value ?? 1.5,
      },
      exitOnOppositeSignal: config.exit.exitOnOppositeSignal ?? false,
    });
  }

  // Risk config
  if (config.risk) {
    this.form.get("risk")?.patchValue({
      positionSizeType: config.risk.positionSizeType,
      positionSizeValue: config.risk.positionSizeValue,
      leverage: config.risk.leverage,
      maxOpenTrades: config.risk.maxOpenTrades,
    });
  }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` — existing `_loadStrategy()` and `_applyEmaPullbackTemplate()` methods using `patchValue()` + `ConditionFactoryService`

### Task 4.7: Implement re-interpret flow and source text persistence {#task-47-reinterpret-and-source-text}

Handle re-interpretation confirmation dialog, source text pre-loading for edits, and save-through of source metadata.

- **Complexity**: Medium
- **Risk Factors**: Confirmation dialog must prevent accidental overwrites; source text must survive save/load cycle
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` — re-interpret logic
  - `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.ts` — pass through source metadata
- **Success**:
  - When form has existing values, re-generate shows confirmation dialog
  - When editing NL-created strategy, source text pre-loaded in NL input
  - Source metadata included in saved config
- **Dependencies**: Tasks 4.5, 4.6

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts — modification

// Update onNlInterpreted to add confirmation:
public onNlInterpreted(result: StrategyIntentDto): void {
  if (this._formHasValues()) {
    const confirmed = window.confirm(
      "This will replace your current form values with the generated configuration. Continue?"
    );
    if (!confirmed) return;
  }

  this.nlResult = result;
  this._populateFormFromIntent(result);
}

private _formHasValues(): boolean {
  const name = this.form.get("strategyName")?.value;
  return !!name && name.trim().length > 0;
}

// When loading existing strategy, set initialText from source metadata:
// In _loadStrategy() method, after populating form:
// if (strategy.source?.sourceText) {
//   this.nlResult = { config: strategy, confidence: 1, assumptions: [], clarificationNeeded: null };
// }
```

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.ts — modification
// Ensure source metadata passes through when mapping form → StrategyConfig for save:
// Add sourceText to the source metadata mapping if nlResult exists
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` — existing `_loadStrategy()` for loading saved strategies
- `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.ts` — form → config mapping

### Task 4.8: Add Angular tests {#task-48-add-angular-tests}

Add spec tests for the new components and service method.

- **Complexity**: Medium
- **Risk Factors**: Component tests need mocked API service
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/components/nl-input-card/nl-input-card.component.spec.ts` — new spec
  - `frontend/trading-ui/src/app/features/strategy-builder/components/confidence-badge/confidence-badge.component.spec.ts` — new spec
  - `frontend/trading-ui/src/app/features/strategy-builder/components/assumptions-panel/assumptions-panel.component.spec.ts` — new spec
- **Success**:
  - NL input card: renders, disables button when empty, emits on success, shows error on failure
  - Confidence badge: renders correct level and colour class for each range
  - Assumptions panel: renders assumption list, emits edit event
  - All specs pass (`npm run test`)
- **Dependencies**: Tasks 4.2, 4.3, 4.4

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/components/confidence-badge/confidence-badge.component.spec.ts — new file
import { ComponentFixture, TestBed } from "@angular/core/testing";
import { ConfidenceBadgeComponent } from "./confidence-badge.component";

describe("ConfidenceBadgeComponent", () => {
  let component: ConfidenceBadgeComponent;
  let fixture: ComponentFixture<ConfidenceBadgeComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ConfidenceBadgeComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(ConfidenceBadgeComponent);
    component = fixture.componentInstance;
  });

  it("should return 'high' level for confidence >= 0.8", () => {
    component.confidence = 0.92;
    expect(component.level).toBe("high");
  });

  it("should return 'medium' level for confidence between 0.5 and 0.79", () => {
    component.confidence = 0.65;
    expect(component.level).toBe("medium");
  });

  it("should return 'low' level for confidence < 0.5", () => {
    component.confidence = 0.3;
    expect(component.level).toBe("low");
  });

  it("should render warning message for low confidence", () => {
    component.confidence = 0.3;
    fixture.detectChanges();
    const warning = fixture.nativeElement.querySelector(".confidence-badge__warning");
    expect(warning).toBeTruthy();
  });

  it("should render clarification when provided", () => {
    component.confidence = 0.5;
    component.clarificationNeeded = "Ichimoku is not supported";
    fixture.detectChanges();
    const clarification = fixture.nativeElement.querySelector(".confidence-badge__clarification");
    expect(clarification?.textContent).toContain("Ichimoku is not supported");
  });
});
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.spec.ts` — Jasmine + TestBed pattern with spy services and `baseFormValue()` factory

### Task 4.9: Frontend build and lint verification {#task-49-build-and-lint}

Run frontend build and lint to ensure all changes compile and meet code quality standards.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: No files to create
- **Success**:
  - `npm run build` succeeds with no errors
  - `npm run lint` passes with no violations
  - `npm run test` passes for all specs
- **Dependencies**: All previous tasks in phase

## Phase Success Criteria

- NL input card renders in Strategy Builder with text area, character counter, and generate button
- Generate button calls API and populates the form with the interpreted config
- Confidence badge shows correct colour and level based on score
- Assumptions panel lists each assumption with edit action
- Re-interpret shows confirmation dialog when form has existing values
- Source text pre-loaded for strategies created via NL
- All Angular tests pass, build and lint clean
