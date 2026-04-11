<!-- markdownlint-disable-file -->

# Task Details: Risk Management UI — R-Based Position Sizing

## Phase 3: Live Calculation Preview

## Standards and Knowledge References

- `angular.instructions.md` — `inject()` DI, `DestroyRef` + `takeUntilDestroyed`, SCSS tokens, new control flow
- `33-risk-management-and-trade-sizing.md` — R-based sizing formulas:
  - `R = equity × riskPerTradePercent / 100`
  - `positionNotional = R / (SL% / 100)`
  - `autoLeverage = 1 / (SL% / 100 + maintenanceMarginRate)`
  - `maintenanceMarginRate = 0.5 / maxLeverage` (0.01 for BTC@50x)
  - `margin = notional / leverage`
  - `estLiquidation = entry ± (entry × (SL% / 100 + maintenanceMarginRate))`

## Design References

- `HyperliquidApiService.getAccountSummary()` returns `Observable<AccountSummary>` with `.equity` — existing endpoint
- Backtest form inline preview pattern: `backtest-form.component.html` `.backtest-form__preview-grid` div with label/value pairs
- Preview only works with `fixed_percent` SL type — non-percentage types show a "configure fixed-percent SL" message
- Maintenance margin rate assumption: `0.01` (BTC@50x max leverage). Hardcoded for now — future PBI may make this per-asset

---

### Task 3.1: Add equity fetching and preview calculation logic to `risk-management-card.component.ts` {#task-31-add-equity-fetching-and-preview-logic}

Inject `HyperliquidApiService`, fetch equity on init, subscribe to SL and risk field changes, and compute preview values reactively.

- **Complexity**: High
- **Risk Factors**: Equity fetch may fail (no wallet connected) — must handle null/zero equity gracefully; reactive stream must combine multiple value changes
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.ts` — modification
- **Success**:
  - Equity fetched on init via `HyperliquidApiService.getAccountSummary()`
  - Preview values computed: `rAmount`, `positionSize`, `derivedLeverage`, `marginRequired`, `estLiquidation`
  - Preview updates reactively on `riskPerTradePercent`, `autoLeverage`, or SL value change
  - Null guard: `equity = 0` or no wallet → preview shows "Connect wallet to see preview"
  - SL guard: no SL or non-`fixed_percent` SL → preview shows "Configure a fixed-percent stop-loss to see position sizing preview"
- **Dependencies**: Phase 2 complete

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.ts — additions
// Add to imports at top of file:
import { HyperliquidApiService } from "../../../../core/services/hyperliquid-api.service";
import { DecimalPipe } from "@angular/common";
import { catchError, of } from "rxjs";

// Add DecimalPipe to the component's imports array (alongside existing imports):
// imports: [...existing imports, DecimalPipe],

// Add inside the class:

  private readonly _apiService = inject(HyperliquidApiService);

  public equity: number = 0;
  public rAmount: number = 0;
  public positionSize: number = 0;
  public derivedLeverage: number = 0;
  public marginRequired: number = 0;
  public estLiquidationPercent: number = 0;

  // Maintenance margin rate: 0.5 / maxLeverage (BTC@50x = 0.01)
  private readonly _maintenanceMarginRate = 0.01;

  public get showPreview(): boolean {
    return this.isRiskBased && this.equity > 0 && this.stopLossPercent > 0;
  }

  public get noEquity(): boolean {
    return this.equity <= 0;
  }

  public get stopLossPercent(): number {
    if (!this.exitGroup) {
      return 0;
    }
    const slEnabled = Boolean(this.exitGroup.get("stopLoss.enabled")?.value);
    const slType = String(this.exitGroup.get("stopLoss.type")?.value ?? "");
    const slValue = Number(this.exitGroup.get("stopLoss.value")?.value ?? 0);

    if (!slEnabled || slType !== "fixed_percent" || slValue <= 0) {
      return 0;
    }
    return slValue;
  }

  // Add to ngOnInit() — after existing sync calls:
  // this._fetchEquity();
  // this._subscribeToPreviewInputs();

  private _fetchEquity(): void {
    this._apiService.getAccountSummary()
      .pipe(
        catchError(() => of(null)),
        takeUntilDestroyed(this._destroyRef)
      )
      .subscribe((summary) => {
        this.equity = summary?.equity ?? 0;
        this._recalcPreview();
      });
  }

  private _subscribeToPreviewInputs(): void {
    // Watch risk fields
    const riskPercent = this.group.get("riskPerTradePercent");
    const autoLeverage = this.group.get("autoLeverage");

    riskPercent?.valueChanges
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe(() => this._recalcPreview());

    autoLeverage?.valueChanges
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe(() => this._recalcPreview());

    // Watch SL fields from exitGroup
    if (this.exitGroup) {
      this.exitGroup.get("stopLoss.enabled")?.valueChanges
        .pipe(takeUntilDestroyed(this._destroyRef))
        .subscribe(() => this._recalcPreview());

      this.exitGroup.get("stopLoss.type")?.valueChanges
        .pipe(takeUntilDestroyed(this._destroyRef))
        .subscribe(() => this._recalcPreview());

      this.exitGroup.get("stopLoss.value")?.valueChanges
        .pipe(takeUntilDestroyed(this._destroyRef))
        .subscribe(() => this._recalcPreview());
    }
  }

  private _recalcPreview(): void {
    const riskPercent = Number(this.group.get("riskPerTradePercent")?.value ?? 0);
    const slPercent = this.stopLossPercent;
    const autoLev = Boolean(this.group.get("autoLeverage")?.value);

    if (this.equity <= 0 || riskPercent <= 0 || slPercent <= 0) {
      this.rAmount = 0;
      this.positionSize = 0;
      this.derivedLeverage = 0;
      this.marginRequired = 0;
      this.estLiquidationPercent = 0;
      return;
    }

    // R = equity × riskPerTradePercent / 100
    this.rAmount = this.equity * (riskPercent / 100);

    // Position Size = R / (SL% / 100)
    this.positionSize = this.rAmount / (slPercent / 100);

    // Auto-leverage = 1 / (SL% / 100 + maintenanceMarginRate)
    if (autoLev) {
      this.derivedLeverage = Math.floor(1 / (slPercent / 100 + this._maintenanceMarginRate));
    } else {
      this.derivedLeverage = Number(this.group.get("leverage")?.value ?? 1);
    }

    // Margin = notional / leverage
    this.marginRequired = this.derivedLeverage > 0 ? this.positionSize / this.derivedLeverage : this.positionSize;

    // Est. liquidation % = SL% + maintenance margin rate × 100
    this.estLiquidationPercent = slPercent + (this._maintenanceMarginRate * 100);
  }
```

##### Pattern References

- Equity fetching: `frontend/trading-ui/src/app/core/services/hyperliquid-api.service.ts` `getAccountSummary()`
- Reactive valueChanges: `frontend/trading-ui/src/app/features/strategy-builder/components/exit-rules-card/exit-rules-card.component.ts`
- `catchError` + `of(null)` pattern: common RxJS error handling

---

### Task 3.2: Add preview panel template to `risk-management-card.component.html` {#task-32-add-preview-panel-template}

Add an inline preview panel below the risk fields that shows R, position size, leverage, margin, and estimated liquidation distance.

- **Complexity**: Medium
- **Risk Factors**: Must handle edge cases (no equity, no SL) with appropriate messages
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.html` — modification
- **Success**:
  - Preview panel visible when `risk_based` and SL configured and equity > 0
  - Shows R, Position Size, Leverage, Margin Required, Est. Liquidation Distance
  - Shows "Connect wallet to see preview" when no equity
  - Shows "Configure a fixed-percent stop-loss to see position sizing preview" when no valid SL
- **Dependencies**: Task 3.1

#### Implementation Details

```html
<!-- frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.html — append before closing </mat-card> -->
<!-- Add after the risk-card__error div, before </mat-card-content> -->

    @if (isRiskBased) {
      <div class="risk-card__preview">
        <div class="risk-card__preview-header">
          <span>Position Sizing Preview</span>
          <app-info-popover
            title="Position Sizing Preview"
            description="This preview shows how your R-based risk settings translate into actual position sizing. R is your dollar risk per trade, position size is the total notional exposure, and leverage is derived from your stop-loss distance."
          />
        </div>

        @if (noEquity) {
          <div class="risk-card__preview-message">
            Connect a wallet to see position sizing preview.
          </div>
        } @else if (stopLossPercent <= 0) {
          <div class="risk-card__preview-message">
            Configure a fixed-percent stop-loss to see position sizing preview.
          </div>
        } @else if (showPreview) {
          <div class="risk-card__preview-grid">
            <div class="risk-card__preview-item">
              <span class="risk-card__preview-label">R (risk per trade)</span>
              <span class="risk-card__preview-value">${{ rAmount | number:"1.2-2" }}</span>
            </div>
            <div class="risk-card__preview-item">
              <span class="risk-card__preview-label">Position Size</span>
              <span class="risk-card__preview-value">${{ positionSize | number:"1.2-2" }}</span>
            </div>
            <div class="risk-card__preview-item">
              <span class="risk-card__preview-label">Leverage</span>
              <span class="risk-card__preview-value">{{ derivedLeverage }}x</span>
            </div>
            <div class="risk-card__preview-item">
              <span class="risk-card__preview-label">Margin Required</span>
              <span class="risk-card__preview-value">≈${{ marginRequired | number:"1.2-2" }}</span>
            </div>
            <div class="risk-card__preview-item">
              <span class="risk-card__preview-label">Est. Liquidation Distance</span>
              <span class="risk-card__preview-value">≈{{ estLiquidationPercent | number:"1.1-1" }}%</span>
            </div>
          </div>
        }
      </div>
    }
```

**Note**: `DecimalPipe` import was added in Task 3.1 above — ensure it is included in the component's `imports` array.

##### Pattern References

- Inline preview layout: `frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.html` `.backtest-form__preview-grid`

---

### Task 3.3: Add preview panel styles to `risk-management-card.component.scss` {#task-33-add-preview-panel-styles}

Add styles for the preview panel with header, message, and grid layout.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.scss` — modification
- **Success**:
  - Preview panel has a subtle background, border, and fits within the card
  - Grid layout for preview items is responsive
- **Dependencies**: Task 3.2

#### Implementation Details

```scss
// frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.scss — additions
// Add inside the .risk-card block:

  &__preview {
    margin-top: 1rem;
    padding: 0.85rem;
    border-radius: 4px;
    background: var(--colour-surface-darker, rgba(0, 0, 0, 0.15));
    border: 1px solid var(--colour-border-subtle);
  }

  &__preview-header {
    display: flex;
    align-items: center;
    gap: 0.3rem;
    font-size: 0.9rem;
    font-weight: 500;
    margin-bottom: 0.65rem;
    color: var(--colour-text-secondary);
  }

  &__preview-message {
    font-size: 0.85rem;
    color: var(--colour-text-muted, rgba(255, 255, 255, 0.5));
    font-style: italic;
  }

  &__preview-grid {
    display: grid;
    gap: 0.5rem 1.5rem;
    grid-template-columns: repeat(auto-fit, minmax(10rem, 1fr));
  }

  &__preview-item {
    display: flex;
    flex-direction: column;
    gap: 0.15rem;
  }

  &__preview-label {
    font-size: 0.75rem;
    color: var(--colour-text-muted, rgba(255, 255, 255, 0.5));
    text-transform: uppercase;
    letter-spacing: 0.03em;
  }

  &__preview-value {
    font-size: 0.95rem;
    font-weight: 500;
    color: var(--colour-text-primary);
  }
```

##### Pattern References

- CSS tokens + BEM naming: `frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.scss`

---

### Task 3.4: Add unit tests for preview calculations {#task-34-add-preview-calculation-tests}

Add unit tests to the existing `risk-management-card.component.spec.ts` covering preview calculation logic, equity handling, and SL reactive updates.

- **Complexity**: Medium
- **Risk Factors**: Need to mock `HyperliquidApiService.getAccountSummary()` — service injection in standalone component spec
- **Files**:
  - `frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.spec.ts` — modification
- **Success**:
  - Tests verify preview values for known inputs (R = $100, Position Size = $5,000 at 1% risk, 2% SL, $10,000 equity)
  - Tests verify preview updates when SL changes
  - Tests verify "no equity" message
  - Tests verify "no SL" message
- **Dependencies**: Tasks 3.1–3.3

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/strategy-builder/components/risk-management-card/risk-management-card.component.spec.ts — modifications
// Update imports and TestBed setup:
import { HyperliquidApiService } from "../../../../core/services/hyperliquid-api.service";
import { of } from "rxjs";

// In beforeEach, add service mock:
let apiService: jasmine.SpyObj<HyperliquidApiService>;

// Before TestBed.configureTestingModule:
apiService = jasmine.createSpyObj<HyperliquidApiService>("HyperliquidApiService", ["getAccountSummary"]);
apiService.getAccountSummary.and.returnValue(of({ equity: 10000, availableMargin: 8000, crossMarginRatio: 0, maintenanceMargin: 0, unrealisedPnl: 0 }));

// In TestBed providers:
providers: [{ provide: HyperliquidApiService, useValue: apiService }],

// Add test suites:

  describe("Given preview calculation", () => {
    beforeEach(() => {
      group.patchValue({ positionSizeType: "risk_based", riskPerTradePercent: 1, autoLeverage: true });
      exitGroup.get("stopLoss")?.patchValue({ enabled: true, type: "fixed_percent", value: 2 });
      fixture.detectChanges();
    });

    it("When equity=$10,000 risk=1% SL=2% Then R=$100", () => {
      expect(component.rAmount).toBeCloseTo(100, 2);
    });

    it("When equity=$10,000 risk=1% SL=2% Then positionSize=$5,000", () => {
      expect(component.positionSize).toBeCloseTo(5000, 0);
    });

    it("When autoLeverage and SL=2% Then derivedLeverage=33", () => {
      // 1 / (0.02 + 0.01) = 33.33, floor = 33
      expect(component.derivedLeverage).toBe(33);
    });

    it("When SL changes from 2% to 5% Then positionSize shrinks", () => {
      exitGroup.get("stopLoss")?.patchValue({ value: 5 });
      fixture.detectChanges();
      // R = $100, positionSize = 100 / (5/100) = $2,000
      expect(component.positionSize).toBeCloseTo(2000, 0);
    });

    it("When SL changes from 2% to 5% Then leverage drops", () => {
      exitGroup.get("stopLoss")?.patchValue({ value: 5 });
      fixture.detectChanges();
      // 1 / (0.05 + 0.01) = 16.66, floor = 16
      expect(component.derivedLeverage).toBe(16);
    });
  });

  describe("Given no equity", () => {
    beforeEach(() => {
      apiService.getAccountSummary.and.returnValue(of({ equity: 0, availableMargin: 0, crossMarginRatio: 0, maintenanceMargin: 0, unrealisedPnl: 0 }));
      fixture = TestBed.createComponent(RiskManagementCardComponent);
      component = fixture.componentInstance;
      component.group = group;
      component.exitGroup = exitGroup;
      group.patchValue({ positionSizeType: "risk_based" });
      fixture.detectChanges();
    });

    it("When risk_based Then shows no-equity message", () => {
      expect(fixture.nativeElement.querySelector(".risk-card__preview-message")?.textContent).toContain("Connect a wallet");
    });
  });

  describe("Given no stop-loss", () => {
    it("When risk_based and SL disabled Then shows configure SL message", () => {
      group.patchValue({ positionSizeType: "risk_based" });
      exitGroup.get("stopLoss")?.patchValue({ enabled: false });
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector(".risk-card__preview-message")?.textContent).toContain("fixed-percent stop-loss");
    });

    it("When risk_based and SL type is swing_low Then shows configure SL message", () => {
      group.patchValue({ positionSizeType: "risk_based" });
      exitGroup.get("stopLoss")?.patchValue({ enabled: true, type: "swing_low" });
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector(".risk-card__preview-message")?.textContent).toContain("fixed-percent stop-loss");
    });
  });
```

##### Pattern References

- Service mocking: `frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.spec.ts`

---

### Task 3.5: Build + test verification {#task-35-build-test-verification}

Run `ng build`, `ng lint`, and `ng test --watch=false`.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification step)
- **Success**:
  - Frontend builds without errors
  - All tests pass including new preview tests
- **Dependencies**: Tasks 3.1–3.4

## Phase Success Criteria

- Preview panel appears when `risk_based` mode is active, equity available, and fixed-percent SL configured
- Preview shows R, Position Size, Leverage, Margin Required, and Est. Liquidation Distance
- Preview updates reactively when `riskPerTradePercent`, `autoLeverage`, or SL value changes
- "Connect wallet" message when equity is 0
- "Configure a fixed-percent stop-loss" message when SL not configured or non-percentage type
- All unit tests pass
- Frontend builds and lints without errors
