import { Injectable, Signal, inject } from '@angular/core';
import { HttpClient, httpResource } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_URL } from '@core/tokens/app.tokens';
import { AdminWorkspace } from '@core/models/admin-workspace';
import { Workspace, WorkspaceMember, WorkspaceRole } from '@core/models/workspace';

@Injectable({ providedIn: 'root' })
export class WorkspaceService {
  private http = inject(HttpClient);
  private apiUrl = inject(API_URL);

  getMyWorkspaces(): Observable<Workspace[]> {
    return this.http.get<Workspace[]>(`${this.apiUrl}/workspaces`);
  }

  getMyDeletedWorkspaces(): Observable<Workspace[]> {
    return this.http.get<Workspace[]>(`${this.apiUrl}/workspaces/trash`);
  }

  getWorkspaceById(id: string): Observable<Workspace> {
    return this.http.get<Workspace>(`${this.apiUrl}/workspaces/${id}`);
  }

  createWorkspace(payload: { name: string; description?: string }): Observable<Workspace> {
    return this.http.post<Workspace>(`${this.apiUrl}/workspaces`, payload);
  }

  updateWorkspace(
    id: string,
    payload: { name: string; description?: string },
  ): Observable<Workspace> {
    return this.http.put<Workspace>(`${this.apiUrl}/workspaces/${id}`, payload);
  }

  deleteWorkspace(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/workspaces/${id}`);
  }

  restoreWorkspace(id: string): Observable<Workspace> {
    return this.http.post<Workspace>(`${this.apiUrl}/workspaces/${id}/restore`, {});
  }

  changeMemberRole(
    workspaceId: string,
    userId: string,
    role: WorkspaceRole,
  ): Observable<WorkspaceMember> {
    return this.http.patch<WorkspaceMember>(
      `${this.apiUrl}/workspaces/${workspaceId}/members/${userId}/role`,
      { role },
    );
  }

  removeMember(workspaceId: string, userId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/workspaces/${workspaceId}/members/${userId}`);
  }

  leaveWorkspace(workspaceId: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/workspaces/${workspaceId}/members/leave`, {});
  }

  /**
   * Reads only. Mutations go through the plain HttpClient methods below and then
   * reload() this — httpResource keeps the previous rows visible while it
   * refetches, so the table never blanks out mid-update.
   */
  membersResource(workspaceId: Signal<string>) {
    return httpResource<WorkspaceMember[]>(
      () => `${this.apiUrl}/workspaces/${workspaceId()}/members`,
      { defaultValue: [] },
    );
  }

  allDeletedWorkspaces() {
    return httpResource<AdminWorkspace[]>(() => `${this.apiUrl}/admin/workspaces/trash`, {
      defaultValue: [],
    });
  }

  restoreWorkspaces(ids: string[]): Observable<{ restoredCount: number }> {
    return this.http.post<{ restoredCount: number }>(
      `${this.apiUrl}/admin/workspaces/restore`,
      ids,
    );
  }

  purgeWorkspaces(ids: string[]): Observable<{ purgedCount: number }> {
    return this.http.post<{ purgedCount: number }>(`${this.apiUrl}/admin/workspaces/purge`, ids);
  }
}
