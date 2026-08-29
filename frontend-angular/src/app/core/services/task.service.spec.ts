import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';

import { TaskService, TaskFilter } from './task.service';
import { TodayService } from './today.service';
import { API_URL } from '@core/tokens/app.tokens';

const apiUrl = 'http://api.test';
const workspaceId = 'w1';

describe('TaskService.workspaceTasks', () => {
  let httpMock: HttpTestingController;
  let today: ReturnType<typeof signal<string>>;

  function setup(filter: TaskFilter) {
    today = signal('2026-08-28');

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_URL, useValue: apiUrl },
        { provide: TodayService, useValue: { today } },
      ],
    });

    httpMock = TestBed.inject(HttpTestingController);

    // The factory calls httpResource(), which needs an injection context — components get
    // one for free from their field initializers.
    const resource = TestBed.runInInjectionContext(() =>
      TestBed.inject(TaskService).workspaceTasks(signal(workspaceId), signal(filter)),
    );
    TestBed.tick();
    return resource;
  }

  afterEach(() => httpMock.verify());

  it('asks for tasks due before the reader’s day, not a server-side flag', () => {
    setup('overdue');

    const request = httpMock.expectOne((r) => r.url.endsWith('/tasks'));
    expect(request.request.params.get('dueBefore')).toBe('2026-08-28');
    request.flush([]);
  });

  /**
   * The half that was missing: the bands re-banded at midnight while the request kept
   * yesterday's cutoff, so an overdue list left open overnight disagreed with its own rows.
   * Reading the day signal inside the resource is what makes the request follow it.
   */
  it('refetches with the new day when midnight rolls over', () => {
    setup('overdue');
    httpMock.expectOne((r) => r.url.endsWith('/tasks')).flush([]);

    today.set('2026-08-29');
    TestBed.tick();

    const request = httpMock.expectOne((r) => r.url.endsWith('/tasks'));
    expect(request.request.params.get('dueBefore')).toBe('2026-08-29');
    request.flush([]);
  });

  // A filter that does not depend on the day must not refetch when it turns.
  it('leaves the other filters alone when the day changes', () => {
    setup('mine');
    httpMock.expectOne((r) => r.url.endsWith('/tasks')).flush([]);

    today.set('2026-08-29');
    TestBed.tick();

    httpMock.expectNone((r) => r.url.endsWith('/tasks'));
  });
});
