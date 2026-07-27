import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_URL } from '@core/tokens/app.tokens';
import { Profile } from '@core/models/profile';

@Injectable({ providedIn: 'root' })
export class ProfileService {
  private http = inject(HttpClient);
  private apiUrl = inject(API_URL);

  getProfile(): Observable<Profile> {
    return this.http.get<Profile>(`${this.apiUrl}/profile`);
  }

  updateProfile(payload: { firstName: string; lastName: string, nickname?: string | null }): Observable<Profile> {
    return this.http.put<Profile>(`${this.apiUrl}/profile`, payload);
  }
}
