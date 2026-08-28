import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, UrlTree, provideRouter } from '@angular/router';
import { vi } from 'vitest';

import { WorkspaceSwitcherComponent } from './workspace-switcher.component';
import { API_URL } from '@core/tokens/app.tokens';
import { WorkspaceContextService } from '@core/services/workspace-context.service';
import { StorageKeys } from '@core/utils/storage-keys';
import { Workspace } from '@core/models/workspace';
import { provideTranslocoTesting } from '@shared/testing/transloco-testing';

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

// The stored name is an untranslatable English possessive; the UI must never
// show it. Naming it here makes the assertion below mean something.
const personal = workspace('p1', { name: "dev1's Workspace", isPersonal: true, myRole: 'Owner' });
const acme = workspace('a1', { name: 'Acme Team', myRole: 'Owner' });

describe('WorkspaceSwitcherComponent', () => {
  let fixture: ComponentFixture<WorkspaceSwitcherComponent>;
  let context: WorkspaceContextService;
  let router: Router;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [WorkspaceSwitcherComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideTranslocoTesting(),
        { provide: API_URL, useValue: apiUrl },
      ],
    });

    fixture = TestBed.createComponent(WorkspaceSwitcherComponent);
    context = TestBed.inject(WorkspaceContextService);
    router = TestBed.inject(Router);
    fixture.detectChanges();
  });

  // The workspace routes load the list before the sidebar exists, so the
  // switcher renders what the store already holds and never fetches.
  function seed(list: Workspace[] = [personal, acme], current = personal.id) {
    list.forEach((w) => context.upsert(w));
    context.setCurrent(current);
    fixture.detectChanges();
  }

  function triggerText(): string {
    return (fixture.nativeElement as HTMLElement).querySelector('.ws-trigger')?.textContent ?? '';
  }

  it('renders nothing while the store is empty', () => {
    expect((fixture.nativeElement as HTMLElement).querySelector('.ws-trigger')).toBeNull();
  });

  it('labels the personal workspace from the dictionary, never from the API name', () => {
    seed();

    expect(triggerText()).toContain('My Workspace');
    expect(triggerText()).not.toContain("dev1's Workspace");
  });

  it('names the workspace the store has selected', () => {
    seed([personal, acme], acme.id);

    expect(triggerText()).toContain('Acme Team');
  });

  it('switches and persists the choice', () => {
    seed();

    fixture.componentInstance.select(acme.id);
    fixture.detectChanges();

    expect(triggerText()).toContain('Acme Team');
    expect(localStorage.getItem(StorageKeys.CURRENT_WORKSPACE_ID)).toBe(acme.id);
  });

  // A store-only selection leaves the URL — and every call keyed on it — behind.
  it('carries the current page over to the workspace it switches to', () => {
    seed();
    vi.spyOn(router, 'url', 'get').mockReturnValue(`/w/${personal.id}/members`);
    const navigate = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    fixture.componentInstance.select(acme.id);

    expect(router.serializeUrl(navigate.mock.calls[0][0] as UrlTree)).toBe(`/w/${acme.id}/members`);
  });

  it('does not navigate from a page that has no workspace in its URL', () => {
    seed();
    vi.spyOn(router, 'url', 'get').mockReturnValue('/dashboard');
    const navigate = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    fixture.componentInstance.select(acme.id);

    expect(navigate).not.toHaveBeenCalled();
    expect(context.currentWorkspaceId()).toBe(acme.id);
  });

  it('disappears again once the context is cleared', () => {
    seed();

    context.clear();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('.ws-trigger')).toBeNull();
  });
});
