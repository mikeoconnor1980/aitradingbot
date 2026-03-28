import { HttpContext } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
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

  public validateCoverage(
    symbol: string,
    intervals: string[],
    _startDate?: string,
    _endDate?: string,
    context?: HttpContext
  ): Observable<CoverageReport> {
    void _startDate;
    void _endDate;

    const encodedSymbol = encodeURIComponent(symbol);
    const encodedIntervals = intervals.map((interval) => encodeURIComponent(interval)).join(",");

    return this._apiClient.get<CoverageReport>(
      `backtests/validate?symbol=${encodedSymbol}&intervals=${encodedIntervals}`,
      context
    );
  }

  public getBacktestList(page = 1, pageSize = 20, context?: HttpContext): Observable<PagedResult<BacktestSummary>> {
    return this._apiClient.get<PagedResult<BacktestSummary>>(
      `backtests?page=${page}&pageSize=${pageSize}`,
      context
    );
  }
}