import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { BehaviorSubject, Observable, tap, catchError, of } from "rxjs";
import { environment } from "../../../environments/environment";

export interface SubscriptionStatusResponse {
  tier: string | null;
  status: string | null;
  expiresAtUtc: number | null;
  isActive: boolean;
}

@Injectable({ providedIn: "root" })
export class SubscriptionService {
  private readonly _http = inject(HttpClient);
  private readonly _url = `${environment.apiBaseUrl}/subscriptions`;

  private readonly _status$ = new BehaviorSubject<SubscriptionStatusResponse | null>(null);
  public readonly status$: Observable<SubscriptionStatusResponse | null> = this._status$.asObservable();

  public get currentStatus(): SubscriptionStatusResponse | null {
    return this._status$.value;
  }

  public loadStatus(): void {
    this._http
      .get<SubscriptionStatusResponse>(`${this._url}/status`)
      .pipe(catchError(() => of(null)))
      .subscribe((status) => this._status$.next(status));
  }

  public subscribeFreeTier(): Observable<{ id: string }> {
    return this._http
      .post<{ id: string }>(`${this._url}/free`, {})
      .pipe(tap(() => this.loadStatus()));
  }

  public clearCache(): void {
    this._status$.next(null);
  }

  public setStatus(status: SubscriptionStatusResponse): void {
    this._status$.next(status);
  }
}
