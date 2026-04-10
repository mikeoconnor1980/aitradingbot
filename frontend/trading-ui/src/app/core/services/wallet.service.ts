import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { BehaviorSubject, Observable, catchError, of, tap } from "rxjs";

export interface WalletStatus {
  isConfigured: boolean;
  walletAddress: string | null;
}

export interface WalletAddressResponse {
  walletAddress: string;
  exchange: string;
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
      .get<WalletAddressResponse>("/api/wallet-address")
      .pipe(
        catchError(() => of(null))
      )
      .subscribe((response) => {
        this._status$.next({
          isConfigured: response !== null,
          walletAddress: response?.walletAddress ?? null
        });
      });
  }

  public configure(walletAddress: string): Observable<WalletAddressResponse> {
    return this._http
      .post<WalletAddressResponse>("/api/wallet-address", { walletAddress })
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
    return this._http.delete<void>("/api/wallet-address").pipe(
      tap(() => {
        this._status$.next({ isConfigured: false, walletAddress: null });
      })
    );
  }
}
