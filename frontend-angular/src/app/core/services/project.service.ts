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
  //
  // Collections hang off a workspace, single projects do not: an id is enough to
  // find one, and the caller rarely knows which workspace holds it.

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

  workspaceTrash(workspaceId: Signal<string | null | undefined>) {
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
    return httpResource<Project[]>(() => `${this.apiUrl}/projects/admin`, { defaultValue: [] });
  }

  allDeletedProjects() {
    return httpResource<Project[]>(() => `${this.apiUrl}/projects/admin/trash`, {
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
    return this.http.get<Project>(`${this.apiUrl}/projects/admin/${id}`);
  }

  deleteAnyProject(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/projects/admin/${id}`);
  }

  restoreProjects(ids: string[]): Observable<{ restoredCount: number }> {
    return this.http.post<{ restoredCount: number }>(`${this.apiUrl}/projects/admin/restore`, ids);
  }

  purgeProjects(ids: string[]): Observable<{ purgedCount: number }> {
    return this.http.post<{ purgedCount: number }>(`${this.apiUrl}/projects/admin/purge`, ids);
  }
}
