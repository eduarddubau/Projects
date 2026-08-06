import { Injectable, inject } from '@angular/core';
import { HttpClient, httpResource } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_URL } from '@core/tokens/app.tokens';
import { Profile } from '@core/models/profile';

@Injectable({ providedIn: 'root' })
export class ProfileService {
  private http = inject(HttpClient);
  private apiUrl = inject(API_URL);

  // Resource factory: call from a component field initializer so it lives and
  // dies with that component. There is no injection context anywhere else.
  myProfile() {
    return httpResource<Profile>(() => `${this.apiUrl}/profile`);
  }

  updateProfile(payload: { firstName: string; lastName: string; email: string; nickname?: string | null }): Observable<Profile> {
    return this.http.put<Profile>(`${this.apiUrl}/profile`, payload);
  }
}
