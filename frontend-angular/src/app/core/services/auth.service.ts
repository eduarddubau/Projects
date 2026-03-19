import { Injectable, inject, signal, computed, PLATFORM_ID } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { API_URL } from '@core/tokens/app.tokens'; 
import { AuthResponse } from '@models/auth-response';
import { LoginCredentials } from '@models/login-credentials';
import { RegisterCredentials } from '@models/register-credentials';
import { jwtDecode } from 'jwt-decode';
import { isPlatformBrowser } from '@angular/common';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);
  private apiUrl = inject(API_URL);
  private platformId = inject(PLATFORM_ID);

  currentUser = signal<AuthResponse['user'] | null>(this.getInitialUser());
  isAuthenticated = computed(() => !!this.currentUser());

  private getInitialUser(): AuthResponse['user'] | null {

    // Ensure this code only runs in the browser, as localStorage is not available on the server
    if (!isPlatformBrowser(this.platformId)) {
      return null;
    }

    const token = localStorage.getItem('authToken');
    if (!token) return null;

    try {
      const decoded: any = jwtDecode(token);
      
      const currentTime = Date.now() / 1000;
      if (decoded.exp < currentTime) {
        this.logout();
        return null;
      }

      return {
        id: decoded.nameid,
        email: decoded.email,
        firstName: decoded.given_name,
        lastName: decoded.family_name
      };
    } catch {
      return null;
    }
  }

  register(credentials: RegisterCredentials) {
    return this.http.post<AuthResponse>(`${this.apiUrl}/auth/register`, credentials).pipe(
      tap(response => this.setSession(response))
    );
  }

  login(credentials: LoginCredentials) {
    return this.http.post<AuthResponse>(`${this.apiUrl}/auth/login`, credentials).pipe(
      tap(response => this.setSession(response))
    );
  }

  private setSession(response: AuthResponse) {
    localStorage.setItem('authToken', response.token);
    this.currentUser.set(response.user);
    this.router.navigate(['/entities']);
  }

  logout() {
    localStorage.removeItem('authToken');
    this.currentUser.set(null);
    this.router.navigate(['/login']);
  }
}