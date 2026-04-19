import { convertToParamMap } from "@angular/router";
import { ComponentFixture, TestBed } from "@angular/core/testing";
import { By } from "@angular/platform-browser";
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
import { HyperliquidApiService } from "../../core/services/hyperliquid-api.service";
import { NotificationFacade } from "../../core/services/notification-facade.service";
import { SubscriptionService } from "../../core/services/subscription.service";
import { StrategyTemplateSelectorComponent } from "./components/strategy-template-selector/strategy-template-selector.component";

describe("StrategyBuilderPageComponent", () => {
  let fixture: ComponentFixture<StrategyBuilderPageComponent>;
  let component: StrategyBuilderPageComponent;
  let routerSpy: jasmine.SpyObj<Router>;
  let dialogSpy: jasmine.SpyObj<MatDialog>;
  let activatedRouteStub: {
    snapshot: {
      paramMap: ReturnType<typeof convertToParamMap>;
      queryParamMap: ReturnType<typeof convertToParamMap>;
    };
  };

  beforeEach(async () => {
    routerSpy = jasmine.createSpyObj<Router>("Router", ["navigate"]);
    routerSpy.navigate.and.resolveTo(true);
    dialogSpy = jasmine.createSpyObj<MatDialog>("MatDialog", ["open"]);
    dialogSpy.open.and.returnValue({ afterClosed: () => of(true) } as never);
    activatedRouteStub = {
      snapshot: {
        paramMap: convertToParamMap({}),
        queryParamMap: convertToParamMap({})
      }
    };

    await TestBed.configureTestingModule({
      imports: [StrategyBuilderPageComponent, NoopAnimationsModule],
      providers: [
        { provide: Router, useValue: routerSpy },
        { provide: ActivatedRoute, useValue: activatedRouteStub },
        {
          provide: StrategyApiService,
          useValue: jasmine.createSpyObj("StrategyApiService", ["getStrategy", "getTemplates", "createStrategy", "updateStrategy", "interpretStrategy"])
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
                riskPerTradePercent: 1,
                autoLeverage: true,
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
          provide: NotificationFacade,
          useValue: jasmine.createSpyObj("NotificationFacade", ["success", "error"])
        },
        {
          provide: SubscriptionService,
          useValue: {
            hasFeature: () => true,
          }
        },
        {
          provide: HyperliquidApiService,
          useValue: {
            getAccountSummary: () => of({
              equity: 10000,
              availableMargin: 8000,
              crossMarginRatio: 0,
              maintenanceMargin: 0,
              unrealisedPnl: 0,
            })
          }
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

  it("should switch into DCA mode and lock timeframe and direction", () => {
    component.onTemplateSelected("dca");

    expect(component.isDcaMode).toBeTrue();
    expect(component.form.get("grid")?.disabled).toBeTrue();
    expect(component.form.get("timeframe")?.disabled).toBeTrue();
    expect(component.form.get("direction")?.disabled).toBeTrue();
    expect(component.form.get("timeframe")?.value).toBe("1h");
    expect(component.form.get("direction")?.value).toBe("long");
    expect(component.form.get("dca.baseAmountUsd")?.value).toBe(100);
  });

  it("should initialize DCA mode from the mode query param", () => {
    activatedRouteStub.snapshot.queryParamMap = convertToParamMap({ mode: "dca" });

    const dcaFixture = TestBed.createComponent(StrategyBuilderPageComponent);
    const dcaComponent = dcaFixture.componentInstance;

    dcaFixture.detectChanges();

    expect(dcaComponent.isDcaMode).toBeTrue();
    expect(dcaComponent.selectedTemplateId).toBe("dca");
    expect(dcaComponent.form.get("timeframe")?.value).toBe("1h");
    expect(dcaComponent.form.get("direction")?.value).toBe("long");
    expect(dcaComponent.form.pristine).toBeTrue();
    expect(dcaComponent.hasUnsavedChanges()).toBeFalse();
  });

  it("should hide the template selector when editing an existing strategy", () => {
    activatedRouteStub.snapshot.paramMap = convertToParamMap({ id: "strategy-1" });

    const strategyApi = TestBed.inject(StrategyApiService) as jasmine.SpyObj<StrategyApiService>;
    strategyApi.getTemplates.and.returnValue(of([]));
    strategyApi.getStrategy.and.returnValue(of({
      id: "strategy-1",
      name: "BTC DCA",
      strategyType: "dca",
      version: 1,
      createdAt: "2026-04-19T00:00:00Z",
      updatedAt: "2026-04-19T00:00:00Z",
      config: {
        schemaVersion: 1,
        strategyMode: "dca",
        strategyName: "BTC DCA",
        exchange: "Hyperliquid",
        market: "BTC-USD",
        timeframe: "1h",
        direction: "long",
        enabled: true,
        templateId: "dca",
        dca: {
          interval: "weekly",
          dayOfWeek: 1,
          dayOfMonth: null,
          timeOfDayUtc: "00:00",
          baseAmountUsd: 100,
          allocations: [{ market: "BTC-USD", weightPercent: 100 }],
          gateConditions: {
            maxPriceUsd: null,
            minFearGreedIndex: null,
            maxFearGreedIndex: null,
          },
          scalingBands: [],
          profitTaking: null,
          budgetCapUsd: null,
        },
        exit: {
          takeProfit: { enabled: false, type: "fixed_percent", value: null },
          stopLoss: { enabled: false, type: "fixed_percent", value: null },
          exitOnOppositeSignal: false,
        },
        risk: {
          positionSizeType: "fixed_notional",
          positionSizeValue: 100,
          riskPerTradePercent: 1,
          autoLeverage: true,
          leverage: 1,
          maxOpenTrades: 1,
          cooldownValue: 0,
          cooldownUnit: "candles",
          allowSameCandleReentry: false,
        },
        source: {
          entryPoint: "ui_builder",
          summary: "Created in strategy builder",
          sourceText: null,
        }
      }
    }));

    const editFixture = TestBed.createComponent(StrategyBuilderPageComponent);
    const editComponent = editFixture.componentInstance;

    editFixture.detectChanges();

    expect(editComponent.editId).toBe("strategy-1");
    expect(editFixture.debugElement.query(By.directive(StrategyTemplateSelectorComponent))).toBeNull();
  });

  it("should include risk-based controls in the risk form group", () => {
    expect(component.riskFormGroup.get("riskPerTradePercent")).not.toBeNull();
    expect(component.riskFormGroup.get("autoLeverage")).not.toBeNull();
    expect(component.riskFormGroup.get("riskPerTradePercent")?.value).toBe(1);
    expect(component.riskFormGroup.get("autoLeverage")?.value).toBeTrue();
    expect(component.riskFormGroup.get("riskPerTradePercent")?.disabled).toBeTrue();
    expect(component.riskFormGroup.get("autoLeverage")?.disabled).toBeTrue();
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
          riskPerTradePercent: 1,
          autoLeverage: true,
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
              operator: "cross_above_signal",
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
          riskPerTradePercent: 1,
          autoLeverage: true,
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
    expect(component.conditionsFormArray.at(0).get("operator")?.value).toBe("cross_above_signal");
  });
});