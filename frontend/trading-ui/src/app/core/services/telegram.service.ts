import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { ApiRestClient } from "./api-rest-client.service";

export interface LinkCodeResponse {
  code: string;
  expiresAtUtc: string;
  botUsername: string;
}

export interface TelegramStatusResponse {
  linked: boolean;
  chatId: number | null;
}

@Injectable({ providedIn: "root" })
export class TelegramService {
  private readonly _api = inject(ApiRestClient);

  public getStatus(): Observable<TelegramStatusResponse> {
    return this._api.get<TelegramStatusResponse>("notifications/telegram/status");
  }

  public generateLinkCode(): Observable<LinkCodeResponse> {
    return this._api.post<LinkCodeResponse>("notifications/telegram/link-code", {});
  }

  public unlink(): Observable<void> {
    return this._api.delete<void>("notifications/telegram/link");
  }

  public sendTest(): Observable<{ message: string }> {
    return this._api.post<{ message: string }>("notifications/telegram/test", {});
  }
}
