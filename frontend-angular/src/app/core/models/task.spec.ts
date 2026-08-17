import { TASK_STATUSES, Task, TaskStatus, applyMove, resolveDropIndex, sortTasks } from './task';

function task(id: string, status: TaskStatus, position: number): Task {
  return {
    id,
    title: id,
    status,
    position,
    projectId: 'p1',
    createdAt: '2026-08-16T00:00:00Z',
    isDeleted: false,
  };
}

/** Renders every column and each card's position, so a wrong slot reads off the failure. */
function layout(tasks: readonly Task[]): string {
  return TASK_STATUSES.map((status) => {
    const cards = tasks
      .filter((t) => t.status === status)
      .map((t) => `${t.id}${t.position}`)
      .join(' ');
    return `${status}: ${cards || '-'}`;
  }).join(' | ');
}

describe('resolveDropIndex', () => {
  const column = [task('a', 'Todo', 0), task('b', 'Todo', 1), task('c', 'Todo', 2)];

  it('lands after the previous neighbour', () => {
    expect(resolveDropIndex(column, 'a', 'b')).toBe(1);
  });

  it('lands at the head when there is only a next neighbour', () => {
    expect(resolveDropIndex(column, undefined, 'a')).toBe(0);
  });

  it('lands at the end when neither neighbour is given', () => {
    expect(resolveDropIndex(column, undefined, undefined)).toBe(3);
  });

  it('ignores a neighbour that is no longer in the column', () => {
    expect(resolveDropIndex(column, 'gone', 'b')).toBe(1);
    expect(resolveDropIndex(column, 'gone', 'also-gone')).toBe(3);
  });
});

describe('applyMove', () => {
  const board = [
    task('a', 'Todo', 0),
    task('b', 'Todo', 1),
    task('c', 'Todo', 2),
    task('x', 'InProgress', 0),
  ];

  it('reorders within a column and renumbers it', () => {
    expect(layout(applyMove(board, 'c', 'Todo', undefined, 'a'))).toBe(
      'Todo: c0 a1 b2 | InProgress: x0 | Done: -',
    );
  });

  it('drops between two cards', () => {
    expect(layout(applyMove(board, 'a', 'Todo', 'b', 'c'))).toBe(
      'Todo: b0 a1 c2 | InProgress: x0 | Done: -',
    );
  });

  it('renumbers both columns when the status changes', () => {
    // 'b' leaves a hole at position 1; Todo must close it rather than stay 0,2.
    expect(layout(applyMove(board, 'b', 'InProgress', 'x', undefined))).toBe(
      'Todo: a0 c1 | InProgress: x0 b1 | Done: -',
    );
  });

  it('moves to the head of another column', () => {
    expect(layout(applyMove(board, 'a', 'InProgress', undefined, 'x'))).toBe(
      'Todo: b0 c1 | InProgress: a0 x1 | Done: -',
    );
  });

  it('appends when both neighbours are stale', () => {
    expect(layout(applyMove(board, 'a', 'Todo', 'gone', 'also-gone'))).toBe(
      'Todo: b0 c1 a2 | InProgress: x0 | Done: -',
    );
  });

  it('leaves the list alone when the id is unknown', () => {
    expect(layout(applyMove(board, 'missing', 'Todo', undefined, undefined))).toBe(
      'Todo: a0 b1 c2 | InProgress: x0 | Done: -',
    );
  });
});

describe('sortTasks', () => {
  it('orders by workflow status, then position', () => {
    const shuffled = [task('d', 'Done', 0), task('t', 'Todo', 1), task('i', 'InProgress', 0)];
    expect(sortTasks(shuffled).map((t) => t.id)).toEqual(['t', 'i', 'd']);
  });

  it('breaks position ties by id so the order is stable', () => {
    const tied = [task('z', 'Todo', 0), task('a', 'Todo', 0)];
    expect(sortTasks(tied).map((t) => t.id)).toEqual(['a', 'z']);
  });
});
