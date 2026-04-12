import { TestBed } from "@angular/core/testing";
import { StrategyMapperService } from "./strategy-mapper.service";

describe("StrategyMapperService", () => {
  let service: StrategyMapperService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(StrategyMapperService);
  });

  it("should map a grid form to a grid strategy config", () => {
    const config = service.mapFormToConfig(buildGridFormValue());

    expect(config.strategyMode).toBe("grid");
    expect(config.grid).not.toBeNull();
    expect(config.entryConditions).toBeNull();
    expect(config.entryLogic).toBeNull();
  });

  it("should map a signal form to a signal strategy config", () => {
    const config = service.mapFormToConfig(buildSignalFormValue());

    expect(config.strategyMode).toBe("signal");
    expect(config.grid).toBeNull();
    expect(config.entryLogic).toBe("all");
    expect(config.entryConditions).not.toBeNull();
    expect(config.entryConditions).toHaveSize(1);
  });

  it("should map RSI condition params for signal mode", () => {
    const config = service.mapFormToConfig(buildSignalFormValue());
    const condition = config.entryConditions?.[0];

    expect(condition).toEqual({
      id: "cond-1",
      enabled: true,
      type: "rsi",
      label: "RSI Oversold",
      params: {
        period: 14,
        operator: "lt",
        value: 40,
      },
    });
  });

  it("should map trend filter config for signal mode", () => {
    const config = service.mapFormToConfig(buildSignalFormValue());

    expect(config.trendFilter).toEqual({
      enabled: true,
      type: "ema_cross",
      period: null,
      fastPeriod: 50,
      slowPeriod: 200,
      operator: "gt",
      appliesTo: "both",
    });
  });

  it("should map swing low stop loss to lookback", () => {
    const config = service.mapFormToConfig({
      ...buildSignalFormValue(),
      exit: {
        takeProfit: { enabled: true, type: "fixed_percent", value: 2 },
        stopLoss: { enabled: true, type: "swing_low", value: 6, lookback: 5 },
        exitOnOppositeSignal: false,
      },
    });

    expect(config.exit.stopLoss).toEqual({
      enabled: true,
      type: "swing_low",
      value: null,
      lookback: 5,
      atrMultiplier: null,
      trailingStopWarmup: null,
    });
  });

  it("should preserve r-multiple take profit type from the form", () => {
    const config = service.mapFormToConfig({
      ...buildGridFormValue(),
      exit: {
        takeProfit: { enabled: true, type: "r_multiple", value: 2.5 },
        stopLoss: { enabled: true, type: "fixed_percent", value: 6 },
        exitOnOppositeSignal: false,
      },
    });

    expect(config.exit.takeProfit).toEqual({
      enabled: true,
      type: "r_multiple",
      value: 2.5,
      lookback: null,
    });
  });

  it("should map MACD condition params for signal mode", () => {
    const config = service.mapFormToConfig({
      ...buildSignalFormValue(),
      templateId: "macd_cross",
      conditions: [{
        id: "cond-1",
        enabled: true,
        type: "macd",
        label: "MACD bullish crossover",
        fastPeriod: 12,
        slowPeriod: 26,
        signalPeriod: 9,
        operator: "cross_above_signal",
      }],
    });
    const condition = config.entryConditions?.[0];

    expect(config.strategyMode).toBe("signal");
    expect(condition).toEqual({
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
    });
  });
});

function buildGridFormValue(): Record<string, unknown> {
  return {
    templateId: "grid",
    strategyName: "Test Grid",
    exchange: "Hyperliquid",
    market: "BTC-USD",
    timeframe: "15m",
    direction: "long",
    grid: {
      levels: 10,
      spacing: 0.5,
      entryMode: "auto_from_signal_candle",
      anchorPrice: null,
      breakdownThreshold: 1.5,
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
    trendFilter: {
      enabled: false,
      type: "ema_cross",
      period: 200,
      fastPeriod: 50,
      slowPeriod: 200,
      operator: "gt",
      appliesTo: "both",
    },
    metadata: { tags: [], notes: "" },
    conditions: [],
  };
}

function buildSignalFormValue(): Record<string, unknown> {
  return {
    ...buildGridFormValue(),
    templateId: "custom_signal",
    trendFilter: {
      enabled: true,
      type: "ema_cross",
      period: 200,
      fastPeriod: 50,
      slowPeriod: 200,
      operator: "gt",
      appliesTo: "both",
    },
    conditions: [{
      id: "cond-1",
      enabled: true,
      type: "rsi",
      label: "RSI Oversold",
      period: 14,
      operator: "lt",
      value: 40,
    }],
  };
}