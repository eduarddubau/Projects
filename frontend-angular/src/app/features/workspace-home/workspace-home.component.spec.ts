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
import { provideTranslocoTesting } from '@shared/testing/transloco-testing';

const apiUrl = 'http://api.test';
const workspaceId = '99999999-9999-9999-9999-999999999999';
const dashboardUrl = `${apiUrl}/workspaces/${workspaceId}/dashboard`;
const projectsUrl = `${apiUrl}/workspaces/${workspaceId}/projects`;

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

  it('scopes both requests to the workspace in the path', async () => {
    httpMock.expectOne(dashboardUrl).flush(sampleDashboard);
    httpMock.expectOne(projectsUrl).flush([rocket]);
    await fixture.whenStable();
  });

  it('renders the two workspace tiles', async () => {
    httpMock.expectOne(dashboardUrl).flush(sampleDashboard);
    httpMock.expectOne(projectsUrl).flush([]);
    await fixture.whenStable();

    const tiles = (fixture.nativeElement as HTMLElement).querySelectorAll('.kpi');
    expect(tiles).toHaveLength(2);
    expect(text()).toContain('Open tasks');
    expect(text()).toContain('12');
    expect(text()).toContain('Assigned to me');
    expect(text()).toContain('7');
  });

  it('renders the workspace projects below the tiles', async () => {
    httpMock.expectOne(dashboardUrl).flush(sampleDashboard);
    httpMock.expectOne(projectsUrl).flush([rocket]);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(text()).toContain('Rocket Plans');
  });

  it('greets the signed-in user and links their name to the profile', async () => {
    TestBed.inject(AuthService).currentUser.set({
      id: '1',
      email: 'dev@example.com',
      firstName: 'Dev',
      lastName: 'User',
      isAdmin: false,
    });
    httpMock.expectOne(dashboardUrl).flush(sampleDashboard);
    httpMock.expectOne(projectsUrl).flush([]);
    await fixture.whenStable();

    const title = (fixture.nativeElement as HTMLElement).querySelector('.page-title');
    expect(title?.textContent).toContain('Dev');

    const nameLink = (fixture.nativeElement as HTMLElement).querySelector('.name-link');
    expect(nameLink?.getAttribute('href')).toContain('/profile');
  });

  // The counts and the projects load independently, so one failing must not take
  // the other down with it.
  it('still shows the projects when the tiles fail to load', async () => {
    httpMock.expectOne(dashboardUrl).flush('boom', { status: 500, statusText: 'Server Error' });
    httpMock.expectOne(projectsUrl).flush([rocket]);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(text()).toContain("Couldn't load this workspace's numbers.");
    expect(text()).toContain('Rocket Plans');
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('.kpi')).toHaveLength(0);
  });
});
