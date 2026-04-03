import { convertToParamMap } from "@angular/router";
import { ComponentFixture, TestBed } from "@angular/core/testing";
import { MatDialog } from "@angular/material/dialog";
import { NoopAnimationsModule } from "@angular/platform-browser/animations";
import { ActivatedRoute, Router } from "@angular/router";
import { of } from "rxjs";
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
          useValue: jasmine.createSpyObj("StrategyApiService", ["getStrategy", "createStrategy", "updateStrategy"])
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
                entryPoint: "ui_builder",
                summary: "Test"
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

  it("should confirm before leaving a dirty form", () => {
    component.form.patchValue({ strategyName: "BTC Grid" });
    component.form.markAsDirty();

    expect(component.hasUnsavedChanges()).toBeTrue();

    component.onCancel();

    expect(dialogSpy.open).toHaveBeenCalled();
    expect(routerSpy.navigate).toHaveBeenCalledWith(["/strategies"]);
    expect(component.hasUnsavedChanges()).toBeFalse();
  });
});