<!-- markdownlint-disable-file -->

# Task Details: Risk Management UI — R-Based Position Sizing

## Phase 2: Risk Management Card UI & Unit Tests

## Standards and Knowledge References

- `angular.instructions.md` — Standalone components, `inject()` DI, `DestroyRef` + `takeUntilDestroyed`, new control flow (`@if`), SCSS
- `testing.instructions.md` — Jasmine specs, `TestBed.configureTestingModule`, `NoopAnimationsModule`, `FormBuilder` for input groups
- Component pattern: `exit-rules-card.component.ts` — reactive `valueChanges` subscribe with `takeUntilDestroyed`, `_syncDisabledState()`, `OnInit`
- Component pattern: `grid-config-card.component.ts` — conditional visibility via getter (`get showsAnchorPrice()`)

## Design References

- `MatSlideToggleModule` — new import, not used elsewhere in the codebase. Import from `@angular/material/slide-toggle`
- `app-info-popover` with `matSuffix` — existing pattern in risk-management-card for contextual help

---

### Task 2.1: Add imports, inputs, and reactive lifecycle to `risk-management-card.component.ts` {#task-21-add-imports-inputs-and-reactive-lifecycle}

Transform the risk card from a thin wrapper into a component with reactive lifecycle. Add `OnInit`, `DestroyRef`, new `@Input` for `exitGroup`, visibility getters, and `MatSlideToggleModule` import.

- **Complexity**: Medium
- **Risk Factors**: `MatSlideToggleModule` is a new dependency — ensure it's compatible with the Angular Material version in use
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.ts` — modification
- **Success**:
  - Component implements `OnInit`
  - `exitGroup` input added (optional)
  - Visibility getters: `isRiskBased`, `showPositionSizeValue`, `showLeverage`, `showRiskWarning`
  - Reactive `_syncPositionSizeType()` method enables/disables controls based on mode
  - `MatSlideToggleModule` imported
- **Dependencies**: Phase 1 complete

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.ts — full replacement
import { Component, DestroyRef, Input, OnInit, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormGroup, ReactiveFormsModule } from "@angular/forms";
import { MatCardModule } from "@angular/material/card";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import { MatSlideToggleModule } from "@angular/material/slide-toggle";
import { InfoPopoverComponent } from "../info-popover/info-popover.component";

@Component({
  selector: "app-risk-management-card",
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    InfoPopoverComponent,
  ],
  templateUrl: "./risk-management-card.component.html",
  styleUrl: "./risk-management-card.component.scss"
})
export class RiskManagementCardComponent implements OnInit {
  private readonly _destroyRef = inject(DestroyRef);

  @Input({ required: true }) public group!: FormGroup;
  @Input() public exitGroup: FormGroup | null = null;

  public get isRiskBased(): boolean {
    return this.group.get("positionSizeType")?.value === "risk_based";
  }

  public get showPositionSizeValue(): boolean {
    return !this.isRiskBased;
  }

  public get showLeverage(): boolean {
    if (!this.isRiskBased) {
      return true;
    }
    return !this.group.get("autoLeverage")?.value;
  }

  public get showRiskWarning(): boolean {
    const riskPercent = Number(this.group.get("riskPerTradePercent")?.value ?? 0);
    return this.isRiskBased && riskPercent > 5;
  }

  public get stopLossNotEnabled(): boolean {
    if (!this.exitGroup) {
      return true;
    }
    return !this.exitGroup.get("stopLoss.enabled")?.value;
  }

  public ngOnInit(): void {
    this._syncPositionSizeType();
    this._syncAutoLeverage();
  }

  public hasError(controlName: string, errorCode: string): boolean {
    const control = this.group.get(controlName);
    return Boolean(control?.hasError(errorCode) && (control.touched || control.dirty));
  }

  private _syncPositionSizeType(): void {
    const typeControl = this.group.get("positionSizeType");
    if (typeControl === null) {
      return;
    }

    typeControl.valueChanges
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe(() => {
        this._applyPositionSizeType();
      });

    this._applyPositionSizeType();
  }

  private _applyPositionSizeType(): void {
    const isRiskBased = this.group.get("positionSizeType")?.value === "risk_based";
    const positionSizeValue = this.group.get("positionSizeValue");
    const riskPerTradePercent = this.group.get("riskPerTradePercent");
    const autoLeverage = this.group.get("autoLeverage");

    if (isRiskBased) {
      positionSizeValue?.disable();
      riskPerTradePercent?.enable();
      autoLeverage?.enable();
    } else {
      positionSizeValue?.enable();
      riskPerTradePercent?.disable();
      autoLeverage?.disable();
    }

    this._applyAutoLeverage();
  }

  private _syncAutoLeverage(): void {
    const autoLeverageControl = this.group.get("autoLeverage");
    if (autoLeverageControl === null) {
      return;
    }

    autoLeverageControl.valueChanges
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe(() => {
        this._applyAutoLeverage();
      });
  }

  private _applyAutoLeverage(): void {
    const isRiskBased = this.group.get("positionSizeType")?.value === "risk_based";
    const autoLeverageOn = Boolean(this.group.get("autoLeverage")?.value);
    const leverageControl = this.group.get("leverage");

    if (isRiskBased && autoLeverageOn) {
      leverageControl?.disable();
    } else {
      leverageControl?.enable();
    }
  }
}
```

##### Pattern References

- Reactive enable/disable: `frontend/trading-ui/src/app/features/strategy-builder/components/exit-rules-card/exit-rules-card.component.ts` lines 47-98
- Conditional getter: `frontend/trading-ui/src/app/features/strategy-builder/components/grid-config-card/grid-config-card.component.ts` `get showsAnchorPrice()`

---

### Task 2.2: Update `risk-management-card.component.html` with new fields and conditional visibility {#task-22-update-template-with-new-fields}

Add `risk_based` mat-option, conditional `riskPerTradePercent` input, `autoLeverage` slide toggle, warning banner, and SL validation message. Use `@if` control flow for conditional visibility.

- **Complexity**: High
- **Risk Factors**: Complex conditional layout — many `@if` blocks; ensure mat-form-field accessibility attributes
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.html` — modification
- **Success**:
  - `risk_based` option appears in position size type select
  - `riskPerTradePercent` input visible only when `risk_based` selected
  - `autoLeverage` slide toggle visible only when `risk_based` selected
  - `positionSizeValue` field hidden when `risk_based` selected
  - `leverage` field hidden when `risk_based` and `autoLeverage` is on
  - Warning banner when `riskPerTradePercent > 5%`
  - "Stop-loss required" message when `risk_based` active and no SL enabled
- **Dependencies**: Task 2.1

#### Implementation Details

```html
<!-- frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.html — full replacement -->
<mat-card class="risk-card" [formGroup]="group">
  <mat-card-header>
    <mat-card-title class="risk-card__title">
      <span>Risk Management</span>
      <app-info-popover
        title="Risk Management"
        description="Risk management controls how aggressively the strategy trades. Position size sets exposure, leverage magnifies that exposure, max open trades limits simultaneous positions, and cooldown slows re-entry after a trade."
      />
    </mat-card-title>
  </mat-card-header>

  <mat-card-content>
    <div class="risk-card__grid">
      <mat-form-field appearance="outline">
        <mat-label>Position size type</mat-label>
        <mat-select formControlName="positionSizeType">
          <mat-option value="percent_wallet">Percent of wallet</mat-option>
          <mat-option value="fixed_notional">Fixed notional</mat-option>
          <mat-option value="risk_based">Risk-based (R%)</mat-option>
        </mat-select>
        <app-info-popover
          matSuffix
          title="Position Size Type"
          description="Position size type controls how trade exposure is measured. Percent of wallet scales each trade relative to account size, fixed notional keeps each trade at a fixed currency amount, and risk-based (R%) sizes each trade so that your stop-loss equals a defined percentage of equity."
        />
      </mat-form-field>

      @if (showPositionSizeValue) {
        <mat-form-field appearance="outline">
          <mat-label>Position size value</mat-label>
          <input matInput type="number" formControlName="positionSizeValue" min="0.01" max="100" step="0.01" />
          <app-info-popover
            matSuffix
            title="Position Size Value"
            description="Position size value sets the actual exposure amount for each entry. Use smaller values for tighter risk control and larger values only when the strategy has room to absorb drawdown."
          />
          @if (hasError("positionSizeValue", "required")) {
            <mat-error>Position size is required.</mat-error>
          }
          @if (hasError("positionSizeValue", "min") || hasError("positionSizeValue", "max")) {
            <mat-error>Position size must be between 0.01 and 100.</mat-error>
          }
        </mat-form-field>
      }

      @if (isRiskBased) {
        <mat-form-field appearance="outline">
          <mat-label>Risk per trade (%)</mat-label>
          <input matInput type="number" formControlName="riskPerTradePercent" min="0.01" max="100" step="0.1" />
          <app-info-popover
            matSuffix
            title="Risk Per Trade"
            description="The percentage of your account equity risked per trade. R = Equity × this value. For example, 1% of a $10,000 account means $100 at risk per trade."
          />
          @if (hasError("riskPerTradePercent", "min") || hasError("riskPerTradePercent", "max")) {
            <mat-error>Risk per trade must be between 0.01% and 100%.</mat-error>
          }
        </mat-form-field>

        <mat-slide-toggle formControlName="autoLeverage" class="risk-card__toggle">
          Auto-leverage
          <app-info-popover
            title="Auto-Leverage"
            description="When enabled, leverage is calculated from your stop-loss distance so the stop-loss fires before liquidation. The formula is: leverage = 1 / (SL% + maintenance margin rate)."
          />
        </mat-slide-toggle>
      }

      @if (showLeverage) {
        <mat-form-field appearance="outline">
          <mat-label>Leverage</mat-label>
          <input matInput type="number" formControlName="leverage" min="1" max="50" step="1" />
          <app-info-popover
            matSuffix
            title="Leverage"
            description="Leverage multiplies position exposure beyond the underlying wallet allocation. Higher leverage increases both potential return and liquidation risk, so it should stay aligned with the strategy's stop loss and volatility."
          />
          @if (hasError("leverage", "required")) {
            <mat-error>Leverage is required.</mat-error>
          }
          @if (hasError("leverage", "min") || hasError("leverage", "max")) {
            <mat-error>Leverage must be between 1 and 50.</mat-error>
          }
        </mat-form-field>
      }

      <mat-form-field appearance="outline">
        <mat-label>Max open trades</mat-label>
        <input matInput type="number" formControlName="maxOpenTrades" min="1" max="10" step="1" />
        <app-info-popover
          matSuffix
          title="Max Open Trades"
          description="Max open trades limits how many positions or grid cycles the strategy can have active at the same time. Lower values reduce overlapping risk and capital fragmentation."
        />
        @if (hasError("maxOpenTrades", "required")) {
          <mat-error>Max open trades is required.</mat-error>
        }
        @if (hasError("maxOpenTrades", "min") || hasError("maxOpenTrades", "max")) {
          <mat-error>Max open trades must be between 1 and 10.</mat-error>
        }
      </mat-form-field>

      <mat-form-field appearance="outline">
        <mat-label>Cooldown value</mat-label>
        <input matInput type="number" formControlName="cooldownValue" min="0" step="1" />
        <app-info-popover
          matSuffix
          title="Cooldown Value"
          description="Cooldown value sets how long the strategy waits after a trade or completed cycle before it can enter again. A value of 0 allows immediate reuse of the next valid signal."
        />
      </mat-form-field>

      <mat-form-field appearance="outline">
        <mat-label>Cooldown unit</mat-label>
        <mat-select formControlName="cooldownUnit">
          <mat-option value="candles">Candles</mat-option>
          <mat-option value="minutes">Minutes</mat-option>
        </mat-select>
        <app-info-popover
          matSuffix
          title="Cooldown Unit"
          description="Cooldown unit defines whether the cooldown is measured in strategy candles or elapsed clock minutes. Candle-based cooldowns stay aligned with deterministic candle-close execution."
        />
      </mat-form-field>

      <mat-checkbox formControlName="allowSameCandleReentry">Allow same candle re-entry</mat-checkbox>
    </div>

    @if (showRiskWarning) {
      <div class="risk-card__warning">
        ⚠️ Risk per trade is above 5%. This is aggressive — ensure you understand the impact on drawdown.
      </div>
    }

    @if (isRiskBased && stopLossNotEnabled) {
      <div class="risk-card__error">
        Risk-based sizing requires a stop-loss. Enable a stop-loss in the exit rules section.
      </div>
    }
  </mat-card-content>
</mat-card>
```

##### Pattern References

- Existing template: `frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.html`
- Conditional `@if` pattern: `frontend/trading-ui/src/app/features/strategy-builder/components/exit-rules-card/exit-rules-card.component.html`

---

### Task 2.3: Update `risk-management-card.component.scss` for new layout sections {#task-23-update-styles}

Add styles for the slide toggle, warning banner, error message, and ensure the preview section integrates into the responsive grid.

- **Complexity**: Low
- **Risk Factors**: None — follows existing CSS token patterns
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.scss` — modification
- **Success**:
  - Warning banner styled with `--colour-warning-*` tokens
  - Error message styled with `--colour-error-*` tokens
  - Slide toggle aligns within the grid
- **Dependencies**: Task 2.2

#### Implementation Details

```scss
// frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.scss — full replacement
.risk-card {
  background: var(--colour-surface-dark);
  border: 1px solid var(--colour-border-subtle);

  .mat-mdc-card-header {
    padding: 1.1rem 1rem 0.45rem;
    min-height: 0;
  }

  .mat-mdc-card-header-text {
    margin: 0;
  }

  .mat-mdc-card-content {
    padding: 0 1rem 1rem;
  }

  &__title {
    display: flex;
    align-items: center;
    gap: 0.3rem;
    margin: 0;
    line-height: 1.2;
  }

  &__grid {
    display: grid;
    gap: 1rem;
    grid-template-columns: repeat(auto-fit, minmax(12rem, 1fr));
  }

  &__toggle {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    padding-top: 0.5rem;
  }

  &__warning {
    margin-top: 0.75rem;
    padding: 0.6rem 0.85rem;
    border-radius: 4px;
    background: var(--colour-warning-surface, rgba(255, 152, 0, 0.1));
    border: 1px solid var(--colour-warning-border, rgba(255, 152, 0, 0.3));
    color: var(--colour-warning-text, #ffb74d);
    font-size: 0.85rem;
    line-height: 1.4;
  }

  &__error {
    margin-top: 0.75rem;
    padding: 0.6rem 0.85rem;
    border-radius: 4px;
    background: var(--colour-error-surface, rgba(244, 67, 54, 0.1));
    border: 1px solid var(--colour-error-border, rgba(244, 67, 54, 0.3));
    color: var(--colour-error-text, #ef5350);
    font-size: 0.85rem;
    line-height: 1.4;
  }

  app-info-popover[matSuffix] {
    margin-right: 0.15rem;
  }
}
```

##### Pattern References

- Current SCSS: `frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.scss`
- CSS token pattern: all existing card components use `var(--colour-*)` tokens

---

### Task 2.4: Create `risk-management-card.component.spec.ts` {#task-24-create-unit-tests}

Create unit tests for the risk management card covering mode switching, field visibility, warning banner, and error states.

- **Complexity**: High
- **Risk Factors**: First spec for this component — TestBed configuration needs all Material module imports
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.spec.ts` — new file
- **Success**:
  - Tests for: mode switching shows/hides correct fields, warning banner at >5%, SL required message, leverage conditional on autoLeverage, hasError helper
  - All tests pass
- **Dependencies**: Tasks 2.1–2.3

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.spec.ts — new file
import { ComponentFixture, TestBed } from "@angular/core/testing";
import { FormBuilder, FormGroup, Validators } from "@angular/forms";
import { NoopAnimationsModule } from "@angular/platform-browser/animations";
import { RiskManagementCardComponent } from "./risk-management-card.component";

describe("RiskManagementCardComponent", () => {
  let component: RiskManagementCardComponent;
  let fixture: ComponentFixture<RiskManagementCardComponent>;
  let group: FormGroup;
  let exitGroup: FormGroup;
  const fb = new FormBuilder();

  beforeEach(async () => {
    group = fb.group({
      positionSizeType: ["percent_wallet", Validators.required],
      positionSizeValue: [5, [Validators.required, Validators.min(0.01), Validators.max(100)]],
      leverage: [1, [Validators.required, Validators.min(1), Validators.max(50)]],
      maxOpenTrades: [1, [Validators.required, Validators.min(1), Validators.max(10)]],
      cooldownValue: [0, [Validators.min(0)]],
      cooldownUnit: ["candles", Validators.required],
      allowSameCandleReentry: [false],
      riskPerTradePercent: [1, [Validators.min(0.01), Validators.max(100)]],
      autoLeverage: [true],
    });

    exitGroup = fb.group({
      stopLoss: fb.group({
        enabled: [true],
        type: ["fixed_percent"],
        value: [2],
      }),
    });

    await TestBed.configureTestingModule({
      imports: [RiskManagementCardComponent, NoopAnimationsModule],
    }).compileComponents();

    fixture = TestBed.createComponent(RiskManagementCardComponent);
    component = fixture.componentInstance;
    component.group = group;
    component.exitGroup = exitGroup;
    fixture.detectChanges();
  });

  it("should create", () => {
    expect(component).toBeTruthy();
  });

  describe("Given percent_wallet mode", () => {
    it("When rendered Then positionSizeValue field is visible", () => {
      expect(fixture.nativeElement.querySelector("[formControlName='positionSizeValue']")).not.toBeNull();
    });

    it("When rendered Then riskPerTradePercent field is hidden", () => {
      expect(fixture.nativeElement.querySelector("[formControlName='riskPerTradePercent']")).toBeNull();
    });

    it("When rendered Then autoLeverage toggle is hidden", () => {
      expect(fixture.nativeElement.querySelector("[formControlName='autoLeverage']")).toBeNull();
    });

    it("When rendered Then leverage field is visible", () => {
      expect(fixture.nativeElement.querySelector("[formControlName='leverage']")).not.toBeNull();
    });
  });

  describe("Given risk_based mode", () => {
    beforeEach(() => {
      group.patchValue({ positionSizeType: "risk_based" });
      fixture.detectChanges();
    });

    it("When rendered Then riskPerTradePercent field is visible", () => {
      expect(fixture.nativeElement.querySelector("[formControlName='riskPerTradePercent']")).not.toBeNull();
    });

    it("When rendered Then positionSizeValue field is hidden", () => {
      expect(fixture.nativeElement.querySelector("[formControlName='positionSizeValue']")).toBeNull();
    });

    it("When rendered Then autoLeverage toggle is visible", () => {
      expect(fixture.nativeElement.querySelector("[formControlName='autoLeverage']")).not.toBeNull();
    });

    it("When autoLeverage is on Then leverage field is hidden", () => {
      group.patchValue({ autoLeverage: true });
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector("[formControlName='leverage']")).toBeNull();
    });

    it("When autoLeverage is off Then leverage field is visible", () => {
      group.patchValue({ autoLeverage: false });
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector("[formControlName='leverage']")).not.toBeNull();
    });
  });

  describe("Given risk warning", () => {
    it("When riskPerTradePercent is 8 Then warning banner is visible", () => {
      group.patchValue({ positionSizeType: "risk_based", riskPerTradePercent: 8 });
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector(".risk-card__warning")).not.toBeNull();
    });

    it("When riskPerTradePercent is 3 Then warning banner is hidden", () => {
      group.patchValue({ positionSizeType: "risk_based", riskPerTradePercent: 3 });
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector(".risk-card__warning")).toBeNull();
    });
  });

  describe("Given stop-loss validation", () => {
    it("When risk_based and SL disabled Then error message is visible", () => {
      group.patchValue({ positionSizeType: "risk_based" });
      exitGroup.get("stopLoss")?.patchValue({ enabled: false });
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector(".risk-card__error")).not.toBeNull();
    });

    it("When risk_based and SL enabled Then error message is hidden", () => {
      group.patchValue({ positionSizeType: "risk_based" });
      exitGroup.get("stopLoss")?.patchValue({ enabled: true });
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector(".risk-card__error")).toBeNull();
    });
  });

  describe("Given hasError helper", () => {
    it("When control has error and is touched Then returns true", () => {
      group.get("positionSizeValue")?.setValue(0);
      group.get("positionSizeValue")?.markAsTouched();
      expect(component.hasError("positionSizeValue", "min")).toBeTrue();
    });

    it("When control has no error Then returns false", () => {
      group.get("positionSizeValue")?.setValue(5);
      expect(component.hasError("positionSizeValue", "min")).toBeFalse();
    });
  });
});
```

##### Pattern References

- TestBed setup: `frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.spec.ts`
- DOM assertion: `frontend/trading-ui/src/app/features/dashboard/positions-table/set-sltp-modal/set-sltp.modal.component.spec.ts`

---

### Task 2.5: Build + lint + test verification {#task-25-build-lint-test}

Run `ng build`, `ng lint`, and `ng test --watch=false` to verify all changes compile and tests pass.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification step)
- **Success**:
  - `ng build` without errors
  - `ng lint` passes
  - `ng test --watch=false` — all tests pass including the new spec
- **Dependencies**: Tasks 2.1–2.4

## Phase Success Criteria

- Position size type dropdown includes "Risk-based (R%)" option
- Selecting `risk_based` shows `riskPerTradePercent` input and `autoLeverage` toggle
- Selecting `risk_based` hides `positionSizeValue` field
- `autoLeverage` on hides manual leverage input; off shows it
- Warning banner appears when `riskPerTradePercent > 5%`
- Error message appears when `risk_based` active and stop-loss not enabled
- All unit tests pass
- Frontend builds and lints without errors
