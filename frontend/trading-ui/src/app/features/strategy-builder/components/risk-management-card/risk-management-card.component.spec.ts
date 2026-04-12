import { ComponentFixture, TestBed } from "@angular/core/testing";
import { FormBuilder, FormGroup, Validators } from "@angular/forms";
import { NoopAnimationsModule } from "@angular/platform-browser/animations";
import { of } from "rxjs";
import { HyperliquidApiService } from "../../../../core/services/hyperliquid-api.service";
import { RiskManagementCardComponent } from "./risk-management-card.component";

describe("RiskManagementCardComponent", () => {
  let component: RiskManagementCardComponent;
  let fixture: ComponentFixture<RiskManagementCardComponent>;
  let group: FormGroup;
  let exitGroup: FormGroup;
  let apiService: jasmine.SpyObj<HyperliquidApiService>;

  const fb = new FormBuilder();

  beforeEach(async () => {
    apiService = jasmine.createSpyObj<HyperliquidApiService>("HyperliquidApiService", ["getAccountSummary"]);
    apiService.getAccountSummary.and.returnValue(of({
      equity: 10000,
      availableMargin: 8000,
      crossMarginRatio: 0,
      maintenanceMargin: 0,
      unrealisedPnl: 0,
    }));

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
      providers: [{ provide: HyperliquidApiService, useValue: apiService }],
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

  describe("Given preview calculation", () => {
    beforeEach(() => {
      group.patchValue({ positionSizeType: "risk_based", riskPerTradePercent: 1, autoLeverage: true });
      exitGroup.get("stopLoss")?.patchValue({ enabled: true, type: "fixed_percent", value: 2 });
      fixture.detectChanges();
    });

    it("When equity is 10000 risk is 1 percent and SL is 2 percent Then R is 100", () => {
      expect(component.rAmount).toBeCloseTo(100, 2);
    });

    it("When equity is 10000 risk is 1 percent and SL is 2 percent Then position size is 5000", () => {
      expect(component.positionSize).toBeCloseTo(5000, 0);
    });

    it("When auto leverage is enabled and SL is 2 percent Then derived leverage is 33", () => {
      expect(component.derivedLeverage).toBe(33);
    });

    it("When SL changes from 2 percent to 5 percent Then position size shrinks", () => {
      exitGroup.get("stopLoss")?.patchValue({ value: 5 });
      fixture.detectChanges();

      expect(component.positionSize).toBeCloseTo(2000, 0);
    });

    it("When SL changes from 2 percent to 5 percent Then leverage drops", () => {
      exitGroup.get("stopLoss")?.patchValue({ value: 5 });
      fixture.detectChanges();

      expect(component.derivedLeverage).toBe(16);
    });
  });

  describe("Given no equity", () => {
    beforeEach(() => {
      fixture.destroy();
      apiService.getAccountSummary.and.returnValue(of({
        equity: 0,
        availableMargin: 0,
        crossMarginRatio: 0,
        maintenanceMargin: 0,
        unrealisedPnl: 0,
      }));

      fixture = TestBed.createComponent(RiskManagementCardComponent);
      component = fixture.componentInstance;
      component.group = group;
      component.exitGroup = exitGroup;
      group.patchValue({ positionSizeType: "risk_based" });
      fixture.detectChanges();
    });

    it("When risk based mode is active Then the wallet connection message is shown", () => {
      expect(fixture.nativeElement.querySelector(".risk-card__preview-message")?.textContent).toContain("Connect a wallet");
    });
  });

  describe("Given no fixed percent stop loss", () => {
    beforeEach(() => {
      group.patchValue({ positionSizeType: "risk_based" });
      exitGroup.get("stopLoss")?.patchValue({ enabled: true, type: "swing_low", value: 2 });
      fixture.detectChanges();
    });

    it("When risk based mode is active Then the stop loss configuration message is shown", () => {
      expect(fixture.nativeElement.querySelector(".risk-card__preview-message")?.textContent).toContain("fixed-percent stop-loss");
    });
  });
});