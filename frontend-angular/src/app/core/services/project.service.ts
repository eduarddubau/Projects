import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_URL } from '@core/tokens/app.tokens';
import { Project } from '@core/models/project';

@Injectable({ providedIn: 'root' })
export class ProjectService {
  private http = inject(HttpClient);
  private apiUrl = inject(API_URL);

  // Standard user
  getMyProjects(): Observable<Project[]> {
    return this.http.get<Project[]>(`${this.apiUrl}/projects`);
  }

  getMyProjectById(id: string): Observable<Project> {
    return this.http.get<Project>(`${this.apiUrl}/projects/${id}`);
  }

  createProject(payload: { name: string; description?: string }): Observable<Project> {
    return this.http.post<Project>(`${this.apiUrl}/projects`, payload);
  }

  updateProject(id: string, payload: { name: string; description?: string }): Observable<Project> {
    return this.http.patch<Project>(`${this.apiUrl}/projects/${id}`, payload);
  }

  deleteMyProject(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/projects/${id}`);
  }

  // Admin
  getAllProjects(): Observable<Project[]> {
    return this.http.get<Project[]>(`${this.apiUrl}/projects/admin`);
  }

  getAnyProjectById(id: string): Observable<Project> {
    return this.http.get<Project>(`${this.apiUrl}/projects/admin/${id}`);
  }

  deleteAnyProject(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/projects/admin/${id}`);
  }

  restoreProject(id: string): Observable<Project> {
    return this.http.patch<Project>(`${this.apiUrl}/projects/admin/${id}/restore`, {});
  }

  getDeletedProjects(): Observable<Project[]> {
    return this.http.get<Project[]>(`${this.apiUrl}/projects/admin/trash`);
  }
}