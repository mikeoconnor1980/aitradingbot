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
      gridLevels: 8,
      gridSpacing: 0.45,
      takeProfitPercent: 1.2,
      breakdownThreshold: -3,
      makerFee: 0.0001,
      takerFee: 0.00035,
      slippage: 0,
      positionSize: 250,
      leverage: 4,
      stopLossPercent: 5
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

    expect(cards.length).toBe(10);
    expect(fixture.nativeElement.textContent).toContain("Total PnL");
    expect(fixture.nativeElement.textContent).toContain("Avg Hold Time");
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
  });

  it("should show the empty state when the result has zero trades", () => {
    fixture.componentRef.setInput("result", { ...mockResult, totalTrades: 0, winningTrades: 0, losingTrades: 0 });
    fixture.detectChanges();

    const emptyState = fixture.nativeElement.querySelector(".backtest-result__empty");

    expect(emptyState).not.toBeNull();
    expect(emptyState.textContent).toContain("did not generate any trades");
  });
});