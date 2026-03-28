import { ComponentFixture, TestBed } from "@angular/core/testing";
import { NoopAnimationsModule } from "@angular/platform-browser/animations";
import { BacktestTrade } from "../../../core/models/backtest.model";
import { TradeLogTableComponent } from "./trade-log-table.component";

describe("TradeLogTableComponent", () => {
  let component: TradeLogTableComponent;
  let fixture: ComponentFixture<TradeLogTableComponent>;

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
      tradeType: "GridFill"
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
      tradeType: "HedgeOpen"
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TradeLogTableComponent, NoopAnimationsModule]
    }).compileComponents();

    fixture = TestBed.createComponent(TradeLogTableComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput("trades", mockTrades);
    fixture.detectChanges();
  });

  it("should render the trade rows", () => {
    const rows = fixture.nativeElement.querySelectorAll(".mat-mdc-row");

    expect(rows.length).toBe(2);
    expect(fixture.nativeElement.textContent).toContain("Entry Time");
    expect(fixture.nativeElement.textContent).toContain("Short");
  });

  it("should colour positive and negative pnl values", () => {
    const profitCell = fixture.nativeElement.querySelector(".trade-log__pnl--profit");
    const lossCell = fixture.nativeElement.querySelector(".trade-log__pnl--loss");

    expect(profitCell.textContent).toContain("$125.50");
    expect(lossCell.textContent).toContain("$-48.20");
  });

  it("should sort by pnl using the configured sorting accessor", () => {
    component.sort.active = "pnl";
    component.sort.direction = "desc";

    const sorted = component.dataSource.sortData(component.dataSource.data.slice(), component.sort);

    expect(sorted.map((trade: BacktestTrade) => trade.pnl)).toEqual([125.5, -48.2]);
  });

  it("should show the empty state when there are no trades", () => {
    fixture.componentRef.setInput("trades", []);
    fixture.detectChanges();

    const emptyState = fixture.nativeElement.querySelector(".trade-log__empty");

    expect(emptyState).not.toBeNull();
    expect(emptyState.textContent).toContain("No completed trades");
  });
});