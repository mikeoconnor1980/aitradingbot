import { HttpClient, HttpContext } from "@angular/common/http";
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

  public getAccountSummary(context?: HttpContext): Observable<AccountSummary> {
    return this._http.get<AccountSummary>(`${this._baseUrl}/api/account`, context ? { context } : undefined);
  }

  public getPositions(context?: HttpContext): Observable<Position[]> {
    return this._http.get<Position[]>(`${this._baseUrl}/api/account/positions`, context ? { context } : undefined);
  }

  public getOpenOrders(context?: HttpContext): Observable<OpenOrder[]> {
    return this._http.get<OpenOrder[]>(`${this._baseUrl}/api/account/orders`, context ? { context } : undefined);
  }
}