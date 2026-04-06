import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { BehaviorSubject, Observable, catchError, of, tap } from "rxjs";

export interface WalletStatus {
  isConfigured: boolean;
  walletAddress: string | null;
}

export interface WalletConfiguredResponse {
  walletAddress: string;
}

@Injectable({ providedIn: "root" })
export class WalletService {
  private readonly _http = inject(HttpClient);

  private readonly _status$ = new BehaviorSubject<WalletStatus>({
    isConfigured: false,
    walletAddress: null
  });

  public readonly status$: Observable<WalletStatus> = this._status$.asObservable();

  public refreshStatus(): void {
    this._http
      .get<WalletStatus>("/api/wallet/status")
      .pipe(
        catchError(() =>
          of<WalletStatus>({ isConfigured: false, walletAddress: null })
        )
      )
      .subscribe((status) => this._status$.next(status));
  }

  public configure(privateKey: string): Observable<WalletConfiguredResponse> {
    return this._http
      .post<WalletConfiguredResponse>("/api/wallet/configure", { privateKey })
      .pipe(
        tap((response) => {
          this._status$.next({
            isConfigured: true,
            walletAddress: response.walletAddress
          });
        })
      );
  }

  public disconnect(): Observable<void> {
    return this._http.delete<void>("/api/wallet/configure").pipe(
      tap(() => {
        this._status$.next({ isConfigured: false, walletAddress: null });
      })
    );
  }
}
