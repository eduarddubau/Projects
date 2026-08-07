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
  myProjects() {
    return httpResource<Project[]>(() => `${this.apiUrl}/projects`, { defaultValue: [] });
  }

  // Returning undefined keeps the resource idle until the id is known.
  myProject(id: Signal<string | undefined>) {
    return httpResource<Project>(() => (id() ? `${this.apiUrl}/projects/${id()}` : undefined));
  }

  myDeletedProjects() {
    return httpResource<Project[]>(() => `${this.apiUrl}/projects/trash`, { defaultValue: [] });
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

  createProject(payload: { name: string; description?: string }): Observable<Project> {
    return this.http.post<Project>(`${this.apiUrl}/projects`, payload);
  }

  updateProject(id: string, payload: { name: string; description?: string }): Observable<Project> {
    return this.http.put<Project>(`${this.apiUrl}/projects/${id}`, payload);
  }

  deleteMyProject(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/projects/${id}`);
  }

  restoreMyProject(id: string): Observable<Project> {
    return this.http.post<Project>(`${this.apiUrl}/projects/${id}/restore`, {});
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
