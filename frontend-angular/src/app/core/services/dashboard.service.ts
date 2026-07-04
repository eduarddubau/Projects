import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_URL } from '@core/tokens/app.tokens';
import { UserDashboard } from '@core/models/user-dashboard';
import { AdminDashboard } from '@core/models/admin-dashboard';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private http = inject(HttpClient);
  private apiUrl = inject(API_URL);

  getMyDashboard(): Observable<UserDashboard> {
    return this.http.get<UserDashboard>(`${this.apiUrl}/dashboard`);
  }

  getAdminDashboard(): Observable<AdminDashboard> {
    return this.http.get<AdminDashboard>(`${this.apiUrl}/dashboard/admin`);
  }
}
