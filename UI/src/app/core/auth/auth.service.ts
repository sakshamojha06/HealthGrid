import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { AppConfig } from '../config/app-config';
import { AuthTokens, CurrentUser, LoginRequest, Role } from '../models/models';

const ACCESS_KEY = 'hg.access';
const REFRESH_KEY = 'hg.refresh';

/**
 * Owns authentication state for the SPA.
 *
 * Access token lives in memory + sessionStorage (survives refresh, not tab close).
 * The API is authoritative for authorization — the UI only uses claims to hide
 * controls the user cannot use anyway.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly _user = signal<CurrentUser | null>(null);
  private readonly _accessToken = signal<string | null>(this.read(ACCESS_KEY));

  readonly user = this._user.asReadonly();
  readonly isAuthenticated = computed(() => !!this._accessToken());
  readonly role = computed<Role | null>(() => this._user()?.role ?? null);

  get accessToken(): string | null {
    return this._accessToken();
  }

  get refreshToken(): string | null {
    return this.read(REFRESH_KEY);
  }

  login(payload: LoginRequest): Observable<AuthTokens> {
    return this.http
      .post<AuthTokens>(`${AppConfig.apiBaseUrl}/auth/login`, payload)
      .pipe(tap((tokens) => this.storeTokens(tokens)));
  }

  refresh(): Observable<AuthTokens> {
    return this.http
      .post<AuthTokens>(`${AppConfig.apiBaseUrl}/auth/refresh`, {
        refreshToken: this.refreshToken,
      })
      .pipe(tap((tokens) => this.storeTokens(tokens)));
  }

  loadCurrentUser(): Observable<CurrentUser> {
    return this.http
      .get<CurrentUser>(`${AppConfig.apiBaseUrl}/auth/me`)
      .pipe(tap((u) => this._user.set(u)));
  }

  logout(navigate = true): void {
    const token = this.refreshToken;
    if (token) {
      this.http
        .post(`${AppConfig.apiBaseUrl}/auth/logout`, { refreshToken: token })
        .subscribe({ error: () => void 0 });
    }
    this.clear();
    if (navigate) {
      this.router.navigate(['/login']);
    }
  }

  hasAnyRole(...roles: Role[]): boolean {
    const current = this.role();
    return !!current && roles.includes(current);
  }

  private storeTokens(tokens: AuthTokens): void {
    this._accessToken.set(tokens.accessToken);
    this.write(ACCESS_KEY, tokens.accessToken);
    this.write(REFRESH_KEY, tokens.refreshToken);
  }

  private clear(): void {
    this._accessToken.set(null);
    this._user.set(null);
    sessionStorage.removeItem(ACCESS_KEY);
    sessionStorage.removeItem(REFRESH_KEY);
  }

  private read(key: string): string | null {
    try {
      return sessionStorage.getItem(key);
    } catch {
      return null;
    }
  }

  private write(key: string, value: string): void {
    try {
      sessionStorage.setItem(key, value);
    } catch {
      /* private mode — tokens stay in memory only */
    }
  }
}
