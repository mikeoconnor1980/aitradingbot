import { TestBed } from "@angular/core/testing";
import { provideHttpClient } from "@angular/common/http";
import { HttpTestingController, provideHttpClientTesting } from "@angular/common/http/testing";
import {
  BacktestRequest,
  BacktestResult,
  BacktestSummary,
  CoverageReport,
  PagedResult
} from "../models/backtest.model";
import { environment } from "../../../environments/environment";
import { BacktestService } from "./backtest.service";

describe("BacktestService", () => {
  let service: BacktestService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(BacktestService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it("should POST to backtests when runBacktest is called", () => {
    const request: BacktestRequest = {
      symbol: "BTC",
      intervals: ["15m", "1h", "4h"],
      startDate: "2024-01-01T00:00:00Z",
      endDate: "2024-12-31T23:59:59Z",
      initialCapital: 10000,
      strategyConfig: {
        gridLevels: 10,
        gridSpacing: 0.5,
        takeProfitPercent: 1,
        breakdownThreshold: -3,
        makerFee: 0.0001,
        takerFee: 0.00035,
        slippage: 0,
        positionSize: 100,
        leverage: 3,
        stopLossPercent: 5
      }
    };
    const mockResult: BacktestResult = {
      id: "run-1",
      symbol: "BTC",
      intervals: ["15m", "1h", "4h"],
      startDate: "2024-01-01T00:00:00Z",
      endDate: "2024-12-31T23:59:59Z",
      strategyConfig: request.strategyConfig,
      initialCapital: 10000,
      candlesReplayed: 35040,
      elapsedMs: 1234,
      totalTrades: 100,
      winningTrades: 65,
      losingTrades: 35,
      winRate: 65,
      totalPnl: 1500.5,
      maxDrawdown: -500.25,
      averageTradePnl: 15,
      averageHoldTimeMinutes: 120,
      hedgesOpened: 5,
      totalFeesPaid: 120.5,
      trades: [],
      createdAt: "2026-03-28T12:00:00Z"
    };

    service.runBacktest(request).subscribe((result) => {
      expect(result.totalTrades).toBe(100);
      expect(result.id).toBe("run-1");
    });

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/backtests`);
    expect(req.request.method).toBe("POST");
    expect(req.request.body).toEqual(request);
    req.flush(mockResult);
  });

  it("should GET backtest by id", () => {
    const id = "35c4c2fd-7d83-4179-a8a3-98369ec19db2";
    const mockResult: BacktestResult = {
      id,
      symbol: "BTC",
      intervals: ["15m"],
      startDate: "2024-01-01T00:00:00Z",
      endDate: "2024-01-31T00:00:00Z",
      strategyConfig: {
        gridLevels: 10,
        gridSpacing: 0.5,
        takeProfitPercent: 1,
        breakdownThreshold: -3,
        makerFee: 0.0001,
        takerFee: 0.00035,
        slippage: 0,
        positionSize: 100,
        leverage: 3,
        stopLossPercent: 5
      },
      initialCapital: 10000,
      candlesReplayed: 100,
      elapsedMs: 200,
      totalTrades: 50,
      winningTrades: 30,
      losingTrades: 20,
      winRate: 60,
      totalPnl: 400,
      maxDrawdown: -100,
      averageTradePnl: 8,
      averageHoldTimeMinutes: 60,
      hedgesOpened: 2,
      totalFeesPaid: 12,
      trades: [],
      createdAt: "2026-03-28T12:00:00Z"
    };

    service.getBacktest(id).subscribe((result) => {
      expect(result.id).toBe(id);
      expect(result.totalTrades).toBe(50);
    });

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/backtests/${encodeURIComponent(id)}`);
    expect(req.request.method).toBe("GET");
    req.flush(mockResult);
  });

  it("should GET validate coverage with query params", () => {
    const mockReport: CoverageReport = {
      coverage: {
        "BTC/15m": {
          from: "2024-01-01T00:00:00Z",
          to: "2024-12-31T23:45:00Z",
          candleCount: 35040
        }
      }
    };

    service.validateCoverage("BTC", ["15m", "1h"], "2024-01-01T00:00:00Z", "2024-12-31T23:59:59Z").subscribe((result) => {
      expect(result.coverage["BTC/15m"].candleCount).toBe(35040);
    });

    const req = httpMock.expectOne(
      `${environment.apiBaseUrl}/backtests/validate?symbol=BTC&intervals=15m,1h`
    );
    expect(req.request.method).toBe("GET");
    req.flush(mockReport);
  });

  it("should GET paginated backtest list with default params", () => {
    const mockResult: PagedResult<BacktestSummary> = {
      items: [],
      page: 1,
      pageSize: 20,
      totalCount: 0,
      totalPages: 0
    };

    service.getBacktestList().subscribe((result) => {
      expect(result.page).toBe(1);
      expect(result.pageSize).toBe(20);
      expect(result.items.length).toBe(0);
    });

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/backtests?page=1&pageSize=20`);
    expect(req.request.method).toBe("GET");
    req.flush(mockResult);
  });
});