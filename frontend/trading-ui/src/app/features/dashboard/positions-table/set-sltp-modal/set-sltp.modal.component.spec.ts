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
  fundingRate: -0.0001,
  stopLossPrice: null,
  takeProfitPrice: null
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
  fundingRate: 0.0001,
  stopLossPrice: null,
  takeProfitPrice: null
};

describe("SetSlTpModalComponent", () => {
  let component: SetSlTpModalComponent;
  let fixture: ComponentFixture<SetSlTpModalComponent>;
  let dialogRefSpy: jasmine.SpyObj<MatDialogRef<SetSlTpModalComponent>>;
  let priceUpdateSubject: Subject<PriceUpdate>;

  async function createComponent(position: Position): Promise<void> {
    TestBed.resetTestingModule();
    dialogRefSpy = jasmine.createSpyObj("MatDialogRef", ["close"]);
    priceUpdateSubject = new Subject<PriceUpdate>();

    await TestBed.configureTestingModule({
      imports: [SetSlTpModalComponent, NoopAnimationsModule],
      providers: [
        { provide: MatDialogRef, useValue: dialogRefSpy },
        { provide: MAT_DIALOG_DATA, useValue: { position } as SetSlTpDialogData },
        { provide: SignalRService, useValue: { priceUpdate$: priceUpdateSubject.asObservable() } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SetSlTpModalComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  describe("with a long position", () => {
    beforeEach(async () => {
      await createComponent(mockLongPosition);
    });

    it("should seed the live price from the mark price", () => {
      expect(component.livePrice).toBe(51000);
    });

    it("should render liquidation price, live price, and liquidation distance", () => {
      const infoElement = fixture.nativeElement.querySelector(".set-sltp-modal__info") as HTMLElement;

      expect(infoElement.textContent).toContain("Liq. Price");
      expect(infoElement.textContent).toContain("42,000.00");
      expect(infoElement.textContent).toContain("Live Price");
      expect(infoElement.textContent).toContain("51,000.00");
      expect(infoElement.textContent).toContain("Dist. to Liq.");
      expect(infoElement.textContent).toContain("21.4% away");
    });

    it("should update the live price when a matching SignalR update arrives", () => {
      priceUpdateSubject.next({ asset: "BTC-PERP", lastPrice: 52000, high24h: 0, low24h: 0, volume24h: 0, timestamp: 0 });
      fixture.detectChanges();

      expect(component.livePrice).toBe(52000);
      expect(component.getDistanceToLiquidation()).toBeCloseTo(23.81, 1);
    });

    it("should ignore a SignalR update for another asset", () => {
      priceUpdateSubject.next({ asset: "ETH-PERP", lastPrice: 3000, high24h: 0, low24h: 0, volume24h: 0, timestamp: 0 });

      expect(component.livePrice).toBe(51000);
    });

    it("should return a profit class when the live price is favorable", () => {
      expect(component.getLivePriceClass()).toBe("set-sltp-modal__price--profit");
    });

    it("should return a loss class when the live price moves below entry", () => {
      priceUpdateSubject.next({ asset: "BTC-PERP", lastPrice: 49000, high24h: 0, low24h: 0, volume24h: 0, timestamp: 0 });

      expect(component.getLivePriceClass()).toBe("set-sltp-modal__price--loss");
    });

    it("should allow a stop loss above entry price but below live price (breakeven trailing)", () => {
      component.form.controls.stopLossPrice.setValue(50500);

      expect(component.form.controls.stopLossPrice.valid).toBeTrue();
    });

    it("should reject a stop loss at or above the live price for long positions", () => {
      component.form.controls.stopLossPrice.setValue(51000);

      expect(component.form.controls.stopLossPrice.hasError("slInvalidSide")).toBeTrue();
      expect(component.getStopLossErrorMessage()).toBe("Stop loss must be below current live price for long positions");
    });

    it("should reject a take profit at or below the live price for long positions", () => {
      component.form.controls.takeProfitPrice.setValue(50500);

      expect(component.form.controls.takeProfitPrice.hasError("tpInvalidSide")).toBeTrue();
      expect(component.getTakeProfitErrorMessage()).toBe("Take profit must be above current live price for long positions");
    });

    it("should revalidate stop loss when live price changes via SignalR", () => {
      component.form.controls.stopLossPrice.setValue(50500);
      expect(component.form.controls.stopLossPrice.valid).toBeTrue();

      priceUpdateSubject.next({ asset: "BTC-PERP", lastPrice: 50200, high24h: 0, low24h: 0, volume24h: 0, timestamp: 0 });

      expect(component.form.controls.stopLossPrice.hasError("slInvalidSide")).toBeTrue();
    });

    it("should surface a liquidation validation error for an invalid stop loss", () => {
      component.form.controls.stopLossPrice.setValue(41000);

      expect(component.form.controls.stopLossPrice.hasError("slBeyondLiquidation")).toBeTrue();
      expect(component.getStopLossErrorMessage()).toBe("Stop loss is beyond liquidation price - it would never trigger");
    });

    it("should disable confirm when the form is invalid and re-enable it when corrected", () => {
      const confirmButton = fixture.nativeElement.querySelector("button[color='primary']") as HTMLButtonElement;

      component.form.controls.stopLossPrice.setValue(41000);
      fixture.detectChanges();
      expect(confirmButton.disabled).toBeTrue();

      component.form.controls.stopLossPrice.setValue(45000);
      fixture.detectChanges();
      expect(confirmButton.disabled).toBeFalse();
    });

    it("should close the dialog with the form values when submitted", () => {
      component.form.controls.stopLossPrice.setValue(45000);
      component.form.controls.takeProfitPrice.setValue(55000);

      component.onSubmit();

      expect(dialogRefSpy.close).toHaveBeenCalledWith({
        stopLossPrice: 45000,
        takeProfitPrice: 55000
      });
    });

    it("should close the dialog without a payload on cancel", () => {
      component.onCancel();

      expect(dialogRefSpy.close).toHaveBeenCalledWith();
    });
  });

  describe("with a short position", () => {
    beforeEach(async () => {
      await createComponent(mockShortPosition);
    });

    it("should return a profit class when the live price is below entry", () => {
      expect(component.getLivePriceClass()).toBe("set-sltp-modal__price--profit");
    });

    it("should mark stop loss beyond liquidation as invalid", () => {
      component.form.controls.stopLossPrice.setValue(59000);

      expect(component.form.controls.stopLossPrice.hasError("slBeyondLiquidation")).toBeTrue();
    });

    it("should reject a stop loss at or below the live price for short positions", () => {
      component.form.controls.stopLossPrice.setValue(49000);

      expect(component.form.controls.stopLossPrice.hasError("slInvalidSide")).toBeTrue();
      expect(component.getStopLossErrorMessage()).toBe("Stop loss must be above current live price for short positions");
    });

    it("should allow a stop loss below entry but above live price for short positions", () => {
      component.form.controls.stopLossPrice.setValue(49500);

      expect(component.form.controls.stopLossPrice.valid).toBeTrue();
    });
  });

  describe("with an unknown liquidation price", () => {
    beforeEach(async () => {
      await createComponent({ ...mockLongPosition, liquidationPrice: 0 });
    });

    it("should return null for the liquidation distance", () => {
      expect(component.getDistanceToLiquidation()).toBeNull();
    });

    it("should render placeholder values for liquidation-based fields", () => {
      const infoElement = fixture.nativeElement.querySelector(".set-sltp-modal__info") as HTMLElement;

      expect(infoElement.textContent).toContain("Liq. Price");
      expect(infoElement.textContent).toContain("-");
      expect(infoElement.textContent).toContain("Dist. to Liq.");
    });

    it("should not add the liquidation validation error", () => {
      component.form.controls.stopLossPrice.setValue(1);

      expect(component.form.controls.stopLossPrice.hasError("slBeyondLiquidation")).toBeFalse();
    });
  });
});