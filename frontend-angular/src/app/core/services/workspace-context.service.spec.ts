import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { WorkspaceContextService } from './workspace-context.service';
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
const other = workspace('other-1', { name: 'Other Team' });

describe('WorkspaceContextService', () => {
  let service: WorkspaceContextService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_URL, useValue: apiUrl },
      ],
    });
    service = TestBed.inject(WorkspaceContextService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  function load(list: Workspace[] = [personal, acme]) {
    service.ensureLoaded().subscribe();
    httpMock.expectOne(`${apiUrl}/workspaces`).flush(list);
  }

  describe('ensureLoaded', () => {
    it('issues one request for concurrent callers', () => {
      service.ensureLoaded().subscribe();
      service.ensureLoaded().subscribe();

      // expectOne fails outright if a second request was issued.
      httpMock.expectOne(`${apiUrl}/workspaces`).flush([personal]);
      expect(service.workspaces()).toEqual([personal]);
    });

    it('serves later callers from cache without a request', () => {
      load();

      service.ensureLoaded().subscribe();

      // Nothing to expect; afterEach's verify() fails if a request escaped.
      expect(service.workspaces().length).toBe(2);
    });

    it('retries after a failed load rather than caching the failure', () => {
      service.ensureLoaded().subscribe({
        error: () => {
          /* The failure is the subject of this test. */
        },
      });
      httpMock
        .expectOne(`${apiUrl}/workspaces`)
        .flush('boom', { status: 500, statusText: 'Server Error' });

      service.ensureLoaded().subscribe();
      httpMock.expectOne(`${apiUrl}/workspaces`).flush([personal]);

      expect(service.workspaces()).toEqual([personal]);
    });

    it('refetches after refresh()', () => {
      load();

      service.refresh().subscribe();
      httpMock.expectOne(`${apiUrl}/workspaces`).flush([personal, acme, other]);

      expect(service.workspaces().length).toBe(3);
    });
  });

  describe('resolve', () => {
    it('keeps a url id the caller is a member of', () => {
      load();
      expect(service.resolve(acme.id)).toBe(acme.id);
    });

    // The redirect the guard performs is exactly this: resolve returns something
    // other than what was asked for.
    it('falls back to personal when the url names a workspace you are not in', () => {
      load();
      expect(service.resolve('not-a-member')).toBe(personal.id);
    });

    it('prefers the url over a stored id', () => {
      localStorage.setItem(StorageKeys.CURRENT_WORKSPACE_ID, acme.id);
      load();

      expect(service.resolve(other.id === acme.id ? personal.id : acme.id)).toBe(acme.id);
      expect(service.resolve(personal.id)).toBe(personal.id);
    });

    it('uses the stored id only when the url is silent', () => {
      localStorage.setItem(StorageKeys.CURRENT_WORKSPACE_ID, acme.id);
      load();

      expect(service.resolve(null)).toBe(acme.id);
    });

    it('ignores a stored id that is no longer a membership', () => {
      localStorage.setItem(StorageKeys.CURRENT_WORKSPACE_ID, 'left-this-one');
      load();

      expect(service.resolve(null)).toBe(personal.id);
    });

    it('falls back to the first workspace when there is no personal one', () => {
      load([acme, other]);
      expect(service.resolve(null)).toBe(acme.id);
    });

    it('returns null only when there are no workspaces at all', () => {
      load([]);
      expect(service.resolve(null)).toBeNull();
    });
  });

  describe('current workspace', () => {
    it('derives the workspace, role and ownership from the selected id', () => {
      load();

      service.setCurrent(acme.id);

      expect(service.currentWorkspace()).toEqual(acme);
      expect(service.myRole()).toBe('Owner');
      expect(service.isOwner()).toBe(true);
    });

    it('reports Member roles as not owning', () => {
      load([personal, other]);

      service.setCurrent(other.id);

      expect(service.myRole()).toBe('Member');
      expect(service.isOwner()).toBe(false);
    });

    it('persists the selection', () => {
      load();
      service.setCurrent(acme.id);

      expect(localStorage.getItem(StorageKeys.CURRENT_WORKSPACE_ID)).toBe(acme.id);
    });
  });

  describe('clear', () => {
    it('drops the list, the selection and the stored id', () => {
      load();
      service.setCurrent(acme.id);

      service.clear();

      expect(service.workspaces()).toEqual([]);
      expect(service.currentWorkspaceId()).toBeNull();
      expect(service.currentWorkspace()).toBeNull();
      expect(localStorage.getItem(StorageKeys.CURRENT_WORKSPACE_ID)).toBeNull();
    });

    // Without resetting the cache flag the next user inherits this user's list.
    it('makes the next load fetch again', () => {
      load();
      service.clear();

      service.ensureLoaded().subscribe();
      httpMock.expectOne(`${apiUrl}/workspaces`).flush([personal]);

      expect(service.workspaces()).toEqual([personal]);
    });
  });

  describe('local list updates', () => {
    it('appends an unknown workspace', () => {
      load([personal]);

      service.upsert(acme);

      expect(service.workspaces()).toEqual([personal, acme]);
    });

    it('replaces a known workspace in place', () => {
      load();

      service.upsert({ ...acme, name: 'Renamed' });

      expect(service.workspaces().length).toBe(2);
      expect(service.workspaces()[1].name).toBe('Renamed');
    });

    // Signals compare by reference, so an in-place mutation would update nothing.
    it('produces a new array so dependents recompute', () => {
      load();
      const before = service.workspaces();

      service.upsert({ ...acme, name: 'Renamed' });

      expect(service.workspaces()).not.toBe(before);
      expect(before[1].name).toBe('Acme Team');
    });

    it('removes a workspace', () => {
      load();

      service.remove(acme.id);

      expect(service.workspaces()).toEqual([personal]);
    });

    it('leaves the current id alone when a different workspace is removed', () => {
      load([personal, acme, other]);
      service.setCurrent(acme.id);

      service.remove(other.id);

      expect(service.currentWorkspaceId()).toBe(acme.id);
    });

    // The store owns the invariant "currentWorkspaceId names a workspace still
    // in the list, or is null". It used to be every caller's job to restore it,
    // which meant one forgetful caller away from a current workspace of null.
    it('repoints the current id when the current workspace is removed', () => {
      load();
      service.setCurrent(acme.id);

      service.remove(acme.id);

      expect(service.currentWorkspaceId()).toBe(personal.id);
      expect(service.currentWorkspace()).toEqual(personal);
      expect(localStorage.getItem(StorageKeys.CURRENT_WORKSPACE_ID)).toBe(personal.id);
    });

    // Computing the fallback before the list update would hand back the very
    // workspace that was just removed.
    it('never falls back to the workspace it just removed', () => {
      load([acme, other]);
      service.setCurrent(acme.id);

      service.remove(acme.id);

      expect(service.currentWorkspaceId()).toBe(other.id);
    });

    it('clears the current id, and the stored one, when the last workspace goes', () => {
      load([acme]);
      service.setCurrent(acme.id);

      service.remove(acme.id);

      expect(service.currentWorkspaceId()).toBeNull();
      expect(localStorage.getItem(StorageKeys.CURRENT_WORKSPACE_ID)).toBeNull();
    });
  });
});
