import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

import { WorkspaceScopeComponent } from './workspace-scope.component';
import { API_URL } from '@core/tokens/app.tokens';
import { WorkspaceContextService } from '@core/services/workspace-context.service';
import { Workspace } from '@core/models/workspace';
import { provideTranslocoTesting } from '@shared/testing/transloco-testing';

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

const personal = workspace('p1', { name: "dev1's Workspace", isPersonal: true, myRole: 'Owner' });
const acme = workspace('a1', { name: 'Acme Team', myRole: 'Owner' });

describe('WorkspaceScopeComponent', () => {
  let fixture: ComponentFixture<WorkspaceScopeComponent>;
  let context: WorkspaceContextService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [WorkspaceScopeComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslocoTesting(),
        { provide: API_URL, useValue: 'http://api.test' },
      ],
    });

    context = TestBed.inject(WorkspaceContextService);
    [personal, acme].forEach((w) => context.upsert(w));
    context.setCurrent(personal.id);

    fixture = TestBed.createComponent(WorkspaceScopeComponent);
    fixture.detectChanges();
  });

  function text(): string {
    return (fixture.nativeElement as HTMLElement).querySelector('.page-eyebrow')?.textContent ?? '';
  }

  it('names the selected workspace, in the dictionary label for a personal one', () => {
    expect(text()).toContain('My Workspace');
    expect(text()).not.toContain("dev1's Workspace");
  });

  // Project detail resolves a project by id alone, so its workspace and the
  // selected one can disagree.
  it('names the workspace it is given instead of the selected one', () => {
    fixture.componentRef.setInput('workspaceId', acme.id);
    fixture.detectChanges();

    expect(text()).toContain('Acme Team');
    expect(text()).not.toContain('My Workspace');
  });

  it('renders nothing for an id that names no workspace you belong to', () => {
    fixture.componentRef.setInput('workspaceId', 'gone');
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('.page-eyebrow')).toBeNull();
  });
});
