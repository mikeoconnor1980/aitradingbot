import { HttpClient, HttpContext } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { environment } from "../../../environments/environment";
import { AccountSummary } from "../models/account-summary.model";
import { DrawdownState } from "../models/drawdown-state.model";
import { FillEvent } from "../models/fill-event.model";
import { HealthResponse } from "../models/health-response.model";
import { OpenOrder } from "../models/open-order.model";
import { PortfolioHeat } from "../models/portfolio-heat.model";
import { Position } from "../models/position.model";

@Injectable({ providedIn: "root" })
export class HyperliquidApiService {
  private readonly _http = inject(HttpClient);
  private readonly _baseUrl = environment.apiBaseUrl;

  public getHealth(): Observable<HealthResponse> {
    return this._http.get<HealthResponse>(`${this._baseUrl}/health`);
  }

  public getAccountSummary(context?: HttpContext): Observable<AccountSummary> {
    return this._http.get<AccountSummary>(`${this._baseUrl}/account`, context ? { context } : undefined);
  }

  public getPositions(context?: HttpContext): Observable<Position[]> {
    return this._http.get<Position[]>(`${this._baseUrl}/account/positions`, context ? { context } : undefined);
  }

  public getOpenOrders(context?: HttpContext): Observable<OpenOrder[]> {
    return this._http.get<OpenOrder[]>(`${this._baseUrl}/account/orders`, context ? { context } : undefined);
  }

  public getPortfolioHeat(context?: HttpContext): Observable<PortfolioHeat> {
    return this._http.get<PortfolioHeat>(`${this._baseUrl}/risk/portfolio-heat`, context ? { context } : undefined);
  }

  public getDrawdownState(context?: HttpContext): Observable<DrawdownState> {
    return this._http.get<DrawdownState>(`${this._baseUrl}/risk/drawdown-state`, context ? { context } : undefined);
  }

  public getRecentFills(asset?: string): Observable<FillEvent[]> {
    const queryString = asset ? `?asset=${encodeURIComponent(asset)}` : "";
    return this._http.get<FillEvent[]>(`${this._baseUrl}/account/fills${queryString}`);
  }
}