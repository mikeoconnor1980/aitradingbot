import { ComponentFixture, TestBed } from "@angular/core/testing";
import { NoopAnimationsModule } from "@angular/platform-browser/animations";
import { of } from "rxjs";
import { BacktestDebugResponse, OrderEventType } from "../../../core/models/backtest-debug.model";
import { BacktestTrade } from "../../../core/models/backtest.model";
import { BacktestService } from "../../../core/services/backtest.service";
import { TradeLogTableComponent } from "./trade-log-table.component";

describe("TradeLogTableComponent", () => {
  let component: TradeLogTableComponent;
  let fixture: ComponentFixture<TradeLogTableComponent>;
  let backtestServiceSpy: jasmine.SpyObj<BacktestService>;

  const mockTrades: BacktestTrade[] = [
    {
      entryTime: "2024-01-05T10:00:00Z",
      exitTime: "2024-01-05T14:00:00Z",
      entryPrice: 43000,
      exitPrice: 43250,
      side: "Long",
      size: 0.12,
      pnl: 125.5,
      fees: 3.25,
      tradeType: "GridFill",
      gridCycleId: "cycle-1"
    },
    {
      entryTime: "2024-01-06T09:30:00Z",
      exitTime: "2024-01-06T12:15:00Z",
      entryPrice: 43150,
      exitPrice: 42990,
      side: "Short",
      size: 0.08,
      pnl: -48.2,
      fees: 2.1,
      tradeType: "HedgeOpen",
      gridCycleId: "cycle-2"
    },
    {
      entryTime: "2024-01-07T09:30:00Z",
      exitTime: null,
      entryPrice: 42850,
      exitPrice: null,
      side: "Long",
      size: 0.04,
      pnl: null,
      fees: 1.1,
      tradeType: "GridFill",
      gridCycleId: "cycle-3"
    }
  ];

  const mockDebugData: BacktestDebugResponse = {
    cycleId: "cycle-1",
    gridCycleSummary: {
      gridCycleId: "cycle-1",
      deployTimestampUtc: 1704448800000,
      anchorPrice: 43000,
      levelsPlaced: 4,
      levelPrices: [43000, 42850, 42700, 42550],
      levelsFilled: 2,
      takeProfitPrice: 43250,
      stopLossPrice: null,
      exitReason: "TakeProfit",
      cyclePnl: 125.5,
      cycleDurationMs: 14400000,
      closeTimestampUtc: 1704463200000
    },
    orderEvents: [
      {
        timestampUtc: 1704448800000,
        eventType: OrderEventType.Placed,
        orderId: "order-1",
        side: "Buy",
        orderType: "Limit",
        price: 43000,
        size: 0.12,
        fillPrice: null,
        fee: null,
        isMaker: null,
        cancellationReason: null,
        gridCycleId: "cycle-1"
      }
    ],
    candleEvaluations: [
      {
        timestampUtc: 1704448800000,
        open: 43000,
        high: 43100,
        low: 42950,
        close: 43075,
        volume: 1200,
        isWarmup: false,
        emaFast: 43020,
        emaSlow: 42980,
        emaTrend: 42890,
        rsi: 58.4,
        atr: 112.2,
        setupDetected: true,
        gridLifecycleState: "GridActive",
        positionSize: 0.12,
        positionAvgEntry: 43000,
        signalsEmitted: ["DeployGrid"],
        gridCycleId: "cycle-1"
      }
    ]
  };

  beforeEach(async () => {
    backtestServiceSpy = jasmine.createSpyObj<BacktestService>("BacktestService", ["getDebugData"]);
    backtestServiceSpy.getDebugData.and.returnValue(of(mockDebugData));

    await TestBed.configureTestingModule({
      imports: [TradeLogTableComponent, NoopAnimationsModule],
      providers: [{ provide: BacktestService, useValue: backtestServiceSpy }]
    }).compileComponents();

    fixture = TestBed.createComponent(TradeLogTableComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput("trades", mockTrades);
    fixture.componentRef.setInput("backtestId", "backtest-1");
    fixture.componentRef.setInput("hasAuditLog", true);
    fixture.detectChanges();
  });

  it("should render the trade rows", () => {
    const rows = fixture.nativeElement.querySelectorAll(".trade-log__row");

    expect(rows.length).toBe(3);
    expect(fixture.nativeElement.textContent).toContain("Entry Time");
    expect(fixture.nativeElement.textContent).toContain("Short");
  });

  it("should split closed trades and open positions into separate sections", () => {
    const text = fixture.nativeElement.textContent;

    expect(text).toContain("Closed Trades");
    expect(text).toContain("Open Positions At End Of Run");
  });

  it("should colour positive and negative pnl values", () => {
    const profitCell = fixture.nativeElement.querySelector(".trade-log__pnl--profit");
    const lossCell = fixture.nativeElement.querySelector(".trade-log__pnl--loss");

    expect(profitCell.textContent).toContain("$125.50");
    expect(lossCell.textContent).toContain("$-48.20");
  });

  it("should sort by pnl using the configured sort state", () => {
    component.onSort("pnl");

    const sorted = component.completedTrades;

    expect(sorted.map((trade: BacktestTrade) => trade.pnl)).toEqual([125.5, -48.2]);
  });

  it("should lazy load debug data when a trade row is expanded", () => {
    component.toggleDetails(mockTrades[0]);
    fixture.detectChanges();

    expect(backtestServiceSpy.getDebugData).toHaveBeenCalledOnceWith("backtest-1", "cycle-1", jasmine.anything());
    expect(fixture.nativeElement.textContent).toContain("Grid Cycle Summary");
    expect(fixture.nativeElement.textContent).toContain("Order Events");
    expect(fixture.nativeElement.textContent).toContain("Stop Loss");
    expect(fixture.nativeElement.textContent).toContain("—");
  });

  it("should disable debug expansion when audit log data is unavailable", () => {
    fixture.componentRef.setInput("hasAuditLog", false);
    fixture.detectChanges();

    const expandButton = fixture.nativeElement.querySelector("button[mat-icon-button]") as HTMLButtonElement;

    expect(expandButton.disabled).toBeTrue();
  });

  it("should show the empty state when there are no trades", () => {
    fixture.componentRef.setInput("trades", []);
    fixture.detectChanges();

    const emptyState = fixture.nativeElement.querySelector(".trade-log__empty");

    expect(emptyState).not.toBeNull();
    expect(emptyState.textContent).toContain("No trades recorded");
  });
});