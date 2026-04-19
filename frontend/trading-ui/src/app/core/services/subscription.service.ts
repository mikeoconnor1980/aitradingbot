import { HttpClient, HttpContext, HttpErrorResponse } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { BehaviorSubject, Observable, tap, catchError, of, timer, switchMap, map, throwError } from "rxjs";
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
      .pipe(
        map((status) => SubscriptionService._normalizeStatus(status)),
        catchError(() => of(SubscriptionService._noSubscription))
      );
  }

  public subscribe(tier: "beginner" | "pro"): Observable<{ id: string }> {
    const request = this._http
      .post<{ id: string }>(`${this._url}/subscribe`, { tier })
      .pipe(tap(() => this.loadStatus()));

    if (tier === "beginner") {
      return request.pipe(
        catchError((error: HttpErrorResponse) => {
          if (error.status === 404) {
            return this.subscribeFreeTier();
          }

          return throwError(() => error);
        })
      );
    }

    return request;
  }

  public subscribeFreeTier(): Observable<{ id: string }> {
    return this._http
      .post<{ id: string }>(`${this._url}/free`, {})
      .pipe(tap(() => this.loadStatus()));
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
    this._status$.next(SubscriptionService._normalizeStatus(status));
  }

  private static _normalizeStatus(status: SubscriptionStatusResponse | null | undefined): SubscriptionStatusResponse {
    if (!status) {
      return SubscriptionService._noSubscription;
    }

    return {
      tier: SubscriptionService._normalizeTier(status.tier),
      status: SubscriptionService._normalizeLifecycle(status.status),
      expiresAtUtc: status.expiresAtUtc,
      isActive: status.isActive,
      features: Array.isArray(status.features) ? status.features : [],
      allowedAssets: Array.isArray(status.allowedAssets) ? status.allowedAssets : [],
      maxLeverage: status.maxLeverage
    };
  }

  private static _normalizeTier(value: unknown): string | null {
    if (value === null || value === undefined) {
      return null;
    }

    const normalized = String(value).trim().toLowerCase();

    if (normalized === "0" || normalized === "free") {
      return "beginner";
    }

    if (normalized === "1" || normalized === "beginner" || normalized === "beginner_trial") {
      return "beginner";
    }

    if (normalized === "2" || normalized === "pro" || normalized === "pro_trial") {
      return "pro";
    }

    return normalized;
  }

  private static _normalizeLifecycle(value: unknown): string | null {
    if (value === null || value === undefined) {
      return null;
    }

    const normalized = String(value).trim().toLowerCase();

    if (normalized === "0" || normalized === "active") {
      return "active";
    }

    if (normalized === "1" || normalized === "expired") {
      return "expired";
    }

    if (normalized === "2" || normalized === "cancelled" || normalized === "canceled") {
      return "cancelled";
    }

    return normalized;
  }
}
