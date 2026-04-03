import { convertToParamMap } from "@angular/router";
import { ComponentFixture, TestBed } from "@angular/core/testing";
import { MatDialog } from "@angular/material/dialog";
import { NoopAnimationsModule } from "@angular/platform-browser/animations";
import { ActivatedRoute, Router } from "@angular/router";
import { of } from "rxjs";
import { StrategyIntentDto } from "./models/strategy-intent.model";
import { ReferenceDataService } from "./services/reference-data.service";
import { StrategyApiService } from "./services/strategy-api.service";
import { StrategyBuilderPageComponent } from "./strategy-builder-page.component";
import { StrategyMapperService } from "./services/strategy-mapper.service";
import { StrategyValidationService } from "./services/strategy-validation.service";
import { NotificationService } from "../../core/services/notification.service";

describe("StrategyBuilderPageComponent", () => {
  let fixture: ComponentFixture<StrategyBuilderPageComponent>;
  let component: StrategyBuilderPageComponent;
  let routerSpy: jasmine.SpyObj<Router>;
  let dialogSpy: jasmine.SpyObj<MatDialog>;

  beforeEach(async () => {
    routerSpy = jasmine.createSpyObj<Router>("Router", ["navigate"]);
    routerSpy.navigate.and.resolveTo(true);
    dialogSpy = jasmine.createSpyObj<MatDialog>("MatDialog", ["open"]);
    dialogSpy.open.and.returnValue({ afterClosed: () => of(true) } as never);

    await TestBed.configureTestingModule({
      imports: [StrategyBuilderPageComponent, NoopAnimationsModule],
      providers: [
        { provide: Router, useValue: routerSpy },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({})
            }
          }
        },
        {
          provide: StrategyApiService,
          useValue: jasmine.createSpyObj("StrategyApiService", ["getStrategy", "createStrategy", "updateStrategy", "interpretStrategy"])
        },
        {
          provide: StrategyMapperService,
          useValue: {
            mapFormToConfig: (formValue: Record<string, unknown>) => ({
              schemaVersion: 1,
              strategyMode: "grid",
              strategyName: String(formValue["strategyName"] ?? ""),
              exchange: String(formValue["exchange"] ?? "Hyperliquid"),
              market: String(formValue["market"] ?? "BTC-USD"),
              timeframe: String(formValue["timeframe"] ?? "15m"),
              direction: String(formValue["direction"] ?? "long"),
              enabled: true,
              grid: {
                levels: 10,
                spacing: 0.5,
                entryMode: "auto_from_signal_candle",
                anchorPrice: null,
                breakdownThreshold: 1.5
              },
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
              source: {
                entryPoint: String((formValue["source"] as Record<string, unknown> | undefined)?.["entryPoint"] ?? "ui_builder"),
                summary: String((formValue["source"] as Record<string, unknown> | undefined)?.["summary"] ?? "Test"),
                sourceText: ((formValue["source"] as Record<string, unknown> | undefined)?.["sourceText"] as string | null | undefined) ?? null,
              }
            })
          }
        },
        {
          provide: StrategyValidationService,
          useValue: {
            validate: () => [],
            validateServer: () => of({
              isValid: true,
              errors: [],
              warnings: [],
              infoMessages: []
            })
          }
        },
        {
          provide: ReferenceDataService,
          useValue: {
            getReferenceData: () => of({ markets: ["BTC-USD"], timeframes: ["15m", "1h"] })
          }
        },
        {
          provide: NotificationService,
          useValue: jasmine.createSpyObj("NotificationService", ["success", "error"])
        },
        { provide: MatDialog, useValue: dialogSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(StrategyBuilderPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it("should navigate directly when cancel is clicked on a clean form", () => {
    component.onCancel();

    expect(dialogSpy.open).not.toHaveBeenCalled();
    expect(routerSpy.navigate).toHaveBeenCalledWith(["/strategies"]);
  });

  it("should not allow save for a clean valid form", () => {
    component.form.patchValue({ strategyName: "RSI Test" });
    (component as unknown as { _savedFormSnapshot: string })._savedFormSnapshot = JSON.stringify(component.form.getRawValue());
    component.form.markAsPristine();

    expect(component.form.valid).toBeTrue();
    expect(component.hasUnsavedChanges()).toBeFalse();
    expect(component.canSave).toBeFalse();
  });

  it("should allow save only when the valid form has changes", () => {
    component.form.patchValue({ strategyName: "RSI Test" });

    expect(component.form.valid).toBeTrue();
    expect(component.hasUnsavedChanges()).toBeTrue();
    expect(component.canSave).toBeTrue();
  });

  it("should disable save again when changes are reverted", () => {
    const originalName = component.form.controls["strategyName"].value;

    component.form.patchValue({ strategyName: "RSI Test" });
    expect(component.canSave).toBeTrue();

    component.form.patchValue({ strategyName: originalName });

    expect(component.hasUnsavedChanges()).toBeFalse();
    expect(component.canSave).toBeFalse();
  });

  it("should confirm before leaving a dirty form", () => {
    component.form.patchValue({ strategyName: "BTC Grid" });

    expect(component.hasUnsavedChanges()).toBeTrue();

    component.onCancel();

    expect(dialogSpy.open).toHaveBeenCalled();
    expect(routerSpy.navigate).toHaveBeenCalledWith(["/strategies"]);
    expect(component.hasUnsavedChanges()).toBeFalse();
  });

  it("should populate the form from an interpreted grid strategy", () => {
    const result: StrategyIntentDto = {
      config: {
        schemaVersion: 1,
        strategyMode: "grid",
        strategyName: "ETH Grid",
        exchange: "Hyperliquid",
        market: "ETH-USD",
        timeframe: "1h",
        direction: "long",
        enabled: true,
        grid: {
          levels: 5,
          spacing: 0.5,
          entryMode: "auto_from_signal_candle",
          breakdownThreshold: 2,
          anchorPrice: null,
        },
        exit: {
          takeProfit: { enabled: true, type: "fixed_percent", value: 3 },
          stopLoss: { enabled: true, type: "fixed_percent", value: 4 },
          exitOnOppositeSignal: false,
        },
        risk: {
          positionSizeType: "percent_wallet",
          positionSizeValue: 8,
          leverage: 2,
          maxOpenTrades: 1,
          cooldownValue: 1,
          cooldownUnit: "candles",
          allowSameCandleReentry: false,
        },
        source: {
          entryPoint: "naturalLanguage",
          summary: "Interpreted from natural language",
          sourceText: "Set up a 5-level ETH grid",
        },
      },
      confidence: 0.82,
      assumptions: [],
      clarificationNeeded: null,
    };

    component.onNlInterpreted(result);

    expect(component.form.get("strategyName")?.value).toBe("ETH Grid");
    expect(component.gridFormGroup.get("levels")?.value).toBe(5);
    expect(component.form.get("source.sourceText")?.value).toBe("Set up a 5-level ETH grid");
    expect(component.nlSourceText).toBe("Set up a 5-level ETH grid");
  });

  it("should populate MACD conditions from an interpreted signal strategy", () => {
    const result: StrategyIntentDto = {
      config: {
        schemaVersion: 1,
        strategyMode: "signal",
        strategyName: "BTC Momentum",
        exchange: "Hyperliquid",
        market: "BTC-USD",
        timeframe: "15m",
        direction: "long",
        enabled: true,
        templateId: "custom_signal",
        trendFilter: {
          enabled: false,
          type: "ema_cross",
          period: null,
          fastPeriod: 50,
          slowPeriod: 200,
          operator: "gt",
          appliesTo: "both",
        },
        entryLogic: "all",
        entryConditions: [
          {
            id: "cond-1",
            enabled: true,
            type: "macd",
            label: "MACD bullish crossover",
            params: {
              fastPeriod: 12,
              slowPeriod: 26,
              signalPeriod: 9,
              operator: "cross_above",
            },
          }
        ],
        exit: {
          takeProfit: { enabled: true, type: "fixed_percent", value: 2 },
          stopLoss: { enabled: true, type: "fixed_percent", value: 1 },
          exitOnOppositeSignal: true,
        },
        risk: {
          positionSizeType: "percent_wallet",
          positionSizeValue: 4,
          leverage: 3,
          maxOpenTrades: 1,
          cooldownValue: 0,
          cooldownUnit: "candles",
          allowSameCandleReentry: false,
        },
        source: {
          entryPoint: "naturalLanguage",
          summary: "Interpreted from natural language",
          sourceText: "Buy BTC when MACD crosses above signal",
        },
      },
      confidence: 0.78,
      assumptions: [],
      clarificationNeeded: null,
    };

    component.onNlInterpreted(result);

    expect(component.isSignalMode).toBeTrue();
    expect(component.conditionsFormArray.length).toBe(1);
    expect(component.conditionsFormArray.at(0).get("type")?.value).toBe("macd");
    expect(component.conditionsFormArray.at(0).get("operator")?.value).toBe("cross_above");
  });
});