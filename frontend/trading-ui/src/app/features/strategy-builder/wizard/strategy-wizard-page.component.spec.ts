import { ComponentFixture, TestBed } from "@angular/core/testing";
import { NO_ERRORS_SCHEMA } from "@angular/core";
import { NoopAnimationsModule } from "@angular/platform-browser/animations";
import { Router } from "@angular/router";
import { of } from "rxjs";
import { NotificationFacade } from "../../../core/services/notification-facade.service";
import { StrategyConfig, StrategyTemplateDto } from "../models/strategy.model";
import { StrategyApiService } from "../services/strategy-api.service";
import { StrategyDraftService } from "../services/strategy-draft.service";
import { StrategyMapperService } from "../services/strategy-mapper.service";
import { StrategyValidationService } from "../services/strategy-validation.service";
import { StrategyWizardPageComponent } from "./strategy-wizard-page.component";

describe("StrategyWizardPageComponent", () => {
  let fixture: ComponentFixture<StrategyWizardPageComponent>;
  let component: StrategyWizardPageComponent;
  let routerSpy: jasmine.SpyObj<Router>;

  beforeEach(async () => {
    routerSpy = jasmine.createSpyObj<Router>("Router", ["navigate"]);
    routerSpy.navigate.and.resolveTo(true);

    await TestBed.configureTestingModule({
      imports: [StrategyWizardPageComponent, NoopAnimationsModule],
      providers: [
        { provide: Router, useValue: routerSpy },
        {
          provide: StrategyApiService,
          useValue: {
            getTemplates: () => of([]),
            validateStrategy: () => of({ isValid: true, errors: [], warnings: [], infoMessages: [] }),
            createStrategy: () => of({ id: "strategy-1" }),
          }
        },
        {
          provide: StrategyMapperService,
          useValue: {
            mapFormToConfig: (formValue: Record<string, unknown>): StrategyConfig => ({
              schemaVersion: 1,
              strategyMode: String(formValue["templateId"] ?? "grid") === "dca"
                ? "dca"
                : String(formValue["templateId"] ?? "grid") === "signal"
                  ? "signal"
                  : "grid",
              strategyName: String(formValue["strategyName"] ?? ""),
              exchange: String(formValue["exchange"] ?? "Hyperliquid"),
              market: String(formValue["market"] ?? "BTC-USD"),
              timeframe: String(formValue["timeframe"] ?? "15m"),
              direction: String(formValue["direction"] ?? "long") as "long" | "short" | "both",
              enabled: true,
              templateId: String(formValue["templateId"] ?? "grid"),
              grid: null,
              dca: null,
              exit: {
                takeProfit: { enabled: false, type: "fixed_percent", value: null },
                stopLoss: { enabled: false, type: "fixed_percent", value: null },
                exitOnOppositeSignal: false,
              },
              risk: {
                positionSizeType: "percent_wallet",
                positionSizeValue: 5,
                leverage: 1,
                maxOpenTrades: 1,
                cooldownValue: 0,
                cooldownUnit: "candles",
                allowSameCandleReentry: false,
              },
            })
          }
        },
        {
          provide: StrategyValidationService,
          useValue: {
            validate: () => [],
          }
        },
        {
          provide: StrategyDraftService,
          useValue: {
            draft: null,
            save: jasmine.createSpy("save"),
            clear: jasmine.createSpy("clear"),
          }
        },
        {
          provide: NotificationFacade,
          useValue: jasmine.createSpyObj("NotificationFacade", ["success", "error"])
        },
      ],
      schemas: [NO_ERRORS_SCHEMA],
    })
      .overrideComponent(StrategyWizardPageComponent, {
        set: {
          template: "",
          imports: [],
        }
      })
      .compileComponents();

    fixture = TestBed.createComponent(StrategyWizardPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it("should keep DCA selection inside the wizard", () => {
    component.onTemplateSelected("dca");

    expect(routerSpy.navigate).not.toHaveBeenCalled();
    expect(component.isDcaMode).toBeTrue();
    expect(component.form.get("templateId")?.value).toBe("dca");
    expect(component.form.get("timeframe")?.value).toBe("1h");
    expect(component.form.get("direction")?.value).toBe("long");
    expect(component.form.get("timeframe")?.disabled).toBeTrue();
    expect(component.form.get("direction")?.disabled).toBeTrue();
  });

  it("should keep grid selection inside the wizard", () => {
    component.onTemplateSelected("grid");

    expect(routerSpy.navigate).not.toHaveBeenCalled();
    expect(component.isDcaMode).toBeFalse();
    expect(component.isSignalMode).toBeFalse();
    expect(component.form.get("templateId")?.value).toBe("grid");
  });

  it("should load DCA library templates into the wizard instead of redirecting", () => {
    const template: StrategyTemplateDto = {
      id: "template-1",
      slug: "btc-dca",
      name: "BTC DCA",
      description: "Weekly BTC accumulation",
      strategyMode: "dca",
      direction: "long",
      market: "BTC-USD",
      tags: ["dca"],
      config: {
        schemaVersion: 1,
        strategyMode: "dca",
        strategyName: "BTC DCA",
        exchange: "Hyperliquid",
        assetType: "spot",
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
          baseAmountUsd: 250,
          allocations: [{ market: "BTC-USD", weightPercent: 100 }],
          gateConditions: {
            maxPriceUsd: 95000,
            minFearGreedIndex: null,
            maxFearGreedIndex: 40,
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
          positionSizeValue: 250,
          leverage: 1,
          maxOpenTrades: 1,
          cooldownValue: 0,
          cooldownUnit: "candles",
          allowSameCandleReentry: false,
        },
      },
      sortOrder: 1,
      isSystemTemplate: true,
      isBeginnerVisible: true,
      createdAtUtc: 0,
      updatedAtUtc: 0,
    };

    component.stepper = { next: jasmine.createSpy("next") } as unknown as typeof component.stepper;

    component.onLibraryTemplateSelected(template);

    expect(routerSpy.navigate).not.toHaveBeenCalled();
    expect(component.isDcaMode).toBeTrue();
    expect(component.form.get("strategyName")?.value).toBe("BTC DCA");
    expect(component.form.get("dca.baseAmountUsd")?.value).toBe(250);
  });
});