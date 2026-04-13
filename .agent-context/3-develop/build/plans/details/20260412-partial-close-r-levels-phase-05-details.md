<!-- markdownlint-disable-file -->

# Task Details: Partial Close at R-Levels

## Phase 5: Frontend Tranche Editor

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — standalone components, `inject()`, member ordering, SCSS, DDX/DDS components
- `.github/instructions/csharp.instructions.md` — N/A (frontend only)
- `.agent-context/0-knowledge/12-strategy-customisation.md` — Strategy CRUD, frontend integration

## Design References

- Exit config form is a `FormGroup` with `takeProfit`, `stopLoss`, `exitOnOppositeSignal` sub-groups. Adding `tranches` as a `FormArray` follows the same pattern as `conditions` FormArray in `EntryConditionsCardComponent`.
- `ExitRulesCardComponent` receives `@Input() group: FormGroup` — the full `exit` FormGroup. The tranche `FormArray` is accessed as `this.group.get("tranches") as FormArray`.
- Tranche editor should only be visible when the strategy uses `RiskBased` position sizing. The `PositionSizeType` is available on the parent form.
- `StrategyMapperService.mapFormToConfig()` maps raw form values to `StrategyConfig` — must be extended to map tranches.
- `StrategyValidationService.validate()` handles client-side cross-field validation — must add tranche validation.
- `patchValue` does not work for FormArray — need a manual `_loadTranches()` helper like `_addLoadedCondition`.

### Task 5.1: Add `PartialCloseTranche` model and extend `ExitConfig` interface {#task-51-add-partialclosetranche-model-and-extend-exitconfig}

Add the TypeScript interface for partial close tranches and extend the `ExitConfig` interface.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts` — Add interface and extend ExitConfig
- **Success**:
  - `PartialCloseTranche` interface exists with `atRMultiple` and `closePercent` properties
  - `ExitConfig.partialCloses` is an optional `PartialCloseTranche[] | null`
  - Naming matches camelCase JSON serialization from backend

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts — additions

export interface PartialCloseTranche {
  atRMultiple: number;
  closePercent: number;
}

export interface ExitConfig {
  takeProfit: ExitRuleConfig;
  stopLoss: ExitRuleConfig;
  exitOnOppositeSignal: boolean;
  partialCloses?: PartialCloseTranche[] | null;  // NEW
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts` — existing ExitConfig/ExitRuleConfig interfaces

### Task 5.2: Add tranche `FormArray` to strategy builder form and mapper {#task-52-add-tranche-formarray-to-strategy-builder}

Add the `partialCloses` `FormArray` to the exit `FormGroup` in `_buildForm()`, extend the mapper to read it, and add a `_loadTranches()` helper for patchValue flows.

- **Complexity**: Medium
- **Risk Factors**: FormArray cannot be patched via `patchValue` — needs manual loading. Must handle empty/null tranches on load.
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` — Add FormArray and load helper
  - `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.ts` — Map tranches to config
- **Success**:
  - `exit.partialCloses` FormArray exists in the form
  - `_loadTranches()` correctly populates FormArray from loaded strategy config
  - `mapFormToConfig()` maps FormArray values to `PartialCloseTranche[]`
  - Duplicate/load/new strategy flows all work

#### Implementation Details

> **Note**: Use `inject(NonNullableFormBuilder)` per Angular instructions — the `_fb` references below assume this pattern is already in use in the component.

```typescript
exit: this._fb.group({
  // ... existing controls ...
  partialCloses: this._fb.array([]),  // NEW
}),

// Add helper method:
private _loadTranches(tranches: PartialCloseTranche[] | null | undefined): void {
  const formArray = this.form.get("exit.partialCloses") as FormArray;
  formArray.clear();
  if (!tranches?.length) return;

  for (const tranche of tranches) {
    formArray.push(this._fb.group({
      atRMultiple: [tranche.atRMultiple, [Validators.required, Validators.min(0.1)]],
      closePercent: [tranche.closePercent, [Validators.required, Validators.min(1), Validators.max(100)]],
    }));
  }
}

// Call in _loadStrategy / _populateFromConfig / _duplicateStrategy:
this._loadTranches(config.exit?.partialCloses);
```

```typescript
// strategy-mapper.service.ts — in mapFormToConfig(), exit section:
const tranchesRaw = (exit["partialCloses"] ?? []) as Record<string, unknown>[];
// ...
partialCloses: tranchesRaw.length > 0
  ? tranchesRaw.map(t => ({
      atRMultiple: Number(t["atRMultiple"]),
      closePercent: Number(t["closePercent"]),
    }))
  : null,
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/components/entry-conditions-card/entry-conditions-card.component.ts` — FormArray add/remove pattern
- `frontend/trading-ui/src/app/features/strategy-builder/services/condition-factory.service.ts` — FormGroup factory pattern

### Task 5.3: Add tranche editor UI to exit-rules-card component {#task-53-add-tranche-editor-ui-to-exit-rules-card}

Add a tranche editor section to the exit-rules-card component. Shows a list of R-level tranches with add/remove buttons. Only visible when `PositionSizeType = RiskBased`.

- **Complexity**: Medium
- **Risk Factors**: Must receive `positionSizeType` signal/input to conditionally show section. Must follow DDX/DDS component patterns.
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/components/exit-rules-card/exit-rules-card.component.ts` — Add tranche management logic
  - `frontend/trading-ui/src/app/features/strategy-builder/components/exit-rules-card/exit-rules-card.component.html` — Add tranche editor template
  - `frontend/trading-ui/src/app/features/strategy-builder/components/exit-rules-card/exit-rules-card.component.scss` — Add tranche editor styles
- **Success**:
  - Tranche section visible only when `positionSizeType === 'risk_based'`
  - Each tranche row shows R-multiple input and Close % input with remove button
  - "Add Tranche" button adds a new row with defaults
  - Sum of close percentages displayed with warning if > 100 or warning if < 100 with no trailing stop

#### Implementation Details

The parent `strategy-builder-page.component.html` must pass the `positionSizeType` value to the exit-rules-card:
```html
<app-exit-rules-card [group]="exitGroup" [positionSizeType]="form.get('risk.positionSizeType')?.value">
</app-exit-rules-card>
```

```typescript
// exit-rules-card.component.ts — additions

@Input() public positionSizeType: PositionSizeType = "percent_wallet";

public get partialClosesArray(): FormArray {
  return this.group.get("partialCloses") as FormArray;
}

public get isRiskBased(): boolean {
  return this.positionSizeType === "risk_based";
}

public get totalClosePercent(): number {
  return this.partialClosesArray.controls
    .reduce((sum, ctrl) => sum + (Number(ctrl.get("closePercent")?.value) || 0), 0);
}

public addTranche(): void {
  this.partialClosesArray.push(this._fb.group({
    atRMultiple: [1.0, [Validators.required, Validators.min(0.1)]],
    closePercent: [25, [Validators.required, Validators.min(1), Validators.max(100)]],
  }));
}

public removeTranche(index: number): void {
  this.partialClosesArray.removeAt(index);
}
```

```html
<!-- exit-rules-card.component.html — add after existing exit config sections -->

@if (isRiskBased) {
  <div class="partial-closes-section">
    <h4>Partial Close at R-Levels</h4>
    <p class="section-description">
      Scale out of winning positions at R-multiple milestones.
      Leave empty to use single take-profit.
    </p>

    @for (ctrl of partialClosesArray.controls; track $index; let i = $index) {
      <div class="tranche-row" [formGroup]="$any(ctrl)">
        <mat-form-field>
          <mat-label>At R-Multiple</mat-label>
          <input matInput type="number" formControlName="atRMultiple" step="0.5" min="0.1" />
        </mat-form-field>

        <mat-form-field>
          <mat-label>Close %</mat-label>
          <input matInput type="number" formControlName="closePercent" step="5" min="1" max="100" />
        </mat-form-field>

        <button mat-icon-button color="warn" (click)="removeTranche(i)" aria-label="Remove tranche">
          <mat-icon>delete</mat-icon>
        </button>
      </div>
    }

    <button mat-stroked-button (click)="addTranche()" type="button">
      <mat-icon>add</mat-icon> Add Tranche
    </button>

    @if (totalClosePercent > 0) {
      <div class="tranche-summary">
        Total: {{ totalClosePercent }}%
        @if (totalClosePercent > 100) {
          <span class="error-text">exceeds 100%</span>
        }
        @if (totalClosePercent < 100) {
          <span class="info-text">{{ 100 - totalClosePercent }}% managed by SL/trailing stop</span>
        }
      </div>
    }
  </div>
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/components/entry-conditions-card/entry-conditions-card.component.html` — FormArray iteration with `@for`, add/remove buttons
- `frontend/trading-ui/src/app/features/strategy-builder/components/exit-rules-card/exit-rules-card.component.html` — existing exit config card template

### Task 5.4: Add frontend validation for tranches {#task-54-add-frontend-validation-for-tranches}

Add client-side validation in `StrategyValidationService` for partial close tranches.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.ts` — Add tranche validation
- **Success**:
  - Validation error if sum of `closePercent` > 100
  - Validation error if any `atRMultiple` ≤ 0
  - Validation error if duplicate R-levels
  - Warning if no trailing stop configured on remainder (< 100% total)
  - No errors when tranches are empty or null

#### Implementation Details

```typescript
// strategy-validation.service.ts — add to validate() method

private _validateTranches(exit: Record<string, unknown>): ValidationResult[] {
  const tranches = (exit["partialCloses"] ?? []) as Record<string, unknown>[];
  if (tranches.length === 0) return [];

  const results: ValidationResult[] = [];
  const totalPercent = tranches.reduce((sum, t) => sum + Number(t["closePercent"]), 0);

  if (totalPercent > 100) {
    results.push({ code: "PARTIAL_CLOSE_PERCENT_EXCEEDS_100", level: "error",
      message: `Partial close percentages sum to ${totalPercent}%, must not exceed 100%` });
  }

  const rLevels = tranches.map(t => Number(t["atRMultiple"]));
  const duplicates = rLevels.filter((r, i) => rLevels.indexOf(r) !== i);
  if (duplicates.length > 0) {
    results.push({ code: "PARTIAL_CLOSE_DUPLICATE_R", level: "error",
      message: `Duplicate R-levels: ${[...new Set(duplicates)].join(", ")}` });
  }

  return results;
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.ts` — existing `_validateExitRule()` pattern

### Task 5.5: Run frontend build and lint {#task-55-run-frontend-build-and-lint}

Run the Angular build and lint to verify no compilation or style errors.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (build execution only)
- **Success**:
  - `ng build` completes without errors
  - `npm run lint` passes

## Phase Success Criteria

- Tranche editor visible in strategy builder when `PositionSizeType = RiskBased`
- Add/remove tranche rows work correctly
- Form values correctly map to `ExitConfig.partialCloses` in the request body
- Client-side validation covers all edge cases
- Existing strategies without partial closes load correctly (empty FormArray)
- Frontend builds and lints clean
