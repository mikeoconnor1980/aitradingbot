import { HttpContext } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { OptimizationListResult, OptimizationRun, RunOptimizationRequest } from "../models/optimizer.model";
import { ApiRestClient } from "./api-rest-client.service";

@Injectable({ providedIn: "root" })
export class OptimizerService {
  private readonly _apiClient = inject(ApiRestClient);

  public runOptimization(request: RunOptimizationRequest, context?: HttpContext): Observable<OptimizationRun> {
    return this._apiClient.post<OptimizationRun>("optimizations", request, context);
  }

  public getOptimization(id: string, context?: HttpContext): Observable<OptimizationRun> {
    return this._apiClient.get<OptimizationRun>(`optimizations/${encodeURIComponent(id)}`, context);
  }

  public getOptimizationList(page = 1, pageSize = 10, context?: HttpContext): Observable<OptimizationListResult> {
    return this._apiClient.get<OptimizationListResult>(`optimizations?page=${page}&pageSize=${pageSize}`, context);
  }
}