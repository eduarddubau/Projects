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
import { WorkspaceContextService } from '@core/services/workspace-context.service';
import { WorkspaceDashboard } from '@core/models/workspace-dashboard';
import { Workspace } from '@core/models/workspace';
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

// The header reads the workspace out of the context store, which the shell loads.
const workspaces = signal<Workspace[]>([]);

function workspace(overrides: Partial<Workspace> = {}): Workspace {
  return {
    id: workspaceId,
    name: 'Acme Team',
    description: 'Design and ship the marketing site.',
    isPersonal: false,
    myRole: 'Owner',
    memberCount: 3,
    projectCount: 2,
    createdAt: '2026-06-01T09:00:00Z',
    isDeleted: false,
    ...overrides,
  };
}

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
    workspaces.set([workspace()]);
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
        {
          provide: WorkspaceContextService,
          // The page re-reads the store on entry; the stub is already current.
          useValue: { workspaces, refresh: () => of(workspaces()) },
        },
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

  it('names the workspace it is the home of, and what it is for', async () => {
    await respond(sampleDashboard, []);

    const heading = (fixture.nativeElement as HTMLElement).querySelector('h1');
    expect(heading?.textContent?.trim()).toBe('Acme Team');
    expect(text()).toContain('Design and ship the marketing site.');
  });

  it('counts members, projects and open work, and names my role', async () => {
    await respond(sampleDashboard, []);

    const tiles = (fixture.nativeElement as HTMLElement).querySelectorAll('.kpi');
    expect(tiles).toHaveLength(3);
    expect(tiles[0].textContent).toContain('3');
    expect(tiles[0].textContent).toContain('Members');
    expect(tiles[0].textContent).toContain('Owner');
    expect(tiles[1].textContent).toContain('Projects');
    expect(tiles[2].textContent).toContain('Open tasks');
  });

  // Every metric opens the page behind it. The task link carries no filter because the
  // page now opens on "all", which is the number the tile shows.
  // The count came from the workspace store before, which loads once a session: create a
  // project and the tile disagreed with the grid directly below it.
  it('counts the projects it actually loaded, not the ones the store remembers', async () => {
    // The store is deliberately behind — it says two, the page fetched three.
    await respond(sampleDashboard, [
      rocket,
      { ...rocket, id: 'p2', name: 'Second' },
      { ...rocket, id: 'p3', name: 'Third' },
    ]);

    const tiles = (fixture.nativeElement as HTMLElement).querySelectorAll('.kpi');
    expect(tiles[1].textContent).toContain('3');
    expect(tiles[1].textContent).toContain('Projects');
  });

  it('opens the page behind each metric', async () => {
    await respond(sampleDashboard, []);

    const links = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLAnchorElement>('a.kpi'),
    ).map((a) => a.getAttribute('href'));

    expect(links).toEqual([
      `/w/${workspaceId}/members`,
      `/w/${workspaceId}/projects`,
      `/w/${workspaceId}/tasks`,
    ]);
  });

  // A count of one and an Owner chip describe a space nobody else can reach.
  it('states the privacy of a personal workspace instead of counting to one', async () => {
    workspaces.set([
      workspace({
        isPersonal: true,
        name: "Dev's Workspace",
        description: undefined,
        memberCount: 1,
      }),
    ]);
    await respond(sampleDashboard, []);

    expect((fixture.nativeElement as HTMLElement).querySelector('h1')?.textContent?.trim()).toBe(
      'My Workspace',
    );
    expect(text()).toContain('Only you can see this workspace.');

    // Still three cards: a workspace of one says so, in the singular, with the role.
    const tiles = (fixture.nativeElement as HTMLElement).querySelectorAll('.kpi');
    expect(tiles).toHaveLength(3);
    expect(tiles[0].textContent).toContain('1');
    expect(tiles[0].textContent).toContain('Member');
    expect(tiles[0].textContent).not.toContain('Members');
    expect(tiles[0].textContent).toContain('Owner');
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

    // The greeting sits above the title now; the title belongs to the workspace.
    const greeting = (fixture.nativeElement as HTMLElement).querySelector('.home-greeting');
    expect(greeting?.textContent).toContain('Dev');

    const nameLink = (fixture.nativeElement as HTMLElement).querySelector('.name-link');
    expect(nameLink?.getAttribute('href')).toContain('/profile');
  });

  // Reading a resource in its error state throws, and this tile renders outside the error
  // branch that guards the grid below — so a failed fetch would blank the whole page.
  it('keeps the page up when the projects themselves fail to load', async () => {
    httpMock.expectOne(dashboardUrl).flush(sampleDashboard);
    httpMock.expectOne((r) => r.url === tasksUrl).flush([]);
    httpMock.expectOne(projectsUrl).flush('boom', { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    fixture.detectChanges();

    // The header and the other metrics survive, and the tile falls back to the store's count.
    expect((fixture.nativeElement as HTMLElement).querySelector('h1')?.textContent).toContain(
      'Acme Team',
    );
    const tiles = (fixture.nativeElement as HTMLElement).querySelectorAll('.kpi');
    expect(tiles[1].textContent).toContain('2');
    expect(text()).toContain("Couldn't load the projects.");
  });

  // The three reads are independent, so one failing must not take the others down.
  it('still shows the projects when the tiles fail to load', async () => {
    await respond(null, [rocket]);

    expect(text()).toContain("Couldn't load this workspace's numbers.");
    expect(text()).toContain('Rocket Plans');
    // Members and projects read the workspace, not the dashboard, so they survive it.
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('.kpi')).toHaveLength(2);
  });
});
