import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { of } from 'rxjs';
import { vi } from 'vitest';

import { MembersComponent } from './members.component';
import { API_URL } from '@core/tokens/app.tokens';
import { Invitation } from '@core/models/invitation';
import { Workspace, WorkspaceMember } from '@core/models/workspace';
import { AuthService } from '@core/services/auth.service';
import { WorkspaceContextService } from '@core/services/workspace-context.service';
import { provideTranslocoTesting } from '@shared/testing/transloco-testing';

const apiUrl = 'http://api.test';
const membersUrl = `${apiUrl}/workspaces/a1/members`;

const me = {
  id: 'u-me',
  email: 'dev1@example.com',
  firstName: 'D',
  lastName: 'One',
  isAdmin: false,
};

function workspace(overrides: Partial<Workspace> = {}): Workspace {
  return {
    id: 'a1',
    name: 'Acme Team',
    isPersonal: false,
    myRole: 'Owner',
    memberCount: 2,
    projectCount: 0,
    createdAt: '2026-01-01T00:00:00Z',
    isDeleted: false,
    ...overrides,
  };
}

function member(userId: string, overrides: Partial<WorkspaceMember> = {}): WorkspaceMember {
  return {
    workspaceId: 'a1',
    userId,
    userDisplayName: `User ${userId}`,
    role: 'Member',
    joinedAt: '2026-02-01T00:00:00Z',
    ...overrides,
  };
}

const mine = member('u-me', { userDisplayName: 'Dev One', role: 'Owner' });
const other = member('u-other', { userDisplayName: 'Dev Two' });

function invitation(id: string, overrides: Partial<Invitation> = {}): Invitation {
  return {
    id,
    workspaceId: 'a1',
    email: `${id}@example.com`,
    role: 'Member',
    createdAt: '2026-02-01T00:00:00Z',
    // Far enough out that the "days left" text is stable whenever this runs.
    expiresAt: new Date(Date.now() + 5 * 86_400_000).toISOString(),
    invitedByDisplayName: 'Dev One',
    ...overrides,
  };
}

function dialogStub(confirmed: boolean) {
  return { open: () => ({ afterClosed: () => of(confirmed) }) };
}

describe('MembersComponent', () => {
  let fixture: ComponentFixture<MembersComponent>;
  let httpMock: HttpTestingController;
  let context: WorkspaceContextService;
  let router: Router;

  async function setup(
    ws: Workspace = workspace(),
    members: WorkspaceMember[] = [mine, other],
    confirmed = true,
    invites: Invitation[] = [],
  ) {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [MembersComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideTranslocoTesting(),
        { provide: API_URL, useValue: apiUrl },
        { provide: MatDialog, useValue: dialogStub(confirmed) },
      ],
    }).compileComponents();

    context = TestBed.inject(WorkspaceContextService);
    router = TestBed.inject(Router);
    // isOwner reads the CURRENT workspace, which the guard set on the way in.
    context.upsert(ws);
    context.setCurrent(ws.id);
    TestBed.inject(AuthService).currentUser.set(me);

    fixture = TestBed.createComponent(MembersComponent);
    // withComponentInputBinding does this from the route at runtime; there is no
    // active navigation in a unit test, so set it before the first CD or the
    // resource builds its URL from a required input that was never assigned.
    fixture.componentRef.setInput('workspaceId', ws.id);
    httpMock = TestBed.inject(HttpTestingController);

    // Never await whenStable() with a request outstanding — it waits for the app
    // to settle, the request is what stops it settling, and the flush that would
    // release it is on the line after. Flush first, then let it settle.
    fixture.detectChanges();
    httpMock.expectOne(`${apiUrl}/workspaces/${ws.id}/members`).flush(members);
    // match, not expectOne: the pending resource only fires for an owner of a
    // shared workspace, and several tests are deliberately neither.
    httpMock
      .match(`${apiUrl}/workspaces/${ws.id}/invitations`)
      .forEach((req) => req.flush(invites));
    await fixture.whenStable();
    fixture.detectChanges();
  }

  function rows(): HTMLElement[] {
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr'));
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  afterEach(() => httpMock.verify());

  it('renders one row per member', async () => {
    await setup();

    expect(rows().length).toBe(2);
    expect(text()).toContain('Dev Two');
  });

  // The whole reason the component knows who you are.
  it('marks your own row and offers no actions on it', async () => {
    await setup();

    expect(rows()[0].textContent).toContain('You');
    expect(rows()[0].querySelectorAll('button').length).toBe(0);
    expect(rows()[1].querySelectorAll('button').length).toBeGreaterThan(0);
  });

  it('gives a plain member no actions at all', async () => {
    await setup(workspace({ myRole: 'Member' }));

    expect(rows().every((r) => r.querySelectorAll('button').length === 0)).toBe(true);
  });

  it('changes a role and refetches rather than editing locally', async () => {
    await setup();

    fixture.componentInstance.changeRole(other.userId, 'Owner');
    const req = httpMock.expectOne({
      url: `${membersUrl}/${other.userId}/role`,
      method: 'PATCH',
    });
    expect(req.request.body).toEqual({ role: 'Owner' });
    req.flush({ ...other, role: 'Owner' });

    // reload() schedules through an effect, so the GET is not queued until a
    // change-detection pass runs. detectChanges rather than whenStable: the
    // latter would then wait for the request it just issued.
    fixture.detectChanges();

    // The reload is the assertion: the API owns the last-owner rule, so the page
    // must not guess at the resulting state.
    httpMock.expectOne(membersUrl).flush([mine, { ...other, role: 'Owner' }]);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(rows()[1].textContent).toContain('Owner');
  });

  it('removes a member after the confirmation', async () => {
    await setup();

    fixture.componentInstance.remove(other.userId, other.userDisplayName);
    httpMock.expectOne({ url: `${membersUrl}/${other.userId}`, method: 'DELETE' }).flush(null);

    fixture.detectChanges();
    httpMock.expectOne(membersUrl).flush([mine]);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(rows().length).toBe(1);
  });

  it('does nothing when the confirmation is declined', async () => {
    await setup(workspace(), [mine, other], false);

    fixture.componentInstance.remove(other.userId, other.userDisplayName);

    // Nothing to expect; afterEach's verify() fails if a request escaped.
    expect(rows().length).toBe(2);
  });

  it('leaving drops the workspace from the store and navigates away', async () => {
    await setup();
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    fixture.componentInstance.leave();
    httpMock.expectOne({ url: `${membersUrl}/leave`, method: 'POST' }).flush(null);

    expect(context.workspaces().some((w) => w.id === 'a1')).toBe(false);
    expect(navigate).toHaveBeenCalledWith(['/workspaces']);
  });

  // Refetching the members of a workspace you just left would 403.
  it('does not reload the list after leaving', async () => {
    await setup();
    vi.spyOn(router, 'navigate').mockResolvedValue(true);

    fixture.componentInstance.leave();
    httpMock.expectOne({ url: `${membersUrl}/leave`, method: 'POST' }).flush(null);

    // The CD pass is what makes this an assertion: reload() queues its GET from
    // an effect, so checking before one runs would pass whether or not the code
    // reloads. Mutation-checked — without this line, dropping the else branch in
    // mutate() fails nothing.
    fixture.detectChanges();
    httpMock.expectNone(membersUrl);
  });

  it('hides the leave button on a personal workspace', async () => {
    await setup(workspace({ isPersonal: true }), [mine]);

    expect(text()).not.toContain('Leave workspace');
  });

  // The gate lives in the resource's URL function, not in the template, because
  // httpResource fetches from an effect whether or not anything renders it.
  // Without it every plain member's page load collects a 403.
  it('never asks for pending invitations when you are not an owner', async () => {
    await setup(workspace({ myRole: 'Member' }));

    httpMock.expectNone(`${apiUrl}/workspaces/a1/invitations`);
    expect(text()).not.toContain('Pending invitations');
  });

  it('never asks for them on a personal workspace either', async () => {
    await setup(workspace({ isPersonal: true }), [mine]);

    httpMock.expectNone(`${apiUrl}/workspaces/a1/invitations`);
  });

  it('renders the pending invitations an owner has outstanding', async () => {
    await setup(workspace(), [mine, other], true, [invitation('inv-1')]);

    expect(text()).toContain('Pending invitations');
    expect(text()).toContain('inv-1@example.com');
    expect(text()).toContain('in 5 days');
  });

  it('revoking refetches the invitations and leaves the members alone', async () => {
    await setup(workspace(), [mine, other], true, [invitation('inv-1')]);

    fixture.componentInstance.revoke('inv-1');
    httpMock
      .expectOne({ url: `${apiUrl}/workspaces/a1/invitations/inv-1`, method: 'DELETE' })
      .flush(null);

    fixture.detectChanges();
    httpMock.expectOne(`${apiUrl}/workspaces/a1/invitations`).flush([]);
    // The member table is untouched: revoking is not a membership change.
    httpMock.expectNone(membersUrl);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(text()).not.toContain('Pending invitations');
  });

  // The API refuses to strip the last owner; the page must not reload or edit
  // rows on a refusal.
  it('leaves the rows alone when the server refuses', async () => {
    await setup();

    fixture.componentInstance.changeRole(mine.userId, 'Member');
    httpMock
      .expectOne({ url: `${membersUrl}/${mine.userId}/role`, method: 'PATCH' })
      .flush({ code: 'WorkspaceMustHaveOwner' }, { status: 409, statusText: 'Conflict' });
    await fixture.whenStable();
    fixture.detectChanges();

    httpMock.expectNone(membersUrl);
    expect(rows().length).toBe(2);
  });
});
