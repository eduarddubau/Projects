import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import {
  ActivatedRouteSnapshot,
  Router,
  RouterStateSnapshot,
  UrlTree,
  convertToParamMap,
  provideRouter,
} from '@angular/router';
import { Observable } from 'rxjs';
import { vi } from 'vitest';
import { workspaceGuard } from './workspace.guard';
import { WorkspaceContextService } from '@core/services/workspace-context.service';
import { API_URL } from '@core/tokens/app.tokens';
import { StorageKeys } from '@core/utils/storage-keys';
import { Workspace } from '@core/models/workspace';

const apiUrl = 'http://api.test';

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

const personal = workspace('personal-1', { isPersonal: true, myRole: 'Owner' });
const acme = workspace('acme-1', { name: 'Acme Team', myRole: 'Owner' });

describe('workspaceGuard', () => {
  let context: WorkspaceContextService;
  let httpMock: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_URL, useValue: apiUrl },
      ],
    });
    context = TestBed.inject(WorkspaceContextService);
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => httpMock.verify());

  function invoke(workspaceId: string | null, url: string): Observable<boolean | UrlTree> {
    const route = {
      paramMap: convertToParamMap(workspaceId === null ? {} : { workspaceId }),
    } as ActivatedRouteSnapshot;

    return TestBed.runInInjectionContext(() =>
      workspaceGuard(route, { url } as RouterStateSnapshot),
    ) as Observable<boolean | UrlTree>;
  }

  /** Runs the guard and answers the workspace list it asks for. */
  function run(
    workspaceId: string | null,
    url: string,
    list: Workspace[] = [personal, acme],
  ): boolean | UrlTree {
    let result!: boolean | UrlTree;
    invoke(workspaceId, url).subscribe((value) => (result = value));
    httpMock.expectOne(`${apiUrl}/workspaces`).flush(list);
    return result;
  }

  function redirectedTo(result: boolean | UrlTree): string {
    if (typeof result === 'boolean') throw new Error(`expected a redirect, got ${result}`);
    return router.serializeUrl(result);
  }

  it('admits a member of the workspace named in the url', () => {
    expect(run(acme.id, '/w/acme-1/members')).toBe(true);
  });

  it('makes that workspace current, and persists it', () => {
    run(acme.id, '/w/acme-1/members');

    expect(context.currentWorkspaceId()).toBe(acme.id);
    expect(localStorage.getItem(StorageKeys.CURRENT_WORKSPACE_ID)).toBe(acme.id);
  });

  // The rest of the path surviving is the whole reason this guard rebuilds the
  // tree instead of returning parseUrl('/w/' + best).
  it('redirects an unknown workspace to the fallback, keeping the rest of the path', () => {
    expect(redirectedTo(run('bogus', '/w/bogus/members'))).toBe('/w/personal-1/members');
  });

  it('carries query params and the fragment through the redirect', () => {
    expect(redirectedTo(run('bogus', '/w/bogus/members?tab=pending#roster'))).toBe(
      '/w/personal-1/members?tab=pending#roster',
    );
  });

  // Only the second pass, arriving with a good id, switches workspace. Doing it
  // on the redirect as well would reach the same end state by writing
  // localStorage twice for one navigation.
  it('does not switch workspace on the pass that redirects', () => {
    run('bogus', '/w/bogus/members');

    expect(context.currentWorkspaceId()).toBeNull();
  });

  // Redirecting anywhere under /w with nothing to resolve to would re-enter this
  // guard forever.
  it('leaves the /w tree entirely when there are no workspaces', () => {
    expect(redirectedTo(run('bogus', '/w/bogus/members', []))).toBe('/workspaces');
  });

  // resolve() reads the loaded list synchronously, so calling it before the load
  // emits would see an empty list and redirect every navigation to /workspaces.
  it('resolves only after the list has arrived', () => {
    const resolve = vi.spyOn(context, 'resolve');

    invoke(acme.id, '/w/acme-1/members').subscribe();
    expect(resolve).not.toHaveBeenCalled();

    httpMock.expectOne(`${apiUrl}/workspaces`).flush([personal, acme]);
    expect(resolve).toHaveBeenCalledWith(acme.id);
  });

  // ensureLoaded's cache is what stops a guard on every /w navigation from being
  // an HTTP call on every /w navigation.
  it('does not refetch the list on a later navigation', () => {
    run(acme.id, '/w/acme-1/members');

    let second!: boolean | UrlTree;
    invoke(personal.id, '/w/personal-1/members').subscribe((value) => (second = value));

    // Nothing to expect; afterEach's verify() fails if a request escaped.
    expect(second).toBe(true);
  });
});
