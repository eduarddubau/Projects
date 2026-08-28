import { Injectable, Signal, inject } from '@angular/core';
import { HttpClient, httpResource } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_URL } from '@core/tokens/app.tokens';
import { Task, TaskPayload, TaskStatus, WorkspaceTask } from '@core/models/task';
import { todayIso } from '@core/utils/iso-date';

/** The one filter a workspace task list is under; the chips are single-select. */
export type TaskFilter = 'mine' | 'all' | 'overdue' | 'unassigned';

// A function, not a constant: "overdue" is relative to the caller's calendar day, and
// the server's UTC one is a different day for part of every night in this timezone.
function paramsFor(filter: TaskFilter): Record<string, string> {
  switch (filter) {
    case 'mine':
      return { assignee: 'me' };
    case 'unassigned':
      return { assignee: 'unassigned' };
    case 'overdue':
      return { dueBefore: todayIso() };
    default:
      return {};
  }
}

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

  // Open tasks across one workspace's projects. The endpoint never returns Done ones:
  // this is a work list, and the board is where finished cards live.
  workspaceTasks(workspaceId: Signal<string | null | undefined>, filter: Signal<TaskFilter>) {
    return httpResource<WorkspaceTask[]>(
      () => {
        const id = workspaceId();
        return id
          ? { url: `${this.apiUrl}/workspaces/${id}/tasks`, params: paramsFor(filter()) }
          : undefined;
      },
      { defaultValue: [] },
    );
  }

  // Deleted tasks still inside the retention window, newest first.
  projectDeletedTasks(projectId: Signal<string | null | undefined>) {
    return httpResource<Task[]>(
      () => {
        const id = projectId();
        return id ? `${this.apiUrl}/projects/${id}/tasks/trash` : undefined;
      },
      { defaultValue: [] },
    );
  }

  restoreTask(id: string): Observable<Task> {
    return this.http.post<Task>(`${this.apiUrl}/tasks/${id}/restore`, {});
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
