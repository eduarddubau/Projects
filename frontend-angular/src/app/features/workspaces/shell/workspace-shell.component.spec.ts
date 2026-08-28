import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, Router, Routes, convertToParamMap, provideRouter } from '@angular/router';
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

  function setup(current: Workspace, routes: Routes = []) {
    localStorage.clear();
    const paramMap = convertToParamMap({ workspaceId: current.id });

    TestBed.configureTestingModule({
      imports: [WorkspaceShellComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter(routes),
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
      '/w/w1/projects',
      '/w/w1/tasks',
      '/w/w1/members',
      '/w/w1/settings',
      '/w/w1/trash',
    ]);
  });

  // Trash goes with them: restoring a project is owner-only, so a member was being
  // shown a page on which every button refused.
  it('offers settings and trash only to an owner', () => {
    setup(workspace('w1', { myRole: 'Member' }));

    expect(navHrefs()).toEqual(['/w/w1', '/w/w1/projects', '/w/w1/tasks', '/w/w1/members']);
  });

  // A personal workspace has no settings to manage, but its trash is still yours.
  it('keeps trash in a personal workspace, where settings has nothing to offer', () => {
    setup(workspace('w1', { myRole: 'Owner', isPersonal: true }));

    expect(navHrefs()).toEqual([
      '/w/w1',
      '/w/w1/projects',
      '/w/w1/tasks',
      '/w/w1/members',
      '/w/w1/trash',
    ]);
  });

  /**
   * The highlight rule was wrong in both directions while the trash lived at
   * /projects/trash — prefix matching lit both rows, exact matching lit neither on a
   * project's own page. Moving the trash out from under /projects is what fixed it, so
   * these pin the behaviour that move bought.
   */
  describe('the Projects row highlight', () => {
    async function highlightedAt(url: string): Promise<boolean> {
      setup(workspace('w1', { myRole: 'Owner' }), [
        { path: 'w/:workspaceId/projects', children: [] },
        { path: 'w/:workspaceId/trash', children: [] },
        { path: 'w/:workspaceId/projects/:id', children: [] },
      ]);

      await TestBed.inject(Router).navigateByUrl(url);
      fixture.detectChanges();

      const projects = (fixture.nativeElement as HTMLElement).querySelector(
        'a[href="/w/w1/projects"]',
      );
      return projects?.classList.contains('active') ?? false;
    }

    it('lights on the projects list', async () => {
      expect(await highlightedAt('/w/w1/projects')).toBe(true);
    });

    it("lights on a project's own page", async () => {
      expect(await highlightedAt('/w/w1/projects/abc')).toBe(true);
    });

    it('stays dark on the trash, which is no longer under it', async () => {
      expect(await highlightedAt('/w/w1/trash')).toBe(false);
    });
  });

  it('seats the switcher in the sidebar', () => {
    setup(workspace('w1', { myRole: 'Owner' }));

    expect(
      (fixture.nativeElement as HTMLElement).querySelector('.ws-sidenav .ws-trigger'),
    ).not.toBeNull();
  });
});
