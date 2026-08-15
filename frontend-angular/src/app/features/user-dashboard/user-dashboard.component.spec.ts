import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';

import { UserDashboardComponent } from './user-dashboard.component';
import { API_URL } from '@core/tokens/app.tokens';
import { AuthService } from '@core/services/auth.service';
import { ThemeService } from '@core/services/theme.service';
import { UserDashboard } from '@core/models/user-dashboard';
import { provideTranslocoTesting } from '@shared/testing/transloco-testing';

const apiUrl = 'http://api.test';

// The real ThemeService touches window.matchMedia, which jsdom lacks.
const themeStub = { provide: ThemeService, useValue: { theme: signal('light') } };

const sampleDashboard: UserDashboard = {
  activeProjectCount: 3,
  deletedProjectCount: 1,
  lastActivityAt: '2026-07-01T09:00:00Z',
  recentProjects: [
    {
      workspaceId: '99999999-9999-9999-9999-999999999999',
      workspaceName: 'Test Workspace',
      id: '22222222-2222-2222-2222-222222222222',
      name: 'Rocket Plans',
      createdAt: '2026-06-20T10:00:00Z',
      updatedAt: '2026-07-01T09:00:00Z',
      isDeleted: false,
      isPurgeable: false,
    },
  ],
};

describe('UserDashboardComponent', () => {
  let fixture: ComponentFixture<UserDashboardComponent>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [UserDashboardComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideTranslocoTesting(),
        themeStub,
        { provide: API_URL, useValue: apiUrl },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(UserDashboardComponent);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => {
    // The overview's weather widget self-fetches once the dashboard renders;
    // fail it so it leaves no open request. It's covered by its own spec.
    httpMock.match('https://ipwho.is/').forEach((r) => r.error(new ProgressEvent('offline')));
    httpMock.verify();
  });

  it('renders the stats and recent projects', async () => {
    httpMock.expectOne(`${apiUrl}/dashboard`).flush(sampleDashboard);
    await fixture.whenStable();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Active Projects');
    expect(text).toContain('3');
    expect(text).toContain('In Trash');
    expect(text).toContain('Rocket Plans');
  });

  it('greets the signed-in user and links their name to the profile', async () => {
    TestBed.inject(AuthService).currentUser.set({
      id: '1',
      email: 'dev@example.com',
      firstName: 'Dev',
      lastName: 'User',
      isAdmin: false,
    });
    httpMock.expectOne(`${apiUrl}/dashboard`).flush(sampleDashboard);
    await fixture.whenStable();

    const title = (fixture.nativeElement as HTMLElement).querySelector('.page-title');
    expect(title?.textContent).toContain('Dev');

    const nameLink = (fixture.nativeElement as HTMLElement).querySelector('.name-link');
    expect(nameLink?.getAttribute('href')).toContain('/profile');
  });

  it('shows the error state when loading fails', async () => {
    httpMock
      .expectOne(`${apiUrl}/dashboard`)
      .flush('boom', { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Failed to load dashboard data.');
  });
});
