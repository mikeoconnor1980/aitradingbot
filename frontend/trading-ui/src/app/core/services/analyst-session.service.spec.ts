import { TestBed } from "@angular/core/testing";
import { Subject } from "rxjs";
import { AnalystChartContextService } from "./analyst-chart-context.service";
import { AnalystService } from "./analyst.service";
import { AnalystSessionService } from "./analyst-session.service";
import { TradingAnalystResult } from "../models/analyst.model";

describe("AnalystSessionService", () => {
  let service: AnalystSessionService;
  let response$: Subject<TradingAnalystResult>;
  let analystService: jasmine.SpyObj<AnalystService>;
  let chartContext: jasmine.SpyObj<AnalystChartContextService>;

  beforeEach(() => {
    response$ = new Subject<TradingAnalystResult>();
    analystService = jasmine.createSpyObj<AnalystService>("AnalystService", ["analyse"]);
    chartContext = jasmine.createSpyObj<AnalystChartContextService>("AnalystChartContextService", ["captureCurrent"]);
    analystService.analyse.and.returnValue(response$);
    TestBed.configureTestingModule({ providers: [
      { provide: AnalystService, useValue: analystService },
      { provide: AnalystChartContextService, useValue: chartContext }
    ] });
    service = TestBed.inject(AnalystSessionService);
  });

  it("refreshes a chart context before submitting a question", () => {
    const initialContext = {
      intent: "AnalyseChart" as const,
      chart: {
        symbol: "BTC-PERP",
        timeframe: "15m",
        visibleFromOpenTimeUtc: "2026-08-16T10:00:00.000Z",
        visibleToOpenTimeUtc: "2026-08-16T12:00:00.000Z",
        activeIndicators: [],
        visibleOverlays: [],
        capturedAtUtc: "2026-08-16T12:00:00.000Z"
      }
    };
    const currentChart = { ...initialContext.chart, symbol: "ETH-PERP", capturedAtUtc: "2026-08-16T12:05:00.000Z" };
    chartContext.captureCurrent.and.returnValue(currentChart);

    service.start(initialContext);
    service.submit("What is the range?");

    expect(analystService.analyse).toHaveBeenCalledWith("What is the range?", { ...initialContext, chart: currentChart });
    expect(service.context()).toEqual({ ...initialContext, chart: currentChart });
    expect(service.progress()).toBe("Analysing ETH-PERP...");
  });
});