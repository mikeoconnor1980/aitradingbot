<!-- markdownlint-disable-file -->

# Task Details: Stop Loss & Take Profit

## Phase 2: Frontend — Order Entry with SL/TP

## Standards and Knowledge References

- `.github/instructions/angular.instructions.md` — standalone components, `inject()`, typed reactive forms, BEM SCSS, `takeUntilDestroyed`, Observable naming
- `.github/instructions/csharp.instructions.md` — (backend model changes only — PlaceOrderRequest extension)
- `.agent-context/0-knowledge/07-ui-design.md` — dashboard UI features
- Angular Material conventions: `MatFormField`, `MatInput`, `MatSlideToggle`, `MatDialog`

## Design References

- Collapsible section pattern: existing `@if (isLimitOrder())` in order-entry template
- Conditional validator pattern: existing `orderType.valueChanges.subscribe(...)` that toggles validators dynamically
- Confirm dialog pattern: existing `ConfirmDialogComponent` with data-driven summary rows

---

### Task 2.1: Extend PlaceOrderRequest model with SL/TP fields {#task-21-extend-placeorderrequest-model-with-sltp-fields}

Add optional `stopLossPrice` and `takeProfitPrice` to the frontend `PlaceOrderRequest` interface.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/core/models/place-order.model.ts` — modification
- **Success**:
  - `PlaceOrderRequest` interface has `stopLossPrice?: number | null` and `takeProfitPrice?: number | null`
  - No build errors

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/core/models/place-order.model.ts — modification
// Add to the existing interface:
export interface PlaceOrderRequest {
  // ... existing fields ...
  stopLossPrice?: number | null;
  takeProfitPrice?: number | null;
}
```

##### Pattern References

- `frontend/trading-ui/src/app/core/models/place-order.model.ts` — existing interface structure

---

### Task 2.2: Add SL/TP toggle section to order-entry component {#task-22-add-sltp-toggle-section-to-order-entry-component}

Add a collapsible "Add SL/TP" toggle that reveals Stop Loss and Take Profit price input fields below the Size field. Add the form controls to the typed reactive form.

- **Complexity**: Medium
- **Risk Factors**: Form group type must be extended without breaking existing form submission logic
- **Files**:
  - `frontend/trading-ui/src/app/features/order-entry/order-entry.component.ts` — modification
  - `frontend/trading-ui/src/app/features/order-entry/order-entry.component.html` — modification
  - `frontend/trading-ui/src/app/features/order-entry/order-entry.component.scss` — modification
- **Success**:
  - "Add SL/TP" toggle appears below the Size field, collapsed by default
  - When toggled, Stop Loss and Take Profit input fields appear
  - Form controls are properly typed with `FormControl<number | null>`
  - SL/TP values are included in the `PlaceOrderRequest` when set
  - When toggle is collapsed, SL/TP values are cleared
- **Dependencies**:
  - Task 2.1 (model fields)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/order-entry/order-entry.component.ts — modification
// 1. Extend the OrderEntryForm interface:
interface OrderEntryForm {
  side: FormControl<'buy' | 'sell'>;
  orderType: FormControl<'market' | 'limit'>;
  price: FormControl<number | null>;
  size: FormControl<number | null>;
  stopLossPrice: FormControl<number | null>;
  takeProfitPrice: FormControl<number | null>;
}

// 2. Add signal for toggle state:
protected showSlTp = signal(false);

// 3. In form initialization (ngOnInit or constructor), add controls:
this.orderForm = this._fb.group<OrderEntryForm>({
  // ... existing controls ...
  stopLossPrice: this._fb.control<number | null>(null),
  takeProfitPrice: this._fb.control<number | null>(null),
});

// 4. Add toggle method:
protected toggleSlTp(): void {
  this.showSlTp.update(v => !v);
  if (!this.showSlTp()) {
    this.orderForm.controls.stopLossPrice.setValue(null);
    this.orderForm.controls.takeProfitPrice.setValue(null);
    this.orderForm.controls.stopLossPrice.clearValidators();
    this.orderForm.controls.takeProfitPrice.clearValidators();
    this.orderForm.controls.stopLossPrice.updateValueAndValidity();
    this.orderForm.controls.takeProfitPrice.updateValueAndValidity();
  }
}

// 5. In the submit method, include SL/TP in the request:
const request: PlaceOrderRequest = {
  // ... existing fields ...
  stopLossPrice: this.orderForm.controls.stopLossPrice.value,
  takeProfitPrice: this.orderForm.controls.takeProfitPrice.value,
};
```

```html
<!-- frontend/trading-ui/src/app/features/order-entry/order-entry.component.html — modification -->
<!-- Add after the Size field section, before the Submit button: -->

<div class="order-entry__sltp-toggle">
  <button type="button"
          class="order-entry__sltp-toggle-btn"
          (click)="toggleSlTp()">
    {{ showSlTp() ? '− Hide SL/TP' : '+ Add SL/TP' }}
  </button>
</div>

@if (showSlTp()) {
  <div class="order-entry__field">
    <mat-form-field appearance="outline" class="order-entry__input">
      <mat-label>Stop Loss Price (USD)</mat-label>
      <input matInput type="number" formControlName="stopLossPrice" step="0.01" />
      @if (orderForm.controls.stopLossPrice.hasError('slInvalidSide')) {
        <mat-error>{{ orderForm.controls.stopLossPrice.getError('slInvalidSide') }}</mat-error>
      }
    </mat-form-field>
  </div>

  <div class="order-entry__field">
    <mat-form-field appearance="outline" class="order-entry__input">
      <mat-label>Take Profit Price (USD)</mat-label>
      <input matInput type="number" formControlName="takeProfitPrice" step="0.01" />
      @if (orderForm.controls.takeProfitPrice.hasError('tpInvalidSide')) {
        <mat-error>{{ orderForm.controls.takeProfitPrice.getError('tpInvalidSide') }}</mat-error>
      }
    </mat-form-field>
  </div>
}
```

```scss
// frontend/trading-ui/src/app/features/order-entry/order-entry.component.scss — modification
// Add:
.order-entry {
  // ... existing styles ...

  &__sltp-toggle {
    margin: 0.5rem 0;
    text-align: center;
  }

  &__sltp-toggle-btn {
    background: none;
    border: 1px solid var(--colour-border-subtle);
    color: var(--colour-label);
    padding: 0.25rem 0.75rem;
    border-radius: 4px;
    cursor: pointer;
    font-size: 0.8rem;

    &:hover {
      border-color: var(--colour-label);
    }
  }
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/order-entry/order-entry.component.ts` — existing `OrderEntryForm` interface and form initialization
- `frontend/trading-ui/src/app/features/order-entry/order-entry.component.html` — existing `@if (isLimitOrder())` toggle pattern

---

### Task 2.3: Add cross-field validation for SL/TP prices {#task-23-add-cross-field-validation-for-sltp-prices}

Add custom validators that check:
- SL price must be below entry price for longs, above for shorts
- TP price must be above entry price for longs, below for shorts
- Warning if SL is beyond liquidation price

For limit orders, entry price = the limit price. For market orders, entry price is unknown at order time — use the current mark price as a reference (from the selected asset's price feed).

- **Complexity**: Medium
- **Risk Factors**: Entry price source depends on order type; mark price may not reflect actual fill
- **Files**:
  - `frontend/trading-ui/src/app/features/order-entry/order-entry.component.ts` — modification (add validators)
- **Success**:
  - SL price on wrong side shows validation error
  - TP price on wrong side shows validation error
  - Validation triggers on value change
  - Validation errors clear when corrected
- **Dependencies**:
  - Task 2.2 (form controls exist)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/order-entry/order-entry.component.ts — modification
// Add validator factory functions:

private createSlValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const slPrice = control.value;
    if (slPrice == null) return null;

    const side = this.orderForm?.controls.side.value;
    const referencePrice = this.getReferencePrice();
    if (referencePrice == null) return null;

    if (side === 'buy' && slPrice >= referencePrice) {
      return { slInvalidSide: 'Stop loss must be below entry price for long positions' };
    }
    if (side === 'sell' && slPrice <= referencePrice) {
      return { slInvalidSide: 'Stop loss must be above entry price for short positions' };
    }
    return null;
  };
}

private createTpValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const tpPrice = control.value;
    if (tpPrice == null) return null;

    const side = this.orderForm?.controls.side.value;
    const referencePrice = this.getReferencePrice();
    if (referencePrice == null) return null;

    if (side === 'buy' && tpPrice <= referencePrice) {
      return { tpInvalidSide: 'Take profit must be above entry price for long positions' };
    }
    if (side === 'sell' && tpPrice >= referencePrice) {
      return { tpInvalidSide: 'Take profit must be below entry price for short positions' };
    }
    return null;
  };
}

// Helper to get reference price (limit price for limit orders, mark price for market)
private getReferencePrice(): number | null {
  if (this.orderForm.controls.orderType.value === 'limit') {
    return this.orderForm.controls.price.value;
  }
  // For market orders, use the current mark price from the selected asset
  return this.currentMarkPrice();
}

// Apply validators when SL/TP toggle is activated:
protected toggleSlTp(): void {
  this.showSlTp.update(v => !v);
  if (this.showSlTp()) {
    this.orderForm.controls.stopLossPrice.setValidators([this.createSlValidator()]);
    this.orderForm.controls.takeProfitPrice.setValidators([this.createTpValidator()]);
  } else {
    // ... clear validators and values as in Task 2.2 ...
  }
}

// Re-validate SL/TP when side or price changes:
// In ngOnInit, subscribe to side and price changes:
merge(
  this.orderForm.controls.side.valueChanges,
  this.orderForm.controls.price.valueChanges
).pipe(takeUntilDestroyed(this._destroyRef)).subscribe(() => {
  this.orderForm.controls.stopLossPrice.updateValueAndValidity();
  this.orderForm.controls.takeProfitPrice.updateValueAndValidity();
});
```

##### Pattern References

- `frontend/trading-ui/src/app/features/order-entry/order-entry.component.ts` — existing conditional validator pattern (`orderType.valueChanges.subscribe` toggling `price` validators)

---

### Task 2.4: Update confirm dialog to display SL/TP values {#task-24-update-confirm-dialog-to-display-sltp-values}

Update the order confirmation dialog to show SL and TP prices when set.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/features/order-entry/confirm-dialog/confirm-dialog.component.ts` — modification (extend dialog data interface)
  - `frontend/trading-ui/src/app/features/order-entry/confirm-dialog/confirm-dialog.component.html` — modification (display SL/TP rows)
  - `frontend/trading-ui/src/app/features/order-entry/order-entry.component.ts` — modification (pass SL/TP to dialog data)
- **Success**:
  - When SL is set, confirm dialog shows "Stop Loss: $64,000.00"
  - When TP is set, confirm dialog shows "Take Profit: $70,000.00"
  - When neither is set, no extra rows appear
- **Dependencies**:
  - Task 2.2 (SL/TP form values)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/order-entry/confirm-dialog/confirm-dialog.component.ts — modification
// Extend the ConfirmDialogData interface (or wherever order details are passed):
// Add optional SL/TP fields to the data passed to the dialog.

// In the component that opens the dialog (order-entry.component.ts):
// Add SL/TP values to the summary rows passed as dialog data:
const summaryRows = [
  // ... existing rows (asset, side, type, price, size) ...
];

if (this.orderForm.controls.stopLossPrice.value) {
  summaryRows.push({ label: 'Stop Loss', value: `$${this.orderForm.controls.stopLossPrice.value.toLocaleString()}` });
}
if (this.orderForm.controls.takeProfitPrice.value) {
  summaryRows.push({ label: 'Take Profit', value: `$${this.orderForm.controls.takeProfitPrice.value.toLocaleString()}` });
}
```

##### Pattern References

- `frontend/trading-ui/src/app/features/order-entry/confirm-dialog/confirm-dialog.component.ts` — existing dialog data structure and summary row display
- `frontend/trading-ui/src/app/features/order-entry/confirm-dialog/confirm-dialog.component.html` — summary row template

---

### Task 2.5: Add partial SL/TP warning {#task-25-add-partial-sltp-warning}

Show a non-blocking warning when the user sets SL but not TP (or vice versa) before confirming the order.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `frontend/trading-ui/src/app/features/order-entry/order-entry.component.ts` — modification
  - `frontend/trading-ui/src/app/features/order-entry/order-entry.component.html` — modification
- **Success**:
  - When only SL or only TP is set and SL/TP toggle is active, a warning message appears
  - Warning is non-blocking (does not prevent submission)
  - Warning disappears when both are set or both are empty
- **Dependencies**:
  - Task 2.2 (SL/TP form controls)

#### Implementation Details

```typescript
// frontend/trading-ui/src/app/features/order-entry/order-entry.component.ts — modification
// Add method (not computed signal, since reactive form values aren't signals):
protected getPartialSlTpWarning(): string | null {
  if (!this.showSlTp()) return null;
  const sl = this.orderForm?.controls.stopLossPrice.value;
  const tp = this.orderForm?.controls.takeProfitPrice.value;
  if (sl && !tp) return 'Consider adding a Take Profit to lock in gains';
  if (!sl && tp) return 'Consider adding a Stop Loss to limit downside risk';
  return null;
}
```

```html
<!-- frontend/trading-ui/src/app/features/order-entry/order-entry.component.html — modification -->
<!-- Add after the SL/TP fields, inside the showSlTp block: -->
@if (getPartialSlTpWarning(); as warning) {
  <div class="order-entry__sltp-warning">
    ⚠ {{ warning }}
  </div>
}
```

```scss
// frontend/trading-ui/src/app/features/order-entry/order-entry.component.scss — modification
.order-entry {
  &__sltp-warning {
    color: var(--colour-muted);
    font-size: 0.75rem;
    padding: 0.25rem 0.5rem;
    margin: 0.25rem 0;
    border-left: 2px solid #f59e0b; // amber warning
  }
}
```

Note: `getPartialSlTpWarning()` is a template method rather than a `computed()` signal because reactive form values aren't signals. This method is called from the template and re-evaluated on each change detection cycle, which is acceptable for this lightweight check.

##### Pattern References

- `frontend/trading-ui/src/app/features/order-entry/order-entry.component.ts` — existing signal patterns
- `frontend/trading-ui/src/app/features/order-entry/order-entry.component.html` — existing conditional display patterns

---

### Task 2.6: Build frontend and lint {#task-26-build-frontend-and-lint}

Build the Angular frontend and run linting to verify no errors.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification only)
- **Success**:
  - `npx ng build` succeeds with no errors
  - `npx ng lint` passes with no errors
- **Dependencies**:
  - All previous tasks in Phase 2

## Phase Success Criteria

- "Add SL/TP" toggle appears below Size field in the order entry form
- Stop Loss and Take Profit input fields appear when toggled
- Cross-field validation prevents SL/TP on the wrong side of entry price
- Confirmation dialog shows SL/TP values when set
- Non-blocking warning displays when only one of SL/TP is set
- SL/TP values are included in the place order request
- Frontend builds and lints cleanly
