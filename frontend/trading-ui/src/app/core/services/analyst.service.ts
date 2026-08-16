import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { AnalystQuestionRequest, AnalystRequestContext, TradingAnalystResult } from "../models/analyst.model";
import { ApiRestClient } from "./api-rest-client.service";

@Injectable({ providedIn: "root" })
export class AnalystService {
  private readonly _apiClient = inject(ApiRestClient);

  public analyse(question: string, context?: AnalystRequestContext): Observable<TradingAnalystResult> {
    const request: AnalystQuestionRequest = { question, context };
    return this._apiClient.post<TradingAnalystResult>("analyst/analyse", request);
  }
}