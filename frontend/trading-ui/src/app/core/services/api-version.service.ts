import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { ApiRestClient } from "./api-rest-client.service";

export interface ApiVersionInfo {
  version: string;
  informationalVersion: string;
  commitSha: string;
  buildTimeUtc: string;
  runId: string;
  environmentName: string;
  matchesExpectedVersion: boolean | null;
  matchesExpectedCommit: boolean | null;
  isExpectedBuild: boolean | null;
}

@Injectable({ providedIn: "root" })
export class ApiVersionService {
  private readonly _apiClient = inject(ApiRestClient);

  public getVersion(): Observable<ApiVersionInfo> {
    return this._apiClient.get<ApiVersionInfo>("version");
  }
}