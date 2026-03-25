import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { AccountSummary } from "../models/account-summary.model";
import { HealthResponse } from "../models/health-response.model";
import { OpenOrder } from "../models/open-order.model";
import { Position } from "../models/position.model";

@Injectable({ providedIn: "root" })
export class HyperliquidApiService {
  private readonly _http = inject(HttpClient);
  private readonly _baseUrl = "";

  public getHealth(): Observable<HealthResponse> {
    return this._http.get<HealthResponse>(`${this._baseUrl}/api/health`);
  }

  public getAccountSummary(): Observable<AccountSummary> {
    return this._http.get<AccountSummary>(`${this._baseUrl}/api/account`);
  }

  public getPositions(): Observable<Position[]> {
    return this._http.get<Position[]>(`${this._baseUrl}/api/account/positions`);
  }

  public getOpenOrders(): Observable<OpenOrder[]> {
    return this._http.get<OpenOrder[]>(`${this._baseUrl}/api/account/orders`);
  }
}