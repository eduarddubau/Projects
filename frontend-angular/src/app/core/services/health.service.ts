import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { HEALTH_URL } from '@core/tokens/app.tokens';
import { timer, Observable, of } from 'rxjs';
import { map, switchMap, catchError, shareReplay } from 'rxjs/operators';

@Injectable({ providedIn: 'root' })
export class HealthService {
  private http = inject(HttpClient);
  private healthUrl = inject(HEALTH_URL);

  // Create a polling stream that checks every n seconds
  public status$: Observable<'online' | 'offline'> = timer(0, 5000).pipe(
    switchMap(() => this.http.get(this.healthUrl, { responseType: 'text' })),
    map(() => 'online' as const),
    catchError(() => of('offline' as const)),
    shareReplay(1)
  );
}