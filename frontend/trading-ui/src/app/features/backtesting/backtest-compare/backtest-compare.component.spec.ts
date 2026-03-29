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
      gridLevels: 10,
      entryMode: "AutoFromSignalCandle",
      manualAnchorPrice: null,
      gridSpacing: 0.5,
      takeProfitPercent: 1.2,
      breakdownThreshold: 2,
      makerFee: 0.0001,
      takerFee: 0.00035,
      slippage: 0,
      positionSize: 100,
      leverage: 3,
      stopLossPercent: 5
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
      gridLevels: 12,
      entryMode: "WaitForLimitPrice",
      manualAnchorPrice: 41850,
      gridSpacing: 0.75,
      takeProfitPercent: 1.5,
      breakdownThreshold: 2.5,
      makerFee: 0.0001,
      takerFee: 0.0004,
      slippage: 0.0002,
      positionSize: 125,
      leverage: 4,
      stopLossPercent: 4.5
    } }));
    fixture.detectChanges();
  });

  it("renders comparison rows", () => {
    expect(component.comparisonRows.length).toBe(10);
    expect(fixture.nativeElement.textContent).toContain("Metrics Comparison");
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
});