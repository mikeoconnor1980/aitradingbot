import { TestBed } from "@angular/core/testing";
import { of } from "rxjs";
import { StrategyApiService } from "./strategy-api.service";
import { StrategyValidationService } from "./strategy-validation.service";

describe("StrategyValidationService", () => {
  let service: StrategyValidationService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        StrategyValidationService,
        {
          provide: StrategyApiService,
          useValue: {
            validateStrategy: jasmine.createSpy("validateStrategy").and.returnValue(of({
              isValid: true,
              errors: [],
              warnings: [],
              infoMessages: []
            }))
          }
        }
      ]
    });

    service = TestBed.inject(StrategyValidationService);
  });

  it("should return an error when strategy name is empty", () => {
    const errors = service.validate({
      strategyName: "",
      market: "BTC-USD",
      timeframe: "15m",
      grid: { levels: 10, spacing: 0.5, breakdownThreshold: 1.5, entryMode: "auto_from_signal_candle" },
      exit: {
        takeProfit: { enabled: true, value: 2 },
        stopLoss: { enabled: true, value: 6 }
      },
      risk: { positionSizeValue: 5, leverage: 1, maxOpenTrades: 1, cooldownValue: 0 }
    });

    expect(errors.some((error) => error.fieldPath === "strategyName" && error.severity === "error")).toBeTrue();
  });

  it("should return an error when grid levels exceed 50", () => {
    const errors = service.validate({
      strategyName: "BTC Grid",
      market: "BTC-USD",
      timeframe: "15m",
      grid: { levels: 51, spacing: 0.5, breakdownThreshold: 1.5, entryMode: "auto_from_signal_candle" },
      exit: {
        takeProfit: { enabled: true, value: 2 },
        stopLoss: { enabled: true, value: 6 }
      },
      risk: { positionSizeValue: 5, leverage: 1, maxOpenTrades: 1, cooldownValue: 0 }
    });

    expect(errors.some((error) => error.fieldPath === "grid.levels" && error.severity === "error")).toBeTrue();
  });

  it("should return no error severity issues for a valid grid form", () => {
    const errors = service.validate({
      strategyName: "BTC Grid",
      market: "BTC-USD",
      timeframe: "15m",
      grid: { levels: 10, spacing: 0.5, breakdownThreshold: 1.5, entryMode: "auto_from_signal_candle" },
      exit: {
        takeProfit: { enabled: true, value: 2 },
        stopLoss: { enabled: true, value: 6 }
      },
      risk: { positionSizeValue: 5, leverage: 1, maxOpenTrades: 1, cooldownValue: 0 }
    });

    expect(errors.filter((error) => error.severity === "error")).toHaveSize(0);
  });
});