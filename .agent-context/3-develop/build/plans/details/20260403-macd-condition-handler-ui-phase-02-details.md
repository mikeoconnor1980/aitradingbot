<!-- markdownlint-disable-file -->

# Task Details: F8 — MACD Condition Handler + UI Card

## Phase 2: Frontend — Models, Services & Validation

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — `standalone: true`, `inject()`, double quotes, explicit access modifiers, explicit return types, SCSS only, new control flow syntax
- `.github/instructions/testing.instructions.md` — MSTest patterns (frontend uses Jasmine/Karma but follow consistent naming)
- `.agent-context/0-knowledge/13-strategy-config-schema.md` — Schema structure for condition params

## Design References

- PBI specifies 6 operators: `cross_above_signal`, `cross_below_signal`, `above_zero`, `below_zero`, `histogram_rising`, `histogram_falling`
- PBI period validation: fast ∈ [2, 50], slow ∈ [5, 200], signal ∈ [2, 50], fast < slow
- "MACD Cross" template: MACD `cross_above_signal` with defaults (12/26/9), strategyMode `signal`, TP 2% (fixed_percent), SL 1.5% (fixed_percent)

### Task 2.1: Update `MacdOperator` type and add `MACD Cross` template to `strategy.model.ts` {#task-21-update-macdoperator-type-and-add-macd-cross-template}

Replace the existing `MacdOperator` type alias (provisional values from F5) with the PBI's 6 operators, and add the "MACD Cross" template to `STRATEGY_TEMPLATES`. Note: `MacdParams` interface (lines 54-59) and `EntryConditionConfig.params` union (line 63) already include MACD and do NOT need changes.

- **Complexity**: Low
- **Risk Factors**: Changing `MacdOperator` values may cause type errors in existing code that uses the old operator strings (`cross_above`, `cross_below`, `gt`, `lt`)
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts` — modify
- **Success**:
  - `MacdOperator` type updated from `"cross_above" | "cross_below" | "gt" | "lt"` to `"cross_above_signal" | "cross_below_signal" | "above_zero" | "below_zero" | "histogram_rising" | "histogram_falling"`
  - `STRATEGY_TEMPLATES` includes `{ id: "macd_cross", label: "MACD Cross", available: true }`
  - `MacdParams` interface unchanged (already correct)
  - `EntryConditionConfig.params` union unchanged (already includes `MacdParams`)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts — modification

// REPLACE existing MacdOperator type (line 10):
// Before: export type MacdOperator = "cross_above" | "cross_below" | "gt" | "lt";
// After:
export type MacdOperator = "cross_above_signal" | "cross_below_signal" | "above_zero" | "below_zero" | "histogram_rising" | "histogram_falling";

// MacdParams interface (lines 54-59) already has correct shape — NO CHANGES NEEDED
// EntryConditionConfig.params union (line 63) already includes MacdParams — NO CHANGES NEEDED

// Add to STRATEGY_TEMPLATES array (after "ema_pullback", before "rsi_reversal"):
{ id: "macd_cross", label: "MACD Cross", available: true },
```

##### Pattern References

- Existing `MacdOperator` type at line 10 of `frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts`
- `STRATEGY_TEMPLATES` array at line 192 of the same file

---

### Task 2.2: Create `macd-operator.enum.ts` operator enum file {#task-22-create-macd-operator-enum-file}

Create a new enum file following the established operator enum pattern.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/enums/macd-operator.enum.ts` — new file
- **Success**:
  - `MacdOperatorOption` interface exported
  - `MACD_OPERATORS` constant array exported with 6 options and user-friendly labels

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/enums/macd-operator.enum.ts — new file
import { MacdOperator } from "../models/strategy.model";

export interface MacdOperatorOption {
  value: MacdOperator;
  label: string;
}

export const MACD_OPERATORS: MacdOperatorOption[] = [
  { value: "cross_above_signal", label: "Crosses above signal line" },
  { value: "cross_below_signal", label: "Crosses below signal line" },
  { value: "above_zero", label: "Above zero line" },
  { value: "below_zero", label: "Below zero line" },
  { value: "histogram_rising", label: "Histogram rising" },
  { value: "histogram_falling", label: "Histogram falling" },
];
```

##### Pattern References

- Based on `frontend/trading-ui/src/app/features/strategy-builder/enums/price-vs-ema-operator.enum.ts`

---

### Task 2.3: Add `createMacdCondition()` to `ConditionFactoryService` {#task-23-add-createmacdcondition-to-conditionfactoryservice}

Add the MACD condition factory method and its overrides interface.

- **Complexity**: Medium
- **Risk Factors**: Validator ranges must match PBI requirements (fast ∈ [2, 50], slow ∈ [5, 200], signal ∈ [2, 50])
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/services/condition-factory.service.ts` — modify
- **Success**:
  - `createMacdCondition()` creates a `FormGroup` with correct defaults (12/26/9, `cross_above_signal`)
  - Validators enforce min/max ranges for all period fields
  - Type control is set to `"macd"`
  - ID generation uses existing `_generateId()` / `_advancePastId()` pattern

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/services/condition-factory.service.ts — modification

// Add import at top:
import { MacdOperator, PriceVsEmaDistanceType, PriceVsEmaOperator, RsiOperator } from "../models/strategy.model";

// Add interface after CreatePriceVsEmaConditionOverrides:
export interface CreateMacdConditionOverrides {
  id: string;
  enabled: boolean;
  label: string;
  fastPeriod: number;
  slowPeriod: number;
  signalPeriod: number;
  operator: MacdOperator;
}

// Add method to ConditionFactoryService class (after createPriceVsEmaCondition):
public createMacdCondition(overrides?: Partial<CreateMacdConditionOverrides>): FormGroup {
  if (overrides?.id !== undefined) {
    this._advancePastId(overrides.id);
  }

  return this._fb.group({
    id: [overrides?.id ?? this._generateId()],
    enabled: [overrides?.enabled ?? true],
    type: ["macd"],
    label: [overrides?.label ?? ""],
    fastPeriod: [overrides?.fastPeriod ?? 12, [Validators.required, Validators.min(2), Validators.max(50)]],
    slowPeriod: [overrides?.slowPeriod ?? 26, [Validators.required, Validators.min(5), Validators.max(200)]],
    signalPeriod: [overrides?.signalPeriod ?? 9, [Validators.required, Validators.min(2), Validators.max(50)]],
    operator: [overrides?.operator ?? "cross_above_signal", Validators.required],
  });
}
```

##### Pattern References

- Based on `createPriceVsEmaCondition()` in `frontend/trading-ui/src/app/features/strategy-builder/services/condition-factory.service.ts`
- Uses same `_generateId()` and `_advancePastId()` utilities

---

### Task 2.4: Add MACD branch to `StrategyMapperService` {#task-24-add-macd-branch-to-strategymapperservice}

Add a MACD branch to `_mapConditionParams()` and widen its return type to include `MacdParams`.

- **Complexity**: Low
- **Risk Factors**: Return type must widen; import `MacdParams` and `MacdOperator`
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.ts` — modify
- **Success**:
  - MACD conditions map correctly from form values to `MacdParams`
  - Return type includes `MacdParams`
  - Existing RSI and PriceVsEma mapping unaffected

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.ts — modification

// Update import to include MacdOperator, MacdParams:
import { ..., MacdOperator, MacdParams, ... } from "../models/strategy.model";

// Update _mapConditionParams return type and add MACD branch:
private _mapConditionParams(condition: Record<string, unknown>): RsiParams | PriceVsEmaParams | MacdParams {
  const type = String(condition["type"] ?? "rsi");

  if (type === "price_vs_ema") {
    return {
      period: Number(condition["period"] ?? 50),
      operator: String(condition["operator"] ?? "near") as PriceVsEmaOperator,
      distanceType: String(condition["distanceType"] ?? "percent") as PriceVsEmaDistanceType,
      distanceValue: this._toNullableNumber(condition["distanceValue"]),
    };
  }

  if (type === "macd") {
    return {
      fastPeriod: Number(condition["fastPeriod"] ?? 12),
      slowPeriod: Number(condition["slowPeriod"] ?? 26),
      signalPeriod: Number(condition["signalPeriod"] ?? 9),
      operator: String(condition["operator"] ?? "cross_above_signal") as MacdOperator,
    };
  }

  return {
    period: Number(condition["period"] ?? 14),
    operator: String(condition["operator"] ?? "lt") as RsiOperator,
    value: Number(condition["value"] ?? 40),
  };
}
```

##### Pattern References

- Based on existing `_mapConditionParams()` in `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.ts` (lines 122-136)

---

### Task 2.5: Add MACD validation to `StrategyValidationService` {#task-25-add-macd-validation-to-strategyvalidationservice}

Add MACD-specific validation in `_validateSignalMode()`, exclude MACD from the generic `period` check, add `_validateMacdCondition()` method, and add MACD branch to `_createConditionSignature()`.

- **Complexity**: Medium
- **Risk Factors**: Must exclude MACD from the generic `period < 1` check (which false-positives on MACD since it has no `period` field); must add fast < slow cross-field validation
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.ts` — modify
- **Success**:
  - MACD conditions validate: all periods in range, fast < slow
  - MACD excluded from generic `period` check and RSI `value` check
  - MACD condition signature uses `fastPeriod|slowPeriod|signalPeriod|operator`
  - Existing RSI and PriceVsEma validation unaffected

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.ts — modification

// In _validateSignalMode(), modify the conditions.forEach loop:
conditions.forEach((condition, index) => {
  const type = String(condition["type"] ?? "rsi");

  // MACD has its own period validation — skip the generic period check
  if (type === "macd") {
    this._validateMacdCondition(condition, index, errors);
    return;
  }

  const period = Number(condition["period"] ?? 0);

  if (period < 1) {
    const periodLabel = type === "price_vs_ema" ? "EMA period" : "RSI period";
    errors.push(this._error(`entryConditions[${index}].params.period`, "RANGE", `${periodLabel} must be at least 1.`));
  }

  if (type === "price_vs_ema") {
    this._validatePriceVsEmaCondition(condition, index, errors);
    return;
  }

  const value = Number(condition["value"] ?? -1);
  if (value < 0 || value > 100) {
    errors.push(this._error(`entryConditions[${index}].params.value`, "RANGE", "RSI value must be between 0 and 100."));
  }
});

// Add new _validateMacdCondition method (after _validatePriceVsEmaCondition):
private _validateMacdCondition(condition: Record<string, unknown>, index: number, errors: ValidationError[]): void {
  const fast = Number(condition["fastPeriod"] ?? 0);
  const slow = Number(condition["slowPeriod"] ?? 0);
  const signal = Number(condition["signalPeriod"] ?? 0);

  if (fast < 2 || fast > 50) {
    errors.push(this._error(`entryConditions[${index}].params.fastPeriod`, "RANGE", "Fast period must be between 2 and 50."));
  }

  if (slow < 5 || slow > 200) {
    errors.push(this._error(`entryConditions[${index}].params.slowPeriod`, "RANGE", "Slow period must be between 5 and 200."));
  }

  if (signal < 2 || signal > 50) {
    errors.push(this._error(`entryConditions[${index}].params.signalPeriod`, "RANGE", "Signal period must be between 2 and 50."));
  }

  if (fast >= slow) {
    errors.push(this._error(`entryConditions[${index}].params.fastPeriod`, "RANGE", "Fast period must be less than slow period."));
  }
}

// Update _createConditionSignature() — add MACD branch before default:
private _createConditionSignature(condition: Record<string, unknown>): string {
  const type = String(condition["type"] ?? "rsi");

  if (type === "price_vs_ema") {
    return [
      type,
      String(condition["period"] ?? ""),
      String(condition["operator"] ?? ""),
      String(condition["distanceType"] ?? ""),
      String(condition["distanceValue"] ?? ""),
    ].join("|");
  }

  if (type === "macd") {
    return [
      type,
      String(condition["fastPeriod"] ?? ""),
      String(condition["slowPeriod"] ?? ""),
      String(condition["signalPeriod"] ?? ""),
      String(condition["operator"] ?? ""),
    ].join("|");
  }

  return [
    type,
    String(condition["period"] ?? ""),
    String(condition["operator"] ?? ""),
    String(condition["value"] ?? ""),
  ].join("|");
}
```

##### Pattern References

- Based on existing `_validateSignalMode()` and `_createConditionSignature()` in `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.ts`
- `_validatePriceVsEmaCondition()` pattern for type-specific validation method

---

### Task 2.6: Update `_isSignalTemplate()` in all 4 locations {#task-26-update-issignaltemplate-in-all-4-locations}

Add `"macd_cross"` to the `_isSignalTemplate()` method in all 4 files where it exists.

- **Complexity**: Low
- **Risk Factors**: Must find and update all 4 copies; missing one causes inconsistent signal-mode detection
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.ts` — modify
  - `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.ts` — modify
  - `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` — modify
  - `frontend/trading-ui/src/app/features/strategy-builder/components/preview-summary-card/preview-summary-card.component.ts` — modify
- **Success**:
  - All 4 `_isSignalTemplate()` methods return `true` for `"macd_cross"`

#### Implementation Details

In all 4 files, change:
```typescript
// Before:
private _isSignalTemplate(templateId: string): boolean {
  return templateId === "custom_signal" || templateId === "ema_pullback";
}

// After:
private _isSignalTemplate(templateId: string): boolean {
  return templateId === "custom_signal" || templateId === "ema_pullback" || templateId === "macd_cross";
}
```

##### Pattern References

- Existing `_isSignalTemplate()` in all 4 files (identified during discovery)

---

### Task 2.7: Add unit tests for MACD factory and mapper {#task-27-add-unit-tests-for-macd-factory-and-mapper}

Add tests for `ConditionFactoryService.createMacdCondition()` and `StrategyMapperService` MACD condition mapping.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/services/condition-factory.service.spec.ts` — modify (add MACD tests)
  - `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.spec.ts` — modify (add MACD mapping test)
- **Success**:
  - Factory tests verify: defaults (12/26/9, cross_above_signal), overrides, unique ID generation, validators
  - Mapper tests verify: MACD condition maps to `MacdParams` with correct field mapping
  - All tests pass
- **Dependencies**:
  - Tasks 2.1, 2.3, 2.4

---

### Task 2.8: Run frontend build and lint {#task-28-run-frontend-build-and-lint}

Run the frontend build and lint to verify no compilation or style errors.

- **Complexity**: Low
- **Risk Factors**: Union type widening may cause type errors in narrowing code
- **Files**: None (verification only)
- **Success**:
  - `ng build` succeeds
  - `npm run lint` passes

Run commands:
```powershell
Set-Location frontend/trading-ui; npx ng build; npm run lint
```

## Phase Success Criteria

- `MacdParams` and `MacdOperator` types are exported and used correctly
- "MACD Cross" template appears in `STRATEGY_TEMPLATES`
- `createMacdCondition()` creates forms with correct defaults and validators
- `_mapConditionParams()` maps MACD conditions bidirectionally
- MACD validation enforces period ranges and fast < slow
- All 4 `_isSignalTemplate()` sites recognise `"macd_cross"`
- Frontend builds and lints cleanly
- Factory and mapper tests pass
