import { Injectable, inject, signal, computed, PLATFORM_ID } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { isPlatformBrowser } from '@angular/common';
import { jwtDecode } from 'jwt-decode';
import { API_URL } from '@core/tokens/app.tokens';
import { AuthResponse } from '@core/models/auth-response';
import { LoginCredentials } from '@core/models/login-credentials';
import { RegisterCredentials } from '@core/models/register-credentials';
import { User } from '@core/models/user';

@Injectable({ providedIn: 'root' })
export class AuthService {
    private http = inject(HttpClient);
    private router = inject(Router);
    private apiUrl = inject(API_URL);
    private platformId = inject(PLATFORM_ID);

    currentUser = signal<User | null>(this.getInitialUser());
    isAuthenticated = computed(() => !!this.currentUser());

    private getInitialUser(): AuthResponse['user'] | null {
        if (!isPlatformBrowser(this.platformId)) return null;

        const token = localStorage.getItem('authToken');
        if (!token) return null;

        try {
            const decoded: any = jwtDecode(token);

            if (decoded.exp < Date.now() / 1000) {
                localStorage.removeItem('authToken');
                return null;
            }

            return {
                id: decoded.sub,
                email: decoded.email,
                firstName: decoded.given_name,
                lastName: decoded.family_name
            };
        } catch {
            localStorage.removeItem('authToken');
            return null;
        }
    }

    getToken(): string | null {
        if (!isPlatformBrowser(this.platformId)) return null;
        return localStorage.getItem('authToken');
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
            localStorage.setItem('authToken', response.token);
        }
        this.currentUser.set(response.user);
    }

    logout() {
        if (isPlatformBrowser(this.platformId)) {
            localStorage.removeItem('authToken');
        }
        this.currentUser.set(null);
        this.router.navigate(['/login']);
    }
}