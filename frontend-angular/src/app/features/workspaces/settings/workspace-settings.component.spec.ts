import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { of } from 'rxjs';
import { vi } from 'vitest';

import { WorkspaceSettingsComponent } from './workspace-settings.component';
import { API_URL } from '@core/tokens/app.tokens';
import { Workspace } from '@core/models/workspace';
import { WorkspaceContextService } from '@core/services/workspace-context.service';
import { provideTranslocoTesting } from '@shared/testing/transloco-testing';

const apiUrl = 'http://api.test';

function workspace(overrides: Partial<Workspace> = {}): Workspace {
  return {
    id: 'a1',
    name: 'Acme Team',
    description: 'The shared one.',
    isPersonal: false,
    myRole: 'Owner',
    memberCount: 3,
    projectCount: 0,
    createdAt: '2026-01-01T00:00:00Z',
    isDeleted: false,
    ...overrides,
  };
}

describe('WorkspaceSettingsComponent', () => {
  let fixture: ComponentFixture<WorkspaceSettingsComponent>;
  let httpMock: HttpTestingController;
  let context: WorkspaceContextService;
  let router: Router;
  let dialogData: Record<string, unknown> | undefined;

  async function setup(ws: Workspace = workspace(), confirmed = true, routeId = ws.id) {
    localStorage.clear();
    dialogData = undefined;

    await TestBed.configureTestingModule({
      imports: [WorkspaceSettingsComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        // Not provideRouter([]): the delete navigates here, and an unmatched
        // route rejects out of band as an unhandled error.
        provideRouter([{ path: 'workspaces', children: [] }]),
        provideTranslocoTesting(),
        { provide: API_URL, useValue: apiUrl },
        {
          provide: MatDialog,
          useValue: {
            open: (_: unknown, config: { data: Record<string, unknown> }) => {
              dialogData = config.data;
              return { afterClosed: () => of(confirmed) };
            },
          },
        },
      ],
    }).compileComponents();

    context = TestBed.inject(WorkspaceContextService);
    router = TestBed.inject(Router);
    // The form reads this in a field initialiser, so it must precede create.
    context.upsert(ws);
    context.setCurrent(ws.id);

    fixture = TestBed.createComponent(WorkspaceSettingsComponent);
    fixture.componentRef.setInput('workspaceId', routeId);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  function saveButton(): HTMLButtonElement | null {
    return (fixture.nativeElement as HTMLElement).querySelector('button[type="submit"]');
  }

  afterEach(() => httpMock.verify());

  describe('as the owner of a shared workspace', () => {
    it('fills the form from the workspace the guard selected', async () => {
      await setup();

      expect(fixture.componentInstance.form.getRawValue()).toEqual({
        name: 'Acme Team',
        description: 'The shared one.',
      });
    });

    it('disables save until the form is dirty', async () => {
      await setup();

      expect(saveButton()!.disabled).toBe(true);

      fixture.componentInstance.form.controls.name.setValue('Renamed');
      fixture.componentInstance.form.markAsDirty();
      fixture.detectChanges();

      expect(saveButton()!.disabled).toBe(false);
    });

    it('sends the trimmed values and pushes the result into the store', async () => {
      await setup();
      fixture.componentInstance.form.setValue({ name: '  Renamed  ', description: '  ' });
      fixture.componentInstance.form.markAsDirty();

      fixture.componentInstance.save();

      const req = httpMock.expectOne(`${apiUrl}/workspaces/a1`);
      expect(req.request.method).toBe('PUT');
      // Absent, not empty string: the API treats the two differently.
      expect(req.request.body).toEqual({ name: 'Renamed', description: undefined });
      req.flush(workspace({ name: 'Renamed' }));

      // Without the upsert the old name survives in the switcher and the list.
      expect(context.currentWorkspace()?.name).toBe('Renamed');
    });

    it('keeps the typed values and disables save again after a successful save', async () => {
      await setup();
      fixture.componentInstance.form.controls.name.setValue('Renamed');
      fixture.componentInstance.form.markAsDirty();

      fixture.componentInstance.save();
      httpMock.expectOne(`${apiUrl}/workspaces/a1`).flush(workspace({ name: 'Renamed' }));
      fixture.detectChanges();

      expect(fixture.componentInstance.form.controls.name.value).toBe('Renamed');
      expect(saveButton()!.disabled).toBe(true);
    });

    it('leaves the form dirty when the save fails, so the edit is not lost', async () => {
      await setup();
      fixture.componentInstance.form.controls.name.setValue('Renamed');
      fixture.componentInstance.form.markAsDirty();

      fixture.componentInstance.save();
      httpMock
        .expectOne(`${apiUrl}/workspaces/a1`)
        .flush({ code: 'DuplicateWorkspaceName' }, { status: 409, statusText: 'Conflict' });
      fixture.detectChanges();

      expect(fixture.componentInstance.form.dirty).toBe(true);
      expect(fixture.componentInstance.isBusy()).toBe(false);
      expect(saveButton()!.disabled).toBe(false);
    });

    it('sends nothing when the form is untouched', async () => {
      await setup();

      fixture.componentInstance.save();

      httpMock.expectNone(`${apiUrl}/workspaces/a1`);
    });
  });

  describe('deleting', () => {
    it('asks for the workspace name back before it will delete', async () => {
      await setup();

      fixture.componentInstance.confirmDelete();

      expect(dialogData?.['confirmPhrase']).toBe('Acme Team');
      expect(dialogData?.['warn']).toBe(true);
      httpMock.expectOne(`${apiUrl}/workspaces/a1`).flush(null);
    });

    it('does nothing when the confirmation is dismissed', async () => {
      await setup(workspace(), false);

      fixture.componentInstance.confirmDelete();

      httpMock.expectNone(`${apiUrl}/workspaces/a1`);
    });

    // Order matters: the guard on the way out reads that list.
    it('drops the workspace from the store before navigating away', async () => {
      await setup();
      const order: string[] = [];
      vi.spyOn(context, 'remove').mockImplementation(() => void order.push('remove'));
      const navigate = vi
        .spyOn(router, 'navigate')
        .mockImplementation(() => (order.push('navigate'), Promise.resolve(true)));

      fixture.componentInstance.confirmDelete();
      httpMock.expectOne(`${apiUrl}/workspaces/a1`).flush(null);

      expect(order).toEqual(['remove', 'navigate']);
      expect(navigate).toHaveBeenCalledWith(['/workspaces']);
    });

    // Regression: the switcher used to let the URL and the store disagree.
    it('deletes the workspace in the URL, not whatever the store points at', async () => {
      await setup(workspace(), true, 'a1');
      context.upsert(workspace({ id: 'other', name: 'Other Team' }));
      context.setCurrent('other');

      fixture.componentInstance.confirmDelete();

      httpMock.expectOne(`${apiUrl}/workspaces/a1`).flush(null);
      httpMock.expectNone(`${apiUrl}/workspaces/other`);
    });
  });

  describe('when there is nothing to manage', () => {
    it('offers a member the reason and the members page instead of a form', async () => {
      await setup(workspace({ myRole: 'Member' }));

      expect(saveButton()).toBeNull();
      expect(text()).toContain('Only the owner can change these settings.');
      expect(text()).not.toContain('Danger zone');
    });

    it('explains a personal workspace rather than offering a rename it cannot do', async () => {
      await setup(workspace({ isPersonal: true, myRole: 'Owner' }));

      expect(saveButton()).toBeNull();
      expect(text()).toContain("Your personal workspace can't be renamed or deleted.");
    });

    // The stored personal name is an English possessive; untranslatable.
    it('titles a personal workspace from the dictionary, not the stored name', async () => {
      await setup(workspace({ isPersonal: true, name: "dev3's Workspace" }));

      expect(text()).toContain('My Workspace');
      expect(text()).not.toContain("dev3's Workspace");
    });
  });
});
