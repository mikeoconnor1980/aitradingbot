<!-- markdownlint-disable-file -->

# Task Details: SL/TP Modal — Liquidation Price, Live Price & Distance to Liquidation

## Phase 1: Modal Logic — Live Price, Liquidation Display & Validation

## Standards and Knowledge References

- **angular.instructions.md**: Standalone components, `inject()` for all DI, `takeUntilDestroyed(this._destroyRef)` for infinite observables, BEM SCSS naming, explicit `public`/`private` access, explicit return types, `_` prefix for private fields, member ordering
- **testing.instructions.md**: Angular tests use Jasmine (`describe`/`it`/`beforeEach`/`expect`) with `TestBed` and `ComponentFixture`
- **Domain**: `Position.liquidationPrice` is always a number (0 if unknown). `PriceUpdate.asset` uses `-PERP` suffix while `Position.asset` does not. Live price comes from `SignalRService.priceUpdate$` (Subject-backed, infinite observable)

## Design References

- `OrderEntryComponent._subscribeToPriceUpdates()` — canonical live price subscription pattern with `takeUntilDestroyed` and asset normalisation
- `CloseAllDialogComponent` spec — canonical dialog test setup with `MAT_DIALOG_DATA`, `MatDialogRef` spy, `NoopAnimationsModule`

---

### Task 1.1: Inject SignalRService and subscribe to live price updates {#task-11-inject-signalrservice-and-subscribe-to-live-price-updates}

Inject `SignalRService` and `DestroyRef` into the modal component. Add a `livePrice` field seeded from `position.markPrice`. Subscribe to `priceUpdate$` in the constructor, filtering for the position's asset and updating `livePrice` on each emission.

- **Complexity**: Medium
- **Risk Factors**: Asset name format mismatch (`"BTC"` vs `"BTC-PERP"`) could silently miss updates if not normalised
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/set-sltp-modal/set-sltp.modal.component.ts` — modification
- **Success**:
  - `SignalRService` and `DestroyRef` are injected via `inject()`
  - `livePrice` field exists and is seeded from `position.markPrice`
  - Subscription to `priceUpdate$` with `takeUntilDestroyed` filters by asset and updates `livePrice`
- **Dependencies**: None

#### Implementation Details

```typescript
// set-sltp.modal.component.ts — modification
// Add imports at top of file:
import { DestroyRef } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { SignalRService } from "../../../../core/services/signalr.service";

// Add new injected fields (after existing private readonly fields):
private readonly _signalRService = inject(SignalRService);
private readonly _destroyRef = inject(DestroyRef);

// Add new public field (after existing public readonly fields):
public livePrice: number;

// In constructor, after form creation:
this.livePrice = this.data.position.markPrice;

this._signalRService.priceUpdate$
  .pipe(takeUntilDestroyed(this._destroyRef))
  .subscribe((update) => {
    const positionAsset = this.data.position.asset.replace("-PERP", "").toUpperCase();
    const updateAsset = update.asset.replace("-PERP", "").toUpperCase();
    if (positionAsset === updateAsset) {
      this.livePrice = update.lastPrice;
    }
  });
```

##### Pattern References

- `frontend/trading-ui/src/app/features/order-entry/order-entry.component.ts` — `_subscribeToPriceUpdates()` method (lines ~190-200) uses identical pattern: `takeUntilDestroyed`, asset normalisation via `.replace("-PERP", "")`, updates `this.livePrice`

---

### Task 1.2: Add computed properties for liquidation distance and live price colour {#task-12-add-computed-properties-for-liquidation-distance-and-live-price-colour}

Add public methods to compute the percentage distance from live price to liquidation price, and to determine the CSS class for live price colour coding (green for profit, red for loss).

- **Complexity**: Medium
- **Risk Factors**: Division by zero if `liquidationPrice` is 0; direction-awareness for long vs short colour logic
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/set-sltp-modal/set-sltp.modal.component.ts` — modification
- **Success**:
  - `getDistanceToLiquidation()` returns a number (%) or `null` when liquidation price is 0
  - `getLivePriceClass()` returns `'set-sltp-modal__price--profit'` or `'set-sltp-modal__price--loss'` based on side and price vs entry
- **Dependencies**: Task 1.1 (livePrice field must exist)

#### Implementation Details

```typescript
// set-sltp.modal.component.ts — modification
// Add after existing public methods (after getTakeProfitErrorMessage):

public getDistanceToLiquidation(): number | null {
  const liquidationPrice = this.data.position.liquidationPrice;
  if (liquidationPrice <= 0) {
    return null;
  }

  return Math.abs((this.livePrice - liquidationPrice) / liquidationPrice) * 100;
}

public getLivePriceClass(): string {
  const entryPrice = this.data.position.entryPrice;
  if (this.livePrice === entryPrice) {
    return "";
  }

  const isInProfit = this.isLong
    ? this.livePrice > entryPrice
    : this.livePrice < entryPrice;

  return isInProfit ? "set-sltp-modal__price--profit" : "set-sltp-modal__price--loss";
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/positions-table/positions-table.component.ts` — `getMarkPriceClass()` uses identical direction-aware logic with `isLong` to return colour class names. Same pattern of checking side + price relationship.

---

### Task 1.3: Convert SL-beyond-liquidation check to a form validator {#task-13-convert-sl-beyond-liquidation-check-to-a-form-validator}

Replace the display-only `isSlBeyondLiquidation()` method with a `slBeyondLiquidation` validation error inside `_createSlValidator()`. Update `getStopLossErrorMessage()` to also return this error. Add `[disabled]="form.invalid"` to the Confirm button to block submission when the validator fires.

- **Complexity**: Medium
- **Risk Factors**: Must preserve the existing `slInvalidSide` validation alongside the new `slBeyondLiquidation` check; ordering of validator checks matters for user-visible error message priority
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/set-sltp-modal/set-sltp.modal.component.ts` — modification
- **Success**:
  - `_createSlValidator()` returns `{ slBeyondLiquidation: "..." }` when SL is beyond liquidation
  - `getStopLossErrorMessage()` returns the liquidation error message when applicable
  - `isSlBeyondLiquidation()` method is removed (replaced by form validator)
  - The validation error key (`slBeyondLiquidation`) makes the form invalid, which disables the Confirm button
- **Dependencies**: None (this modifies existing code, independent of Tasks 1.1-1.2)

#### Implementation Details

```typescript
// set-sltp.modal.component.ts — modification

// REMOVE the isSlBeyondLiquidation() method entirely.

// MODIFY _createSlValidator() to add liquidation check after the side check:
private _createSlValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const stopLossPrice = control.value as number | null;
    if (stopLossPrice == null) {
      return null;
    }

    const entryPrice = this.data.position.entryPrice;
    if (this.isLong && stopLossPrice >= entryPrice) {
      return { slInvalidSide: "Stop loss must be below entry price for long positions" };
    }

    if (!this.isLong && stopLossPrice <= entryPrice) {
      return { slInvalidSide: "Stop loss must be above entry price for short positions" };
    }

    // Liquidation price check
    const liquidationPrice = this.data.position.liquidationPrice;
    if (liquidationPrice > 0) {
      if (this.isLong && stopLossPrice <= liquidationPrice) {
        return { slBeyondLiquidation: "Stop loss is beyond liquidation price — it would never trigger" };
      }
      if (!this.isLong && stopLossPrice >= liquidationPrice) {
        return { slBeyondLiquidation: "Stop loss is beyond liquidation price — it would never trigger" };
      }
    }

    return null;
  };
}

// MODIFY getStopLossErrorMessage() to include liquidation error:
public getStopLossErrorMessage(): string | null {
  const control = this.form.controls.stopLossPrice;
  if (control.hasError("min")) {
    return "Stop loss must be greater than 0";
  }

  if (control.hasError("slInvalidSide")) {
    return control.getError("slInvalidSide");
  }

  return control.getError("slBeyondLiquidation") ?? null;
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/positions-table/set-sltp-modal/set-sltp.modal.component.ts` — existing `_createSlValidator()` and `isSlBeyondLiquidation()` methods. The liquidation check logic is directly transplanted from `isSlBeyondLiquidation()` into the validator.
- `frontend/trading-ui/src/app/features/dashboard/orders-table/modify-order-modal/modify-order.modal.component.ts` — Confirm button uses `[disabled]="form.invalid"` pattern.

---

### Task 1.4: Update template to show reference data and disable Confirm button {#task-14-update-template-to-show-reference-data-and-disable-confirm-button}

Add liquidation price, live price (colour-coded), and % distance to liquidation rows in the modal info grid. Replace the amber warning div with the `mat-error` driven by the form validator. Add `[disabled]="form.invalid"` to the Confirm button.

- **Complexity**: Medium
- **Risk Factors**: Info grid layout expanding from 3 rows to 6 rows; ensuring `@if` guards handle edge cases (liquidation price 0, null distance)
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/set-sltp-modal/set-sltp.modal.component.html` — modification
- **Success**:
  - Liquidation Price row appears in the info grid with muted label styling
  - Live Price row appears with dynamic colour class (green/red)
  - Distance to Liquidation row appears with `% away` suffix
  - Existing amber liquidation warning div is removed (replaced by `mat-error` in form)
  - Confirm button has `[disabled]="form.invalid"`
  - Rows with missing data (liquidation price 0) show `"—"` fallback
- **Dependencies**: Tasks 1.1, 1.2, 1.3

#### Implementation Details

```html
<!-- set-sltp.modal.component.html — modification -->
<!-- Replace the existing info grid with expanded version: -->

<div class="set-sltp-modal__info">
  <span class="set-sltp-modal__label">Side</span>
  <span [class]="isLong ? 'set-sltp-modal__long' : 'set-sltp-modal__short'">
    {{ isLong ? "Long" : "Short" }}
  </span>
  <span class="set-sltp-modal__label">Entry</span>
  <span>{{ data.position.entryPrice | number: "1.2-2" }}</span>
  <span class="set-sltp-modal__label">Size</span>
  <span>{{ data.position.size | number: "1.4-4" }}</span>

  <span class="set-sltp-modal__label">Liq. Price</span>
  @if (data.position.liquidationPrice > 0) {
    <span class="set-sltp-modal__muted">{{ data.position.liquidationPrice | number: "1.2-2" }}</span>
  } @else {
    <span class="set-sltp-modal__muted">—</span>
  }

  <span class="set-sltp-modal__label">Live Price</span>
  <span [class]="getLivePriceClass()">{{ livePrice | number: "1.2-2" }}</span>

  <span class="set-sltp-modal__label">Dist. to Liq.</span>
  @let distance = getDistanceToLiquidation();
  @if (distance !== null) {
    <span class="set-sltp-modal__muted">{{ distance | number: "1.1-1" }}% away</span>
  } @else {
    <span class="set-sltp-modal__muted">—</span>
  }
</div>

<!-- REMOVE the existing @if (isSlBeyondLiquidation()) warning div entirely -->
<!-- The slBeyondLiquidation error is now surfaced via mat-error in getStopLossErrorMessage() -->

<!-- Modify Confirm button to add disabled binding: -->
<button mat-flat-button color="primary" type="button" (click)="onSubmit()" [disabled]="form.invalid">Confirm</button>
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/positions-table/set-sltp-modal/set-sltp.modal.component.html` — existing info grid layout with label/value pairs in a CSS Grid
- `frontend/trading-ui/src/app/features/dashboard/orders-table/modify-order-modal/modify-order.modal.component.html` — `[disabled]="form.invalid"` on Confirm button

---

### Task 1.5: Add SCSS styles for new reference data rows {#task-15-add-scss-styles-for-new-reference-data-rows}

Add BEM-style classes for the new muted reference text and profit/loss colour coding of the live price field.

- **Complexity**: Low
- **Risk Factors**: None — straightforward CSS using existing variables
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/set-sltp-modal/set-sltp.modal.component.scss` — modification
- **Success**:
  - `__muted` class uses `var(--colour-muted)` with smaller font size
  - `__price--profit` class uses `var(--colour-profit)`
  - `__price--loss` class uses `var(--colour-loss)`
  - Liquidation warning hard-coded colour `#f59e0b` is replaced with `var(--colour-warning)` (if retained; otherwise the warning div is removed per Task 1.4)
- **Dependencies**: None

#### Implementation Details

```scss
// set-sltp.modal.component.scss — modification
// Add new BEM classes inside the .set-sltp-modal block:

  &__muted {
    color: var(--colour-muted);
    font-size: 0.82rem;
  }

  &__price--profit {
    color: var(--colour-profit);
    font-weight: 600;
  }

  &__price--loss {
    color: var(--colour-loss);
    font-weight: 600;
  }
```

Also remove the `&__liquidation-warning` block since the warning div is replaced by the form validator error.

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/positions-table/set-sltp-modal/set-sltp.modal.component.scss` — existing BEM classes (`__long`, `__short`, `__label`) using `var(--colour-profit)` and `var(--colour-loss)`
- `frontend/trading-ui/src/styles.scss` — global CSS variables: `--colour-profit`, `--colour-loss`, `--colour-muted`

---

### Task 1.6: Create comprehensive spec file for SetSlTpModalComponent {#task-16-create-comprehensive-spec-file-for-setSltpModalComponent}

Create `set-sltp.modal.component.spec.ts` with full test coverage for: info display (liquidation price, live price, distance), live price updates via SignalR mock, colour coding logic, form validation (SL beyond liquidation blocks submission), and Confirm button disabled state.

- **Complexity**: High
- **Risk Factors**: No existing spec file — must be created from scratch. Must mock `SignalRService.priceUpdate$` as a Subject to push test price updates. Must set up `MAT_DIALOG_DATA` with various position configurations.
- **Files**:
  - `frontend/trading-ui/src/app/features/dashboard/positions-table/set-sltp-modal/set-sltp.modal.component.spec.ts` — new file
- **Success**:
  - Test file created and all tests pass
  - Tests cover: initial display values, live price subscription updates, colour class computation, distance to liquidation calculation, SL-beyond-liquidation validation error, Confirm button disabled when form invalid, Confirm button enabled when form valid, submit result structure, cancel closes dialog
- **Dependencies**: Tasks 1.1–1.5

#### Implementation Details

```typescript
// set-sltp.modal.component.spec.ts — new file
import { ComponentFixture, TestBed } from "@angular/core/testing";
import { MAT_DIALOG_DATA, MatDialogRef } from "@angular/material/dialog";
import { NoopAnimationsModule } from "@angular/platform-browser/animations";
import { Subject } from "rxjs";
import { Position } from "../../../../core/models/position.model";
import { PriceUpdate } from "../../../../core/models/price-update.model";
import { SignalRService } from "../../../../core/services/signalr.service";
import { SetSlTpDialogData, SetSlTpModalComponent } from "./set-sltp.modal.component";

const mockLongPosition: Position = {
  asset: "BTC",
  side: "Long",
  size: 0.001,
  entryPrice: 50000,
  markPrice: 51000,
  unrealisedPnl: 10,
  unrealisedPnlPercent: 2,
  liquidationPrice: 42000,
  leverage: 10,
  marginMode: "cross",
  marginUsed: 5.1,
  fundingRate: -0.0001
};

const mockShortPosition: Position = {
  asset: "BTC",
  side: "Short",
  size: 0.001,
  entryPrice: 50000,
  markPrice: 49000,
  unrealisedPnl: 10,
  unrealisedPnlPercent: 2,
  liquidationPrice: 58000,
  leverage: 10,
  marginMode: "cross",
  marginUsed: 5.1,
  fundingRate: 0.0001
};

describe("SetSlTpModalComponent", () => {
  let component: SetSlTpModalComponent;
  let fixture: ComponentFixture<SetSlTpModalComponent>;
  let dialogRefSpy: jasmine.SpyObj<MatDialogRef<SetSlTpModalComponent>>;
  let priceSubject: Subject<PriceUpdate>;

  function createComponent(position: Position): void {
    dialogRefSpy = jasmine.createSpyObj("MatDialogRef", ["close"]);
    priceSubject = new Subject<PriceUpdate>();

    TestBed.configureTestingModule({
      imports: [SetSlTpModalComponent, NoopAnimationsModule],
      providers: [
        { provide: MatDialogRef, useValue: dialogRefSpy },
        { provide: MAT_DIALOG_DATA, useValue: { position } as SetSlTpDialogData },
        { provide: SignalRService, useValue: { priceUpdate$: priceSubject.asObservable() } }
      ]
    });

    fixture = TestBed.createComponent(SetSlTpModalComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  describe("with long position", () => {
    beforeEach(() => createComponent(mockLongPosition));

    it("should seed livePrice from markPrice", () => {
      expect(component.livePrice).toBe(51000);
    });

    it("should update livePrice on matching SignalR price update", () => {
      priceSubject.next({ asset: "BTC-PERP", lastPrice: 52000, high24h: 0, low24h: 0, volume24h: 0, timestamp: 0 });
      expect(component.livePrice).toBe(52000);
    });

    it("should ignore price updates for non-matching assets", () => {
      priceSubject.next({ asset: "ETH-PERP", lastPrice: 3000, high24h: 0, low24h: 0, volume24h: 0, timestamp: 0 });
      expect(component.livePrice).toBe(51000);
    });

    it("should return profit class when live price above entry for long", () => {
      expect(component.getLivePriceClass()).toBe("set-sltp-modal__price--profit");
    });

    it("should return loss class when live price below entry for long", () => {
      priceSubject.next({ asset: "BTC-PERP", lastPrice: 49000, high24h: 0, low24h: 0, volume24h: 0, timestamp: 0 });
      expect(component.getLivePriceClass()).toBe("set-sltp-modal__price--loss");
    });

    it("should compute distance to liquidation", () => {
      const distance = component.getDistanceToLiquidation();
      // (51000 - 42000) / 42000 * 100 ≈ 21.43%
      expect(distance).toBeCloseTo(21.43, 1);
    });

    it("should return slBeyondLiquidation error when SL is below liquidation for long", () => {
      component.form.controls.stopLossPrice.setValue(41000);
      expect(component.form.controls.stopLossPrice.hasError("slBeyondLiquidation")).toBeTrue();
    });

    it("should return liquidation error message from getStopLossErrorMessage", () => {
      component.form.controls.stopLossPrice.setValue(41000);
      expect(component.getStopLossErrorMessage()).toBe("Stop loss is beyond liquidation price — it would never trigger");
    });

    it("should not return slBeyondLiquidation error for valid SL", () => {
      component.form.controls.stopLossPrice.setValue(45000);
      expect(component.form.controls.stopLossPrice.hasError("slBeyondLiquidation")).toBeFalse();
    });

    it("should disable Confirm button when form is invalid", () => {
      component.form.controls.stopLossPrice.setValue(41000);
      fixture.detectChanges();
      const button = fixture.nativeElement.querySelector("button[color='primary']") as HTMLButtonElement;
      expect(button.disabled).toBeTrue();
    });

    it("should close dialog with result on submit", () => {
      component.form.controls.stopLossPrice.setValue(45000);
      component.form.controls.takeProfitPrice.setValue(55000);
      component.onSubmit();
      expect(dialogRefSpy.close).toHaveBeenCalledWith(
        jasmine.objectContaining({ stopLossPrice: 45000, takeProfitPrice: 55000 })
      );
    });

    it("should close dialog without result on cancel", () => {
      component.onCancel();
      expect(dialogRefSpy.close).toHaveBeenCalledWith();
    });
  });

  describe("with short position", () => {
    beforeEach(() => createComponent(mockShortPosition));

    it("should return profit class when live price below entry for short", () => {
      expect(component.getLivePriceClass()).toBe("set-sltp-modal__price--profit");
    });

    it("should return slBeyondLiquidation error when SL is above liquidation for short", () => {
      component.form.controls.stopLossPrice.setValue(59000);
      expect(component.form.controls.stopLossPrice.hasError("slBeyondLiquidation")).toBeTrue();
    });
  });

  describe("with zero liquidation price", () => {
    beforeEach(() => createComponent({ ...mockLongPosition, liquidationPrice: 0 }));

    it("should return null for distance to liquidation", () => {
      expect(component.getDistanceToLiquidation()).toBeNull();
    });

    it("should not return slBeyondLiquidation error", () => {
      component.form.controls.stopLossPrice.setValue(1);
      expect(component.form.controls.stopLossPrice.hasError("slBeyondLiquidation")).toBeFalse();
    });
  });
});
```

##### Pattern References

- `frontend/trading-ui/src/app/features/dashboard/positions-table/close-all-dialog/close-all-dialog.component.spec.ts` — Dialog TestBed setup with `MAT_DIALOG_DATA`, `MatDialogRef` spy, `NoopAnimationsModule`. Mock position data structure.
- `frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.spec.ts` — Reactive form validation testing patterns (`form.controls.x.setValue()`, `hasError()`)

---

### Task 1.7: Run frontend build and lint {#task-17-run-frontend-build-and-lint}

Run `ng build` and `ng lint` to verify the changes compile and follow code standards. Run `ng test --watch=false` to verify all tests pass.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification step)
- **Success**:
  - `ng build` completes without errors
  - `ng lint` completes without errors
  - `ng test --watch=false` completes with all tests passing
- **Dependencies**: Tasks 1.1–1.6

---

## Phase Success Criteria

- Modal displays liquidation price, live price (colour-coded), and % distance to liquidation in the header section
- Live price updates in real-time from SignalR `priceUpdate$`
- Setting SL beyond liquidation price produces a form validation error, disables Confirm, and shows inline error message
- Correcting or clearing SL removes the error and re-enables Confirm
- All existing tests continue to pass
- New spec file has full coverage of new and existing functionality
- Frontend builds and lints cleanly
