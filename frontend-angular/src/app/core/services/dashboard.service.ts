import { Injectable, inject } from '@angular/core';
import { httpResource } from '@angular/common/http';
import { API_URL } from '@core/tokens/app.tokens';
import { UserDashboard } from '@core/models/user-dashboard';
import { AdminDashboard } from '@core/models/admin-dashboard';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private apiUrl = inject(API_URL);

  // Resource factories, not shared fields: called from a component's field
  // initializer so each resource lives and dies with that component. Calling one
  // from anywhere else throws — there's no injection context outside construction.
  myDashboard() {
    return httpResource<UserDashboard>(() => `${this.apiUrl}/dashboard`);
  }

  adminDashboard() {
    return httpResource<AdminDashboard>(() => `${this.apiUrl}/admin/dashboard`);
  }
}
