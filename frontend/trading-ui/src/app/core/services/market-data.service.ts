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

  public getCandles(asset: string, timeframe: string): Observable<Candle[]> {
    return this._apiClient.get<Candle[]>(
      `market/candles?asset=${encodeURIComponent(asset)}&timeframe=${encodeURIComponent(timeframe)}`
    );
  }
}