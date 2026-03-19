import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject, PLATFORM_ID } from '@angular/core'; // Use @angular/core
import { isPlatformBrowser } from '@angular/common';
import { throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from '@core/services/auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const platformId = inject(PLATFORM_ID);
  
  let token: string | null = null;

  // Safe access to localStorage (Browser only)
  if (isPlatformBrowser(platformId)) {
    token = localStorage.getItem('authToken');
  }

  // Clone the request if a token exists
  if (token) {
    req = req.clone({
      setHeaders: {
        Authorization: `Bearer ${token}`
      }
    });
  }

  // Process the request and watch for 401s
  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      // If the server says the token is invalid/expired
      if (error.status === 401) {
        authService.logout();
      }
      return throwError(() => error);
    })
  );
};