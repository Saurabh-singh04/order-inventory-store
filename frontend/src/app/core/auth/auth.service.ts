import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, finalize, firstValueFrom, map, shareReplay, tap, throwError } from 'rxjs';
import { AuthApiService } from '../api/auth-api.service';
import { AuthResponse, LoginRequest, RegisterRequest, Role, User } from '../models/api.models';
import { SESSION_STORAGE_KEY, StoredSession, TokenStorageService } from './token-storage.service';

/** Refresh this long before the access token actually expires. */
const EXPIRY_MARGIN_MS = 30_000;

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(AuthApiService);
  private readonly storage = inject(TokenStorageService);
  private readonly router = inject(Router);

  private readonly session = signal<StoredSession | null>(this.storage.load());
  private refreshInFlight$: Observable<string> | null = null;

  readonly user = computed<User | null>(() => this.session()?.user ?? null);
  readonly isLoggedIn = computed(() => this.session() !== null);
  readonly isAdmin = computed(() => this.user()?.role === 'Admin');
  readonly isCustomer = computed(() => this.user()?.role === 'Customer');

  constructor() {
    // Another tab logged in, refreshed or logged out. Picking up its tokens matters:
    // refresh tokens rotate, so a tab still holding the old one would trigger the
    // server's reuse detection and revoke every session.
    if (typeof window !== 'undefined') {
      window.addEventListener('storage', (event) => {
        if (event.key === SESSION_STORAGE_KEY) {
          this.session.set(this.storage.load());
        }
      });
    }
  }

  accessToken(): string | null {
    return this.session()?.accessToken ?? null;
  }

  /**
   * Access token that is still valid for at least a few seconds, refreshing first if needed.
   * Used by SignalR, which can't go through the HTTP interceptor.
   */
  async freshAccessToken(): Promise<string> {
    const current = this.session();
    if (!current) return '';

    const expiresAt = Date.parse(current.accessTokenExpiresAt ?? '');
    if (Number.isFinite(expiresAt) && expiresAt - Date.now() > EXPIRY_MARGIN_MS) {
      return current.accessToken;
    }

    try {
      return await firstValueFrom(this.refreshAccessToken());
    } catch {
      this.sessionExpired();
      return '';
    }
  }

  hasRole(role: Role): boolean {
    return this.user()?.role === role;
  }

  login(request: LoginRequest): Observable<User> {
    return this.api.login(request).pipe(map((response) => this.startSession(response)));
  }

  register(request: RegisterRequest): Observable<User> {
    return this.api.register(request).pipe(map((response) => this.startSession(response)));
  }

  /**
   * Gets a new access token with the refresh token. Concurrent callers share one
   * request, so five parallel 401s still trigger a single /refresh call.
   */
  refreshAccessToken(): Observable<string> {
    const refreshToken = this.session()?.refreshToken;
    if (!refreshToken) {
      return throwError(() => new Error('No refresh token'));
    }

    if (!this.refreshInFlight$) {
      this.refreshInFlight$ = this.api.refresh(refreshToken).pipe(
        tap((response) => this.startSession(response)),
        map((response) => response.accessToken),
        finalize(() => (this.refreshInFlight$ = null)),
        shareReplay(1),
      );
    }
    return this.refreshInFlight$;
  }

  logout(): void {
    const refreshToken = this.session()?.refreshToken;
    if (refreshToken) {
      // Best effort - the session is cleared locally either way.
      this.api.logout(refreshToken).subscribe({ error: () => undefined });
    }
    this.clearSession();
    this.router.navigate(['/login']);
  }

  /** Called by the interceptor when the refresh token is no longer valid. */
  sessionExpired(): void {
    this.clearSession();
    this.router.navigate(['/login'], {
      queryParams: { returnUrl: this.router.url, expired: 1 },
    });
  }

  private startSession(response: AuthResponse): User {
    this.session.set(this.storage.save(response));
    return response.user;
  }

  private clearSession(): void {
    this.storage.clear();
    this.session.set(null);
  }
}
