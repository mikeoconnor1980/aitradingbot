<!-- markdownlint-disable-file -->

# Task Details: F6 — UI: RSI Condition Card + Signal Mode

## Phase 3: Page Integration — Wiring, Mapper, Validation, Preview & Tests

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — Standalone components, `inject()` DI, `takeUntilDestroyed`, modern `@if`/`@for` control flow
- `.github/instructions/testing.instructions.md` — MSTest naming conventions (Given_When_Then) for reference; Angular tests use Jasmine/Karma with same naming spirit
- `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.spec.ts` — Existing page tests: `jasmine.createSpyObj`, `TestBed.configureTestingModule`, form value assertions
- `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.spec.ts` — Existing validation tests: `fieldPath` + `severity` + `code` assertion pattern

## Design References

- PBI acceptance criteria: preview shows "Enter a long trade on BTC-USD 15m when RSI(14) is below 40."
- Backend `CrossFieldValidator`: signal mode requires non-empty `entryConditions` + `entryLogic`
- Backend `BusinessRuleValidator`: RSI period > 0, value 0–100
- Operator display mapping for preview: `lt` → "below", `lte` → "at or below", `gt` → "above", `gte` → "at or above", `cross_above` → "crosses above", `cross_below` → "crosses below"

---

### Task 3.1: Update page component for signal mode support {#task-31-update-page-component-for-signal-mode}

Update the `StrategyBuilderPageComponent` to: add a `conditions` FormArray to `_buildForm()`, add an `isSignalMode` getter, update `onTemplateSelected()` to switch modes, add `conditionsFormArray` getter, and update `_loadStrategy()` to handle signal-mode strategies.

- **Complexity**: High
- **Risk Factors**: Mode switching must correctly enable/disable form groups to avoid validation conflicts; loading strategy must populate FormArray from API response
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` — Modify
- **Success**:
  - `form` includes a `conditions` FormArray
  - `isSignalMode` returns true when `templateId` is `"custom_signal"`
  - Template selection switches between grid and signal mode, resetting conditions or grid as appropriate
  - `conditionsFormArray` getter returns the FormArray for passing to entry-conditions-card
  - Loading a signal-mode strategy populates the conditions FormArray from the API response
  - `pageSubtitle` updates for signal mode

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts — modification

// Add import for FormArray and ConditionFactoryService:
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from "@angular/forms";
import { ConditionFactoryService } from "./services/condition-factory.service";
// Merge with existing imports from strategy.model — add EntryConditionConfig to the existing import line:
import { EntryConditionConfig, ServerValidationResult, StrategyConfig, ValidationError } from "./models/strategy.model";

// Add to injected services:
private readonly _conditionFactory = inject(ConditionFactoryService);

// Add new getter:
public get isSignalMode(): boolean {
  return this.selectedTemplateId === "custom_signal";
}

// Add conditionsFormArray getter:
public get conditionsFormArray(): FormArray {
  return this.form.get("conditions") as FormArray;
}

// Update pageSubtitle getter to handle signal mode:
public get pageSubtitle(): string {
  if (this.editId !== null) {
    return "Update the saved strategy configuration and review the resulting JSON.";
  }
  return this.isSignalMode
    ? "Build a signal strategy with entry conditions."
    : "Build a grid strategy with the visual editor.";
}

// Update onTemplateSelected to handle mode switching:
public onTemplateSelected(templateId: string): void {
  this.form.patchValue({ templateId });

  if (templateId === "custom_signal") {
    this.form.get("grid")?.disable();
  } else {
    this.form.get("grid")?.enable();
    this._clearConditions();
  }
}

// Add private helper to clear conditions:
private _clearConditions(): void {
  const conditions = this.conditionsFormArray;
  while (conditions.length > 0) {
    conditions.removeAt(0);
  }
}

// Update _buildForm to include conditions FormArray:
// In _buildForm(), add this field to the FormGroup after the 'metadata' group:
//   conditions: this._fb.array([]),

// Update _loadStrategy to handle signal-mode strategies.
// After existing grid patchValue, add signal-mode branch:
// In the next callback of _loadStrategy, after form.patchValue:
//
// if (strategy.config.strategyMode === "signal") {
//   this.form.patchValue({ templateId: strategy.config.templateId ?? "custom_signal" });
//   this.form.get("grid")?.disable();
//   this._clearConditions();
//   if (strategy.config.entryConditions) {
//     for (const condition of strategy.config.entryConditions) {
//       if (condition.type === "rsi") {
//         const params = condition.params as { period: number; operator: string; value: number };
//         this.conditionsFormArray.push(this._conditionFactory.createRsiCondition({
//           id: condition.id,
//           enabled: condition.enabled,
//           label: condition.label,
//           period: params.period,
//           operator: params.operator,
//           value: params.value,
//         }));
//       }
//     }
//   }
// }
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` — existing `_buildForm()`, `onTemplateSelected()`, `_loadStrategy()`, getter patterns
- `frontend/trading-ui/src/app/features/strategy-builder/components/exit-rules-card/exit-rules-card.component.ts` — `enable()`/`disable()` control pattern for conditional form groups

---

### Task 3.2: Update page template for conditional card rendering {#task-32-update-page-template}

Update the page HTML template to: conditionally show grid card OR entry conditions card based on `isSignalMode`, and pass the `conditionsFormArray` to the entry conditions card.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.html` — Modify
- **Success**:
  - Grid card hidden when `isSignalMode` is true
  - Entry conditions card shown with `[conditions]` input when `isSignalMode` is true
  - Entry conditions card hidden (or shows disabled stub) when not in signal mode
  - Trend filter card remains as disabled stub

#### Implementation Details

```html
<!-- frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.html — modification -->

<!-- Replace the grid-config-card and entry-conditions-card lines with: -->

@if (!isSignalMode) {
  <app-grid-config-card [group]="gridFormGroup" />
}

<!-- ... existing exit-rules-card, risk-management-card, trend-filter-card ... -->

@if (isSignalMode) {
  <app-entry-conditions-card [conditions]="conditionsFormArray" />
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.html` — existing card layout with `@if` control flow

---

### Task 3.3: Update mapper service for signal mode {#task-33-update-mapper-for-signal-mode}

Update `StrategyMapperService.mapFormToConfig()` to branch on `templateId`/strategy mode and produce correct signal-mode canonical JSON.

- **Complexity**: Medium
- **Risk Factors**: Condition serialization must match backend `EntryConditionConfig` shape exactly; `entryLogic` hardcoded to `"all"` for now
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.ts` — Modify
  - `frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts` — Import types (already done in Phase 1)
- **Success**:
  - Signal mode: `strategyMode: "signal"`, `grid: null`, `entryLogic: "all"`, `entryConditions: [...]`
  - Grid mode: unchanged behaviour (`strategyMode: "grid"`, `grid: {...}`, `entryConditions: null`)
  - Each condition maps: `{ id, enabled, type: "rsi", label, params: { period, operator, value } }`

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.ts — modification

// Add imports:
import { EntryConditionConfig } from "../models/strategy.model";

// In mapFormToConfig(), replace the hardcoded strategyMode/grid/entryConditions/entryLogic lines:

public mapFormToConfig(formValue: Record<string, unknown>): StrategyConfig {
  // ... existing exit/risk/metadata extraction ...

  const templateId = String(formValue["templateId"] ?? "grid");
  const isSignalMode = templateId === "custom_signal";
  const conditions = (formValue["conditions"] ?? []) as Record<string, unknown>[];

  return {
    schemaVersion: 1,
    strategyMode: isSignalMode ? "signal" : "grid",
    // ... existing strategyName, exchange, market, timeframe, direction, enabled, templateId ...
    grid: isSignalMode ? null : {
      // ... existing grid mapping ...
    },
    trendFilter: null,
    entryLogic: isSignalMode ? "all" : null,
    entryConditions: isSignalMode ? this._mapConditions(conditions) : null,
    // ... existing exit, risk, metadata, source ...
  };
}

private _mapConditions(conditions: Record<string, unknown>[]): EntryConditionConfig[] {
  return conditions.map((c) => ({
    id: String(c["id"] ?? ""),
    enabled: Boolean(c["enabled"] ?? true),
    type: String(c["type"] ?? "rsi") as import("../models/strategy.model").EntryConditionType,
    label: String(c["label"] ?? ""),
    params: {
      period: Number(c["period"] ?? 14),
      operator: String(c["operator"] ?? "lt") as import("../models/strategy.model").RsiOperator,
      value: Number(c["value"] ?? 40),
    },
  }));
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.ts` — existing `mapFormToConfig()` with grid extraction pattern
- `src/TradingApp.Application/StrategyAuthoring/Models/EntryConditionConfig.cs` — backend shape: `{ Id, Enabled, Type, Label, Params: { Period, Operator, Value } }`

---

### Task 3.4: Update client validation service for signal mode {#task-34-update-validation-for-signal-mode}

Update `StrategyValidationService.validate()` to skip grid validation in signal mode and add signal-mode-specific validations.

- **Complexity**: Medium
- **Risk Factors**: Must not break existing grid validation; condition validation must mirror backend rules
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.ts` — Modify
- **Success**:
  - Signal mode: grid validation skipped, no "Grid configuration is required" error
  - Signal mode with no conditions: error "At least one entry condition required"
  - Signal mode with RSI period < 1: error on `entryConditions[n].params.period`
  - Signal mode with RSI value outside 0–100: error on `entryConditions[n].params.value`
  - Grid mode: unchanged behaviour

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.ts — modification

// In validate(), replace the grid validation block with mode-aware logic:

public validate(formValue: Record<string, unknown>): ValidationError[] {
  const errors: ValidationError[] = [];
  const templateId = String(formValue["templateId"] ?? "grid");
  const isSignalMode = templateId === "custom_signal";

  // ... existing name, market, timeframe validation (unchanged) ...

  if (isSignalMode) {
    this._validateSignalMode(formValue, errors);
  } else {
    // ... existing grid validation block (unchanged) ...
  }

  // ... existing exit/risk validation (unchanged) ...

  return errors;
}

private _validateSignalMode(formValue: Record<string, unknown>, errors: ValidationError[]): void {
  const conditions = (formValue["conditions"] ?? []) as Record<string, unknown>[];

  if (conditions.length === 0) {
    errors.push(this._error("entryConditions", "REQUIRED", "At least one entry condition required."));
    return;
  }

  conditions.forEach((condition, index) => {
    const period = Number(condition["period"] ?? 0);
    const value = Number(condition["value"] ?? -1);

    if (period < 1) {
      errors.push(this._error(`entryConditions[${index}].params.period`, "RANGE", "RSI period must be at least 1."));
    }

    if (value < 0 || value > 100) {
      errors.push(this._error(`entryConditions[${index}].params.value`, "RANGE", "RSI value must be between 0 and 100."));
    }
  });
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.ts` — existing `validate()` method with `_error()` helper
- `src/TradingApp.Application/StrategyAuthoring/Validation/CrossFieldValidator.cs` — backend: `ENTRY_CONDITIONS_REQUIRED_FOR_SIGNAL_MODE`
- `src/TradingApp.Application/StrategyAuthoring/Validation/BusinessRuleValidator.cs` — backend: `RSI_PERIOD_INVALID`, `RSI_VALUE_INVALID`

---

### Task 3.5: Update preview summary card for signal mode {#task-35-update-preview-for-signal-mode}

Update `PreviewSummaryCardComponent` to generate signal-mode preview text instead of falling through to the "Fill in the form" fallback.

- **Complexity**: Medium
- **Risk Factors**: Must handle multiple conditions gracefully; operator display text must be human-readable
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/components/preview-summary-card/preview-summary-card.component.ts` — Modify
- **Success**:
  - Single RSI condition: "Enter a long trade on BTC-USD 15m when RSI(14) is below 40."
  - Multiple conditions: joined by " and " (matching `entryLogic: "all"`)
  - Grid mode: unchanged behaviour

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/components/preview-summary-card/preview-summary-card.component.ts — modification

// Add to imports:
import { RsiOperator } from "../../models/strategy.model";

// In the previewText getter, replace the `if (grid === null)` early return with mode-aware branching:

public get previewText(): string {
  const formValue = this._formValue();

  if (formValue === null) {
    return "Fill in the form to see a preview.";
  }

  const templateId = String(formValue["templateId"] ?? "grid");
  const isSignalMode = templateId === "custom_signal";

  if (isSignalMode) {
    return this._buildSignalPreview(formValue);
  }

  // ... existing grid preview logic (unchanged, but remove the grid === null early return) ...
}

private _buildSignalPreview(formValue: Record<string, unknown>): string {
  const conditions = (formValue["conditions"] ?? []) as Record<string, unknown>[];
  const direction = String(formValue["direction"] ?? "long");
  const market = String(formValue["market"] ?? "market");
  const timeframe = String(formValue["timeframe"] ?? "timeframe");

  if (conditions.length === 0) {
    return "Add entry conditions to see a preview.";
  }

  const conditionTexts = conditions
    .filter((c) => Boolean(c["enabled"] ?? true))
    .map((c) => {
      const period = Number(c["period"] ?? 14);
      const operator = String(c["operator"] ?? "lt") as RsiOperator;
      const value = Number(c["value"] ?? 0);
      return `RSI(${period}) ${this._operatorText(operator)} ${value}`;
    });

  if (conditionTexts.length === 0) {
    return "All conditions are disabled.";
  }

  const joined = conditionTexts.join(" and ");
  return `Enter a ${direction} trade on ${market} ${timeframe} when ${joined}.`;
}

private _operatorText(operator: RsiOperator): string {
  const map: Record<RsiOperator, string> = {
    lt: "is below",
    lte: "is at or below",
    gt: "is above",
    gte: "is at or above",
    cross_above: "crosses above",
    cross_below: "crosses below",
  };
  return map[operator] ?? operator;
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/components/preview-summary-card/preview-summary-card.component.ts` — existing `previewText` getter and `_formatNumber()` pattern

---

### Task 3.6: Add unit tests for condition factory {#task-36-unit-tests-condition-factory}

Create unit tests for `ConditionFactoryService` to verify default values, validators, and override support.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/services/condition-factory.service.spec.ts` — New file
- **Success**:
  - Tests pass: default RSI values (period=14, operator="lt", value=40, enabled=true, type="rsi")
  - Tests pass: overrides applied correctly
  - Tests pass: validation rejects period < 1, value > 100, value < 0
  - Tests pass: unique IDs generated for each condition

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/services/condition-factory.service.spec.ts — new file

import { TestBed } from "@angular/core/testing";
import { ConditionFactoryService } from "./condition-factory.service";

describe("ConditionFactoryService", () => {
  let service: ConditionFactoryService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(ConditionFactoryService);
  });

  describe("createRsiCondition", () => {
    it("should create a FormGroup with RSI defaults", () => {
      const group = service.createRsiCondition();
      expect(group.get("type")?.value).toBe("rsi");
      expect(group.get("period")?.value).toBe(14);
      expect(group.get("operator")?.value).toBe("lt");
      expect(group.get("value")?.value).toBe(40);
      expect(group.get("enabled")?.value).toBeTrue();
      expect(group.get("label")?.value).toBe("");
    });

    it("should apply overrides", () => {
      const group = service.createRsiCondition({ period: 7, operator: "gte", value: 70, label: "Overbought" });
      expect(group.get("period")?.value).toBe(7);
      expect(group.get("operator")?.value).toBe("gte");
      expect(group.get("value")?.value).toBe(70);
      expect(group.get("label")?.value).toBe("Overbought");
    });

    it("should generate unique IDs", () => {
      const group1 = service.createRsiCondition();
      const group2 = service.createRsiCondition();
      expect(group1.get("id")?.value).not.toBe(group2.get("id")?.value);
    });

    it("should invalidate period less than 1", () => {
      const group = service.createRsiCondition({ period: 0 });
      expect(group.get("period")?.valid).toBeFalse();
    });

    it("should invalidate value greater than 100", () => {
      const group = service.createRsiCondition({ value: 101 });
      expect(group.get("value")?.valid).toBeFalse();
    });

    it("should invalidate value less than 0", () => {
      const group = service.createRsiCondition({ value: -1 });
      expect(group.get("value")?.valid).toBeFalse();
    });

    it("should accept value of exactly 0", () => {
      const group = service.createRsiCondition({ value: 0 });
      expect(group.get("value")?.valid).toBeTrue();
    });

    it("should accept value of exactly 100", () => {
      const group = service.createRsiCondition({ value: 100 });
      expect(group.get("value")?.valid).toBeTrue();
    });
  });
});
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.spec.ts` — Existing service test pattern with `TestBed.inject`

---

### Task 3.7: Add unit tests for mapper signal-mode branch {#task-37-unit-tests-mapper}

Add tests to verify the mapper produces correct signal-mode output and continues to produce correct grid-mode output.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.spec.ts` — New file (or extend existing if present)
- **Success**:
  - Signal mode: output has `strategyMode: "signal"`, `grid: null`, `entryLogic: "all"`, `entryConditions` array with correct shape
  - Grid mode: output has `strategyMode: "grid"`, `grid` populated, `entryConditions: null`

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.spec.ts — new file

import { TestBed } from "@angular/core/testing";
import { StrategyMapperService } from "./strategy-mapper.service";

describe("StrategyMapperService", () => {
  let service: StrategyMapperService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(StrategyMapperService);
  });

  it("should map grid-mode form to config with strategyMode grid", () => {
    const formValue = buildGridFormValue();
    const config = service.mapFormToConfig(formValue);

    expect(config.strategyMode).toBe("grid");
    expect(config.grid).not.toBeNull();
    expect(config.entryConditions).toBeNull();
    expect(config.entryLogic).toBeNull();
  });

  it("should map signal-mode form to config with strategyMode signal", () => {
    const formValue = buildSignalFormValue();
    const config = service.mapFormToConfig(formValue);

    expect(config.strategyMode).toBe("signal");
    expect(config.grid).toBeNull();
    expect(config.entryLogic).toBe("all");
    expect(config.entryConditions).not.toBeNull();
    expect(config.entryConditions!.length).toBe(1);
  });

  it("should map RSI condition params correctly", () => {
    const formValue = buildSignalFormValue();
    const config = service.mapFormToConfig(formValue);
    const condition = config.entryConditions![0];

    expect(condition.type).toBe("rsi");
    expect(condition.enabled).toBeTrue();
    expect(condition.params.period).toBe(14);
    expect(condition.params.operator).toBe("lt");
    expect(condition.params.value).toBe(40);
  });

  // Helper functions:
  function buildGridFormValue(): Record<string, unknown> {
    return {
      templateId: "grid",
      strategyName: "Test Grid",
      exchange: "Hyperliquid",
      market: "BTC-USD",
      timeframe: "15m",
      direction: "long",
      grid: { levels: 10, spacing: 0.5, entryMode: "auto_from_signal_candle", anchorPrice: null, breakdownThreshold: 1.5 },
      exit: { takeProfit: { enabled: true, type: "fixed_percent", value: 2 }, stopLoss: { enabled: true, type: "fixed_percent", value: 6 }, exitOnOppositeSignal: false },
      risk: { positionSizeType: "percent_wallet", positionSizeValue: 5, leverage: 1, maxOpenTrades: 1, cooldownValue: 0, cooldownUnit: "candles", allowSameCandleReentry: false },
      metadata: { tags: [], notes: "" },
      conditions: [],
    };
  }

  function buildSignalFormValue(): Record<string, unknown> {
    return {
      ...buildGridFormValue(),
      templateId: "custom_signal",
      conditions: [{ id: "cond-1", enabled: true, type: "rsi", label: "RSI Oversold", period: 14, operator: "lt", value: 40 }],
    };
  }
});
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.spec.ts` — Service test pattern

---

### Task 3.8: Add unit tests for validation signal-mode branch {#task-38-unit-tests-validation}

Add tests to verify the validation service handles signal-mode rules correctly.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.spec.ts` — Extend existing file
- **Success**:
  - Signal mode with no conditions: produces error with code `REQUIRED` on `entryConditions`
  - Signal mode with valid RSI condition: no condition-related errors
  - Signal mode with RSI value 150: produces error with code `RANGE` on `entryConditions[0].params.value`
  - Signal mode with RSI period 0: produces error with code `RANGE` on `entryConditions[0].params.period`
  - Grid mode: unchanged test behaviour (no signal-mode errors)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.spec.ts — append to existing tests

// Add a new describe block for signal mode:

describe("signal mode validation", () => {
  it("should require at least one condition in signal mode", () => {
    const formValue = { ...baseFormValue(), templateId: "custom_signal", conditions: [] };
    const errors = service.validate(formValue);
    expect(errors.some((e) => e.fieldPath === "entryConditions" && e.code === "REQUIRED")).toBeTrue();
  });

  it("should not produce grid errors in signal mode", () => {
    const formValue = { ...baseFormValue(), templateId: "custom_signal", conditions: [validRsiCondition()] };
    const errors = service.validate(formValue);
    expect(errors.some((e) => e.fieldPath.startsWith("grid"))).toBeFalse();
  });

  it("should reject RSI value greater than 100", () => {
    const formValue = { ...baseFormValue(), templateId: "custom_signal", conditions: [{ ...validRsiCondition(), value: 150 }] };
    const errors = service.validate(formValue);
    expect(errors.some((e) => e.fieldPath.includes("params.value") && e.code === "RANGE")).toBeTrue();
  });

  it("should reject RSI period less than 1", () => {
    const formValue = { ...baseFormValue(), templateId: "custom_signal", conditions: [{ ...validRsiCondition(), period: 0 }] };
    const errors = service.validate(formValue);
    expect(errors.some((e) => e.fieldPath.includes("params.period") && e.code === "RANGE")).toBeTrue();
  });

  it("should accept valid RSI condition", () => {
    const formValue = { ...baseFormValue(), templateId: "custom_signal", conditions: [validRsiCondition()] };
    const errors = service.validate(formValue);
    expect(errors.some((e) => e.fieldPath.startsWith("entryConditions"))).toBeFalse();
  });
});

// Helper:
function validRsiCondition(): Record<string, unknown> {
  return { id: "cond-1", enabled: true, type: "rsi", label: "", period: 14, operator: "lt", value: 40 };
}
```

Note: `baseFormValue()` may already exist in the spec file. If not, create it with the same shape as the grid form value from Task 3.7.

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.spec.ts` — Existing test structure and assertion patterns

---

### Task 3.9: Frontend build, lint, and test verification {#task-39-frontend-build-lint-test}

Run full frontend build, lint, and test suite to verify all changes work together.

- **Complexity**: Low
- **Risk Factors**: Integration issues between page/card/mapper/validation may surface
- **Files**: None (verification only)
- **Success**:
  - `ng build` completes without errors
  - `ng lint` completes without errors
  - `ng test --watch=false` completes with all tests passing (existing + new)

## Phase Success Criteria

- Template selection of "Custom Signal" hides grid card, shows entry conditions card
- RSI conditions can be added, configured (period/operator/value), duplicated, and removed
- Mapper produces `strategyMode: "signal"`, `grid: null`, `entryLogic: "all"`, `entryConditions: [...]`
- Client validation: signal mode requires ≥1 condition; RSI period ≥ 1; RSI value 0–100
- Preview shows: "Enter a long trade on BTC-USD 15m when RSI(14) is below 40."
- Loading a saved signal-mode strategy populates conditions in the form
- All new tests pass
- Frontend builds, lints, and passes full test suite
