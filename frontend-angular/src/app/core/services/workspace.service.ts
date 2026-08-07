import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_URL } from '@core/tokens/app.tokens';
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

  getMembers(workspaceId: string): Observable<WorkspaceMember[]> {
    return this.http.get<WorkspaceMember[]>(`${this.apiUrl}/workspaces/${workspaceId}/members`);
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
}
