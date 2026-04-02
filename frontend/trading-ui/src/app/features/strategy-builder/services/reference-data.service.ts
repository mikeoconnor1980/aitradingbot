import { Injectable, inject } from "@angular/core";
import { Observable, shareReplay } from "rxjs";
import { ApiRestClient } from "../../../core/services/api-rest-client.service";
import { ReferenceDataResponse } from "../models/strategy.model";

@Injectable({ providedIn: "root" })
export class ReferenceDataService {
  private readonly _apiClient = inject(ApiRestClient);
  private readonly _referenceData$: Observable<ReferenceDataResponse>;

  public constructor() {
    this._referenceData$ = this._apiClient
      .get<ReferenceDataResponse>("reference-data/markets")
      .pipe(shareReplay({ bufferSize: 1, refCount: true }));
  }

  public getReferenceData(): Observable<ReferenceDataResponse> {
    return this._referenceData$;
  }
}