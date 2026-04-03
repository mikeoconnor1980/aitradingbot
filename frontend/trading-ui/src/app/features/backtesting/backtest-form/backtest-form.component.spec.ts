import { ComponentFixture, TestBed } from "@angular/core/testing";
import { provideNativeDateAdapter } from "@angular/material/core";
import { NoopAnimationsModule } from "@angular/platform-browser/animations";
import { of, throwError } from "rxjs";
import { BacktestResult } from "../../../core/models/backtest.model";
import { NotificationService } from "../../../core/services/notification.service";
import { StrategyDto, StrategySummaryDto } from "../../strategy-builder/models/strategy.model";
import { StrategyApiService } from "../../strategy-builder/services/strategy-api.service";
import { BacktestFormComponent } from "./backtest-form.component";

describe("BacktestFormComponent", () => {
  let component: BacktestFormComponent;
  let fixture: ComponentFixture<BacktestFormComponent>;
  let strategyApiService: jasmine.SpyObj<StrategyApiService>;
  let notificationService: jasmine.SpyObj<NotificationService>;

  const strategySummary: StrategySummaryDto = {
    id: "strategy-1",
    name: "BTC Grid",
    market: "BTC",
    timeframe: "15m",
    direction: "long",
    strategyMode: "grid",
    version: 3,
    createdAt: "2026-03-28T12:00:00Z",
    updatedAt: "2026-03-29T12:00:00Z"
  };

  const strategyDetail: StrategyDto = {
    id: "strategy-1",
    name: "BTC Grid",
    strategyType: "grid",
    version: 3,
    createdAt: "2026-03-28T12:00:00Z",
    updatedAt: "2026-03-29T12:00:00Z",
    config: {
      schemaVersion: 1,
      strategyMode: "grid",
      strategyName: "BTC Grid",
      exchange: "Hyperliquid",
      market: "BTC",
      timeframe: "15m",
      direction: "long",
      enabled: true,
      grid: {
        levels: 8,
        entryMode: "auto_from_signal_candle",
        anchorPrice: null,
        spacing: 0.4,
        breakdownThreshold: -2.5
      },
      exit: {
        takeProfit: { enabled: true, type: "fixed_percent", value: 1.2 },
        stopLoss: { enabled: true, type: "fixed_percent", value: 4 },
        exitOnOppositeSignal: false
      },
      risk: {
        positionSizeType: "fixed_notional",
        positionSizeValue: 250,
        leverage: 5,
        maxOpenTrades: 1,
        cooldownValue: 0,
        cooldownUnit: "candles",
        allowSameCandleReentry: false
      },
      source: { entryPoint: "builder", summary: "BTC Grid" }
    }
  };

  const signalStrategySummary: StrategySummaryDto = {
    id: "strategy-2",
    name: "RSI Test",
    market: "BTC-USD",
    timeframe: "15m",
    direction: "long",
    strategyMode: "signal",
    version: 1,
    createdAt: "2026-03-28T12:00:00Z",
    updatedAt: "2026-03-29T12:00:00Z"
  };

  const signalStrategyDetail: StrategyDto = {
    id: "strategy-2",
    name: "RSI Test",
    strategyType: "signal",
    version: 1,
    createdAt: "2026-03-28T12:00:00Z",
    updatedAt: "2026-03-29T12:00:00Z",
    config: {
      schemaVersion: 1,
      strategyMode: "signal",
      strategyName: "RSI Test",
      exchange: "Hyperliquid",
      market: "BTC-USD",
      timeframe: "15m",
      direction: "long",
      enabled: true,
      grid: null,
      entryLogic: "all",
      entryConditions: [
        {
          id: "cond-1",
          enabled: true,
          type: "rsi",
          label: "RSI Oversold",
          params: {
            period: 14,
            operator: "lt",
            value: 40
          }
        }
      ],
      exit: {
        takeProfit: { enabled: true, type: "fixed_percent", value: 2 },
        stopLoss: { enabled: true, type: "fixed_percent", value: 6 },
        exitOnOppositeSignal: false
      },
      risk: {
        positionSizeType: "percent_wallet",
        positionSizeValue: 5,
        leverage: 1,
        maxOpenTrades: 1,
        cooldownValue: 0,
        cooldownUnit: "candles",
        allowSameCandleReentry: false
      },
      source: { entryPoint: "builder", summary: "RSI Test" }
    }
  };

  beforeEach(async () => {
    strategyApiService = jasmine.createSpyObj<StrategyApiService>("StrategyApiService", ["getStrategies", "getStrategy"]);
    notificationService = jasmine.createSpyObj<NotificationService>("NotificationService", ["error"]);
    strategyApiService.getStrategies.and.returnValue(of([strategySummary, signalStrategySummary]));
    strategyApiService.getStrategy.and.callFake((strategyId: string) => {
      return of(strategyId === signalStrategySummary.id ? signalStrategyDetail : strategyDetail);
    });

    await TestBed.configureTestingModule({
      imports: [BacktestFormComponent, NoopAnimationsModule],
      providers: [
        provideNativeDateAdapter(),
        { provide: StrategyApiService, useValue: strategyApiService },
        { provide: NotificationService, useValue: notificationService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(BacktestFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it("should create with expected default values", () => {
    expect(component).toBeTruthy();
    expect(component.strategies.length).toBe(2);
    expect(component.form.controls.strategyId.value).toBe("");
    expect(component.form.controls.makerFee.value).toBe(0.0001);
    expect(component.form.controls.takerFee.value).toBe(0.00035);
    expect(component.form.controls.initialCapital.value).toBe(10000);
  });

  it("should be invalid until strategy and dates are provided", () => {
    expect(component.isFormValid).toBeFalse();

    component.form.patchValue({
      strategyId: strategySummary.id,
      startDate: new Date("2024-01-01T00:00:00Z"),
      endDate: new Date("2024-12-31T00:00:00Z")
    });

    expect(component.isFormValid).toBeTrue();
  });

  it("should emit runBacktest with strategyId and execution parameters", () => {
    spyOn(component.runBacktest, "emit");
    component.form.patchValue({
      strategyId: strategySummary.id,
      startDate: new Date("2024-01-01T00:00:00Z"),
      endDate: new Date("2024-12-31T00:00:00Z"),
      initialCapital: 25000,
      makerFee: 0.00015,
      takerFee: 0.0004,
      slippage: 0.00005,
      enableAuditLog: false
    });

    component.onRunBacktest();

    expect(component.runBacktest.emit).toHaveBeenCalledWith(jasmine.objectContaining({
      strategyId: strategySummary.id,
      initialCapital: 25000,
      executionConfig: jasmine.objectContaining({
        makerFee: 0.00015,
        takerFee: 0.0004,
        slippage: 0.00005
      }),
      enableAuditLog: false
    }));
  });

  it("should emit validateData using the selected strategy market and required evaluation intervals", () => {
    spyOn(component.validateData, "emit");
    component.form.patchValue({
      strategyId: strategySummary.id,
      startDate: new Date("2024-01-01T00:00:00Z"),
      endDate: new Date("2024-12-31T00:00:00Z")
    });

    component.onValidateData();

    expect(component.validateData.emit).toHaveBeenCalledWith(jasmine.objectContaining({
      symbol: "BTC",
      intervals: ["15m", "1h", "4h"]
    }));
  });

  it("should distinguish strategy timeframe from supporting evaluation data", () => {
    component.form.patchValue({
      strategyId: strategySummary.id,
      startDate: new Date("2024-01-01T00:00:00Z"),
      endDate: new Date("2024-12-31T00:00:00Z")
    });

    expect(component.primaryTimeframe).toBe("15m");
    expect(component.selectedIntervals).toEqual(["15m", "1h", "4h"]);
    expect(component.supportingTimeframesLabel).toBe("1h, 4h");
  });

  it("should hydrate the selected strategy when strategyId is prefilled before init", async () => {
    fixture = TestBed.createComponent(BacktestFormComponent);
    component = fixture.componentInstance;

    fixture.componentRef.setInput("strategyId", signalStrategySummary.id);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(component.form.controls.strategyId.value).toBe(signalStrategySummary.id);
    expect(component.selectedStrategy?.id).toBe(signalStrategySummary.id);
    expect(component.isSignalStrategy).toBeTrue();
    expect(component.previewEntryConditionsLabel).toBe("RSI(14) is below 40");
  });

  it("should expose signal-mode preview details for RSI strategies", () => {
    component.form.patchValue({
      strategyId: signalStrategySummary.id,
      startDate: new Date("2024-01-01T00:00:00Z"),
      endDate: new Date("2024-12-31T00:00:00Z")
    });

    expect(component.isSignalStrategy).toBeTrue();
    expect(component.strategyModeLabel).toBe("Signal");
    expect(component.previewEntryLogicLabel).toBe("All conditions");
    expect(component.previewEntryConditionsLabel).toBe("RSI(14) is below 40");
    expect(component.previewConditionCountLabel).toBe("1 active condition");
  });

  it("should surface signal preview details from unavailable strategy snapshots", async () => {
    strategyApiService.getStrategy.and.returnValue(throwError(() => new Error("not found")));

    const result: BacktestResult = {
      id: "run-signal-1",
      symbol: "BTC-USD",
      intervals: ["15m", "1h", "4h"],
      startDate: "2024-02-01T00:00:00Z",
      endDate: "2024-02-29T00:00:00Z",
      strategyConfig: {
        schemaVersion: 1,
        strategyMode: "signal",
        strategyName: "RSI Test",
        exchange: "Hyperliquid",
        market: "BTC-USD",
        timeframe: "15m",
        direction: "long",
        enabled: true,
        grid: null,
        entryLogic: "all",
        entryConditions: [
          {
            id: "cond-1",
            enabled: true,
            type: "rsi",
            label: "RSI Oversold",
            params: { period: 14, operator: "lt", value: 40 }
          }
        ],
        exit: {
          takeProfit: { enabled: true, type: "fixed_percent", value: 2 },
          stopLoss: { enabled: true, type: "fixed_percent", value: 6 },
          exitOnOppositeSignal: false
        },
        risk: {
          positionSizeType: "percent_wallet",
          positionSizeValue: 5,
          leverage: 1,
          maxOpenTrades: 1,
          cooldownValue: 0,
          cooldownUnit: "candles",
          allowSameCandleReentry: false
        },
        source: { entryPoint: "ui_builder", summary: "RSI Test" }
      },
      executionConfig: {
        feeModel: {
          makerFeeRate: 0.0001,
          takerFeeRate: 0.00035,
          slippageRate: 0
        }
      },
      initialCapital: 10000,
      status: "Completed",
      progress: 100,
      candlesReplayed: 100,
      elapsedMs: 500,
      totalTrades: 3,
      winningTrades: 2,
      losingTrades: 1,
      winRate: 66.7,
      totalPnl: 50,
      maxDrawdown: -10,
      averageTradePnl: 16.6,
      averageHoldTimeMinutes: 30,
      hedgesOpened: 0,
      totalFeesPaid: 2,
      trades: [],
      hasAuditLog: false,
      createdAt: "2026-03-28T12:00:00Z",
      strategyId: signalStrategySummary.id,
      strategyRevisionId: 1,
      strategyName: "RSI Test"
    };

    fixture.componentRef.setInput("prefillConfig", result);
    fixture.detectChanges();
    await fixture.whenStable();

    component.form.patchValue({ strategyId: signalStrategySummary.id });
    fixture.detectChanges();
    await fixture.whenStable();

    expect(component.unavailableStrategySnapshot).not.toBeNull();
    expect(component.isSignalStrategy).toBeTrue();
    expect(component.previewEntryConditionsLabel).toBe("RSI(14) is below 40");
  });

  it("should prefill strategy-linked runs from a backtest result input", async () => {
    const result: BacktestResult = {
      id: "run-1",
      symbol: "SOL",
      intervals: ["15m", "4h"],
      startDate: "2024-02-01T00:00:00Z",
      endDate: "2024-02-29T00:00:00Z",
      strategyConfig: {
        schemaVersion: 1,
        strategyMode: "grid",
        strategyName: "Backtest",
        exchange: "Hyperliquid",
        market: "SOL",
        timeframe: "15m",
        direction: "long",
        enabled: true,
        grid: {
          levels: 7,
          entryMode: "WaitForLimitPrice",
          anchorPrice: 152.25,
          spacing: 0.35,
          breakdownThreshold: -2.5
        },
        exit: {
          takeProfit: { enabled: true, type: "fixed_percent", value: 1.5 },
          stopLoss: { enabled: true, type: "fixed_percent", value: 4 },
          exitOnOppositeSignal: false
        },
        risk: {
          positionSizeType: "fixed_notional",
          positionSizeValue: 250,
          leverage: 5,
          maxOpenTrades: 1,
          cooldownValue: 0,
          cooldownUnit: "candles",
          allowSameCandleReentry: false
        },
        source: { entryPoint: "ui_builder", summary: "Backtest: SOL" }
      },
      executionConfig: {
        feeModel: {
          makerFeeRate: 0.0001,
          takerFeeRate: 0.00035,
          slippageRate: 0.00005
        }
      },
      initialCapital: 25000,
      status: "Completed",
      progress: 100,
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
      hasAuditLog: false,
      createdAt: "2026-03-28T12:00:00Z",
      strategyId: strategySummary.id,
      strategyRevisionId: 3,
      strategyName: "BTC Grid"
    };

    fixture.componentRef.setInput("prefillConfig", result);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(component.form.controls.strategyId.value).toBe(strategySummary.id);
    expect(component.form.controls.makerFee.value).toBe(0.0001);
    expect(component.form.controls.initialCapital.value).toBe(25000);
  });

  it("should map a server date validation message to the form", async () => {
    fixture.componentRef.setInput("validationErrorMessage", "endDate must be after startDate");
    fixture.detectChanges();
    await fixture.whenStable();

    expect(component.form.hasError("serverDateRange")).toBeTrue();
    expect(component.getDateRangeErrorMessage()).toContain("endDate");
  });

  it("should reject future dates", () => {
    const tomorrow = new Date();
    tomorrow.setDate(tomorrow.getDate() + 1);

    component.form.patchValue({
      strategyId: strategySummary.id,
      startDate: tomorrow,
      endDate: tomorrow
    });

    expect(component.form.controls.startDate.hasError("futureDate")).toBeTrue();
    expect(component.form.controls.endDate.hasError("futureDate")).toBeTrue();
    expect(component.getControlErrorMessage("startDate")).toBe("Future dates are not allowed.");
    expect(component.isFormValid).toBeFalse();
  });

  it("should clear the picker and notify when a selected strategy cannot be loaded", async () => {
    strategyApiService.getStrategy.and.returnValue(throwError(() => new Error("not found")));

    component.form.controls.strategyId.setValue("missing-strategy");
    fixture.detectChanges();
    await fixture.whenStable();

    expect(component.form.controls.strategyId.value).toBe("");
    expect(component.selectedStrategy).toBeNull();
    expect(notificationService.error).toHaveBeenCalled();
  });
});
