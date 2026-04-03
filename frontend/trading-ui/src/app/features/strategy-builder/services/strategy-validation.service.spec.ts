import { TestBed } from "@angular/core/testing";
import { of } from "rxjs";
import { StrategyApiService } from "./strategy-api.service";
import { StrategyValidationService } from "./strategy-validation.service";

describe("StrategyValidationService", () => {
  let service: StrategyValidationService;

  function baseFormValue(): Record<string, unknown> {
    return {
      templateId: "grid",
      strategyName: "BTC Grid",
      market: "BTC-USD",
      timeframe: "15m",
      grid: { levels: 10, spacing: 0.5, breakdownThreshold: 1.5, entryMode: "auto_from_signal_candle" },
      exit: {
        takeProfit: { enabled: true, type: "fixed_percent", value: 2 },
        stopLoss: { enabled: true, type: "fixed_percent", value: 6, lookback: null }
      },
      risk: { positionSizeValue: 5, leverage: 1, maxOpenTrades: 1, cooldownValue: 0 },
      conditions: [],
    };
  }

  function validRsiCondition(): Record<string, unknown> {
    return {
      id: "cond-1",
      enabled: true,
      type: "rsi",
      label: "RSI Oversold",
      period: 14,
      operator: "lt",
      value: 40,
    };
  }

  function validMacdCondition(): Record<string, unknown> {
    return {
      id: "cond-1",
      enabled: true,
      type: "macd",
      label: "MACD bullish crossover",
      fastPeriod: 12,
      slowPeriod: 26,
      signalPeriod: 9,
      operator: "cross_above_signal",
    };
  }

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
      ...baseFormValue(),
      strategyName: "",
    });

    expect(errors.some((error) => error.fieldPath === "strategyName" && error.severity === "error")).toBeTrue();
  });

  it("should return an error when grid levels exceed 50", () => {
    const errors = service.validate({
      ...baseFormValue(),
      grid: { levels: 51, spacing: 0.5, breakdownThreshold: 1.5, entryMode: "auto_from_signal_candle" },
    });

    expect(errors.some((error) => error.fieldPath === "grid.levels" && error.severity === "error")).toBeTrue();
  });

  it("should return no error severity issues for a valid grid form", () => {
    const errors = service.validate(baseFormValue());

    expect(errors.filter((error) => error.severity === "error")).toHaveSize(0);
  });

  it("should accept swing low stop loss with lookback", () => {
    const errors = service.validate({
      ...baseFormValue(),
      exit: {
        takeProfit: { enabled: true, type: "fixed_percent", value: 2 },
        stopLoss: { enabled: true, type: "swing_low", value: null, lookback: 5 }
      }
    });

    expect(errors.some((error) => error.fieldPath === "exit.stopLoss.lookback")).toBeFalse();
  });

  it("should require lookback for swing low stop loss", () => {
    const errors = service.validate({
      ...baseFormValue(),
      exit: {
        takeProfit: { enabled: true, type: "fixed_percent", value: 2 },
        stopLoss: { enabled: true, type: "swing_low", value: null, lookback: null }
      }
    });

    expect(errors.some((error) => error.fieldPath === "exit.stopLoss.lookback" && error.code === "REQUIRED")).toBeTrue();
  });

  describe("signal mode validation", () => {
    it("should require at least one condition in signal mode", () => {
      const errors = service.validate({
        ...baseFormValue(),
        templateId: "custom_signal",
        conditions: [],
      });

      expect(errors.some((error) => error.fieldPath === "entryConditions" && error.code === "REQUIRED")).toBeTrue();
    });

    it("should not produce grid errors in signal mode", () => {
      const errors = service.validate({
        ...baseFormValue(),
        templateId: "custom_signal",
        conditions: [validRsiCondition()],
      });

      expect(errors.some((error) => error.fieldPath.startsWith("grid"))).toBeFalse();
    });

    it("should reject RSI value greater than 100", () => {
      const errors = service.validate({
        ...baseFormValue(),
        templateId: "custom_signal",
        conditions: [{ ...validRsiCondition(), value: 150 }],
      });

      expect(errors.some((error) => error.fieldPath === "entryConditions[0].params.value" && error.code === "RANGE")).toBeTrue();
    });

    it("should reject RSI period less than 1", () => {
      const errors = service.validate({
        ...baseFormValue(),
        templateId: "custom_signal",
        conditions: [{ ...validRsiCondition(), period: 0 }],
      });

      expect(errors.some((error) => error.fieldPath === "entryConditions[0].params.period" && error.code === "RANGE")).toBeTrue();
    });

    it("should reject duplicate RSI conditions", () => {
      const errors = service.validate({
        ...baseFormValue(),
        templateId: "custom_signal",
        conditions: [
          validRsiCondition(),
          { ...validRsiCondition(), id: "cond-2", label: "Same rule, different label" },
        ],
      });

      expect(errors.some((error) => error.fieldPath === "entryConditions[0]" && error.code === "DUPLICATE")).toBeTrue();
      expect(errors.some((error) => error.fieldPath === "entryConditions[1]" && error.code === "DUPLICATE")).toBeTrue();
    });

    it("should allow multiple distinct RSI conditions", () => {
      const errors = service.validate({
        ...baseFormValue(),
        templateId: "custom_signal",
        conditions: [
          validRsiCondition(),
          { ...validRsiCondition(), id: "cond-2", operator: "gt", value: 60, label: "RSI Momentum" },
        ],
      });

      expect(errors.some((error) => error.code === "DUPLICATE")).toBeFalse();
    });

    it("should accept a valid RSI condition", () => {
      const errors = service.validate({
        ...baseFormValue(),
        templateId: "custom_signal",
        conditions: [validRsiCondition()],
      });

      expect(errors.some((error) => error.fieldPath.startsWith("entryConditions"))).toBeFalse();
    });

    it("should accept a valid MACD condition", () => {
      const errors = service.validate({
        ...baseFormValue(),
        templateId: "macd_cross",
        conditions: [validMacdCondition()],
      });

      expect(errors.some((error) => error.fieldPath.startsWith("entryConditions"))).toBeFalse();
    });

    it("should reject MACD fast periods outside the allowed range", () => {
      const errors = service.validate({
        ...baseFormValue(),
        templateId: "macd_cross",
        conditions: [{ ...validMacdCondition(), fastPeriod: 1 }],
      });

      expect(errors.some((error) => error.fieldPath === "entryConditions[0].params.fastPeriod" && error.code === "RANGE")).toBeTrue();
    });

    it("should reject MACD fast periods that are not less than slow periods", () => {
      const errors = service.validate({
        ...baseFormValue(),
        templateId: "macd_cross",
        conditions: [{ ...validMacdCondition(), fastPeriod: 26, slowPeriod: 26 }],
      });

      expect(errors.some((error) => error.fieldPath === "entryConditions[0].params.fastPeriod" && error.message === "Fast period must be less than slow period.")).toBeTrue();
    });

    it("should reject duplicate MACD conditions using MACD-specific signatures", () => {
      const errors = service.validate({
        ...baseFormValue(),
        templateId: "macd_cross",
        conditions: [
          validMacdCondition(),
          { ...validMacdCondition(), id: "cond-2", label: "Same MACD rule" },
        ],
      });

      expect(errors.some((error) => error.fieldPath === "entryConditions[0]" && error.code === "DUPLICATE")).toBeTrue();
      expect(errors.some((error) => error.fieldPath === "entryConditions[1]" && error.code === "DUPLICATE")).toBeTrue();
    });
  });
});