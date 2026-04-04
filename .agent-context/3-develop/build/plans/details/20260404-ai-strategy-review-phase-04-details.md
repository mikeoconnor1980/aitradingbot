<!-- markdownlint-disable-file -->

# Task Details: AI Strategy Review

## Phase 4: Angular Frontend

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — Standalone components, `inject()` for DI, explicit member accessibility, double quotes, SCSS, newer control flow syntax, observable naming conventions
- `.agent-context/0-knowledge/06-project-structure.md` — Feature folder conventions

## Design References

- The `strategy-builder-page` component already has action buttons (Backtest, Save) and a side column with cards
- The "AI Review" button goes in the actions bar next to the Backtest button
- The collapsible review summary card goes in the side column
- The full review modal uses Angular Material dialog pattern (like `ConfirmDialogComponent`)
- `marked` is recommended for markdown parsing — lightweight, no Angular wrapper needed
- Cooldown timer uses simple `setInterval` approach

### Task 4.1: Install markdown rendering library {#task-41-install-markdown-rendering-library}

Install `marked` and `@types/marked` for markdown-to-HTML rendering.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/package.json` - Modified by npm install
- **Success**:
  - `marked` package listed in dependencies
  - `@types/marked` listed in devDependencies
- **Dependencies**: None

Run:
```bash
cd frontend/trading-ui
npm install marked
npm install -D @types/marked
```

---

### Task 4.2: Create AI review TypeScript models {#task-42-create-ai-review-typescript-models}

Create the TypeScript interface matching the backend `StrategyReviewDto`.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/models/strategy-review.model.ts` - New file
- **Success**:
  - Interface matches backend DTO shape (camelCase)
- **Dependencies**: None

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/models/strategy-review.model.ts — new file
export interface StrategyReviewDto {
  id: string;
  strategyId: string;
  revisionNumber: number;
  reviewMarkdown: string;
  modelName: string;
  createdAtUtc: number;
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts` — TypeScript interface pattern

---

### Task 4.3: Add review methods to StrategyApiService {#task-43-add-review-methods-to-strategyapiservice}

Add methods to request and retrieve strategy reviews.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-api.service.ts` - Modify
- **Success**:
  - `requestReview(strategyId, revisionNumber)` POST method
  - `getReview(strategyId, revisionNumber)` GET method
  - Both follow existing service method patterns
- **Dependencies**: Task 4.2

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/services/strategy-api.service.ts — add methods

import { StrategyReviewDto } from "../models/strategy-review.model";

// Add these methods to the StrategyApiService class:

public requestReview(strategyId: string, revisionNumber: number, context?: HttpContext): Observable<StrategyReviewDto> {
  const encodedStrategyId = encodeURIComponent(strategyId);

  return this._apiClient.post<StrategyReviewDto>(
    `strategies/${encodedStrategyId}/versions/${revisionNumber}/review`,
    null,
    context
  );
}

public getReview(strategyId: string, revisionNumber: number, context?: HttpContext): Observable<StrategyReviewDto> {
  const encodedStrategyId = encodeURIComponent(strategyId);

  return this._apiClient.get<StrategyReviewDto>(
    `strategies/${encodedStrategyId}/versions/${revisionNumber}/review`,
    context
  );
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-api.service.ts` — `getVersion()` method pattern for URL construction

---

### Task 4.4: Create AiReviewCardComponent (collapsible summary) {#task-44-create-aireviewcardcomponent-collapsible-summary}

Create a collapsible card component for the side column that shows a truncated preview of the review markdown and a "View Full Review" button.

- **Complexity**: Medium
- **Risk Factors**: Markdown rendering via `marked`; must sanitize HTML output; expansion panel usage
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/components/ai-review-card/ai-review-card.component.ts` - New file
  - `frontend/trading-ui/src/app/features/strategy-builder/components/ai-review-card/ai-review-card.component.html` - New file
  - `frontend/trading-ui/src/app/features/strategy-builder/components/ai-review-card/ai-review-card.component.scss` - New file
- **Success**:
  - Shows collapsible card with truncated review summary
  - "View Full Review" button opens modal
  - "Apply Suggestions" button disabled with "Coming Soon" tooltip
  - Review model name and timestamp displayed
- **Dependencies**: Task 4.1, Task 4.2

#### Implementation Details

```typescript
// ai-review-card.component.ts — new file
import { Component, EventEmitter, inject, Input, Output } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatIconModule } from "@angular/material/icon";
import { MatTooltipModule } from "@angular/material/tooltip";
import { marked } from "marked";
import { StrategyReviewDto } from "../../models/strategy-review.model";

@Component({
  selector: "app-ai-review-card",
  standalone: true,
  imports: [MatButtonModule, MatCardModule, MatIconModule, MatTooltipModule],
  templateUrl: "./ai-review-card.component.html",
  styleUrl: "./ai-review-card.component.scss",
})
export class AiReviewCardComponent {
  @Input()
  public review: StrategyReviewDto | null = null;

  @Output()
  public viewFullReview: EventEmitter<void> = new EventEmitter<void>();

  public get summaryHtml(): string {
    if (!this.review) {
      return "";
    }

    // Take first ~500 chars of markdown for summary
    const truncated = this.review.reviewMarkdown.substring(0, 500);
    return marked.parse(truncated, { async: false }) as string;
    // Angular's [innerHTML] binding auto-sanitizes this — no bypassSecurityTrustHtml needed
  }

  public get reviewDate(): string {
    if (!this.review) {
      return "";
    }

    return new Date(this.review.createdAtUtc).toLocaleString();
  }

  public onViewFull(): void {
    this.viewFullReview.emit();
  }
}
```

```html
<!-- ai-review-card.component.html — new file -->
@if (review) {
  <mat-card class="ai-review-card">
    <mat-card-header>
      <mat-card-title>
        <mat-icon>rate_review</mat-icon>
        AI Review
      </mat-card-title>
      <mat-card-subtitle>{{ review.modelName }} · {{ reviewDate }}</mat-card-subtitle>
    </mat-card-header>

    <mat-card-content>
      <div class="ai-review-card__summary" [innerHTML]="summaryHtml"></div>
      @if (review.reviewMarkdown.length > 500) {
        <p class="ai-review-card__truncated">...</p>
      }
    </mat-card-content>

    <mat-card-actions>
      <button mat-stroked-button color="primary" (click)="onViewFull()">
        <mat-icon>open_in_full</mat-icon>
        View Full Review
      </button>
      <button mat-stroked-button disabled matTooltip="Coming Soon">
        <mat-icon>auto_fix_high</mat-icon>
        Apply Suggestions
      </button>
    </mat-card-actions>
  </mat-card>
}
```

```scss
// ai-review-card.component.scss — new file
.ai-review-card {
  &__summary {
    max-height: 200px;
    overflow: hidden;
    font-size: 0.875rem;
    line-height: 1.5;

    ::ng-deep h1, ::ng-deep h2, ::ng-deep h3 {
      font-size: 1rem;
      margin: 0.5rem 0;
    }

    ::ng-deep ul {
      padding-left: 1.25rem;
      margin: 0.25rem 0;
    }
  }

  &__truncated {
    text-align: center;
    color: rgba(0, 0, 0, 0.6);
    margin: 0;
  }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/components/preview-summary-card/preview-summary-card.component.ts` — Side-column card pattern
- `frontend/trading-ui/src/app/features/order-entry/confirm-dialog/confirm-dialog.component.ts` — MatDialog pattern

---

### Task 4.5: Create AiReviewModalComponent (full review modal) {#task-45-create-aireviewmodalcomponent-full-review-modal}

Create a centered modal dialog that displays the full rendered markdown review.

- **Complexity**: Medium
- **Risk Factors**: Must sanitize markdown HTML; modal sizing and scrolling
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/components/ai-review-modal/ai-review-modal.component.ts` - New file
  - `frontend/trading-ui/src/app/features/strategy-builder/components/ai-review-modal/ai-review-modal.component.html` - New file
  - `frontend/trading-ui/src/app/features/strategy-builder/components/ai-review-modal/ai-review-modal.component.scss` - New file
- **Success**:
  - Full markdown rendered in a centered, scrollable modal
  - Close button to dismiss
  - "Apply Suggestions" button disabled with "Coming Soon" tooltip
- **Dependencies**: Task 4.1, Task 4.2

#### Implementation Details

```typescript
// ai-review-modal.component.ts — new file
import { Component, inject } from "@angular/core";
import { Component, inject } from "@angular/core";
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from "@angular/material/dialog";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatTooltipModule } from "@angular/material/tooltip";
import { marked } from "marked";
import { StrategyReviewDto } from "../../models/strategy-review.model";

export interface AiReviewModalData {
  review: StrategyReviewDto;
}

@Component({
  selector: "app-ai-review-modal",
  standalone: true,
  imports: [MatDialogModule, MatButtonModule, MatIconModule, MatTooltipModule],
  templateUrl: "./ai-review-modal.component.html",
  styleUrl: "./ai-review-modal.component.scss",
})
export class AiReviewModalComponent {
  private readonly _dialogRef = inject(MatDialogRef<AiReviewModalComponent>);
  private readonly _data: AiReviewModalData = inject(MAT_DIALOG_DATA);

  public get reviewHtml(): string {
    return marked.parse(this._data.review.reviewMarkdown, { async: false }) as string;
    // Angular's [innerHTML] binding auto-sanitizes this — no bypassSecurityTrustHtml needed
  }

  public get modelName(): string {
    return this._data.review.modelName;
  }

  public get reviewDate(): string {
    return new Date(this._data.review.createdAtUtc).toLocaleString();
  }

  public onClose(): void {
    this._dialogRef.close();
  }
}
```

```html
<!-- ai-review-modal.component.html — new file -->
<h2 mat-dialog-title>
  <mat-icon>rate_review</mat-icon>
  AI Strategy Review
</h2>

<mat-dialog-content class="ai-review-modal__content">
  <p class="ai-review-modal__meta">{{ modelName }} · {{ reviewDate }}</p>
  <div class="ai-review-modal__markdown" [innerHTML]="reviewHtml"></div>
</mat-dialog-content>

<mat-dialog-actions align="end">
  <button mat-stroked-button disabled matTooltip="Coming Soon">
    <mat-icon>auto_fix_high</mat-icon>
    Apply Suggestions
  </button>
  <button mat-raised-button color="primary" (click)="onClose()">Close</button>
</mat-dialog-actions>
```

```scss
// ai-review-modal.component.scss — new file
.ai-review-modal {
  &__content {
    max-height: 70vh;
    overflow-y: auto;
  }

  &__meta {
    color: rgba(0, 0, 0, 0.6);
    font-size: 0.875rem;
    margin-bottom: 1rem;
  }

  &__markdown {
    font-size: 0.9375rem;
    line-height: 1.6;

    ::ng-deep h1, ::ng-deep h2, ::ng-deep h3 {
      margin: 1rem 0 0.5rem;
    }

    ::ng-deep ul {
      padding-left: 1.5rem;
    }

    ::ng-deep p {
      margin: 0.5rem 0;
    }
  }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/order-entry/confirm-dialog/confirm-dialog.component.ts` — Dialog component with MAT_DIALOG_DATA, MatDialogRef, mat-dialog-title/content/actions template structure

---

### Task 4.6: Integrate AI Review button and card into strategy builder page {#task-46-integrate-ai-review-button-and-card-into-strategy-builder-page}

Add the "AI Review" button to the header actions, the review card to the side column, and wire up the review request flow.

- **Complexity**: High
- **Risk Factors**: Must coordinate button state (disabled when unsaved or in cooldown), loading state, review fetch/display, and modal opening
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` - Modify
  - `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.html` - Modify
- **Success**:
  - "AI Review" button visible only for saved strategies (when `editId` is set)
  - Button disabled when strategy not saved, review in progress, or cooldown active
  - Spinner shown on button during review request
  - Review card appears in side column after review completes
  - "View Full Review" opens the modal
  - Error shown via NotificationService on failure
  - Existing review loaded when viewing a saved strategy
- **Dependencies**: Task 4.3, Task 4.4, Task 4.5

#### Implementation Details

```typescript
// strategy-builder-page.component.ts — modifications

// Add imports:
import { AiReviewCardComponent } from "./components/ai-review-card/ai-review-card.component";
import { AiReviewModalComponent, AiReviewModalData } from "./components/ai-review-modal/ai-review-modal.component";
import { StrategyReviewDto } from "./models/strategy-review.model";

// Add to component imports array:
// AiReviewCardComponent

// Add properties to the class:
public currentReview: StrategyReviewDto | null = null;
public isReviewing: boolean = false;
public reviewCooldownSeconds: number = 0;
private _cooldownIntervalId: ReturnType<typeof setInterval> | null = null;

// Add method to request review:
public onRequestReview(): void {
  if (!this.editId || this.isReviewing || this.reviewCooldownSeconds > 0) {
    return;
  }

  const currentVersion = this._currentVersion; // the current revision number
  this.isReviewing = true;

  this._strategyApi.requestReview(this.editId, currentVersion, this._localErrorContext)
    .subscribe({
      next: (review) => {
        this.currentReview = review;
        this.isReviewing = false;
        this._startCooldown();
        this._notifications.success("AI review completed.");
      },
      error: (err: HttpErrorResponse) => {
        this.isReviewing = false;
        if (err.status === 429) {
          this._notifications.warning("Too many review requests. Please wait a moment.");
          this._startCooldown();
        } else {
          this._notifications.error("Failed to generate AI review. Please try again.");
        }
      },
    });
}

// Add method to open full review modal:
public onViewFullReview(): void {
  if (!this.currentReview) {
    return;
  }

  this._dialog.open(AiReviewModalComponent, {
    data: { review: this.currentReview } as AiReviewModalData,
    width: "700px",
    maxHeight: "85vh",
  });
}

// Add method for cooldown timer:
private _startCooldown(): void {
  this.reviewCooldownSeconds = 60;

  if (this._cooldownIntervalId) {
    clearInterval(this._cooldownIntervalId);
  }

  this._cooldownIntervalId = setInterval(() => {
    this.reviewCooldownSeconds--;
    if (this.reviewCooldownSeconds <= 0) {
      this.reviewCooldownSeconds = 0;
      if (this._cooldownIntervalId) {
        clearInterval(this._cooldownIntervalId);
        this._cooldownIntervalId = null;
      }
    }
  }, 1000);
}

// Add method to load existing review when strategy loads:
private _loadExistingReview(): void {
  if (!this.editId) {
    return;
  }

  this._strategyApi.getReview(this.editId, this._currentVersion, this._localErrorContext)
    .subscribe({
      next: (review) => {
        this.currentReview = review;
      },
      error: () => {
        // 404 is expected if no review exists — silently ignore
        this.currentReview = null;
      },
    });
}
```

Call `this._loadExistingReview()` after the strategy is loaded in the existing `ngOnInit` or `loadStrategy` flow.

```html
<!-- strategy-builder-page.component.html — modifications -->

<!-- Add AI Review button in the actions bar, after the Backtest button, inside @if (editId): -->
@if (editId) {
  <button mat-stroked-button type="button" color="primary" (click)="onBacktestStrategy()">
    <mat-icon>science</mat-icon>
    <span>Backtest this strategy</span>
  </button>

  <button mat-stroked-button type="button" color="accent"
    [disabled]="isReviewing || reviewCooldownSeconds > 0"
    [matTooltip]="!editId ? 'Save the strategy first' : reviewCooldownSeconds > 0 ? 'Cooldown: ' + reviewCooldownSeconds + 's' : ''"
    (click)="onRequestReview()">
    @if (isReviewing) {
      <mat-spinner diameter="18"></mat-spinner>
    } @else {
      <mat-icon>rate_review</mat-icon>
      <span>AI Review{{ reviewCooldownSeconds > 0 ? ' (' + reviewCooldownSeconds + 's)' : '' }}</span>
    }
  </button>
}

<!-- Add AI review card in the side column, after the validation card: -->
<app-ai-review-card
  [review]="currentReview"
  (viewFullReview)="onViewFullReview()"
/>
```

Add `MatTooltipModule` to the component imports if not already present.

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.html` — Button pattern with `@if (editId)`, spinner in button, disabled state
- `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` — Service call pattern with subscribe, error handling, NotificationService

---

### Task 4.7: Implement cooldown timer logic {#task-47-implement-cooldown-timer-logic}

Ensure the cooldown timer properly cleans up on component destroy and handles edge cases.

- **Complexity**: Low
- **Risk Factors**: Timer leak if component is destroyed during cooldown
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` - Modify (add cleanup)
- **Success**:
  - Cooldown interval cleared on component destroy
  - No memory leaks
- **Dependencies**: Task 4.6

#### Implementation Details

Add cleanup logic using `DestroyRef` (already injected in the component):

```typescript
// In ngOnInit or constructor, add cleanup:
this._destroyRef.onDestroy(() => {
  if (this._cooldownIntervalId) {
    clearInterval(this._cooldownIntervalId);
    this._cooldownIntervalId = null;
  }
});
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` — Existing `_destroyRef` usage for cleanup

---

### Task 4.8: Build and lint frontend {#task-48-build-and-lint-frontend}

Build and lint the Angular project to verify all changes compile and pass lint rules.

- **Complexity**: Low
- **Risk Factors**: Import resolution, template type errors
- **Files**: None (verification only)
- **Success**:
  - `ng build` completes without errors
  - `ng lint` passes without errors (if lint is configured)
- **Dependencies**: All previous tasks in Phase 4

Run:
```bash
cd frontend/trading-ui
npx ng build
npx ng lint
```

## Phase Success Criteria

- `marked` package installed for markdown rendering
- AI review model, service methods, card component, and modal component created
- "AI Review" button integrated into strategy builder page with cooldown timer
- "Apply Suggestions" button rendered as disabled with "Coming Soon" tooltip
- Existing reviews loaded when viewing a saved strategy
- Review card appears in side column; full modal accessible via "View Full Review"
- Frontend builds and lints successfully
