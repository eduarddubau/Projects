import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { of } from 'rxjs';

import { WorkspaceHomeComponent } from './workspace-home.component';
import { API_URL } from '@core/tokens/app.tokens';
import { AuthService } from '@core/services/auth.service';
import { ThemeService } from '@core/services/theme.service';
import { WorkspaceDashboard } from '@core/models/workspace-dashboard';
import { Project } from '@core/models/project';
import { WorkspaceTask } from '@core/models/task';
import { provideTranslocoTesting } from '@shared/testing/transloco-testing';

const apiUrl = 'http://api.test';
const workspaceId = '99999999-9999-9999-9999-999999999999';
const dashboardUrl = `${apiUrl}/workspaces/${workspaceId}/dashboard`;
const projectsUrl = `${apiUrl}/workspaces/${workspaceId}/projects`;
const tasksUrl = `${apiUrl}/workspaces/${workspaceId}/tasks`;

// The real ThemeService touches window.matchMedia, which jsdom lacks.
const themeStub = { provide: ThemeService, useValue: { theme: signal('light') } };

// The page takes its workspace from the path; without the param nothing is requested.
const routeStub = {
  paramMap: of(convertToParamMap({ workspaceId })),
  snapshot: {
    paramMap: convertToParamMap({ workspaceId }),
    queryParamMap: convertToParamMap({}),
  },
};

const sampleDashboard: WorkspaceDashboard = { openTaskCount: 12, myOpenTaskCount: 7 };

const rocket: Project = {
  workspaceId,
  workspaceName: 'Test Workspace',
  id: '22222222-2222-2222-2222-222222222222',
  name: 'Rocket Plans',
  createdAt: '2026-06-20T10:00:00Z',
  updatedAt: '2026-07-01T09:00:00Z',
  isDeleted: false,
  isPurgeable: false,
};

function task(id: string, overrides: Partial<WorkspaceTask> = {}): WorkspaceTask {
  return {
    id,
    title: `Task ${id}`,
    status: 'Todo',
    position: 0,
    projectId: rocket.id,
    projectName: rocket.name,
    createdAt: '2026-07-01T09:00:00Z',
    isDeleted: false,
    ...overrides,
  };
}

describe('WorkspaceHomeComponent', () => {
  let fixture: ComponentFixture<WorkspaceHomeComponent>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [WorkspaceHomeComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideTranslocoTesting(),
        themeStub,
        { provide: API_URL, useValue: apiUrl },
        { provide: ActivatedRoute, useValue: routeStub },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(WorkspaceHomeComponent);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => {
    // The overview's weather widget self-fetches once the page renders; fail it so it
    // leaves no open request. It's covered by its own spec.
    httpMock.match('https://ipwho.is/').forEach((r) => r.error(new ProgressEvent('offline')));
    httpMock.verify();
  });

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  /** Answers the page's three independent reads; a null dashboard fails that one. */
  async function respond(
    dashboard: WorkspaceDashboard | null,
    projects: Project[],
    tasks: WorkspaceTask[] = [],
  ) {
    const dashboardReq = httpMock.expectOne(dashboardUrl);
    if (dashboard) dashboardReq.flush(dashboard);
    else dashboardReq.flush('boom', { status: 500, statusText: 'Server Error' });

    httpMock.expectOne((r) => r.url === tasksUrl).flush(tasks);
    httpMock.expectOne(projectsUrl).flush(projects);
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('scopes every read to the workspace in the path, and asks only for my tasks', async () => {
    const tasksReq = httpMock.expectOne((r) => r.url === tasksUrl);
    expect(tasksReq.request.params.get('assignee')).toBe('me');
    tasksReq.flush([]);

    httpMock.expectOne(dashboardUrl).flush(sampleDashboard);
    httpMock.expectOne(projectsUrl).flush([rocket]);
    await fixture.whenStable();
  });

  it('renders the two workspace tiles', async () => {
    await respond(sampleDashboard, []);

    const tiles = (fixture.nativeElement as HTMLElement).querySelectorAll('.kpi');
    expect(tiles).toHaveLength(2);
    expect(text()).toContain('Open tasks');
    expect(text()).toContain('12');
    expect(text()).toContain('Assigned to me');
    expect(text()).toContain('7');
  });

  it('lists my assigned tasks, each naming the project it is in', async () => {
    await respond(sampleDashboard, [], [task('t1', { title: 'Ship the thing' })]);

    expect(text()).toContain('Ship the thing');
    expect(text()).toContain('Rocket Plans');
  });

  // Most recently touched first: the full table lives on /projects, this is a shortlist.
  it('shows the most recently updated projects, newest first', async () => {
    const stale = { ...rocket, id: 'p-stale', name: 'Stale', updatedAt: '2026-01-01T00:00:00Z' };
    const fresh = { ...rocket, id: 'p-fresh', name: 'Fresh', updatedAt: '2026-08-01T00:00:00Z' };
    await respond(sampleDashboard, [stale, fresh]);

    const names = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('.project-name'),
    ).map((n) => n.textContent?.trim());
    expect(names).toEqual(['Fresh', 'Stale']);
  });

  // A project nobody has edited still has to sort, on its creation date.
  it('falls back to the creation date for a project never updated', async () => {
    const created = { ...rocket, id: 'p-new', name: 'Never edited', updatedAt: undefined };
    await respond(sampleDashboard, [created]);

    expect(text()).toContain('Never edited');
  });

  it('greets the signed-in user and links their name to the profile', async () => {
    TestBed.inject(AuthService).currentUser.set({
      id: '1',
      email: 'dev@example.com',
      firstName: 'Dev',
      lastName: 'User',
      isAdmin: false,
    });
    await respond(sampleDashboard, []);

    const title = (fixture.nativeElement as HTMLElement).querySelector('.page-title');
    expect(title?.textContent).toContain('Dev');

    const nameLink = (fixture.nativeElement as HTMLElement).querySelector('.name-link');
    expect(nameLink?.getAttribute('href')).toContain('/profile');
  });

  // The three reads are independent, so one failing must not take the others down.
  it('still shows the projects when the tiles fail to load', async () => {
    await respond(null, [rocket]);

    expect(text()).toContain("Couldn't load this workspace's numbers.");
    expect(text()).toContain('Rocket Plans');
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('.kpi')).toHaveLength(0);
  });
});
