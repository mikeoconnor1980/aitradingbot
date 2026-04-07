import { Injectable, inject } from "@angular/core";
import { HttpContext } from "@angular/common/http";
import { Observable } from "rxjs";
import { ApiRestClient } from "./api-rest-client.service";
import { LlmContextDto } from "../models/llm-context.model";

@Injectable({ providedIn: "root" })
export class MarketContextService {
  private readonly _api = inject(ApiRestClient);

  public getCurrentContext(symbol: string, context?: HttpContext): Observable<LlmContextDto> {
    return this._api.get<LlmContextDto>(
      `market-context/current?symbol=${encodeURIComponent(symbol)}`,
      context
    );
  }

  public getContextHistory(symbol: string, fromUtc: number, toUtc: number, context?: HttpContext): Observable<LlmContextDto[]> {
    return this._api.get<LlmContextDto[]>(
      `market-context/history?symbol=${encodeURIComponent(symbol)}&fromUtc=${fromUtc}&toUtc=${toUtc}`,
      context
    );
  }
}
