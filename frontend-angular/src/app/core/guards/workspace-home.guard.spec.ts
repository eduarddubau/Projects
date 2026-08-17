import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import {
  ActivatedRouteSnapshot,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';
import { Observable } from 'rxjs';
import { workspaceHomeGuard } from './workspace-home.guard';
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

@Component({ template: 'home' })
class StubHomeComponent {}

describe('workspaceHomeGuard', () => {
  let httpMock: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        // Mirrors the real config: /dashboard carries the guard and renders nothing.
        provideRouter([
          { path: 'dashboard', canActivate: [workspaceHomeGuard], children: [] },
          { path: 'w/:workspaceId', component: StubHomeComponent },
          { path: 'workspaces', component: StubHomeComponent },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_URL, useValue: apiUrl },
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => httpMock.verify());

  /** Runs the guard directly and answers the workspace list it asks for. */
  function run(list: Workspace[] = [personal, acme]): string {
    let result!: boolean | UrlTree;
    (
      TestBed.runInInjectionContext(() =>
        workspaceHomeGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
      ) as Observable<boolean | UrlTree>
    ).subscribe((value) => (result = value));

    httpMock.expectOne(`${apiUrl}/workspaces`).flush(list);

    if (typeof result === 'boolean') throw new Error(`expected a redirect, got ${result}`);
    return router.serializeUrl(result);
  }

  it('forwards to the personal workspace when nothing is stored', () => {
    expect(run()).toBe('/w/personal-1');
  });

  it('forwards to the stored workspace when there is one', () => {
    localStorage.setItem(StorageKeys.CURRENT_WORKSPACE_ID, acme.id);

    expect(run()).toBe('/w/acme-1');
  });

  // A stored id survives leaving a workspace, and /w/{gone} would bounce through
  // workspaceGuard on arrival. Resolving against the loaded list avoids the round trip.
  it('ignores a stored workspace that is no longer in the list', () => {
    localStorage.setItem(StorageKeys.CURRENT_WORKSPACE_ID, 'left-this-one');

    expect(run()).toBe('/w/personal-1');
  });

  it('offers the workspace list when there are no workspaces', () => {
    expect(run([])).toBe('/workspaces');
  });

  // Without this the observable errors, the navigation rejects, and a caller that
  // does not catch — login's post-success redirect, for one — leaves the user on the
  // form they just submitted, with this route rendering nothing behind it.
  it('sends the caller somewhere with an error state when the list fails', () => {
    let result!: boolean | UrlTree;
    (
      TestBed.runInInjectionContext(() =>
        workspaceHomeGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
      ) as Observable<boolean | UrlTree>
    ).subscribe((value) => (result = value));

    httpMock
      .expectOne(`${apiUrl}/workspaces`)
      .flush('boom', { status: 500, statusText: 'Server Error' });

    expect(router.serializeUrl(result as UrlTree)).toBe('/workspaces');
  });

  // The route has no component, so if the guard ever admitted a navigation there
  // would be nothing to render. This drives the real router to prove it redirects.
  it('never lands on /dashboard itself', async () => {
    const navigation = router.navigateByUrl('/dashboard');
    // A macrotask, not Promise.resolve(): the router reaches the guard — and so the
    // guard reaches ensureLoaded() — a full task after navigateByUrl returns.
    await new Promise((resolve) => setTimeout(resolve, 0));
    httpMock.expectOne(`${apiUrl}/workspaces`).flush([personal, acme]);
    await navigation;

    expect(router.url).toBe('/w/personal-1');
  });
});
