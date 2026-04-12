import { ComponentFixture, TestBed } from "@angular/core/testing";
import { BacktestResult } from "../../../core/models/backtest.model";
import { BacktestResultComponent } from "./backtest-result.component";

describe("BacktestResultComponent", () => {
  let component: BacktestResultComponent;
  let fixture: ComponentFixture<BacktestResultComponent>;

  const mockResult: BacktestResult = {
    id: "run-1",
    symbol: "BTC",
    intervals: ["15m", "1h", "4h"],
    startDate: "2024-01-01T00:00:00Z",
    endDate: "2024-01-31T00:00:00Z",
    strategyConfig: {
      schemaVersion: 1,
      strategyMode: "grid",
      strategyName: "Backtest",
      exchange: "Hyperliquid",
      market: "BTC",
      timeframe: "15m",
      direction: "long",
      enabled: true,
      grid: {
        levels: 8,
        entryMode: "WaitForLimitPrice",
        anchorPrice: 42500,
        spacing: 0.45,
        breakdownThreshold: -3
      },
      exit: {
        takeProfit: { enabled: true, type: "fixed_percent", value: 1.2 },
        stopLoss: { enabled: true, type: "fixed_percent", value: 5 },
        exitOnOppositeSignal: false
      },
      risk: {
        positionSizeType: "fixed_notional",
        positionSizeValue: 250,
        riskPerTradePercent: 1,
        autoLeverage: true,
        leverage: 4,
        maxOpenTrades: 1,
        cooldownValue: 0,
        cooldownUnit: "candles",
        allowSameCandleReentry: false
      },
      source: { entryPoint: "ui_builder", summary: "Backtest: BTC" }
    },
    executionConfig: {
      feeModel: {
        makerFeeRate: 0.0001,
        takerFeeRate: 0.00035,
        slippageRate: 0
      },
      leverage: 4
    },
    initialCapital: 10000,
    status: "Completed",
    progress: 100,
    candlesReplayed: 2500,
    elapsedMs: 2750,
    totalTrades: 12,
    winningTrades: 8,
    losingTrades: 4,
    winRate: 66.7,
    totalPnl: 1250.55,
    maxDrawdown: -340.25,
    averageTradePnl: 104.21,
    averageHoldTimeMinutes: 185,
    hedgesOpened: 2,
    totalFeesPaid: 32.4,
    trades: [],
    hasAuditLog: false,
    createdAt: "2026-03-28T12:00:00Z"
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BacktestResultComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(BacktestResultComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput("result", mockResult);
    fixture.detectChanges();
  });

  it("should render the metric cards", () => {
    const cards = fixture.nativeElement.querySelectorAll(".backtest-result__card");
    const content = fixture.nativeElement.textContent;

    expect(cards.length).toBe(10);
    expect(content).toContain("Total PnL");
    expect(content).toContain("Lot Win Rate");
    expect(content).toContain("Trade Lots");
    expect(content).toContain("Avg Lot PnL");
    expect(content).toContain("Winning Lots");
    expect(content).toContain("Losing Lots");
    expect(content).toContain("Avg Hold Time");
    expect(content).toContain("Trade metrics are counted per trade lot");
  });

  it("should colour positive pnl values as profit", () => {
    expect(component.totalPnlClass).toBe("backtest-result__value--profit");
    expect(fixture.nativeElement.querySelector(".backtest-result__value--profit")?.textContent).toContain("$1,250.55");
  });

  it("should render the configuration echo section", () => {
    const content = fixture.nativeElement.textContent;

    expect(content).toContain("Configuration Used");
    expect(content).toContain("BTC");
    expect(content).toContain("15m, 1h, 4h");
    expect(content).toContain("4x");
    expect(content).toContain("Wait for limit price");
    expect(content).toContain("$42,500.00");
  });

  it("should show the empty state when the result has zero trades", () => {
    fixture.componentRef.setInput("result", { ...mockResult, totalTrades: 0, winningTrades: 0, losingTrades: 0 });
    fixture.detectChanges();

    const emptyState = fixture.nativeElement.querySelector(".backtest-result__empty");

    expect(emptyState).not.toBeNull();
    expect(emptyState.textContent).toContain("did not generate any trades");
  });

  it("should render the hybrid entry mode label", () => {
    fixture.componentRef.setInput("result", {
      ...mockResult,
      strategyConfig: {
        ...mockResult.strategyConfig,
        grid: {
          ...mockResult.strategyConfig.grid!,
          entryMode: "InitialMarketThenGrid",
          anchorPrice: null
        }
      }
    });
    fixture.detectChanges();

    expect(component.entryModeLabel).toBe("Initial market buy, then grid");
    expect(fixture.nativeElement.textContent).toContain("Initial market buy, then grid");
  });

  it("GivenRiskBasedResult_WhenPositionSizeLabel_ThenShowsRiskBased", () => {
    fixture.componentRef.setInput("result", {
      ...mockResult,
      strategyConfig: {
        ...mockResult.strategyConfig,
        risk: {
          ...mockResult.strategyConfig.risk,
          positionSizeType: "risk_based",
          riskPerTradePercent: 2,
          autoLeverage: true
        }
      }
    });
    fixture.detectChanges();

    expect(component.positionSizeLabel).toBe("R-based (2% risk)");
  });
});