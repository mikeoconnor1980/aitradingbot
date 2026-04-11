<!-- markdownlint-disable-file -->

# Task Details: Risk Management UI — R-Based Position Sizing

## Phase 4: Backtest & Preview Summary Updates

## Standards and Knowledge References

- `angular.instructions.md` — Explicit return types, double quotes, `@if` control flow
- `testing.instructions.md` — Jasmine specs, Given_When_Then naming

## Design References

- `backtest-form.component.ts` `positionSizeLabel` getter — currently handles `"percent_wallet"` and `"fixed_notional"` only
- `backtest-result.component.ts` `positionSizeLabel` getter — similar two-branch logic
- `preview-summary-card.component.ts` `previewText` getter — hard-coded "X% of wallet" text

---

### Task 4.1: Update `backtest-form.component.ts` positionSizeLabel for `risk_based` {#task-41-update-backtest-form-label}

Add a third branch to `positionSizeLabel` to display R-based sizing information.

- **Complexity**: Low
- **Risk Factors**: None — additive branch
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.ts` — modification
- **Success**:
  - `positionSizeLabel` returns "R-based (1% risk)" format when `risk_based`
- **Dependencies**: Phase 1 (model changes)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.ts — modification
// Update positionSizeLabel getter — add risk_based branch:

  public get positionSizeLabel(): string {
    const risk = this.selectedStrategy?.config.risk;
    if (!risk) {
      return "";
    }
    if (risk.positionSizeType === "risk_based") {
      return `R-based (${risk.riskPerTradePercent ?? 1}% risk)`;
    }
    return risk.positionSizeType === "percent_wallet"
      ? `${risk.positionSizeValue}% wallet`
      : `$${risk.positionSizeValue} fixed notional`;
  }
```

##### Pattern References

- Current `positionSizeLabel`: `frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.ts`

---

### Task 4.2: Update `backtest-result.component.ts` positionSizeLabel for `risk_based` {#task-42-update-backtest-result-label}

Add a third branch to `positionSizeLabel` in the backtest result component.

- **Complexity**: Low
- **Risk Factors**: None — additive branch
- **Files**:
  - `frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.ts` — modification
- **Success**:
  - `positionSizeLabel` returns "R-based (1% risk)" format when `risk_based`
- **Dependencies**: Phase 1 (model changes)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.ts — modification
// Update positionSizeLabel getter — add risk_based branch before existing logic:

  public get positionSizeLabel(): string {
    const risk = this.result.strategyConfig.risk;
    if (risk.positionSizeType === "risk_based") {
      return `R-based (${risk.riskPerTradePercent ?? 1}% risk)`;
    }
    const formattedNotional = `$${this.positionSize.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
    return risk.positionSizeType === "percent_wallet"
      ? `${risk.positionSizeValue}% wallet (${formattedNotional} at start)`
      : formattedNotional;
  }
```

##### Pattern References

- Current `positionSizeLabel`: `frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.ts`

---

### Task 4.3: Update `preview-summary-card.component.ts` risk display {#task-43-update-preview-summary-card}

Update the `previewText` getter to handle `risk_based` mode in the risk summary section.

- **Complexity**: Low
- **Risk Factors**: None — the preview text is a display-only summary
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/components/preview-summary-card/preview-summary-card.component.ts` — modification
- **Success**:
  - Preview text shows "Risk: R-based 1% risk per trade, auto-leverage" for `risk_based` mode
  - Existing modes display unchanged
- **Dependencies**: Phase 1 (model changes)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/components/preview-summary-card/preview-summary-card.component.ts — modification
// In the previewText getter, update the risk section (currently ~line 56):
// Replace the hard-coded risk line with mode-conditional logic:

    const positionSizeType = risk?.["positionSizeType"];
    if (positionSizeType === "risk_based") {
      const riskPercent = risk?.["riskPerTradePercent"] ?? 1;
      const autoLev = risk?.["autoLeverage"] ? "auto-leverage" : `${leverage}x leverage`;
      parts.push(`Risk: R-based ${riskPercent}% risk per trade, ${autoLev}.`);
    } else {
      parts.push(`Risk: ${positionSize}% of wallet, ${leverage}x leverage.`);
    }
```

##### Pattern References

- Current preview text: `frontend/trading-ui/src/app/features/strategy-builder/components/preview-summary-card/preview-summary-card.component.ts` ~line 56

---

### Task 4.4: Update existing test specs for new `risk_based` mode {#task-44-update-existing-test-specs}

Update existing test specs to cover the `risk_based` mode where relevant. Update mock data and add test cases.

- **Complexity**: Medium
- **Risk Factors**: Tests may reference hardcoded `positionSizeType` values — need to find and update all affected specs
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.spec.ts` — modification
  - `frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.spec.ts` — modification
- **Success**:
  - Strategy builder page spec includes `riskPerTradePercent` and `autoLeverage` in form group fixture
  - Backtest form spec's `positionSizeLabel` tests include a `risk_based` test case
  - All existing tests still pass
- **Dependencies**: Tasks 4.1–4.3

#### Implementation Details

In `strategy-builder-page.component.spec.ts`, update any fixture or mock `StrategyConfig` objects that include a `risk` property to add the two new fields:

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.spec.ts — modification
// In any mock strategy config's risk object, add new fields with defaults:
//   riskPerTradePercent: 1,
//   autoLeverage: true,
// Ensure _buildForm() risk group includes the new controls (verify form fixture matches updated _buildForm).
```

In `backtest-form.component.spec.ts`, add a dedicated test case for the `risk_based` label:

```typescript
// frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.spec.ts — modification
// Add inside the existing describe block for positionSizeLabel (or create one):

  it("GivenRiskBasedStrategy_WhenPositionSizeLabel_ThenShowsRiskBased", () => {
    const strategy = { ...baseStrategy };
    strategy.config = {
      ...strategy.config,
      risk: {
        ...strategy.config.risk,
        positionSizeType: "risk_based",
        riskPerTradePercent: 2,
        autoLeverage: true,
      },
    };
    component.selectedStrategy = strategy;
    fixture.detectChanges();
    expect(component.positionSizeLabel).toBe("R-based (2% risk)");
  });
```

##### Pattern References

- Existing spec fixtures: `frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.spec.ts`

---

### Task 4.5: Full build + lint + all tests {#task-45-full-build-lint-all-tests}

Run full frontend build, lint, and all unit tests to verify the complete feature works.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification step)
- **Success**:
  - `ng build` completes without errors
  - `ng lint` passes
  - `ng test --watch=false` — all tests pass (existing + new)
- **Dependencies**: Tasks 4.1–4.4

## Phase Success Criteria

- `positionSizeLabel` shows "R-based (X% risk)" in both backtest form and result components
- Preview summary card shows R-based risk info for `risk_based` mode
- All existing unit tests updated and passing with new model fields
- Full frontend build and lint clean
- All acceptance criteria verified through unit tests
