import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { environment } from "../../../environments/environment";

export interface ExchangeCredential {
  id: string;
  exchange: string;
  apiKey: string;
  maskedSecret: string;
  label: string;
  createdAtUtc: number;
  isActive: boolean;
}

export interface ExchangeCredentialConnectionTestResult {
  exchange: string;
  success: boolean;
}

@Injectable({ providedIn: "root" })
export class ExchangeCredentialsService {
  private readonly _http = inject(HttpClient);
  private readonly _url = `${environment.apiBaseUrl}/credentials`;

  public list(): Observable<ExchangeCredential[]> {
    return this._http.get<ExchangeCredential[]>(this._url);
  }

  public save(exchange: string, apiKey: string, apiSecret: string, label: string): Observable<ExchangeCredential> {
    return this._http.post<ExchangeCredential>(this._url, { exchange, apiKey, apiSecret, label });
  }

  public remove(id: string): Observable<void> {
    return this._http.delete<void>(`${this._url}/${id}`);
  }

  public test(exchange: string): Observable<ExchangeCredentialConnectionTestResult> {
    return this._http.post<ExchangeCredentialConnectionTestResult>(`${this._url}/${encodeURIComponent(exchange)}/test`, {});
  }
}