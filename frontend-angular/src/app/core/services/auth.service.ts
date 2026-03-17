import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { BehaviorSubject, tap } from 'rxjs';
import { API_URL } from '@core/tokens/app.tokens'; 
import { LoginCredentials } from '@models/login-credentials';

@Injectable({ providedIn: 'root' })
export class AuthService {

  private http = inject(HttpClient);
  private router = inject(Router);
  private apiUrl = inject(API_URL);

  private isAuthenticatedSubject = new BehaviorSubject<boolean>(false);
  isAuthenticated$ = this.isAuthenticatedSubject.asObservable();

  // This method will be called when the user submits the login form
  login(credentials: LoginCredentials) {
    return this.http.post<{ token: string }>(`${this.apiUrl}/auth/login`, credentials).pipe(
      tap((response) => {
        this.isAuthenticatedSubject.next(true);
        localStorage.setItem('authToken', response.token);
        this.router.navigate(['/entities']);
      })
    );
  }

  // This method will be called when the user clicks the logout button
  logout() {
    this.isAuthenticatedSubject.next(false);
    localStorage.removeItem('authToken');
    this.router.navigate(['/login']);
  }
}