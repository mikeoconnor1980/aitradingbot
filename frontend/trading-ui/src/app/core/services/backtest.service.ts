import { HttpContext } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { BacktestDebugResponse } from "../models/backtest-debug.model";
import {
  BacktestRequest,
  BacktestResult,
  BacktestSummary,
  CoverageReport,
  PagedResult
} from "../models/backtest.model";
import { ApiRestClient } from "./api-rest-client.service";

@Injectable({ providedIn: "root" })
export class BacktestService {
  private readonly _apiClient = inject(ApiRestClient);

  public runBacktest(request: BacktestRequest, context?: HttpContext): Observable<BacktestResult> {
    return this._apiClient.post<BacktestResult>("backtests", request, context);
  }

  public getBacktest(id: string, context?: HttpContext): Observable<BacktestResult> {
    return this._apiClient.get<BacktestResult>(`backtests/${encodeURIComponent(id)}`, context);
  }

  public getDebugData(id: string, cycleId: string, context?: HttpContext): Observable<BacktestDebugResponse | null> {
    const encodedId = encodeURIComponent(id);
    const encodedCycleId = encodeURIComponent(cycleId);

    return this._apiClient.get<BacktestDebugResponse | null>(
      `backtests/${encodedId}/debug?cycleId=${encodedCycleId}`,
      context
    );
  }

  public validateCoverage(
    symbol: string,
    intervals: string[],
    context?: HttpContext
  ): Observable<CoverageReport> {
    const encodedSymbol = encodeURIComponent(symbol);
    const encodedIntervals = intervals.map((interval) => encodeURIComponent(interval)).join(",");

    return this._apiClient.get<CoverageReport>(
      `backtests/validate?symbol=${encodedSymbol}&intervals=${encodedIntervals}`,
      context
    );
  }

  public cancelBacktest(id: string, context?: HttpContext): Observable<void> {
    const encodedId = encodeURIComponent(id);
    return this._apiClient.post<void>(`backtests/${encodedId}/cancel`, null, context);
  }

  public getBacktestList(page = 1, pageSize = 20, context?: HttpContext): Observable<PagedResult<BacktestSummary>> {
    return this._apiClient.get<PagedResult<BacktestSummary>>(
      `backtests?page=${page}&pageSize=${pageSize}`,
      context
    );
  }

  public getBacktestsByStrategy(
    strategyId: string,
    page = 1,
    pageSize = 20,
    context?: HttpContext
  ): Observable<PagedResult<BacktestSummary>> {
    const encodedId = encodeURIComponent(strategyId);

    return this._apiClient.get<PagedResult<BacktestSummary>>(
      `strategies/${encodedId}/backtests?page=${page}&pageSize=${pageSize}`,
      context
    );
  }
}