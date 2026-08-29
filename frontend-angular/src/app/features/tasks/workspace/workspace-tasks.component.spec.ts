import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { vi } from 'vitest';

import { WorkspaceTasksComponent } from './workspace-tasks.component';
import { API_URL } from '@core/tokens/app.tokens';
import { ThemeService } from '@core/services/theme.service';
import { TodayService } from '@core/services/today.service';
import { WorkspaceTask } from '@core/models/task';
import { todayIso } from '@core/utils/iso-date';
import { provideTranslocoTesting } from '@shared/testing/transloco-testing';

const apiUrl = 'http://api.test';
const workspaceId = '99999999-9999-9999-9999-999999999999';
const tasksUrl = `${apiUrl}/workspaces/${workspaceId}/tasks`;

// The rows date-format through LanguageService, which pulls in the real ThemeService —
// and that touches window.matchMedia, which jsdom lacks.
const themeStub = { provide: ThemeService, useValue: { theme: signal('light') } };

// A day the test drives, so the consumer's midnight behaviour is observable rather than
// something only TodayService's own spec knows about.
let today: ReturnType<typeof signal<string>>;

function daysFromToday(days: number): string {
  const date = new Date();
  date.setDate(date.getDate() + days);
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(
    date.getDate(),
  ).padStart(2, '0')}`;
}

function task(id: string, overrides: Partial<WorkspaceTask> = {}): WorkspaceTask {
  return {
    id,
    title: `Task ${id}`,
    status: 'Todo',
    position: 0,
    projectId: 'p1',
    projectName: 'Rocket Plans',
    createdAt: '2026-07-01T09:00:00Z',
    isDeleted: false,
    ...overrides,
  };
}

describe('WorkspaceTasksComponent', () => {
  let fixture: ComponentFixture<WorkspaceTasksComponent>;
  let httpMock: HttpTestingController;
  let queryParams: BehaviorSubject<ReturnType<typeof convertToParamMap>>;

  async function setup(filter: string | null = null) {
    today = signal(todayIso());
    queryParams = new BehaviorSubject(convertToParamMap(filter ? { filter } : {}));
    const routeStub = {
      paramMap: new BehaviorSubject(convertToParamMap({ workspaceId })),
      queryParamMap: queryParams,
      snapshot: {
        paramMap: convertToParamMap({ workspaceId }),
        queryParamMap: queryParams.value,
      },
    };

    await TestBed.configureTestingModule({
      imports: [WorkspaceTasksComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideTranslocoTesting(),
        themeStub,
        { provide: TodayService, useValue: { today } },
        { provide: API_URL, useValue: apiUrl },
        { provide: ActivatedRoute, useValue: routeStub },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(WorkspaceTasksComponent);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  }

  async function flush(tasks: WorkspaceTask[]) {
    httpMock.expectOne((r) => r.url === tasksUrl).flush(tasks);
    await fixture.whenStable();
    fixture.detectChanges();
  }

  afterEach(() => httpMock.verify());

  function groupTitles(): string[] {
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.group-title')).map(
      (el) => el.textContent?.trim().split(/\s+/)[0] ?? '',
    );
  }

  it('asks for the caller’s own tasks when the URL names no filter', async () => {
    await setup();

    const request = httpMock.expectOne((r) => r.url === tasksUrl);
    expect(request.request.params.get('assignee')).toBe('me');
    request.flush([]);
  });

  it('translates each filter into the query the API expects', async () => {
    await setup('unassigned');

    const request = httpMock.expectOne((r) => r.url === tasksUrl);
    expect(request.request.params.get('assignee')).toBe('unassigned');
    request.flush([]);
  });

  // "Overdue" is relative to the reader's calendar day, so the client sends its own
  // rather than letting the server's UTC clock decide.
  it('asks for tasks due before today rather than for a server-side flag', async () => {
    await setup('overdue');

    const request = httpMock.expectOne((r) => r.url === tasksUrl);
    expect(request.request.params.get('dueBefore')).toBe(todayIso());
    expect(request.request.params.has('assignee')).toBe(false);
    request.flush([]);
  });

  it('falls back to my tasks for a filter the app does not have', async () => {
    await setup('nonsense');

    const request = httpMock.expectOne((r) => r.url === tasksUrl);
    expect(request.request.params.get('assignee')).toBe('me');
    request.flush([]);
  });

  // Bands render in a fixed order and empty ones are dropped, so the page never
  // shows a heading with nothing under it.
  it('bands tasks by when they are due, in a fixed order, skipping empty bands', async () => {
    await setup();
    await flush([
      task('later', { dueDate: daysFromToday(30) }),
      task('overdue', { dueDate: daysFromToday(-2) }),
      task('today', { dueDate: todayIso() }),
    ]);

    expect(groupTitles()).toEqual(['Overdue', 'Due', 'Later']);
  });

  it('keeps undated tasks in their own band rather than dropping them', async () => {
    await setup();
    await flush([task('undated')]);

    expect(groupTitles()).toEqual(['No']);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Task undated');
  });

  it('says so when nothing matches the filter, in the filter’s own words', async () => {
    await setup();
    await flush([]);

    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'Nothing is assigned to you in this workspace.',
    );
  });

  // The default is absent from the URL, the way the project board's view toggle works.
  it('writes a chosen filter to the URL and clears it again for the default', async () => {
    await setup();
    await flush([]);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    fixture.componentInstance.setFilter('overdue');
    expect(navigate.mock.calls[0][1]?.queryParams).toEqual({ filter: 'overdue' });

    fixture.componentInstance.setFilter('mine');
    expect(navigate.mock.calls[1][1]?.queryParams).toEqual({ filter: null });
  });

  /**
   * The bands are the reason TodayService exists, and until now nothing asserted that this
   * component reads it — reverting every consumer back to todayIso() left the whole frontend
   * suite green. This is the consumer-side half of that guard.
   */
  it('re-bands when the day rolls over under an open page', async () => {
    await setup('all');
    await flush([task('a', { dueDate: daysFromToday(1) })]);

    // "This week" -> first word "This".
    expect(groupTitles()).toEqual(['This']);

    // Tomorrow becomes today; the same task has to change band with the list untouched.
    today.set(daysFromToday(1));
    await fixture.whenStable();
    fixture.detectChanges();

    // "Due today" -> "Due".
    expect(groupTitles()).toEqual(['Due']);
  });
});
