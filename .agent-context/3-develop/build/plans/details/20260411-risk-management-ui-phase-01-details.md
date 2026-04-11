<!-- markdownlint-disable-file -->

# Task Details: Risk Management UI — R-Based Position Sizing

## Phase 1: TypeScript Models & Form Infrastructure

## Standards and Knowledge References

- `angular.instructions.md` — Standalone components, `inject()` DI, explicit visibility, double quotes, SCSS, new control flow syntax
- `33-risk-management-and-trade-sizing.md` — R-based sizing: `R = equity × riskPercent / 100`, `notional = R / (SL% / 100)`, auto-leverage formula
- `13-strategy-config-schema.md` — StrategyConfig JSON schema, snake_case enum serialization

## Design References

- Backend `PositionSizeType` enum serializes as `snake_case_lower` (e.g., `"risk_based"`) via `StrategyJsonOptions.Default`
- Backend `RiskConfig` will add `RiskPerTradePercent` (decimal) and `AutoLeverage` (bool) as optional fields with defaults
- `RiskConfigRequest` (API layer) uses string-typed `PositionSizeType` — `"risk_based"` maps directly

---

### Task 1.1: Update `PositionSizeType` and `RiskConfig` in `strategy.model.ts` {#task-11-update-positionsizetype-and-riskconfig}

Add `"risk_based"` to the `PositionSizeType` union type and add two new optional fields to the `RiskConfig` interface.

- **Complexity**: Low
- **Risk Factors**: None — additive change, no existing code breaks
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts` — modification
- **Success**:
  - `PositionSizeType` includes `"risk_based"`
  - `RiskConfig` has `riskPerTradePercent?: number` and `autoLeverage?: boolean`
- **Dependencies**: None

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts — modification

// Line 4: Update PositionSizeType union
export type PositionSizeType = "percent_wallet" | "fixed_notional" | "risk_based";

// Lines ~82-90: Update RiskConfig interface — add two new optional fields at the end
export interface RiskConfig {
  positionSizeType: PositionSizeType;
  positionSizeValue: number;
  leverage: number;
  maxOpenTrades: number;
  cooldownValue: number;
  cooldownUnit: CooldownUnit;
  allowSameCandleReentry: boolean;
  riskPerTradePercent?: number;
  autoLeverage?: boolean;
}
```

##### Pattern References

- Current `PositionSizeType` definition: `frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts` line 4
- Current `RiskConfig` interface: `frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts` lines 82-90

---

### Task 1.2: Update `BacktestRiskConfig` in `backtest.model.ts` {#task-12-update-backtestriskconfig}

Add the two new optional fields to the `BacktestRiskConfig` interface to mirror the strategy model.

- **Complexity**: Low
- **Risk Factors**: None — additive change
- **Files**:
  - `frontend/trading-ui/src/app/core/models/backtest.model.ts` — modification
- **Success**:
  - `BacktestRiskConfig` has `riskPerTradePercent?: number` and `autoLeverage?: boolean`
- **Dependencies**: None

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/models/backtest.model.ts — modification

export interface BacktestRiskConfig {
  positionSizeType: string;
  positionSizeValue: number;
  leverage: number;
  maxOpenTrades: number;
  cooldownValue: number;
  cooldownUnit: string;
  allowSameCandleReentry: boolean;
  riskPerTradePercent?: number;
  autoLeverage?: boolean;
}
```

##### Pattern References

- Current `BacktestRiskConfig`: `frontend/trading-ui/src/app/core/models/backtest.model.ts` lines 42-50

---

### Task 1.3: Add new form controls in `strategy-builder-page._buildForm()` {#task-13-add-new-form-controls}

Add `riskPerTradePercent` and `autoLeverage` controls to the `risk` FormGroup definition.

- **Complexity**: Medium
- **Risk Factors**: Validators must match the expected ranges (0.01–100 for riskPerTradePercent)
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` — modification
- **Success**:
  - `risk` FormGroup includes `riskPerTradePercent` and `autoLeverage` controls
  - Default values: `riskPerTradePercent = 1`, `autoLeverage = true`
- **Dependencies**: Task 1.1

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts — modification
// In _buildForm(), update the risk FormGroup:

      risk: this._fb.group({
        positionSizeType: ["percent_wallet", Validators.required],
        positionSizeValue: [5, [Validators.required, Validators.min(0.01), Validators.max(100)]],
        leverage: [1, [Validators.required, Validators.min(1), Validators.max(50)]],
        maxOpenTrades: [1, [Validators.required, Validators.min(1), Validators.max(10)]],
        cooldownValue: [0, [Validators.min(0)]],
        cooldownUnit: ["candles", Validators.required],
        allowSameCandleReentry: [false],
        riskPerTradePercent: [1, [Validators.min(0.01), Validators.max(100)]],
        autoLeverage: [true],
      }),
```

##### Pattern References

- Current `_buildForm()` risk group: `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts` lines 419-427

---

### Task 1.4: Pass `exitGroup` to risk card from parent template {#task-14-pass-exitgroup-to-risk-card}

Pass the exit form group to the risk management card so it can reactively read the stop-loss value for the live preview.

- **Complexity**: Low
- **Risk Factors**: None — optional input, backward compatible
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.html` — modification
- **Success**:
  - `exitGroup` input is bound on the `<app-risk-management-card>` element
- **Dependencies**: None

#### Implementation Details

```html
<!-- frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.html — modification -->
<!-- Line ~86: Update risk card to also pass exitGroup -->
        <app-risk-management-card [group]="riskFormGroup" [exitGroup]="exitFormGroup" />
```

##### Pattern References

- Current template binding: `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.html` line 86

---

### Task 1.5: Update `strategy-mapper.service.ts` risk mapping {#task-15-update-strategy-mapper}

Add `riskPerTradePercent` and `autoLeverage` to the risk section of `mapFormToConfig()`. Only emit these fields when `positionSizeType` is `"risk_based"`.

- **Complexity**: Medium
- **Risk Factors**: Must handle undefined/null values correctly for non-risk_based modes
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.ts` — modification
- **Success**:
  - When `positionSizeType` is `"risk_based"`, output includes `riskPerTradePercent` and `autoLeverage`
  - When `positionSizeType` is not `"risk_based"`, these fields are omitted or undefined
- **Dependencies**: Task 1.1

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.ts — modification
// In mapFormToConfig(), update the risk mapping block (~line 74):

      risk: {
        positionSizeType: (risk["positionSizeType"] as PositionSizeType | undefined) ?? "percent_wallet",
        positionSizeValue: Number(risk["positionSizeValue"] ?? 0),
        leverage: Number(risk["leverage"] ?? 1),
        maxOpenTrades: Number(risk["maxOpenTrades"] ?? 1),
        cooldownValue: Number(risk["cooldownValue"] ?? 0),
        cooldownUnit: risk["cooldownUnit"] === "minutes" ? "minutes" : "candles",
        allowSameCandleReentry: !!risk["allowSameCandleReentry"],
        riskPerTradePercent: risk["positionSizeType"] === "risk_based" ? Number(risk["riskPerTradePercent"] ?? 1) : undefined,
        autoLeverage: risk["positionSizeType"] === "risk_based" ? Boolean(risk["autoLeverage"] ?? true) : undefined,
      },
```

##### Pattern References

- Current risk mapping: `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.ts` lines 74-81

---

### Task 1.6: Update `strategy-validation.service.ts` for mode-conditional validation {#task-16-update-strategy-validation}

Make `positionSizeValue` validation conditional on sizing mode. Add `riskPerTradePercent` validation when `risk_based` is selected. Add cross-field validation requiring stop-loss when `risk_based` is active.

- **Complexity**: Medium
- **Risk Factors**: Cross-field validation depends on exit config structure
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.ts` — modification
- **Success**:
  - `positionSizeValue` validation skipped when `risk_based` mode
  - `riskPerTradePercent` validated (0.01–100) when `risk_based` mode
  - Validation error when `risk_based` selected but no stop-loss enabled or SL type is not `fixed_percent`
- **Dependencies**: Task 1.1

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.ts — modification
// Replace the risk validation block (~lines 75-90):

    if (risk !== null) {
      const positionSizeType = String(risk["positionSizeType"] ?? "percent_wallet");
      const positionSizeValue = Number(risk["positionSizeValue"] ?? 0);
      const leverage = Number(risk["leverage"] ?? 0);
      const maxOpenTrades = Number(risk["maxOpenTrades"] ?? 0);
      const cooldownValue = Number(risk["cooldownValue"] ?? 0);

      if (positionSizeType === "risk_based") {
        const riskPercent = Number(risk["riskPerTradePercent"] ?? 0);
        if (riskPercent < 0.01 || riskPercent > 100) {
          errors.push(this._error("risk.riskPerTradePercent", "RANGE", "Risk per trade must be between 0.01% and 100%."));
        }

        const slEnabled = Boolean(stopLoss?.["enabled"] ?? false);
        const slType = String(stopLoss?.["type"] ?? "");
        if (!slEnabled || slType !== "fixed_percent") {
          errors.push(this._error("risk.positionSizeType", "SL_REQUIRED", "Risk-based sizing requires a fixed-percent stop-loss to be enabled."));
        }
      } else {
        if (positionSizeValue < 0.01 || positionSizeValue > 100) {
          errors.push(this._error("risk.positionSizeValue", "RANGE", "Position size must be between 0.01 and 100."));
        }
      }

      if (leverage < 1 || leverage > 50) {
        errors.push(this._error("risk.leverage", "RANGE", "Leverage must be between 1x and 50x."));
      }

      if (maxOpenTrades < 1 || maxOpenTrades > 10) {
        errors.push(this._error("risk.maxOpenTrades", "RANGE", "Max open trades must be between 1 and 10."));
      }

      if (cooldownValue < 0) {
        errors.push(this._error("risk.cooldownValue", "RANGE", "Cooldown cannot be negative."));
      }
    }
```

##### Pattern References

- Current risk validation: `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.ts` lines 75-90

---

### Task 1.7: Build verification {#task-17-build-verification}

Run `ng build` and `ng lint` to verify all TypeScript changes compile correctly.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification step)
- **Success**:
  - `ng build` completes without errors
  - `ng lint` passes
- **Dependencies**: Tasks 1.1–1.6

## Phase Success Criteria

- `PositionSizeType` includes `"risk_based"` with two new fields on `RiskConfig`
- `BacktestRiskConfig` mirrors the new fields
- Form infrastructure includes new controls with appropriate defaults and validators
- Mapper conditionally emits new fields only for `risk_based` mode
- Validation is mode-conditional: `positionSizeValue` for non-risk_based, `riskPerTradePercent` + SL-required for `risk_based`
- Frontend builds and lints without errors
