import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '@core/services/auth.service';
import { API_URL } from '@core/tokens/app.tokens';

// Auth endpoints where a 401 must not trigger a refresh-and-retry (avoids loops).
const NO_REFRESH_PATHS = ['/auth/login', '/auth/register', '/auth/refresh', '/auth/logout'];

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const apiUrl = inject(API_URL);

  // Only our own API carries the bearer token (and the refresh-on-401 dance).
  // Third-party requests — e.g. the weather widget's IP-geo/forecast calls —
  // must never leak the user's access token to another host.
  if (!req.url.startsWith(apiUrl)) {
    return next(req);
  }

  const authService = inject(AuthService);

  const token = authService.getToken();
  const authReq = token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(authReq).pipe(
    catchError((error: HttpErrorResponse) => {
      const canRefresh =
        error.status === 401 &&
        !NO_REFRESH_PATHS.some(path => req.url.includes(path)) &&
        !!authService.getRefreshToken();

      if (!canRefresh) return throwError(() => error);

      // Access token expired: refresh once, then replay the original request.
      return authService.refresh().pipe(
        switchMap(newToken =>
          next(req.clone({ setHeaders: { Authorization: `Bearer ${newToken}` } }))
        ),
        catchError(refreshError => {
          authService.logout('/login');
          return throwError(() => refreshError);
        })
      );
    })
  );
};
