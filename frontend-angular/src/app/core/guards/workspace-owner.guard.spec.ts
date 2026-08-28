import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import {
  ActivatedRouteSnapshot,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';
import { workspaceOwnerGuard } from './workspace-owner.guard';
import { API_URL } from '@core/tokens/app.tokens';
import { WorkspaceContextService } from '@core/services/workspace-context.service';
import { Workspace } from '@core/models/workspace';

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

describe('workspaceOwnerGuard', () => {
  let context: WorkspaceContextService;
  let router: Router;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_URL, useValue: 'http://api.test' },
      ],
    });
    context = TestBed.inject(WorkspaceContextService);
    router = TestBed.inject(Router);
  });

  /**
   * Runs the guard against a workspace already in the context — which is the real
   * arrangement, since workspaceGuard loads the list on the parent route first.
   */
  function run(current: Workspace | null): boolean | string {
    if (current) {
      context.upsert(current);
      context.setCurrent(current.id);
    }

    const result = TestBed.runInInjectionContext(() =>
      workspaceOwnerGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
    );

    return typeof result === 'boolean' ? result : router.serializeUrl(result as UrlTree);
  }

  it('lets an owner through', () => {
    expect(run(workspace('w1', { myRole: 'Owner' }))).toBe(true);
  });

  // A personal workspace fails canManageCurrent but is still owned, and its trash
  // is the only trash its user has.
  it('lets the owner of a personal workspace through', () => {
    expect(run(workspace('w1', { myRole: 'Owner', isPersonal: true }))).toBe(true);
  });

  it('sends a plain member back to the workspace home', () => {
    expect(run(workspace('w1', { myRole: 'Member' }))).toBe('/w/w1');
  });

  // Redirecting to /w/null would re-enter the workspace tree and bounce forever.
  it('leaves the workspace tree when there is no current workspace', () => {
    expect(run(null)).toBe('/workspaces');
  });
});
