import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { HEALTH_URL } from '@core/tokens/app.tokens';
import { timer, Observable, of } from 'rxjs';
import { map, switchMap, catchError, shareReplay } from 'rxjs/operators';
import { HealthStatus } from '@core/models/health-status';

@Injectable({ providedIn: 'root' })
export class HealthService {
  private http = inject(HttpClient);
  private healthUrl = inject(HEALTH_URL);

  public status$: Observable<HealthStatus> = timer(0, 5000).pipe(
    switchMap(() =>
      this.http.get(this.healthUrl, { responseType: 'text' }).pipe(
        map(() => ({ state: 'online' } as HealthStatus)),
        catchError((err: HttpErrorResponse) => {
          const status: HealthStatus = err.status === 0
            ? { state: 'offline', errorKey: 'header.health.networkError' }
            : {
                state: 'offline',
                errorKey: 'header.health.apiError',
                errorParams: { status: err.status, statusText: this.getFriendlyStatus(err.status) },
              };

          return of(status);
        })
      )
    ),
    shareReplay({ bufferSize: 1, refCount: true })
  );

  private getFriendlyStatus(status: number): string {
    const statusMap: Record<number, string> = {
      400: 'Bad Request',
      401: 'Unauthorized',
      403: 'Forbidden',
      404: 'Not Found',
      500: 'Internal Server Error',
      503: 'Service Unavailable'
    };
    return statusMap[status] || 'Unknown Error';
  }
}