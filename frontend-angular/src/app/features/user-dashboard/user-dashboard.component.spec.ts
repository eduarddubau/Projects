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

    // The embedded weather widget self-fetches on init; fail it fast so it
    // leaves no open request for verify(). It's covered by its own spec.
    httpMock.match('https://ipwho.is/').forEach((r) => r.error(new ProgressEvent('offline')));
  });

  afterEach(() => httpMock.verify());

  it('renders the stats and recent projects', () => {
    httpMock.expectOne(`${apiUrl}/dashboard`).flush(sampleDashboard);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Active Projects');
    expect(text).toContain('3');
    expect(text).toContain('In Trash');
    expect(text).toContain('Rocket Plans');
  });

  it('greets the signed-in user in the header with role and profile link', () => {
    TestBed.inject(AuthService).currentUser.set({
      id: '1',
      email: 'dev@example.com',
      firstName: 'Dev',
      lastName: 'User',
      isAdmin: false,
    });
    httpMock.expectOne(`${apiUrl}/dashboard`).flush(sampleDashboard);
    fixture.detectChanges();

    const title = (fixture.nativeElement as HTMLElement).querySelector('.page-title');
    expect(title?.textContent).toContain('Dev');

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Member');
    expect(text).toContain('View profile');
  });

  it('shows the error state when loading fails', () => {
    httpMock.expectOne(`${apiUrl}/dashboard`).flush('boom', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Failed to load dashboard data.');
  });
});
