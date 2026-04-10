import { DestroyRef, Injectable, inject } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { BehaviorSubject, Observable, Subject, merge, of, timer } from "rxjs";
import { catchError, switchMap } from "rxjs/operators";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { HealthResponse } from "../models/health-response.model";
import { environment } from "../../../environments/environment";

@Injectable({ providedIn: "root" })
export class HealthService {
  private readonly _http = inject(HttpClient);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _refresh$ = new Subject<void>();
  private readonly _healthUrl = `${environment.apiBaseUrl}/health`;

  private readonly _health$ = new BehaviorSubject<HealthResponse | null>(null);

  public readonly health$: Observable<HealthResponse | null> = this._health$.asObservable();

  public constructor() {
    merge(timer(0, 10_000), this._refresh$)
      .pipe(
        switchMap(() =>
          this._http.get<HealthResponse>(this._healthUrl).pipe(
            catchError(() =>
              of<HealthResponse>({
                status: "disconnected",
                walletAddress: "",
                network: "",
                timestamp: "",
                error: "Failed to reach backend API"
              })
            )
          )
        ),
        takeUntilDestroyed(this._destroyRef)
      )
      .subscribe({
        next: (response) => this._health$.next(response)
      });
  }

  public refresh(): void {
    this._refresh$.next();
  }
}
