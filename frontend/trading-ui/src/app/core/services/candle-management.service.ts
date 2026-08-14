import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import {
  AllCandleCoverageResponse,
  IngestCandlesRequest,
  IngestionResult
} from "../models/candle-management.model";
import { ApiRestClient } from "./api-rest-client.service";

@Injectable({ providedIn: "root" })
export class CandleManagementService {
  private readonly _apiClient = inject(ApiRestClient);

  public getCoverage(symbols: string[], intervals: string[]): Observable<AllCandleCoverageResponse> {
    const params = new URLSearchParams();
    symbols.forEach((symbol) => params.append("symbols", symbol));
    intervals.forEach((interval) => params.append("intervals", interval));

    return this._apiClient.get<AllCandleCoverageResponse>(`candles/coverage?${params.toString()}`);
  }

  public ingestBinanceCandles(request: IngestCandlesRequest): Observable<IngestionResult> {
    return this._apiClient.post<IngestionResult>("candles/ingest/binance", request);
  }
}
