import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { ApiRestClient } from "./api-rest-client.service";

export interface WebhookConfigDto {
  id: string;
  label: string;
  token: string;
  defaultAsset: string | null;
  targetAgentId: string | null;
  isEnabled: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
  lastTriggeredAtUtc: string | null;
}

export interface CreateWebhookRequest {
  label: string;
  defaultAsset: string | null;
  targetAgentId: string | null;
}

export interface UpdateWebhookRequest {
  label: string;
  defaultAsset: string | null;
  targetAgentId: string | null;
  isEnabled: boolean;
}

@Injectable({ providedIn: "root" })
export class WebhookApiService {
  private readonly _apiClient = inject(ApiRestClient);

  public getWebhooks(): Observable<WebhookConfigDto[]> {
    return this._apiClient.get<WebhookConfigDto[]>("webhooks");
  }

  public createWebhook(request: CreateWebhookRequest): Observable<WebhookConfigDto> {
    return this._apiClient.post<WebhookConfigDto>("webhooks", request);
  }

  public updateWebhook(id: string, request: UpdateWebhookRequest): Observable<WebhookConfigDto> {
    return this._apiClient.patch<WebhookConfigDto>(`webhooks/${id}`, request);
  }

  public regenerateToken(id: string): Observable<WebhookConfigDto> {
    return this._apiClient.post<WebhookConfigDto>(`webhooks/${id}/regenerate`, {});
  }

  public deleteWebhook(id: string): Observable<void> {
    return this._apiClient.delete<void>(`webhooks/${id}`);
  }
}