import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '@core/services/auth.service';

// Auth endpoints where a 401 must not trigger a refresh-and-retry (avoids loops).
const NO_REFRESH_PATHS = ['/auth/login', '/auth/register', '/auth/refresh', '/auth/logout'];

export const authInterceptor: HttpInterceptorFn = (req, next) => {
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
          authService.logout();
          return throwError(() => refreshError);
        })
      );
    })
  );
};
