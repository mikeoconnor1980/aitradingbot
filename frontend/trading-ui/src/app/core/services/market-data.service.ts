import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { Candle } from "../models/candle.model";
import { MarketInfo } from "../models/market-info.model";
import { ApiRestClient } from "./api-rest-client.service";

@Injectable({ providedIn: "root" })
export class MarketDataService {
  private readonly _apiClient = inject(ApiRestClient);

  public getMarketInfo(asset: string): Observable<MarketInfo> {
    return this._apiClient.get<MarketInfo>(`market/info?asset=${encodeURIComponent(asset)}`);
  }

  public getCandles(asset: string, timeframe: string, endTime?: number): Observable<Candle[]> {
    let url = `market/candles?asset=${encodeURIComponent(asset)}&timeframe=${encodeURIComponent(timeframe)}`;
    if (endTime != null) {
      url += `&endTime=${endTime}`;
    }
    return this._apiClient.get<Candle[]>(url);
  }

  public getHistoricalCandles(
    asset: string,
    timeframe: string,
    endTime?: number,
    limit = 500
  ): Observable<Candle[]> {
    let url = `market/candles/history?asset=${encodeURIComponent(asset)}&timeframe=${encodeURIComponent(timeframe)}&limit=${limit}`;
    if (endTime != null) {
      url += `&endTime=${endTime}`;
    }
    return this._apiClient.get<Candle[]>(url);
  }
}