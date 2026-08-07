import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { WorkspaceSwitcherComponent } from './workspace-switcher.component';
import { API_URL } from '@core/tokens/app.tokens';
import { AuthService } from '@core/services/auth.service';
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

const signedInUser = {
  id: '1',
  email: 'dev1@example.com',
  firstName: 'Dev',
  lastName: 'One',
  isAdmin: false,
};

describe('WorkspaceSwitcherComponent', () => {
  let fixture: ComponentFixture<WorkspaceSwitcherComponent>;
  let httpMock: HttpTestingController;
  let auth: AuthService;
  let context: WorkspaceContextService;

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
    httpMock = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
    context = TestBed.inject(WorkspaceContextService);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  function signIn(list: Workspace[] = [personal, acme]) {
    auth.currentUser.set(signedInUser);
    fixture.detectChanges();
    httpMock.expectOne(`${apiUrl}/workspaces`).flush(list);
    fixture.detectChanges();
  }

  function triggerText(): string {
    return (fixture.nativeElement as HTMLElement).querySelector('.ws-trigger')?.textContent ?? '';
  }

  it('renders nothing and fetches nothing while signed out', () => {
    httpMock.expectNone(`${apiUrl}/workspaces`);
    expect((fixture.nativeElement as HTMLElement).querySelector('.ws-trigger')).toBeNull();
  });

  // The header is constructed once, before login, so a constructor-only load
  // would never run for a user who signs in afterwards. This pins the effect.
  it('loads the list when the user signs in after construction', () => {
    signIn();

    expect(context.workspaces().length).toBe(2);
    expect((fixture.nativeElement as HTMLElement).querySelector('.ws-trigger')).not.toBeNull();
  });

  it('labels the personal workspace from the dictionary, never from the API name', () => {
    signIn();

    expect(triggerText()).toContain('My Workspace');
    expect(triggerText()).not.toContain("dev1's Workspace");
  });

  it('selects the personal workspace when nothing is stored', () => {
    signIn();

    expect(context.currentWorkspaceId()).toBe(personal.id);
  });

  it('restores a stored selection instead of defaulting', () => {
    localStorage.setItem(StorageKeys.CURRENT_WORKSPACE_ID, acme.id);

    signIn();

    expect(context.currentWorkspaceId()).toBe(acme.id);
    expect(triggerText()).toContain('Acme Team');
  });

  it('switches and persists the choice', () => {
    signIn();

    fixture.componentInstance.select(acme.id);
    fixture.detectChanges();

    expect(triggerText()).toContain('Acme Team');
    expect(localStorage.getItem(StorageKeys.CURRENT_WORKSPACE_ID)).toBe(acme.id);
  });

  it('disappears again once the context is cleared', () => {
    signIn();

    auth.currentUser.set(null);
    context.clear();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('.ws-trigger')).toBeNull();
  });
});
