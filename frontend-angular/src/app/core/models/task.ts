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

/** A task read away from its board, so it carries the project it belongs to. */
export interface WorkspaceTask extends Task {
  projectName: string;
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

/**
 * Where a card dropped between two neighbours lands. Mirrors the server, which
 * treats a neighbour that has since moved or been deleted as absent rather than
 * as an error, so a stale drag settles at the column edge instead of failing.
 */
export function resolveDropIndex(
  column: readonly Task[],
  previousTaskId?: string,
  nextTaskId?: string,
): number {
  if (previousTaskId) {
    const index = column.findIndex((task) => task.id === previousTaskId);
    if (index >= 0) return index + 1;
  }

  if (nextTaskId) {
    const index = column.findIndex((task) => task.id === nextTaskId);
    if (index >= 0) return index;
  }

  return column.length;
}

/**
 * Applies a move to a local list the way the server will, so the board can render
 * the result before the round-trip finishes. Both columns are renumbered, because
 * leaving the vacated one sparse would misplace the next drop into it.
 */
export function applyMove(
  tasks: readonly Task[],
  movedId: string,
  status: TaskStatus,
  previousTaskId?: string,
  nextTaskId?: string,
): Task[] {
  const moved = tasks.find((task) => task.id === movedId);
  if (!moved) return [...tasks];

  const others = tasks.filter((task) => task.id !== movedId);

  const destination = others.filter((task) => task.status === status);
  destination.splice(resolveDropIndex(destination, previousTaskId, nextTaskId), 0, {
    ...moved,
    status,
  });

  const repositioned = new Map<string, Task>();
  destination.forEach((task, index) => repositioned.set(task.id, { ...task, position: index }));

  if (moved.status !== status) {
    others
      .filter((task) => task.status === moved.status)
      .forEach((task, index) => repositioned.set(task.id, { ...task, position: index }));
  }

  return sortTasks(tasks.map((task) => repositioned.get(task.id) ?? task));
}

export interface TaskPayload {
  title: string;
  description?: string;
  status: TaskStatus;
  assigneeId?: string;
  startDate?: string;
  dueDate?: string;
}
