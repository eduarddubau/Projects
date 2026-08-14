import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { vi } from 'vitest';

import { InviteDialogComponent, InviteDialogResult } from './invite-dialog.component';
import { API_URL } from '@core/tokens/app.tokens';
import { provideTranslocoTesting } from '@shared/testing/transloco-testing';

const apiUrl = 'http://api.test';
const inviteUrl = `${apiUrl}/workspaces/a1/invitations`;

describe('InviteDialogComponent', () => {
  let fixture: ComponentFixture<InviteDialogComponent>;
  let httpMock: HttpTestingController;
  let dialogRef: { close: ReturnType<typeof vi.fn>; disableClose: boolean };

  async function setup() {
    dialogRef = { close: vi.fn(), disableClose: false };

    await TestBed.configureTestingModule({
      imports: [InviteDialogComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslocoTesting(),
        { provide: API_URL, useValue: apiUrl },
        { provide: MatDialogRef, useValue: dialogRef },
        { provide: MAT_DIALOG_DATA, useValue: { workspaceId: 'a1' } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(InviteDialogComponent);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  }

  function fill(email = 'new@example.com') {
    fixture.componentInstance.form.setValue({ email, role: 'Member' });
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  afterEach(() => httpMock.verify());

  it('refuses to send without a valid email', async () => {
    await setup();
    fill('not-an-email');

    fixture.componentInstance.submit();

    // Nothing to expect; afterEach's verify() fails if a request escaped.
    expect(dialogRef.close).not.toHaveBeenCalled();
  });

  // Validators.email is anchored, so an untrimmed address is invalid and submit()
  // would refuse it — the trim has to happen before validation, not at send time.
  it('trims a pasted address on blur so it validates', async () => {
    await setup();
    fill('  new@example.com  ');
    expect(fixture.componentInstance.form.invalid).toBe(true);

    fixture.componentInstance.trimEmail();
    expect(fixture.componentInstance.form.valid).toBe(true);

    fixture.componentInstance.submit();
    expect(httpMock.expectOne(inviteUrl).request.body).toEqual({
      email: 'new@example.com',
      role: 'Member',
    });
  });

  // The dialog holds the only copy of the token from here on, and a dismissal
  // mid-request would abort our side while the server may already have issued.
  it('locks the dialog before sending, not after the reply', async () => {
    await setup();
    fill();

    fixture.componentInstance.submit();

    expect(dialogRef.disableClose).toBe(true);
    httpMock.expectOne(inviteUrl).flush({ outcome: 'Invited', token: 'tok', member: null });
  });

  it('unlocks again when the invitation was refused', async () => {
    await setup();
    fill();

    fixture.componentInstance.submit();
    httpMock
      .expectOne(inviteUrl)
      .flush({ code: 'PendingInvitationExists' }, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();

    expect(dialogRef.disableClose).toBe(false);
    expect(dialogRef.close).not.toHaveBeenCalled();
    expect(text()).toContain('already has a pending invitation');
  });

  // A known address is added outright, so there is no link to show and the
  // member table behind the dialog is now stale.
  it('closes with "joined" when the address already had an account', async () => {
    await setup();
    fill('dev2@example.com');

    fixture.componentInstance.submit();
    httpMock.expectOne(inviteUrl).flush({
      outcome: 'Joined',
      token: null,
      member: { workspaceId: 'a1', userId: 'u2', userDisplayName: 'Dev Two', role: 'Member' },
    });

    expect(dialogRef.close).toHaveBeenCalledWith('joined' satisfies InviteDialogResult);
  });

  it('shows the link for an unknown address, and renders it as text', async () => {
    await setup();
    fill();

    fixture.componentInstance.submit();
    httpMock.expectOne(inviteUrl).flush({ outcome: 'Invited', token: 'tok-123', member: null });
    fixture.detectChanges();

    // Rendered, not merely copied: the clipboard needs a secure context, so this
    // is the path that always works.
    expect(text()).toContain('/invitations/accept?token=tok-123');
    expect(dialogRef.close).not.toHaveBeenCalled();
  });

  it('closes with "invited" only once the link has been dismissed', async () => {
    await setup();
    fill();

    fixture.componentInstance.submit();
    httpMock.expectOne(inviteUrl).flush({ outcome: 'Invited', token: 'tok', member: null });
    fixture.componentInstance.done();

    expect(dialogRef.close).toHaveBeenCalledWith('invited' satisfies InviteDialogResult);
  });
});
