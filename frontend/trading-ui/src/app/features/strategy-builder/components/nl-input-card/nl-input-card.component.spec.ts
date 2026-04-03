import { HttpErrorResponse } from "@angular/common/http";
import { ComponentFixture, TestBed } from "@angular/core/testing";
import { By } from "@angular/platform-browser";
import { NoopAnimationsModule } from "@angular/platform-browser/animations";
import { of, throwError } from "rxjs";
import { StrategyIntentDto } from "../../models/strategy-intent.model";
import { StrategyApiService } from "../../services/strategy-api.service";
import { NlInputCardComponent } from "./nl-input-card.component";

describe("NlInputCardComponent", () => {
  let fixture: ComponentFixture<NlInputCardComponent>;
  let component: NlInputCardComponent;
  let strategyApiSpy: jasmine.SpyObj<StrategyApiService>;

  beforeEach(async () => {
    strategyApiSpy = jasmine.createSpyObj<StrategyApiService>("StrategyApiService", ["interpretStrategy"]);

    await TestBed.configureTestingModule({
      imports: [NlInputCardComponent, NoopAnimationsModule],
      providers: [
        { provide: StrategyApiService, useValue: strategyApiSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(NlInputCardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it("should disable generate when the description is empty", () => {
    const button = fixture.debugElement.query(By.css("button[color='primary']")).nativeElement as HTMLButtonElement;

    expect(button.disabled).toBeTrue();
  });

  it("should emit the interpreted result after a successful generate", () => {
    const result: StrategyIntentDto = {
      config: {
        schemaVersion: 1,
        strategyMode: "grid",
        strategyName: "ETH Grid",
        exchange: "Hyperliquid",
        market: "ETH-USD",
        timeframe: "15m",
        direction: "long",
        enabled: true,
        grid: {
          levels: 5,
          spacing: 0.5,
          entryMode: "auto_from_signal_candle",
          breakdownThreshold: 1.5,
          anchorPrice: null,
        },
        exit: {
          takeProfit: { enabled: true, type: "fixed_percent", value: 2 },
          stopLoss: { enabled: true, type: "fixed_percent", value: 6 },
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
        source: {
          entryPoint: "naturalLanguage",
          summary: "Interpreted from natural language",
          sourceText: "Set up a 5-level ETH grid",
        },
      },
      confidence: 0.87,
      assumptions: [],
      clarificationNeeded: null,
    };
    const emitSpy = spyOn(component.interpreted, "emit");
    strategyApiSpy.interpretStrategy.and.returnValue(of(result));

    component.text = "Set up a 5-level ETH grid";
    component.generate();

    expect(strategyApiSpy.interpretStrategy).toHaveBeenCalledWith("Set up a 5-level ETH grid", jasmine.anything());
    expect(emitSpy).toHaveBeenCalledWith(result);
    expect(component.errorMessage).toBeNull();
  });

  it("should show an inline rate-limit error when interpretation fails with 429", () => {
    strategyApiSpy.interpretStrategy.and.returnValue(throwError(() => new HttpErrorResponse({ status: 429 })));

    component.text = "Interpret me";
    component.generate();
    fixture.detectChanges();

    expect(component.errorMessage).toContain("Too many requests");
  });

  it("should keep a failed interpretation result inline instead of emitting it", () => {
    const result: StrategyIntentDto = {
      config: {
        schemaVersion: 1,
        strategyMode: "grid",
        strategyName: "",
        exchange: "Hyperliquid",
        market: "",
        timeframe: "15m",
        direction: "long",
        enabled: true,
        exit: {
          takeProfit: { enabled: false, type: "fixed_percent", value: null },
          stopLoss: { enabled: false, type: "fixed_percent", value: null },
          exitOnOppositeSignal: false,
        },
        risk: {
          positionSizeType: "percent_wallet",
          positionSizeValue: 10,
          leverage: 1,
          maxOpenTrades: 1,
          cooldownValue: 0,
          cooldownUnit: "candles",
          allowSameCandleReentry: false,
        },
      },
      confidence: 0,
      assumptions: [],
      clarificationNeeded: "The configured Gemini API key has no available quota.",
    };
    const emitSpy = spyOn(component.interpreted, "emit");
    strategyApiSpy.interpretStrategy.and.returnValue(of(result));

    component.text = "Interpret me";
    component.generate();

    expect(emitSpy).not.toHaveBeenCalled();
    expect(component.errorMessage).toContain("no available quota");
  });
});