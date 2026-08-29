import { taskRows, taskRowsById } from './task-row';
import { Task } from '@core/models/task';

const today = '2026-08-28';

function task(id: string, overrides: Partial<Task> = {}): Task {
  return {
    id,
    title: `Task ${id}`,
    status: 'Todo',
    position: 0,
    projectId: 'p1',
    createdAt: '2026-07-01T09:00:00Z',
    isDeleted: false,
    ...overrides,
  };
}

describe('taskRows', () => {
  it('derives the due date and the overdue flag once per task', () => {
    const [row] = taskRows([task('a', { dueDate: '2026-08-27' })], today);

    expect(row.overdue).toBe(true);
    expect(row.due?.getFullYear()).toBe(2026);
    expect(row.due?.getMonth()).toBe(7);
    expect(row.due?.getDate()).toBe(27);
  });

  it('leaves an undated task undated rather than overdue', () => {
    const [row] = taskRows([task('a')], today);

    expect(row.due).toBeNull();
    expect(row.overdue).toBe(false);
  });

  // The day is an argument, so a list re-derives when it turns instead of each binding
  // asking the clock separately.
  it('answers against the day it is given', () => {
    const [before] = taskRows([task('a', { dueDate: '2026-08-28' })], '2026-08-28');
    const [after] = taskRows([task('a', { dueDate: '2026-08-28' })], '2026-08-29');

    expect(before.overdue).toBe(false);
    expect(after.overdue).toBe(true);
  });

  it('never flags a finished task', () => {
    const [row] = taskRows([task('a', { dueDate: '2026-08-01', status: 'Done' })], today);

    expect(row.overdue).toBe(false);
  });

  it('keys by id for templates that only get the task back', () => {
    const rows = taskRowsById([task('a', { dueDate: '2026-08-27' }), task('b')], today);

    expect(rows.get('a')?.overdue).toBe(true);
    expect(rows.get('b')?.overdue).toBe(false);
  });
});
