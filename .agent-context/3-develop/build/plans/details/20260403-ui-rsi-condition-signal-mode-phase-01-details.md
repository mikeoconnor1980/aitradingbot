<!-- markdownlint-disable-file -->

# Task Details: F6 — UI: RSI Condition Card + Signal Mode

## Phase 1: Foundation — Models, Enums & Condition Factory

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — Standalone components, `inject()` DI, double quotes, explicit accessibility/return types, models in `models/` with `.model.ts` suffix, enums in `enums/` with `.enum.ts` suffix + `getXxxDisplayName()` helper
- `.agent-context/0-knowledge/13-strategy-config-schema.md` — Canonical schema for `EntryConditionConfig`, `RsiParams`, `EntryLogic`, `EntryConditionType`
- `src/TradingApp.Application/StrategyAuthoring/Models/RsiParams.cs` — Backend RSI params: `Period` (int, >0), `Operator` (string), `Value` (decimal, 0–100)
- `src/TradingApp.Application/StrategyAuthoring/Models/EntryConditionConfig.cs` — Backend condition: `Id`, `Enabled`, `Type`, `Label`, `Params`

## Design References

- RSI operators from backend `RsiConditionHandler`: `lt`, `lte`, `gt`, `gte`, `cross_above`, `cross_below` (string literals, not an enum server-side)
- `EntryLogic` from backend: `All` | `Any` — serialized as string via `JsonStringEnumConverter`
- `EntryConditionType` from backend: `Unknown`, `Rsi`, `PriceVsEma`, `Macd` — serialized as lowercase string

---

### Task 1.1: Update strategy model types for signal mode {#task-11-update-strategy-model-types}

Add TypeScript interfaces and types to `strategy.model.ts` to support signal-mode conditions, and widen the `StrategyConfig` interface to accept real condition data.

- **Complexity**: Low
- **Risk Factors**: None — additive changes to existing types
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts` — Add new types and widen existing fields
- **Success**:
  - `EntryConditionConfig`, `RsiParams`, `EntryLogic`, `EntryConditionType`, `RsiOperator` types exist
  - `StrategyConfig.entryConditions` accepts `EntryConditionConfig[] | null`
  - `StrategyConfig.entryLogic` accepts `EntryLogic | null`

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts — modification

// Add these new types BEFORE the StrategyConfig interface:

export type EntryLogic = "all" | "any";
export type EntryConditionType = "rsi" | "price_vs_ema" | "macd"; // "unknown" omitted — UI never produces this value; backend uses it as default/sentinel
export type RsiOperator = "lt" | "lte" | "gt" | "gte" | "cross_above" | "cross_below";

export interface RsiParams {
  period: number;
  operator: RsiOperator;
  value: number;
}

export interface EntryConditionConfig {
  id: string;
  enabled: boolean;
  type: EntryConditionType;
  label: string;
  params: RsiParams; // Expands to RsiParams | EmaParams | ... when new types are added
}

// ... existing code ...

// Widen these two fields in the StrategyConfig interface:
//   entryLogic?: null;           →  entryLogic?: EntryLogic | null;
//   entryConditions?: null;      →  entryConditions?: EntryConditionConfig[] | null;
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts` — existing type definitions and `StrategyConfig` interface
- `src/TradingApp.Application/StrategyAuthoring/Models/RsiParams.cs` — backend field names: `Period`, `Operator`, `Value`
- `src/TradingApp.Application/StrategyAuthoring/Models/EntryConditionConfig.cs` — backend condition shape

---

### Task 1.2: Create RSI operator display-name helper {#task-12-create-rsi-operator-display-name-helper}

Create a helper function for RSI operator display names, following the Angular instructions enum pattern.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/enums/rsi-operator.enum.ts` — New file
- **Success**:
  - `getRsiOperatorDisplayName()` returns human-readable labels for all 6 operators
  - `RSI_OPERATORS` constant array available for dropdown options

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/enums/rsi-operator.enum.ts — new file

import { RsiOperator } from "../models/strategy.model";

export interface RsiOperatorOption {
  value: RsiOperator;
  label: string;
}

export const RSI_OPERATORS: RsiOperatorOption[] = [
  { value: "lt", label: "Less than (<)" },
  { value: "lte", label: "Less than or equal (≤)" },
  { value: "gt", label: "Greater than (>)" },
  { value: "gte", label: "Greater than or equal (≥)" },
  { value: "cross_above", label: "Crosses above" },
  { value: "cross_below", label: "Crosses below" },
];

export function getRsiOperatorDisplayName(operator: RsiOperator): string {
  const found = RSI_OPERATORS.find((op) => op.value === operator);
  return found?.label ?? operator;
}
```

##### Pattern References

- `.github/instructions/angular.instructions.md` — Enums in `enums/` folder with `.enum.ts` suffix and `getXxxDisplayName()` helper

---

### Task 1.3: Create condition factory service {#task-13-create-condition-factory-service}

Create a service that produces typed `FormGroup` instances for each condition type. Initially supports RSI only. Future condition types (EMA, MACD) require only adding a new method.

- **Complexity**: Medium
- **Risk Factors**: Validator ranges must match backend rules (RSI period > 0, value 0–100)
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/services/condition-factory.service.ts` — New file
- **Success**:
  - `createRsiCondition()` returns a `FormGroup` with fields: `id`, `enabled`, `type`, `label`, `period`, `operator`, `value`
  - Validators enforce period > 0, value 0–100
  - Default values match PBI spec: period=14, operator="lt", value=40
- **Dependencies**:
  - Task 1.1 (types must exist)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/services/condition-factory.service.ts — new file

import { Injectable, inject } from "@angular/core";
import { FormBuilder, FormGroup, Validators } from "@angular/forms";

@Injectable({ providedIn: "root" })
export class ConditionFactoryService {
  private readonly _fb = inject(FormBuilder);

  private _nextId = 1;

  public createRsiCondition(overrides?: Partial<{
    id: string;
    enabled: boolean;
    label: string;
    period: number;
    operator: string;
    value: number;
  }>): FormGroup {
    return this._fb.group({
      id: [overrides?.id ?? this._generateId()],
      enabled: [overrides?.enabled ?? true],
      type: ["rsi"],
      label: [overrides?.label ?? ""],
      period: [overrides?.period ?? 14, [Validators.required, Validators.min(1)]],
      operator: [overrides?.operator ?? "lt", Validators.required],
      value: [overrides?.value ?? 40, [Validators.required, Validators.min(0), Validators.max(100)]],
    });
  }

  private _generateId(): string {
    return `cond-${this._nextId++}`;
  }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` — `_buildForm()` method uses `this._fb.group({})` with `Validators` for form construction
- `src/TradingApp.Application/StrategyAuthoring/Validation/BusinessRuleValidator.cs` — RSI period > 0 (`RSI_PERIOD_INVALID`), value 0–100 (`RSI_VALUE_INVALID`)

---

### Task 1.4: Add "Custom Signal" template {#task-14-add-custom-signal-template}

Add the "Custom Signal" template entry to `STRATEGY_TEMPLATES` constant.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts` — Add template entry
- **Success**:
  - "Custom Signal" template appears in template selector when strategy builder loads
  - Template has `id: "custom_signal"`, `label: "Custom Signal"`, `available: true`

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts — modification

export const STRATEGY_TEMPLATES: StrategyTemplate[] = [
  { id: "grid", label: "Grid", available: true },
  { id: "custom_signal", label: "Custom Signal", available: true },
  { id: "ema_pullback", label: "EMA Pullback", available: false },
  { id: "rsi_reversal", label: "RSI Reversal", available: false },
  { id: "blank", label: "Blank", available: true },
];
```

##### Pattern References

- `frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts` — existing `STRATEGY_TEMPLATES` array

---

### Task 1.5: Frontend build and lint verification {#task-15-frontend-build-and-lint}

Run `ng build` and `ng lint` from the `frontend/trading-ui` directory to verify all changes compile and pass lint rules.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification only)
- **Success**:
  - `ng build` completes without errors
  - `ng lint` completes without errors

## Phase Success Criteria

- All new TypeScript types (`EntryConditionConfig`, `RsiParams`, `EntryLogic`, `EntryConditionType`, `RsiOperator`) are defined and importable
- `ConditionFactoryService` creates valid RSI FormGroups with correct defaults and validators
- `RSI_OPERATORS` constant provides dropdown options for all 6 operators
- "Custom Signal" template is available in `STRATEGY_TEMPLATES`
- Frontend builds and passes lint
