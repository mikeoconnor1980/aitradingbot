import { ComponentFixture, TestBed } from "@angular/core/testing";
import { NoopAnimationsModule } from "@angular/platform-browser/animations";
import { BacktestResult } from "../../../core/models/backtest.model";
import { BacktestCompareComponent } from "./backtest-compare.component";

describe("BacktestCompareComponent", () => {
  let component: BacktestCompareComponent;
  let fixture: ComponentFixture<BacktestCompareComponent>;

  const createResult = (overrides: Partial<BacktestResult>): BacktestResult => ({
    id: "run-a",
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
        levels: 10,
        entryMode: "AutoFromSignalCandle",
        anchorPrice: null,
        spacing: 0.5,
        breakdownThreshold: 2
      },
      exit: {
        takeProfit: { enabled: true, type: "fixed_percent", value: 1.2 },
        stopLoss: { enabled: true, type: "fixed_percent", value: 5 },
        exitOnOppositeSignal: false
      },
      risk: {
        positionSizeType: "fixed_notional",
        positionSizeValue: 100,
        leverage: 3,
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
      leverage: 3
    },
    initialCapital: 10000,
    status: "Completed",
    progress: 100,
    candlesReplayed: 1200,
    elapsedMs: 2500,
    totalTrades: 40,
    winningTrades: 24,
    losingTrades: 16,
    winRate: 60,
    totalPnl: 900,
    maxDrawdown: -350,
    averageTradePnl: 22.5,
    averageHoldTimeMinutes: 95,
    hedgesOpened: 3,
    totalFeesPaid: 44.2,
    trades: [],
    equityTimeSeries: [],
    hasAuditLog: false,
    createdAt: "2026-03-28T12:00:00Z",
    ...overrides
  });

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BacktestCompareComponent, NoopAnimationsModule]
    }).compileComponents();

    fixture = TestBed.createComponent(BacktestCompareComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput("resultA", createResult({ id: "run-a", totalPnl: 1200, maxDrawdown: -280 }));
    fixture.componentRef.setInput("resultB", createResult({ id: "run-b", totalPnl: 900, maxDrawdown: -420, strategyConfig: {
      ...createResult({}).strategyConfig,
      grid: {
        ...createResult({}).strategyConfig.grid!,
        levels: 12,
        entryMode: "WaitForLimitPrice",
        anchorPrice: 41850,
        spacing: 0.75,
        breakdownThreshold: 2.5
      },
      exit: {
        ...createResult({}).strategyConfig.exit,
        takeProfit: { enabled: true, type: "fixed_percent", value: 1.5 },
        stopLoss: { enabled: true, type: "fixed_percent", value: 4.5 },
        exitOnOppositeSignal: false
      },
      risk: {
        ...createResult({}).strategyConfig.risk,
        positionSizeValue: 125,
        leverage: 4
      }
    }, executionConfig: {
      feeModel: {
        makerFeeRate: 0.0001,
        takerFeeRate: 0.0004,
        slippageRate: 0.0002
      },
      leverage: 4
    } }));
    fixture.detectChanges();
  });

  it("renders comparison rows", () => {
    expect(component.comparisonRows.length).toBe(10);
    expect(fixture.nativeElement.textContent).toContain("Metrics Comparison");
    expect(fixture.nativeElement.textContent).toContain("Trade metrics below are counted per trade lot");
    expect(component.comparisonRows.some((row) => row.metric === "Trade Lots")).toBeTrue();
    expect(component.comparisonRows.some((row) => row.metric === "Winning Lots")).toBeTrue();
  });

  it("marks pnl delta as better when run A is higher", () => {
    const pnlRow = component.comparisonRows.find((row) => row.metric === "Total PnL");

    expect(pnlRow).toBeDefined();
    expect(pnlRow?.delta).toContain("+");
    expect(pnlRow?.deltaClass).toBe("backtest-compare__delta--better");
  });

  it("flags changed config items", () => {
    const changedItems = component.configDiffs.filter((item) => item.changed);

    expect(changedItems.length).toBeGreaterThan(0);
    expect(changedItems.some((item) => item.label === "Grid Levels")).toBeTrue();
    expect(changedItems.some((item) => item.label === "Entry Mode")).toBeTrue();
    expect(changedItems.some((item) => item.label === "Limit Price")).toBeTrue();
  });

  it("uses neutral delta when values are equal", () => {
    fixture.componentRef.setInput("resultB", createResult({ id: "run-b", totalPnl: 1200 }));
    fixture.detectChanges();

    const pnlRow = component.comparisonRows.find((row) => row.metric === "Total PnL");

    expect(pnlRow?.deltaClass).toBe("backtest-compare__delta--neutral");
  });

  it("formats the hybrid entry mode label in config diffs", () => {
    fixture.componentRef.setInput("resultA", createResult({
      id: "run-a",
      strategyConfig: {
        ...createResult({}).strategyConfig,
        grid: {
          ...createResult({}).strategyConfig.grid!,
          entryMode: "InitialMarketThenGrid"
        }
      }
    }));
    fixture.detectChanges();

    const entryModeItem = component.configDiffs.find((item) => item.label === "Entry Mode");

    expect(entryModeItem?.valueA).toBe("Initial market buy, then grid");
  });
});