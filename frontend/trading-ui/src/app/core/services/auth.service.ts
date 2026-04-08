import { HttpClient, HttpContext } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Router } from "@angular/router";
import { BehaviorSubject, Observable, tap } from "rxjs";
import { environment } from "../../../environments/environment";
import { AuthResponse, AuthUser, LoginRequest, RegisterRequest } from "../models/auth.model";
import { SKIP_ERROR_NOTIFICATION } from "../interceptors/http-context-tokens";

const TOKEN_KEY = "auth_token";
const REFRESH_TOKEN_KEY = "auth_refresh_token";
const USER_KEY = "auth_user";

@Injectable({ providedIn: "root" })
export class AuthService {
  private readonly _http = inject(HttpClient);
  private readonly _router = inject(Router);
  private readonly _baseUrl = environment.apiBaseUrl;

  private readonly _user$ = new BehaviorSubject<AuthUser | null>(this.loadUser());
  private readonly _isAuthenticated$ = new BehaviorSubject<boolean>(this.hasToken());

  public readonly user$: Observable<AuthUser | null> = this._user$.asObservable();
  public readonly isAuthenticated$: Observable<boolean> = this._isAuthenticated$.asObservable();

  public get token(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  public get refreshToken(): string | null {
    return localStorage.getItem(REFRESH_TOKEN_KEY);
  }

  public get isAuthenticated(): boolean {
    return this.hasToken();
  }

  public get currentUser(): AuthUser | null {
    return this._user$.value;
  }

  public register(request: RegisterRequest): Observable<AuthResponse> {
    return this._http
      .post<AuthResponse>(`${this._baseUrl}/auth/register`, request)
      .pipe(tap((response) => this.storeAuth(response)));
  }

  public login(request: LoginRequest): Observable<AuthResponse> {
    return this._http
      .post<AuthResponse>(`${this._baseUrl}/auth/login`, request)
      .pipe(tap((response) => this.storeAuth(response)));
  }

  public refresh(): Observable<AuthResponse> {
    const ctx = new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);
    return this._http
      .post<AuthResponse>(
        `${this._baseUrl}/auth/refresh`,
        { refreshToken: this.refreshToken },
        { context: ctx }
      )
      .pipe(tap((response) => this.storeAuth(response)));
  }

  public logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    this._user$.next(null);
    this._isAuthenticated$.next(false);
    this._router.navigate(["/login"]);
  }

  private storeAuth(response: AuthResponse): void {
    localStorage.setItem(TOKEN_KEY, response.token);
    localStorage.setItem(REFRESH_TOKEN_KEY, response.refreshToken);
    localStorage.setItem(USER_KEY, JSON.stringify(response.user));
    this._user$.next(response.user);
    this._isAuthenticated$.next(true);
  }

  private hasToken(): boolean {
    return !!localStorage.getItem(TOKEN_KEY);
  }

  private loadUser(): AuthUser | null {
    const raw = localStorage.getItem(USER_KEY);
    if (!raw) return null;
    try {
      return JSON.parse(raw) as AuthUser;
    } catch {
      return null;
    }
  }
}
