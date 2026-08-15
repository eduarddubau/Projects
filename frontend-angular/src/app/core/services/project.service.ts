import { Injectable, inject } from '@angular/core';
import { HttpClient, httpResource } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Signal } from '@angular/core';
import { API_URL } from '@core/tokens/app.tokens';
import { Project } from '@core/models/project';

@Injectable({ providedIn: 'root' })
export class ProjectService {
  private http = inject(HttpClient);
  private apiUrl = inject(API_URL);

  // Read endpoints are resource factories: call them from a component's field
  // initializer so each resource lives and dies with that component. There is no
  // injection context anywhere else, so calling one from a handler throws.

  // Returning undefined keeps the resource idle until the id is known.
  workspaceProjects(workspaceId: Signal<string | null | undefined>) {
    return httpResource<Project[]>(
      () => {
        const id = workspaceId();
        return id ? `${this.apiUrl}/workspaces/${id}/projects` : undefined;
      },
      { defaultValue: [] },
    );
  }

  workspaceDeletedProjects(workspaceId: Signal<string | null | undefined>) {
    return httpResource<Project[]>(
      () => {
        const id = workspaceId();
        return id ? `${this.apiUrl}/workspaces/${id}/projects/trash` : undefined;
      },
      { defaultValue: [] },
    );
  }

  project(id: Signal<string | undefined>) {
    return httpResource<Project>(() => (id() ? `${this.apiUrl}/projects/${id()}` : undefined));
  }

  allProjects() {
    return httpResource<Project[]>(() => `${this.apiUrl}/admin/projects`, { defaultValue: [] });
  }

  allDeletedProjects() {
    return httpResource<Project[]>(() => `${this.apiUrl}/admin/projects/trash`, {
      defaultValue: [],
    });
  }

  // Standard user

  createProject(
    workspaceId: string,
    payload: { name: string; description?: string },
  ): Observable<Project> {
    return this.http.post<Project>(`${this.apiUrl}/workspaces/${workspaceId}/projects`, payload);
  }

  updateProject(id: string, payload: { name: string; description?: string }): Observable<Project> {
    return this.http.put<Project>(`${this.apiUrl}/projects/${id}`, payload);
  }

  deleteProject(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/projects/${id}`);
  }

  restoreProject(id: string): Observable<Project> {
    return this.http.post<Project>(`${this.apiUrl}/projects/${id}/restore`, {});
  }

  moveProject(id: string, workspaceId: string): Observable<Project> {
    return this.http.post<Project>(`${this.apiUrl}/projects/${id}/move`, { workspaceId });
  }

  // Admin
  getAnyProjectById(id: string): Observable<Project> {
    return this.http.get<Project>(`${this.apiUrl}/admin/projects/${id}`);
  }

  deleteAnyProject(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/admin/projects/${id}`);
  }

  restoreProjects(ids: string[]): Observable<{ restoredCount: number }> {
    return this.http.post<{ restoredCount: number }>(`${this.apiUrl}/admin/projects/restore`, ids);
  }

  purgeProjects(ids: string[]): Observable<{ purgedCount: number }> {
    return this.http.post<{ purgedCount: number }>(`${this.apiUrl}/admin/projects/purge`, ids);
  }
}
