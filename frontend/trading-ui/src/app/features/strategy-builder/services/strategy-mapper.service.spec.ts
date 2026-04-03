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
    metadata: { tags: [], notes: "" },
    conditions: [],
  };
}

function buildSignalFormValue(): Record<string, unknown> {
  return {
    ...buildGridFormValue(),
    templateId: "custom_signal",
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