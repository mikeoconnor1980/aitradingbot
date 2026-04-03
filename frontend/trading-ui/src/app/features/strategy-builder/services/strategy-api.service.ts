import { HttpContext } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { PagedResult } from "../../../core/models/paged-result.model";
import { ApiRestClient } from "../../../core/services/api-rest-client.service";
import {
  StrategyDiffDto,
  ServerValidationResult,
  StrategyConfig,
  StrategyRevisionDto,
  StrategyRevisionSummaryDto,
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

  public getVersions(
    strategyId: string,
    page = 1,
    pageSize = 20,
    context?: HttpContext
  ): Observable<PagedResult<StrategyRevisionSummaryDto>> {
    const encodedStrategyId = encodeURIComponent(strategyId);

    return this._apiClient.get<PagedResult<StrategyRevisionSummaryDto>>(
      `strategies/${encodedStrategyId}/versions?page=${page}&pageSize=${pageSize}`,
      context
    );
  }

  public getVersion(strategyId: string, revisionNumber: number, context?: HttpContext): Observable<StrategyRevisionDto> {
    const encodedStrategyId = encodeURIComponent(strategyId);

    return this._apiClient.get<StrategyRevisionDto>(
      `strategies/${encodedStrategyId}/versions/${revisionNumber}`,
      context
    );
  }

  public getDiff(strategyId: string, from: number, to: number, context?: HttpContext): Observable<StrategyDiffDto> {
    const encodedStrategyId = encodeURIComponent(strategyId);

    return this._apiClient.get<StrategyDiffDto>(
      `strategies/${encodedStrategyId}/diff?from=${from}&to=${to}`,
      context
    );
  }

  public restoreVersion(strategyId: string, revisionNumber: number, context?: HttpContext): Observable<void> {
    const encodedStrategyId = encodeURIComponent(strategyId);

    return this._apiClient.post<void>(
      `strategies/${encodedStrategyId}/versions/${revisionNumber}/restore`,
      null,
      context
    );
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