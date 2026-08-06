import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_URL } from '@core/tokens/app.tokens';
import { AdminUser } from '@core/models/admin-user';

@Injectable({ providedIn: 'root' })
export class UserService {
  private http = inject(HttpClient);
  private apiUrl = inject(API_URL);

  getAllUsers(): Observable<AdminUser[]> {
    return this.http.get<AdminUser[]>(`${this.apiUrl}/users`);
  }

  getUserById(id: string): Observable<AdminUser> {
    return this.http.get<AdminUser>(`${this.apiUrl}/users/${id}`);
  }

  createUser(payload: { firstName: string; lastName: string; email: string }): Observable<AdminUser> {
    return this.http.post<AdminUser>(`${this.apiUrl}/users`, payload);
  }

  deleteUser(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/users/${id}`);
  }

  restoreUser(id: string): Observable<AdminUser> {
    return this.http.post<AdminUser>(`${this.apiUrl}/users/${id}/restore`, {});
  }

  getDeletedUsers(): Observable<AdminUser[]> {
    return this.http.get<AdminUser[]>(`${this.apiUrl}/users/trash`);
  }

  anonymizeUser(id: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/users/${id}/anonymize`, {});
  }
}