import { Injectable, inject, signal, computed, PLATFORM_ID } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap, map, throwError, finalize, shareReplay } from 'rxjs';
import { isPlatformBrowser } from '@angular/common';
import { jwtDecode } from 'jwt-decode';
import { API_URL } from '@core/tokens/app.tokens';
import { AuthResponse } from '@core/models/auth-response';
import { LoginCredentials } from '@core/models/login-credentials';
import { RegisterCredentials } from '@core/models/register-credentials';
import { User } from '@core/models/user';
import { WorkspaceContextService } from '@core/services/workspace-context.service';
import { StorageKeys } from '@core/utils/storage-keys';

interface DecodedToken {
  sub: string;
  email: string;
  given_name: string;
  family_name: string;
  nickname?: string;
  exp: number;
  'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'?: string | string[];
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);
  private apiUrl = inject(API_URL);
  private platformId = inject(PLATFORM_ID);
  private workspaceContext = inject(WorkspaceContextService);

  currentUser = signal<User | null>(this.getInitialUser());
  isAuthenticated = computed(() => !!this.currentUser());
  isAdmin = computed(() => this.currentUser()?.isAdmin ?? false);
  displayName = computed(() => {
    const u = this.currentUser();
    return u?.nickname?.trim() || u?.firstName || '';
  });

  // Shared in-flight refresh so concurrent 401s trigger only one refresh call.
  private refresh$: Observable<string> | null = null;

  private getInitialUser(): User | null {
    if (!isPlatformBrowser(this.platformId)) return null;

    const token = localStorage.getItem(StorageKeys.AUTH_TOKEN);
    if (!token) return null;

    try {
      const decoded = jwtDecode<DecodedToken>(token);

      // An expired access token is fine while a refresh token remains; the
      // interceptor refreshes on demand.
      if (decoded.exp < Date.now() / 1000 && !localStorage.getItem(StorageKeys.REFRESH_TOKEN)) {
        this.clearSession();
        return null;
      }

      return this.mapDecodedToUser(decoded);
    } catch {
      this.clearSession();
      return null;
    }
  }

  private mapDecodedToUser(decoded: DecodedToken): User {
    const roles = this.extractRoles(decoded);
    return {
      id: decoded.sub,
      email: decoded.email,
      firstName: decoded.given_name,
      lastName: decoded.family_name,
      nickname: decoded.nickname,
      isAdmin: roles.includes('Admin')
    };
  }

  private extractRoles(decoded: DecodedToken): string[] {
    const raw = decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
    if (!raw) return [];
    return Array.isArray(raw) ? raw : [raw];
  }

  getToken(): string | null {
    if (!isPlatformBrowser(this.platformId)) return null;
    return localStorage.getItem(StorageKeys.AUTH_TOKEN);
  }

  getRefreshToken(): string | null {
    if (!isPlatformBrowser(this.platformId)) return null;
    return localStorage.getItem(StorageKeys.REFRESH_TOKEN);
  }

  /** Swaps the refresh token for a new access token, sharing one in-flight
   *  request across concurrent callers. */
  refresh(): Observable<string> {
    if (this.refresh$) return this.refresh$;

    const refreshToken = this.getRefreshToken();
    if (!refreshToken) return throwError(() => new Error('No refresh token available.'));

    this.refresh$ = this.http
      .post<AuthResponse>(`${this.apiUrl}/auth/refresh`, { refreshToken })
      .pipe(
        tap(response => this.setSession(response)),
        map(response => response.token),
        finalize(() => (this.refresh$ = null)),
        shareReplay(1)
      );

    return this.refresh$;
  }

  login(credentials: LoginCredentials) {
    return this.http.post<AuthResponse>(`${this.apiUrl}/auth/login`, credentials).pipe(
      tap(response => this.setSession(response))
    );
  }

  register(credentials: RegisterCredentials) {
    return this.http.post<AuthResponse>(`${this.apiUrl}/auth/register`, credentials).pipe(
      tap(response => this.setSession(response))
    );
  }

  private setSession(response: AuthResponse) {
    if (isPlatformBrowser(this.platformId)) {
      localStorage.setItem(StorageKeys.AUTH_TOKEN, response.token);
      localStorage.setItem(StorageKeys.REFRESH_TOKEN, response.refreshToken);
    }
    // Re-parse token to get roles since AuthResponse.user doesn't include them
    const token = response.token;
    try {
      const decoded = jwtDecode<DecodedToken>(token);
      this.currentUser.set(this.mapDecodedToUser(decoded));
    } catch {
      this.currentUser.set(response.user);
    }
  }

  // Explicit sign-out lands on the public home; session-expiry passes '/login'.
  logout(redirectTo: string = '/') {
    // Revoke the token server-side, then clear the local session.
    const refreshToken = this.getRefreshToken();
    if (refreshToken) {
      this.http.post(`${this.apiUrl}/auth/logout`, { refreshToken }).subscribe({ error: () => {} });
    }
    this.clearSession();
    this.currentUser.set(null);
    // One direction only: auth clears the workspace context, never the reverse.
    // Without this the cached list survives a sign-out and the next account sees
    // the previous one's workspaces until a hard reload.
    this.workspaceContext.clear();
    this.router.navigate([redirectTo]);
  }

  private clearSession(): void {
    if (isPlatformBrowser(this.platformId)) {
      localStorage.removeItem(StorageKeys.AUTH_TOKEN);
      localStorage.removeItem(StorageKeys.REFRESH_TOKEN);
    }
  }
}