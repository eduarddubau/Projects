export type TaskStatus = 'Todo' | 'InProgress' | 'Done';

/** Board column order, left to right. The API returns tasks in this order too. */
export const TASK_STATUSES: readonly TaskStatus[] = ['Todo', 'InProgress', 'Done'];

export interface Task {
  id: string;
  title: string;
  description?: string;
  status: TaskStatus;
  position: number;
  projectId: string;
  assigneeId?: string;
  /** Null for an unassigned task and for one whose assignee was deleted. */
  assigneeDisplayName?: string;
  /** yyyy-MM-dd, never a timestamp. */
  startDate?: string;
  dueDate?: string;
  completedAt?: string;
  createdBy?: string;
  updatedBy?: string;
  createdByDisplayName?: string;
  updatedByDisplayName?: string;
  createdAt: string;
  updatedAt?: string;
  isDeleted: boolean;
  deletedAt?: string;
}

/**
 * The order the API returns: workflow status, then position within its column.
 * Re-applied after a local insert so a new card lands in its own column rather
 * than at the tail of the list.
 */
export function sortTasks(tasks: readonly Task[]): Task[] {
  return [...tasks].sort(
    (a, b) =>
      TASK_STATUSES.indexOf(a.status) - TASK_STATUSES.indexOf(b.status) ||
      a.position - b.position ||
      a.id.localeCompare(b.id),
  );
}

export interface TaskPayload {
  title: string;
  description?: string;
  status: TaskStatus;
  assigneeId?: string;
  startDate?: string;
  dueDate?: string;
}
