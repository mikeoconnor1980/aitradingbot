import { HttpClient, HttpContext } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { environment } from "../../../environments/environment";

@Injectable({ providedIn: "root" })
export class ApiRestClient {
  private readonly _http = inject(HttpClient);
  private readonly _baseUrl = environment.apiBaseUrl;

  public get<T>(path: string, context?: HttpContext): Observable<T> {
    return this._http.get<T>(this.buildUrl(path), context ? { context } : undefined);
  }

  public post<T>(path: string, body: unknown, context?: HttpContext): Observable<T> {
    return this._http.post<T>(this.buildUrl(path), body, context ? { context } : undefined);
  }

  public put<T>(path: string, body: unknown, context?: HttpContext): Observable<T> {
    return this._http.put<T>(this.buildUrl(path), body, context ? { context } : undefined);
  }

  public patch<T>(path: string, body: unknown, context?: HttpContext): Observable<T> {
    return this._http.patch<T>(this.buildUrl(path), body, context ? { context } : undefined);
  }

  public delete<T>(path: string, context?: HttpContext): Observable<T> {
    return this._http.delete<T>(this.buildUrl(path), context ? { context } : undefined);
  }

  private buildUrl(path: string): string {
    const normalizedBaseUrl = this._baseUrl.replace(/\/+$/, "");
    const normalizedPath = path.replace(/^\/+/, "");
    return `${normalizedBaseUrl}/${normalizedPath}`;
  }
}