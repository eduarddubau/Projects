import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { of } from 'rxjs';

import { WorkspacesComponent } from './workspaces.component';
import { API_URL } from '@core/tokens/app.tokens';
import { Workspace } from '@core/models/workspace';
import { WorkspaceContextService } from '@core/services/workspace-context.service';
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

// The stored name of a personal workspace is an untranslatable English
// possessive; naming it here is what makes the assertion below mean something.
const personal = workspace('p1', { name: "dev1's Workspace", isPersonal: true, myRole: 'Owner' });
const acme = workspace('a1', { name: 'Acme Team', myRole: 'Owner', memberCount: 3 });

function dialogStub(result: unknown) {
  return { open: () => ({ afterClosed: () => of(result) }) };
}

describe('WorkspacesComponent', () => {
  let fixture: ComponentFixture<WorkspacesComponent>;
  let httpMock: HttpTestingController;
  let context: WorkspaceContextService;

  async function setup(
    list: Workspace[] = [personal, acme],
    dialogResult: unknown = undefined,
    failLoad = false,
  ) {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [WorkspacesComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideTranslocoTesting(),
        { provide: API_URL, useValue: apiUrl },
        { provide: MatDialog, useValue: dialogStub(dialogResult) },
      ],
    }).compileComponents();

    context = TestBed.inject(WorkspaceContextService);
    fixture = TestBed.createComponent(WorkspacesComponent);
    httpMock = TestBed.inject(HttpTestingController);
    const load = httpMock.expectOne(`${apiUrl}/workspaces`);
    if (failLoad) load.flush('boom', { status: 500, statusText: 'Server Error' });
    else load.flush(list);
    fixture.detectChanges();
  }

  function cards(): HTMLElement[] {
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.ws-card'));
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  afterEach(() => httpMock.verify());

  // Nothing else loads the list on the way here, so the page has to.
  it('fetches the list and renders one card per workspace', async () => {
    await setup();

    expect(cards().length).toBe(2);
    expect(text()).toContain('Acme Team');
  });

  it('labels the personal workspace from the dictionary, never from the API name', async () => {
    await setup();

    expect(text()).toContain('My Workspace');
    expect(text()).not.toContain("dev1's Workspace");
  });

  it('marks exactly one card as current', async () => {
    await setup();

    const current = cards().filter((c) => c.classList.contains('is-current'));
    expect(current.length).toBe(1);
    expect(current[0].getAttribute('aria-current')).toBe('true');
  });

  // The page is a chooser: picking a workspace has to take you into it, and the
  // route guard is what makes the choice current.
  it('links each card to its workspace home', async () => {
    await setup();

    expect(cards().map((c) => c.getAttribute('href'))).toEqual([
      `/w/${personal.id}`,
      `/w/${acme.id}`,
    ]);
  });

  // A failed load must not read as "you have none" — the home guard sends people
  // here precisely when it could not load them.
  it('shows an error state when the list fails to load', async () => {
    await setup([], undefined, true);

    expect(cards().length).toBe(0);
    expect(text()).toContain('Failed to load your workspaces.');
  });

  it('shows the empty state when there are no workspaces', async () => {
    await setup([]);

    expect(cards().length).toBe(0);
    expect(text()).toContain("You don't have any workspaces yet.");
  });

  // Proves the dialog -> POST -> store wiring reaches the rendered list. Each
  // half can be broken independently and still compile.
  it('adds a created workspace to the list and switches to it', async () => {
    await setup([personal], { name: 'Gamma', description: 'New one' });

    fixture.componentInstance.openCreateDialog();
    const created = workspace('g1', { name: 'Gamma', myRole: 'Owner' });
    const req = httpMock.expectOne({ url: `${apiUrl}/workspaces`, method: 'POST' });
    expect(req.request.body).toEqual({ name: 'Gamma', description: 'New one' });
    req.flush(created);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(cards().length).toBe(2);
    expect(text()).toContain('Gamma');
    expect(context.currentWorkspaceId()).toBe(created.id);
  });

  it('leaves the list untouched when creation fails', async () => {
    await setup([personal], { name: 'Gamma' });

    fixture.componentInstance.openCreateDialog();
    httpMock
      .expectOne({ url: `${apiUrl}/workspaces`, method: 'POST' })
      .flush('boom', { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(cards().length).toBe(1);
    expect(text()).not.toContain('Gamma');
  });
});
