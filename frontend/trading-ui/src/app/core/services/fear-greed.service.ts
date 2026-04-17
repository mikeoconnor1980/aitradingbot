import { inject, Injectable } from "@angular/core";
import { Observable } from "rxjs";
import { ApiRestClient } from "./api-rest-client.service";
import {
  FearGreedBackfillResultDto,
  FearGreedReadingDto,
  FearGreedStatusDto,
} from "../models/fear-greed.models";

@Injectable({ providedIn: "root" })
export class FearGreedService {
  private readonly _apiClient = inject(ApiRestClient);

  public getStatus(): Observable<FearGreedStatusDto> {
    return this._apiClient.get<FearGreedStatusDto>("fear-greed/status");
  }

  public getHistory(
    from?: number,
    to?: number
  ): Observable<FearGreedReadingDto[]> {
    const params: string[] = [];
    if (from != null) params.push(`from=${from}`);
    if (to != null) params.push(`to=${to}`);
    const query = params.length > 0 ? `?${params.join("&")}` : "";
    return this._apiClient.get<FearGreedReadingDto[]>(
      `fear-greed/history${query}`
    );
  }

  public backfill(): Observable<FearGreedBackfillResultDto> {
    return this._apiClient.post<FearGreedBackfillResultDto>(
      "fear-greed/backfill",
      {}
    );
  }
}
