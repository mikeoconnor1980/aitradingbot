import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { ApiRestClient } from "./api-rest-client.service";
import { GridCycle, LiveFill, LiveOrder } from "../models/live-trading.model";

@Injectable({ providedIn: "root" })
export class LiveTradingService {
  private readonly _api = inject(ApiRestClient);

  public getFills(symbol: string, since?: string, limit = 50): Observable<LiveFill[]> {
    let path = `api/live-trading/fills?symbol=${encodeURIComponent(symbol)}&limit=${limit}`;
    if (since) {
      path += `&since=${encodeURIComponent(since)}`;
    }
    return this._api.get<LiveFill[]>(path);
  }

  public getGridCycles(symbol: string, limit = 20): Observable<GridCycle[]> {
    return this._api.get<GridCycle[]>(
      `api/live-trading/grid-cycles?symbol=${encodeURIComponent(symbol)}&limit=${limit}`
    );
  }

  public getOrdersForCycle(gridCycleId: string): Observable<LiveOrder[]> {
    return this._api.get<LiveOrder[]>(
      `api/live-trading/grid-cycles/${encodeURIComponent(gridCycleId)}/orders`
    );
  }
}
