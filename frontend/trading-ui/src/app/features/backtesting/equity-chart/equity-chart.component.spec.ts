import { ComponentFixture, TestBed } from "@angular/core/testing";
import { BacktestTrade, EquitySnapshot } from "../../../core/models/backtest.model";
import { EquityChartComponent } from "./equity-chart.component";

class ResizeObserverMock {
  public observe = jasmine.createSpy("observe");
  public disconnect = jasmine.createSpy("disconnect");

  public constructor(callback: ResizeObserverCallback) {
    void callback;
  }
}

describe("EquityChartComponent", () => {
  let component: EquityChartComponent;
  let fixture: ComponentFixture<EquityChartComponent>;
  let originalResizeObserver: typeof ResizeObserver | undefined;

  const equityData: EquitySnapshot[] = [
    { timestampUtc: 1704067200000, equity: 10000 },
    { timestampUtc: 1704153600000, equity: 10125 },
    { timestampUtc: 1704240000000, equity: 10080 }
  ];

  const trades: BacktestTrade[] = [
    {
      entryTime: "2024-01-01T00:00:00Z",
      exitTime: "2024-01-02T00:00:00Z",
      entryPrice: 42000,
      exitPrice: 42100,
      side: "Long",
      size: 0.1,
      pnl: 10,
      fees: 0.5,
      tradeType: "GridFill"
    }
  ];

  beforeEach(async () => {
    originalResizeObserver = globalThis.ResizeObserver;
    (globalThis as typeof globalThis & { ResizeObserver: typeof ResizeObserverMock }).ResizeObserver = ResizeObserverMock as never;

    await TestBed.configureTestingModule({
      imports: [EquityChartComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(EquityChartComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput("equityData", equityData);
    fixture.componentRef.setInput("trades", trades);
  });

  afterEach(() => {
    if (originalResizeObserver) {
      (globalThis as typeof globalThis & { ResizeObserver: typeof ResizeObserver }).ResizeObserver = originalResizeObserver;
      return;
    }

    delete (globalThis as Partial<typeof globalThis>).ResizeObserver;
  });

  it("should create the chart without errors when equity data exists", () => {
    expect(() => fixture.detectChanges()).not.toThrow();
    expect(component).toBeTruthy();
    expect((component as unknown as { _chart: unknown })._chart).toBeTruthy();
  });

  it("should clean up chart resources on destroy", () => {
    fixture.detectChanges();

    const resizeObserver = (component as unknown as { _resizeObserver: ResizeObserverMock })._resizeObserver;
    const chart = (component as unknown as { _chart: { remove: () => void } })._chart;
    spyOn(chart, "remove").and.callThrough();

    component.ngOnDestroy();

    expect(resizeObserver.disconnect).toHaveBeenCalled();
    expect(chart.remove).toHaveBeenCalled();
  });
});