import { HttpClient, HttpContext } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { BehaviorSubject, Observable, tap, catchError, of, timer, switchMap } from "rxjs";
import { environment } from "../../../environments/environment";
import { SKIP_ERROR_NOTIFICATION } from "../interceptors/http-context-tokens";

export interface SubscriptionStatusResponse {
  tier: string | null;
  status: string | null;
  expiresAtUtc: number | null;
  isActive: boolean;
  features: string[];
  allowedAssets: string[];
  maxLeverage: number | null;
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

  private static readonly _noSubscription: SubscriptionStatusResponse = {
    tier: null,
    status: null,
    expiresAtUtc: null,
    isActive: false,
    features: [],
    allowedAssets: [],
    maxLeverage: null
  };

  public loadStatus(): void {
    this._fetchStatus().subscribe((status) =>  {
      if (status.isActive) {
        this._status$.next(status);
      } else {
        // Retry once after 1s — handles race where token isn't ready yet
        timer(1000).pipe(
          switchMap(() => this._fetchStatus())
        ).subscribe((retryStatus) => this._status$.next(retryStatus));
      }
    });
  }

  private _fetchStatus(): Observable<SubscriptionStatusResponse> {
    return this._http
      .get<SubscriptionStatusResponse>(`${this._url}/status`, {
        context: new HttpContext().set(SKIP_ERROR_NOTIFICATION, true)
      })
      .pipe(catchError(() => of(SubscriptionService._noSubscription)));
  }

  public subscribe(tier: "beginner" | "pro"): Observable<{ id: string }> {
    return this._http
      .post<{ id: string }>(`${this._url}/subscribe`, { tier })
      .pipe(tap(() => this.loadStatus()));
  }

  public subscribeFreeTier(): Observable<{ id: string }> {
    return this.subscribe("beginner");
  }

  public cancelSubscription(): Observable<void> {
    return this._http
      .post<void>(`${this._url}/cancel`, {})
      .pipe(tap(() => this.loadStatus()));
  }

  public hasFeature(feature: string): boolean {
    const normalizedFeature = feature.trim().toLowerCase();
    return this.currentStatus?.features.some((item) => item.toLowerCase() === normalizedFeature) ?? false;
  }

  public clearCache(): void {
    this._status$.next(null);
  }

  public setStatus(status: SubscriptionStatusResponse): void {
    this._status$.next(status);
  }
}
