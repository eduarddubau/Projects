import { Injectable, Signal, inject } from '@angular/core';
import { httpResource } from '@angular/common/http';
import { API_URL } from '@core/tokens/app.tokens';
import { WorkspaceDashboard } from '@core/models/workspace-dashboard';
import { AdminDashboard } from '@core/models/admin-dashboard';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private apiUrl = inject(API_URL);

  // Resource factories, not shared fields: called from a component's field
  // initializer so each resource lives and dies with that component. Calling one
  // from anywhere else throws — there's no injection context outside construction.

  // Returning undefined keeps the resource idle until the id is known.
  workspaceDashboard(workspaceId: Signal<string | null | undefined>) {
    return httpResource<WorkspaceDashboard>(() => {
      const id = workspaceId();
      return id ? `${this.apiUrl}/workspaces/${id}/dashboard` : undefined;
    });
  }

  adminDashboard() {
    return httpResource<AdminDashboard>(() => `${this.apiUrl}/admin/dashboard`);
  }
}
