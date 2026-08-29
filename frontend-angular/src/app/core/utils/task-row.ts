import { Task } from '@core/models/task';
import { fromIsoDate, isOverdue } from './iso-date';

/** A task plus the bits a template would otherwise recompute on every binding. */
export interface TaskRow<T extends Task = Task> {
  task: T;
  due: Date | null;
  overdue: boolean;
}

/**
 * One derivation for every list of tasks in the app.
 *
 * Templates used to call isOverdue and fromIsoDate per binding — two and three times a row
 * respectively — each rebuilding the same answer on every change-detection pass. Deriving
 * once also puts the day in one place, so a list re-derives when midnight rolls over rather
 * than each binding deciding for itself.
 */
export function taskRows<T extends Task>(tasks: readonly T[], today: string): TaskRow<T>[] {
  return tasks.map((task) => ({
    task,
    due: fromIsoDate(task.dueDate),
    overdue: isOverdue(task.dueDate, task.status, today),
  }));
}

/** The same, keyed by id, for templates whose row context hands back the task itself. */
export function taskRowsById<T extends Task>(
  tasks: readonly T[],
  today: string,
): Map<string, TaskRow<T>> {
  return new Map(taskRows(tasks, today).map((row) => [row.task.id, row]));
}
