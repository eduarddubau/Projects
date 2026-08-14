import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';

import { AcceptInvitationComponent } from './accept-invitation.component';
import { API_URL } from '@core/tokens/app.tokens';
import { Workspace } from '@core/models/workspace';
import { WorkspaceContextService } from '@core/services/workspace-context.service';
import { provideTranslocoTesting } from '@shared/testing/transloco-testing';

const apiUrl = 'http://api.test';
const acceptUrl = `${apiUrl}/invitations/accept`;

const acme: Workspace = {
  id: 'a1',
  name: 'Acme Team',
  isPersonal: false,
  myRole: 'Member',
  memberCount: 3,
  projectCount: 0,
  createdAt: '2026-01-01T00:00:00Z',
  isDeleted: false,
};

describe('AcceptInvitationComponent', () => {
  let fixture: ComponentFixture<AcceptInvitationComponent>;
  let httpMock: HttpTestingController;
  let context: WorkspaceContextService;

  async function setup(queryParams: Record<string, string>) {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [AcceptInvitationComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideTranslocoTesting(),
        { provide: API_URL, useValue: apiUrl },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } },
        },
      ],
    }).compileComponents();

    context = TestBed.inject(WorkspaceContextService);
    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(AcceptInvitationComponent);
    fixture.detectChanges();
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  afterEach(() => httpMock.verify());

  it('redeems the token on arrival, without waiting for a confirmation', async () => {
    await setup({ token: 'tok-123' });

    expect(httpMock.expectOne({ url: acceptUrl, method: 'POST' }).request.body).toEqual({
      token: 'tok-123',
    });
  });

  it('adds the workspace to the store and makes it current', async () => {
    await setup({ token: 'tok-123' });
    httpMock.expectOne(acceptUrl).flush(acme);
    fixture.detectChanges();

    expect(context.workspaces()).toEqual([acme]);
    expect(context.currentWorkspaceId()).toBe(acme.id);
    expect(text()).toContain('You are now a member of Acme Team.');
  });

  // Registration auto-redeems by email, so following the link afterwards is a
  // normal thing to do rather than an error; the API is idempotent and so is this.
  it('treats a second redemption as success', async () => {
    await setup({ token: 'tok-123' });
    httpMock.expectOne(acceptUrl).flush(acme);
    fixture.detectChanges();

    expect(text()).toContain("You're in");
  });

  it('explains a token that is no longer valid', async () => {
    await setup({ token: 'stale' });
    httpMock
      .expectOne(acceptUrl)
      .flush({ code: 'InvitationInvalid' }, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();

    expect(text()).toContain('not valid, or it has expired');
  });

  it('fails without asking the server when the link has no token', async () => {
    await setup({});

    // Nothing to expect; afterEach's verify() fails if a request escaped.
    expect(text()).toContain('missing its invitation token');
  });
});
