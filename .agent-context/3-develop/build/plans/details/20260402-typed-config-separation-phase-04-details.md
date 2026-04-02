<!-- markdownlint-disable-file -->

# Task Details: F0 — Typed Config & Execution Separation

## Phase 4: Frontend

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — standalone components, `inject()`, double quotes, explicit access modifiers, `*.model.ts` files, `@if`/`@for` template syntax
- `frontend/trading-ui/src/app/core/models/backtest.model.ts` — existing TS interfaces
- `frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.ts` — reactive form, emit logic
- `frontend/trading-ui/src/app/features/backtesting/backtest-page.component.ts` — page orchestrator, prefill logic

## Design References

- Split `GridStrategyConfig` interface into `GridStrategyConfig` (strategy only + positionSize) + `ExecutionConfig` (fees + leverage)
- `BacktestRequest` changes from `{ strategyConfig: GridStrategyConfig }` to `{ strategyConfig: GridStrategyConfig, executionConfig: ExecutionConfig }`
- `BacktestResult` also gains `executionConfig: ExecutionConfig` alongside existing `strategyConfig`
- Form HTML sections already visually separate strategy from execution params — no structural HTML changes needed
- `_prefillFromResult` must read from both `result.strategyConfig` and `result.executionConfig`

---

### Task 4.1: Update TypeScript models {#task-41-update-typescript-models}

Split `GridStrategyConfig` and update `BacktestRequest`/`BacktestResult` interfaces to match the new API contract.

- **Complexity**: Medium
- **Risk Factors**: All components using these interfaces must be updated; response shape change affects prefill logic
- **Files**:
  - `frontend/trading-ui/src/app/core/models/backtest.model.ts` — modify interfaces
- **Success**:
  - `GridStrategyConfig` has only strategy params + `positionSize`
  - New `ExecutionConfig` interface with `feeModel` + `leverage`
  - New `FeeModel` interface with `makerFeeRate`, `takerFeeRate`, `slippageRate`
  - `BacktestRequest` has both `strategyConfig` and `executionConfig`
  - `BacktestResult` has both `strategyConfig` and `executionConfig`
- **Dependencies**: Phase 3 complete (API contract finalized)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/models/backtest.model.ts — modification

export interface GridStrategyConfig {
  gridLevels: number;
  entryMode?: BacktestEntryMode;
  manualAnchorPrice?: number | null;
  gridSpacing: number;
  takeProfitPercent: number;
  breakdownThreshold: number;
  stopLossPercent: number;
  positionSize: number;
  // Removed: makerFee, takerFee, slippage, leverage
}

export interface FeeModel {
  makerFeeRate: number;
  takerFeeRate: number;
  slippageRate: number;
}

export interface ExecutionConfig {
  feeModel: FeeModel;
  leverage: number;
}

export interface BacktestRequest {
  symbol: string;
  intervals: string[];
  startDate: string;
  endDate: string;
  initialCapital: number;
  strategyConfig: GridStrategyConfig;
  executionConfig: ExecutionConfig;    // new — was part of strategyConfig
}

// Update BacktestResult to include executionConfig:
export interface BacktestResult {
  // ... existing properties ...
  strategyConfig: GridStrategyConfig;
  executionConfig: ExecutionConfig;    // new
  // ... rest unchanged ...
}
```

**Note**: The API response serializes `FeeModel` with `makerFeeRate`/`takerFeeRate`/`slippageRate` (matching the C# property names via camelCase JSON policy). The request uses `makerFee`/`takerFee`/`slippage` (matching the `ExecutionConfigRequest` DTO). These are different shapes — the request DTO has flat fee fields, the response/domain type has nested `FeeModel`.

For the **request**, the `ExecutionConfig` interface in `BacktestRequest` should match the API DTO shape:

```typescript
// Request-specific shape (matches ExecutionConfigRequest DTO):
export interface BacktestExecutionConfigRequest {
  makerFee: number;
  takerFee: number;
  slippage: number;
  leverage: number;
}

export interface BacktestRequest {
  // ...
  executionConfig: BacktestExecutionConfigRequest;
}
```

For the **response**, `ExecutionConfig` matches the domain record (nested `FeeModel`):

```typescript
// Response shape (matches domain ExecutionConfig):
export interface ExecutionConfig {
  feeModel: FeeModel;
  leverage: number;
}

export interface BacktestResult {
  // ...
  executionConfig: ExecutionConfig;
}
```

##### Pattern References

Based on existing `frontend/trading-ui/src/app/core/models/backtest.model.ts`.

---

### Task 4.2: Update backtest-form component {#task-42-update-backtest-form-component}

Update the form component's `onRunBacktest()` emit to produce the new split request shape. The reactive form structure and HTML don't need changes — only how the form values are assembled into the request object.

- **Complexity**: Medium
- **Risk Factors**: Must correctly map form control values to new nested structure; validation error mapping may need updating
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.ts` — modify `onRunBacktest()` emit and `_applyValidationError` controlMap
- **Success**:
  - `onRunBacktest()` emits `BacktestRequest` with separate `strategyConfig` and `executionConfig` sections
  - Form controls remain the same (no user-facing UI change)
  - `_applyValidationError` maps new API error paths to form controls
- **Dependencies**: Task 4.1

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.ts
// In onRunBacktest() method, restructure the emit:

this.runBacktest.emit({
  symbol: this.form.value.symbol,
  intervals: this.selectedIntervals,
  startDate: this.form.value.startDate,
  endDate: this.form.value.endDate,
  initialCapital: this.form.value.initialCapital,
  strategyConfig: {
    gridLevels: this.form.value.gridLevels,
    entryMode: this.form.value.entryMode,
    manualAnchorPrice: this.form.value.manualAnchorPrice || null,
    gridSpacing: this.form.value.gridSpacing,
    takeProfitPercent: this.form.value.takeProfitPercent,
    breakdownThreshold: this.form.value.breakdownThreshold,
    stopLossPercent: this.form.value.stopLossPercent,
    positionSize: this.form.value.positionSize,
  },
  executionConfig: {
    makerFee: this.form.value.makerFee,
    takerFee: this.form.value.takerFee,
    slippage: this.form.value.slippage,
    leverage: this.form.value.leverage,
  },
  // Remove enableAuditLog if it was in strategyConfig, or keep at top level
});
```

The form HTML template (`backtest-form.component.html`) does NOT need changes — the sections "Grid Strategy", "Position & Risk", and "Fees & Slippage" remain the same. Only the data assembly in the TypeScript changes.

Update `_applyValidationError` controlMap if API error paths change (e.g., `"ExecutionConfig.MakerFee"` → maps to `"makerFee"` form control). The existing substring-matching approach should still work for field names within the sub-objects.

##### Pattern References

Based on existing `frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.ts` `onRunBacktest()` method.

---

### Task 4.3: Update backtest-page component (prefill) {#task-43-update-backtest-page-component}

Update `_prefillFromResult()` in the page component to read from both `result.strategyConfig` and `result.executionConfig` when rehydrating the form from a previous backtest result.

- **Complexity**: Medium
- **Risk Factors**: Must correctly read fee fields from the new nested `executionConfig.feeModel` path
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/backtest-page.component.ts` — modify prefill logic
- **Success**:
  - Prefill reads strategy fields from `result.strategyConfig`
  - Prefill reads fee/leverage fields from `result.executionConfig`
  - Rerunning a previous backtest produces correct form values
- **Dependencies**: Tasks 4.1, 4.2

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/backtesting/backtest-page.component.ts
// In _prefillFromResult() or equivalent prefill method:

// Strategy fields — same path as before:
this.form.patchValue({
  gridLevels: result.strategyConfig.gridLevels,
  gridSpacing: result.strategyConfig.gridSpacing,
  takeProfitPercent: result.strategyConfig.takeProfitPercent,
  stopLossPercent: result.strategyConfig.stopLossPercent,
  breakdownThreshold: result.strategyConfig.breakdownThreshold,
  entryMode: result.strategyConfig.entryMode,
  manualAnchorPrice: result.strategyConfig.manualAnchorPrice,
  positionSize: result.strategyConfig.positionSize,
});

// Execution fields — now from executionConfig (was from strategyConfig):
this.form.patchValue({
  makerFee: result.executionConfig.feeModel.makerFeeRate,   // was: result.strategyConfig.makerFee
  takerFee: result.executionConfig.feeModel.takerFeeRate,   // was: result.strategyConfig.takerFee
  slippage: result.executionConfig.feeModel.slippageRate,   // was: result.strategyConfig.slippage
  leverage: result.executionConfig.leverage,                  // was: result.strategyConfig.leverage
});
```

**Important**: The response `ExecutionConfig.FeeModel` uses `makerFeeRate`/`takerFeeRate`/`slippageRate` (matching the C# `FeeModel` property names), while the form controls and request DTO use `makerFee`/`takerFee`/`slippage`. The prefill must use the response field names (`feeModel.makerFeeRate` etc.).

##### Pattern References

Based on existing prefill logic in `frontend/trading-ui/src/app/features/backtesting/backtest-page.component.ts`.

---

### Task 4.4: Run frontend build and lint {#task-44-run-frontend-build-and-lint}

Verify the Angular application builds and lints cleanly.

- **Complexity**: Low
- **Risk Factors**: TypeScript compiler may catch type mismatches between model and usage
- **Files**: None (verification step)
- **Success**:
  - `npx ng build` succeeds with zero errors
  - `npx ng lint` passes
- **Dependencies**: Tasks 4.1–4.3

**Commands**:

```bash
cd frontend/trading-ui
npx ng build
npx ng lint
```

## Phase Success Criteria

- `GridStrategyConfig` TypeScript interface has only strategy params + `positionSize`
- `ExecutionConfig` and `FeeModel` TypeScript interfaces exist
- `BacktestRequest` sends separate `strategyConfig` and `executionConfig` sections
- `BacktestResult` reads separate `strategyConfig` and `executionConfig` from response
- Prefill from previous result correctly reads fees from `executionConfig.feeModel`
- Angular build and lint pass cleanly
- End-to-end: form submit → API → backtest → result display works with new shapes
