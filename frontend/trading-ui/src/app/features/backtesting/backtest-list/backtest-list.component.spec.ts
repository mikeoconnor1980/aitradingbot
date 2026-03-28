import { ComponentFixture, TestBed } from "@angular/core/testing";
import { NoopAnimationsModule } from "@angular/platform-browser/animations";
import { of } from "rxjs";
import { BacktestSummary, PagedResult } from "../../../core/models/backtest.model";
import { BacktestService } from "../../../core/services/backtest.service";
import { BacktestListComponent } from "./backtest-list.component";

describe("BacktestListComponent", () => {
  let component: BacktestListComponent;
  let fixture: ComponentFixture<BacktestListComponent>;
  let backtestService: jasmine.SpyObj<BacktestService>;

  const mockPage: PagedResult<BacktestSummary> = {
    items: [{
      id: "run-1",
      symbol: "BTC",
      intervals: ["15m", "1h", "4h"],
      startDate: "2024-01-01T00:00:00Z",
      endDate: "2024-01-31T00:00:00Z",
      totalTrades: 42,
      winRate: 61.9,
      totalPnl: 1245.67,
      maxDrawdown: -345.12,
      createdAt: "2026-03-28T12:00:00Z"
    }],
    page: 1,
    pageSize: 20,
    totalCount: 1,
    totalPages: 1
  };

  beforeEach(async () => {
    backtestService = jasmine.createSpyObj<BacktestService>("BacktestService", ["getBacktestList"]);
    backtestService.getBacktestList.and.returnValue(of(mockPage));

    await TestBed.configureTestingModule({
      imports: [BacktestListComponent, NoopAnimationsModule],
      providers: [{ provide: BacktestService, useValue: backtestService }]
    }).compileComponents();

    fixture = TestBed.createComponent(BacktestListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it("loads results on init", () => {
    expect(backtestService.getBacktestList).toHaveBeenCalled();
    expect(component.results.length).toBe(1);
    expect(fixture.nativeElement.textContent).toContain("BTC");
  });

  it("limits selection to two runs", () => {
    component.toggleSelection("run-1");
    component.toggleSelection("run-2");
    component.toggleSelection("run-3");

    expect(component.selectedIds.size).toBe(2);
    expect(component.selectedIds.has("run-3")).toBeFalse();
  });

  it("emits compareSelected when compare is triggered", () => {
    spyOn(component.compareSelected, "emit");
    component.toggleSelection("run-1");
    component.toggleSelection("run-2");

    component.onCompare();

    expect(component.compareSelected.emit).toHaveBeenCalledWith(["run-1", "run-2"]);
  });

  it("renders the empty state when no results exist", () => {
    backtestService.getBacktestList.and.returnValue(of({
      items: [],
      page: 1,
      pageSize: 20,
      totalCount: 0,
      totalPages: 0
    }));

    component.loadPage();
    fixture.detectChanges();

    const emptyState = fixture.nativeElement.querySelector(".backtest-list__empty");

    expect(emptyState).not.toBeNull();
    expect(emptyState.textContent).toContain("No backtests run yet");
  });

  it("emits rerun and view events", () => {
    const event = new Event("click");
    spyOn(event, "stopPropagation");
    spyOn(component.rerunConfig, "emit");
    spyOn(component.viewResult, "emit");

    component.onRerun("run-1", event);
    component.onViewResult("run-1");

    expect(event.stopPropagation).toHaveBeenCalled();
    expect(component.rerunConfig.emit).toHaveBeenCalledWith("run-1");
    expect(component.viewResult.emit).toHaveBeenCalledWith("run-1");
  });
});