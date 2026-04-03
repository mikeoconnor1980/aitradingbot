import { HttpContext } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { ApiRestClient } from "../../../core/services/api-rest-client.service";
import {
  ServerValidationResult,
  StrategyConfig,
  StrategyDto,
  StrategySummaryDto,
} from "../models/strategy.model";

@Injectable({ providedIn: "root" })
export class StrategyApiService {
  private readonly _apiClient = inject(ApiRestClient);

  public getStrategies(context?: HttpContext): Observable<StrategySummaryDto[]> {
    return this._apiClient.get<StrategySummaryDto[]>("strategies", context);
  }

  public getStrategy(id: string, context?: HttpContext): Observable<StrategyDto> {
    return this._apiClient.get<StrategyDto>(`strategies/${encodeURIComponent(id)}`, context);
  }

  public createStrategy(config: StrategyConfig, context?: HttpContext): Observable<{ id: string }> {
    return this._apiClient.post<{ id: string }>("strategies", config, context);
  }

  public updateStrategy(id: string, config: StrategyConfig, context?: HttpContext): Observable<void> {
    return this._apiClient.put<void>(`strategies/${encodeURIComponent(id)}`, config, context);
  }

  public deleteStrategy(id: string, context?: HttpContext): Observable<void> {
    return this._apiClient.delete<void>(`strategies/${encodeURIComponent(id)}`, context);
  }

  public validateStrategy(config: StrategyConfig, context?: HttpContext): Observable<ServerValidationResult> {
    return this._apiClient.post<ServerValidationResult>("strategies/validate", config, context);
  }
}