import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { BreakpointObserver } from '@angular/cdk/layout';
import { of } from 'rxjs';

import { WorkspaceShellComponent } from './workspace-shell.component';
import { API_URL } from '@core/tokens/app.tokens';
import { WorkspaceContextService } from '@core/services/workspace-context.service';
import { Workspace } from '@core/models/workspace';
import { provideTranslocoTesting } from '@shared/testing/transloco-testing';

function workspace(id: string, overrides: Partial<Workspace> = {}): Workspace {
  return {
    id,
    name: `Workspace ${id}`,
    isPersonal: false,
    myRole: 'Member',
    memberCount: 1,
    projectCount: 0,
    createdAt: '2026-01-01T00:00:00Z',
    isDeleted: false,
    ...overrides,
  };
}

describe('WorkspaceShellComponent', () => {
  let fixture: ComponentFixture<WorkspaceShellComponent>;
  let context: WorkspaceContextService;

  function setup(current: Workspace) {
    localStorage.clear();
    const paramMap = convertToParamMap({ workspaceId: current.id });

    TestBed.configureTestingModule({
      imports: [WorkspaceShellComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideTranslocoTesting(),
        { provide: API_URL, useValue: 'http://api.test' },
        // Stubbed so the layout under test is the desktop one, whatever jsdom
        // reports for a media query.
        { provide: BreakpointObserver, useValue: { observe: () => of({ matches: false }) } },
        {
          provide: ActivatedRoute,
          useValue: { paramMap: of(paramMap), snapshot: { paramMap } },
        },
      ],
    });

    context = TestBed.inject(WorkspaceContextService);
    context.upsert(current);
    context.setCurrent(current.id);

    fixture = TestBed.createComponent(WorkspaceShellComponent);
    fixture.detectChanges();
  }

  function navHrefs(): (string | null)[] {
    return Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLAnchorElement>('.ws-nav a'),
    ).map((a) => a.getAttribute('href'));
  }

  it('points every nav link at the workspace in the URL', () => {
    setup(workspace('w1', { myRole: 'Owner' }));

    expect(navHrefs()).toEqual([
      '/w/w1',
      '/w/w1/members',
      '/w/w1/settings',
      '/w/w1/projects/trash',
    ]);
  });

  it('offers settings only to someone who can manage the workspace', () => {
    setup(workspace('w1', { myRole: 'Member' }));

    expect(navHrefs()).toEqual(['/w/w1', '/w/w1/members', '/w/w1/projects/trash']);
  });

  it('seats the switcher in the sidebar', () => {
    setup(workspace('w1', { myRole: 'Owner' }));

    expect(
      (fixture.nativeElement as HTMLElement).querySelector('.ws-sidenav .ws-trigger'),
    ).not.toBeNull();
  });
});
