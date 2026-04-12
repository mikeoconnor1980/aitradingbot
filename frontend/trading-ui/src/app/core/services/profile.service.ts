import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { BehaviorSubject, Observable, tap, catchError, of } from "rxjs";
import { environment } from "../../../environments/environment";

export interface UserProfile {
  id: string;
  email: string;
  displayName: string;
  preferredNetwork: string;
  llmModels: LlmModelsInfo;
  hasActiveSubscription: boolean;
  subscriptionTier: string | null;
  subscriptionStatus: string | null;
  subscriptionExpiresAtUtc: number | null;
}

export interface LlmModelsInfo {
  strategy: string;
  review: string;
}

@Injectable({ providedIn: "root" })
export class ProfileService {
  private readonly _http = inject(HttpClient);
  private readonly _url = `${environment.apiBaseUrl}/profile`;

  private readonly _profile$ = new BehaviorSubject<UserProfile | null>(null);
  public readonly profile$: Observable<UserProfile | null> = this._profile$.asObservable();

  public load(): void {
    this._http
      .get<UserProfile>(this._url)
      .pipe(catchError(() => of(null)))
      .subscribe((profile) => this._profile$.next(profile));
  }

  public updateNetwork(network: string): Observable<UserProfile> {
    return this._http
      .put<UserProfile>(`${this._url}/network`, { network })
      .pipe(tap((profile) => this._profile$.next(profile)));
  }
}
