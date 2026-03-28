import { ComponentFixture, TestBed } from "@angular/core/testing";
import { provideNativeDateAdapter } from "@angular/material/core";
import { NoopAnimationsModule } from "@angular/platform-browser/animations";
import { BacktestResult } from "../../../core/models/backtest.model";
import { BacktestFormComponent } from "./backtest-form.component";

describe("BacktestFormComponent", () => {
  let component: BacktestFormComponent;
  let fixture: ComponentFixture<BacktestFormComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BacktestFormComponent, NoopAnimationsModule],
      providers: [provideNativeDateAdapter()]
    }).compileComponents();

    fixture = TestBed.createComponent(BacktestFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it("should create with expected default values", () => {
    expect(component).toBeTruthy();
    expect(component.form.controls.symbol.value).toBe("BTC");
    expect(component.form.controls.gridLevels.value).toBe(10);
    expect(component.form.controls.makerFee.value).toBe(0.0001);
    expect(component.form.controls.takerFee.value).toBe(0.00035);
    expect(component.form.controls.initialCapital.value).toBe(10000);
  });

  it("should be invalid until start and end dates are provided", () => {
    expect(component.isFormValid).toBeFalse();

    component.form.patchValue({
      startDate: new Date("2024-01-01T00:00:00Z"),
      endDate: new Date("2024-12-31T00:00:00Z")
    });

    expect(component.isFormValid).toBeTrue();
  });

  it("should require at least one interval", () => {
    component.form.patchValue({
      startDate: new Date("2024-01-01T00:00:00Z"),
      endDate: new Date("2024-12-31T00:00:00Z"),
      interval15m: false,
      interval1h: false,
      interval4h: false
    });

    expect(component.isFormValid).toBeFalse();
    expect(component.form.hasError("intervals")).toBeTrue();
  });

  it("should emit runBacktest with the current form values", () => {
    spyOn(component.runBacktest, "emit");
    component.form.patchValue({
      startDate: new Date("2024-01-01T00:00:00Z"),
      endDate: new Date("2024-12-31T00:00:00Z")
    });

    component.onRunBacktest();

    expect(component.runBacktest.emit).toHaveBeenCalledWith(jasmine.objectContaining({
      symbol: "BTC",
      intervals: ["15m", "1h", "4h"],
      initialCapital: 10000,
      strategyConfig: jasmine.objectContaining({
        gridLevels: 10,
        leverage: 3
      })
    }));
  });

  it("should emit validateData when dates and intervals are valid", () => {
    spyOn(component.validateData, "emit");
    component.form.patchValue({
      startDate: new Date("2024-01-01T00:00:00Z"),
      endDate: new Date("2024-12-31T00:00:00Z")
    });

    component.onValidateData();

    expect(component.validateData.emit).toHaveBeenCalledWith(jasmine.objectContaining({
      symbol: "BTC",
      intervals: ["15m", "1h", "4h"]
    }));
  });

  it("should prefill the form from a backtest result input", async () => {
    const result: BacktestResult = {
      id: "run-1",
      symbol: "SOL",
      intervals: ["15m", "4h"],
      startDate: "2024-02-01T00:00:00Z",
      endDate: "2024-02-29T00:00:00Z",
      strategyConfig: {
        gridLevels: 7,
        gridSpacing: 0.35,
        takeProfitPercent: 1.5,
        breakdownThreshold: -2.5,
        makerFee: 0.0001,
        takerFee: 0.00035,
        slippage: 0.00005,
        positionSize: 250,
        leverage: 5,
        stopLossPercent: 4
      },
      initialCapital: 25000,
      candlesReplayed: 500,
      elapsedMs: 1800,
      totalTrades: 12,
      winningTrades: 7,
      losingTrades: 5,
      winRate: 58.3,
      totalPnl: 750,
      maxDrawdown: -140,
      averageTradePnl: 62.5,
      averageHoldTimeMinutes: 180,
      hedgesOpened: 1,
      totalFeesPaid: 22,
      trades: [],
      createdAt: "2026-03-28T12:00:00Z"
    };

    fixture.componentRef.setInput("prefillConfig", result);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(component.form.controls.symbol.value).toBe("SOL");
    expect(component.form.controls.interval15m.value).toBeTrue();
    expect(component.form.controls.interval1h.value).toBeFalse();
    expect(component.form.controls.interval4h.value).toBeTrue();
    expect(component.form.controls.gridLevels.value).toBe(7);
    expect(component.form.controls.initialCapital.value).toBe(25000);
  });

  it("should map a server date validation message to the form", async () => {
    fixture.componentRef.setInput("validationErrorMessage", "endDate must be after startDate");
    fixture.detectChanges();
    await fixture.whenStable();

    expect(component.form.hasError("serverDateRange")).toBeTrue();
    expect(component.getDateRangeErrorMessage()).toContain("endDate");
  });
});