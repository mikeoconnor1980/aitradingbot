<!-- markdownlint-disable-file -->

# Task Details: R-Multiple Exit Types & Trade Tracking

## Phase 5: Frontend — Strategy Configuration

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — standalone components, `inject()`, DDX design system, double quotes, SCSS
- Frontend models in `frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts`
- Frontend exit-rules-card already has disabled `risk_reward` placeholder option

### Task 5.1: Add `r_multiple` to TypeScript ExitRuleType {#task-51-add-r_multiple-to-typescript-exitruletype}

Add `"r_multiple"` to the `ExitRuleType` union type.

- **Complexity**: Low
- **Risk Factors**: None — must match the C# snake_case serialization
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts` — update type
  - `frontend/trading-ui/src/app/core/models/backtest.model.ts` — update `BacktestExitRuleConfig` if it has a separate type
- **Success**:
  - `"r_multiple"` is a valid `ExitRuleType` value in TypeScript
- **Dependencies**: None (frontend-only)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts — modification
export type ExitRuleType = "fixed_percent" | "swing_low" | "atr_trailing" | "r_multiple";
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts` — existing `ExitRuleType` union

### Task 5.2: Enable R-multiple option in exit-rules-card {#task-52-enable-r-multiple-option-in-exit-rules-card}

Enable the existing disabled "Risk reward" placeholder in the exit-rules-card component, rename it to "R-multiple", and add a conditional input field for the R target value.

- **Complexity**: Low
- **Risk Factors**: None — placeholder already exists
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/components/exit-rules-card/exit-rules-card.component.html` — enable option, add conditional input
  - `frontend/trading-ui/src/app/features/strategy-builder/components/exit-rules-card/exit-rules-card.component.ts` — add computed property for R-multiple mode
  - `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` — update form group for TP type
- **Success**:
  - "R-multiple" appears as a selectable TP type
  - Selecting R-multiple shows an R target input field (default: 2.0)
  - TP value input label changes to "R Target" when R-multiple is selected
- **Dependencies**: Task 5.1

#### Implementation Details

```html
<!-- exit-rules-card.component.html — modification -->
<!-- Replace the disabled risk_reward placeholder: -->
<!-- BEFORE: <mat-option value="risk_reward" disabled>Risk reward (coming soon)</mat-option> -->
<!-- AFTER: -->
<mat-option value="r_multiple">R-multiple</mat-option>
```

Update the TP value input label to be dynamic:

```html
<!-- When type is r_multiple, show "R Target" label and hint, otherwise show "%" -->
@if (takeProfitType() === "r_multiple") {
  <mat-form-field>
    <mat-label>R Target</mat-label>
    <input matInput type="number" formControlName="value" step="0.5" min="0.1">
    <mat-hint>e.g. 2.0 = 2× your risk</mat-hint>
  </mat-form-field>
} @else {
  <!-- existing percent input -->
}
```

```typescript
// exit-rules-card.component.ts — modification
// Add signal/computed for TP type:
public takeProfitType(): string {
  return this.form?.get("takeProfit.type")?.value ?? "fixed_percent";
}
```

Update `strategy-builder-page.component.ts` form to use the type from the dropdown:

```typescript
// strategy-builder-page.component.ts — modification
takeProfit: this._fb.group({
  enabled: [true],
  type: ["fixed_percent"],  // now dynamically set from dropdown
  value: [2, [Validators.min(0.01), Validators.max(50)]],
}),
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/components/exit-rules-card/exit-rules-card.component.html` — existing disabled `risk_reward` option
- `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` — existing form group

### Task 5.3: Update strategy-mapper.service.ts {#task-53-update-strategy-mapper}

Fix the hardcoded `"fixed_percent"` in the mapper to read the actual type from the form.

- **Complexity**: Low
- **Risk Factors**: Critical fix — currently the mapper ignores the selected TP type
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.ts` — fix type mapping
- **Success**:
  - Selected TP type flows through to the API request
  - R-multiple type sends `"r_multiple"` to backend
- **Dependencies**: Task 5.1

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.ts — modification
// BEFORE:
// type: "fixed_percent",
// AFTER:
type: (takeProfit["type"] as string) || "fixed_percent",
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.ts` — existing mapping (line ~59)

### Task 5.4: Add sub-1R warning {#task-54-add-sub-1r-warning}

Show a warning message when the R-multiple TP value is between 0 and 1 (sub-1R trade).

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/components/exit-rules-card/exit-rules-card.component.html` — add warning text
- **Success**:
  - Warning text appears when TP type is R-multiple and value < 1
  - Warning disappears when value >= 1
- **Dependencies**: Task 5.2

#### Implementation Details

```html
<!-- exit-rules-card.component.html — modification -->
<!-- Add after the R Target input field: -->
@if (takeProfitType() === "r_multiple" && form.get("takeProfit.value")?.value < 1) {
  <mat-hint class="warn-hint">
    Sub-1R trade — relies on high win rate to be profitable
  </mat-hint>
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/components/exit-rules-card/exit-rules-card.component.html` — existing hint patterns

### Task 5.5: Frontend build and lint {#task-55-frontend-build-and-lint}

Run frontend build and lint to verify no errors.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None
- **Success**:
  - `npm run build` succeeds
  - `npm run lint` reports no errors
- **Dependencies**: Task 5.4

## Phase Success Criteria

- R-multiple is a selectable TP type in the strategy builder
- Strategy mapper sends the correct type to the API
- Sub-1R warning is displayed
- Frontend builds and lints cleanly
