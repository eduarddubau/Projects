import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { of } from 'rxjs';

import { ProjectsCardComponent } from './projects-card.component';
import { API_URL } from '@core/tokens/app.tokens';
import { Project } from '@core/models/project';
import { provideTranslocoTesting } from '@shared/testing/transloco-testing';

const apiUrl = 'http://api.test';
const workspaceId = '99999999-9999-9999-9999-999999999999';
const listUrl = `${apiUrl}/workspaces/${workspaceId}/projects`;

function project(id: string, name: string): Project {
  return {
    id,
    name,
    description: `${name} description`,
    workspaceId,
    workspaceName: 'Test Workspace',
    createdAt: '2026-06-01T10:00:00Z',
    isDeleted: false,
    isPurgeable: false,
  };
}

const alpha = project('11111111-1111-1111-1111-111111111111', 'Alpha');
const beta = project('22222222-2222-2222-2222-222222222222', 'Beta');

// Every dialog in this component resolves through afterClosed(); stubbing it is
// what lets the specs drive create and delete without opening real overlays.
function dialogStub(result: unknown) {
  return { open: () => ({ afterClosed: () => of(result) }) };
}

describe('ProjectsCardComponent', () => {
  let fixture: ComponentFixture<ProjectsCardComponent>;
  let httpMock: HttpTestingController;

  async function setup(dialogResult: unknown = undefined, id: string | null = workspaceId) {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [ProjectsCardComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideTranslocoTesting(),
        { provide: API_URL, useValue: apiUrl },
        { provide: MatDialog, useValue: dialogStub(dialogResult) },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ProjectsCardComponent);
    fixture.componentRef.setInput('workspaceId', id);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  }

  function rowText(): string {
    return (fixture.nativeElement as HTMLElement).querySelector('table')?.textContent ?? '';
  }

  afterEach(() => httpMock.verify());

  it('renders the loaded projects', async () => {
    await setup();
    httpMock.expectOne(listUrl).flush([alpha, beta]);
    await fixture.whenStable();

    expect(rowText()).toContain('Alpha');
    expect(rowText()).toContain('Beta');
  });

  // The card stays idle rather than requesting /workspaces/null/projects while the
  // host is still resolving the workspace.
  it('requests nothing until it has a workspace', async () => {
    await setup(undefined, null);

    httpMock.expectNone(() => true);
  });

  it('shows the error state when loading fails', async () => {
    await setup();
    httpMock.expectOne(listUrl).flush('boom', { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Failed to load projects. Please try again.');
  });

  // Every other table in the app wires this; this one silently did not, so the
  // search box filtered nothing.
  it('filters the rows through the search box', async () => {
    await setup();
    httpMock.expectOne(listUrl).flush([alpha, beta]);
    await fixture.whenStable();

    const search = (fixture.nativeElement as HTMLElement).querySelector('input[matInput]')!;
    (search as HTMLInputElement).value = 'alph';
    search.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(rowText()).toContain('Alpha');
    expect(rowText()).not.toContain('Beta');
  });

  // The load path and the mutation path both run through the resource, so this
  // proves a local edit reaches the rendered rows and not just the resource.
  it('adds a created project to the table', async () => {
    await setup({ name: 'Gamma', description: 'New one' });
    httpMock.expectOne(listUrl).flush([alpha]);
    await fixture.whenStable();

    fixture.componentInstance.openCreateDialog();
    const created = project('33333333-3333-3333-3333-333333333333', 'Gamma');
    httpMock.expectOne({ url: listUrl, method: 'POST' }).flush(created);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(rowText()).toContain('Gamma');
    expect(rowText()).toContain('Alpha');
  });
});
