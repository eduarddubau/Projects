import { Injectable, Signal, inject } from '@angular/core';
import { HttpClient, httpResource } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_URL } from '@core/tokens/app.tokens';
import { Task, TaskPayload, TaskStatus } from '@core/models/task';

/** Where a card was dropped, as its neighbours — the server owns the ordering scheme. */
export interface TaskMove {
  status: TaskStatus;
  previousTaskId?: string;
  nextTaskId?: string;
}

@Injectable({ providedIn: 'root' })
export class TaskService {
  private http = inject(HttpClient);
  private apiUrl = inject(API_URL);

  // Read endpoints are resource factories: call them from a component's field
  // initializer so each resource lives and dies with that component.

  // Returning undefined keeps the resource idle until the id is known.
  projectTasks(projectId: Signal<string | null | undefined>) {
    return httpResource<Task[]>(
      () => {
        const id = projectId();
        return id ? `${this.apiUrl}/projects/${id}/tasks` : undefined;
      },
      { defaultValue: [] },
    );
  }

  createTask(projectId: string, payload: TaskPayload): Observable<Task> {
    return this.http.post<Task>(`${this.apiUrl}/projects/${projectId}/tasks`, payload);
  }

  updateTask(id: string, payload: TaskPayload): Observable<Task> {
    return this.http.put<Task>(`${this.apiUrl}/tasks/${id}`, payload);
  }

  deleteTask(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/tasks/${id}`);
  }

  moveTask(id: string, move: TaskMove): Observable<Task> {
    return this.http.post<Task>(`${this.apiUrl}/tasks/${id}/move`, move);
  }
}
