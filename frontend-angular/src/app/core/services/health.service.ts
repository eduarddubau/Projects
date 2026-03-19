import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { HEALTH_URL } from '@core/tokens/app.tokens';
import { timer, Observable, of } from 'rxjs';
import { map, switchMap, catchError, shareReplay } from 'rxjs/operators';

export interface HealthStatus {
  state: 'online' | 'offline';
  error?: string;
}

@Injectable({ providedIn: 'root' })
export class HealthService {
  private http = inject(HttpClient);
  private healthUrl = inject(HEALTH_URL);

  // Emit health status every n seconds, starting immediately
  public status$: Observable<HealthStatus> = timer(0, 5000).pipe(
    switchMap(() => this.http.get(this.healthUrl, { responseType: 'text' })),
    map(() => ({ state: 'online' } as HealthStatus)),
    catchError((err: HttpErrorResponse) => {
      let errorMessage = '';

      if (err.status === 0) {
        errorMessage = 'Network Error: Check CORS or Backend Status';
      } else {
        errorMessage = `API Error: ${err.status} (${this.getFriendlyStatus(err.status)})`;
      }
      
      return of({ state: 'offline', error: errorMessage } as HealthStatus);
    }),
    shareReplay(1)
  );

  // Convert HTTP status codes to user-friendly messages
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