import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { of } from 'rxjs';

import { ProjectsComponent } from './projects.component';
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

// The component resolves its workspace from the route, so the param has to exist
// or the resource stays idle and nothing is ever requested.
const routeStub = {
  paramMap: of(convertToParamMap({ workspaceId })),
  snapshot: {
    paramMap: convertToParamMap({ workspaceId }),
    queryParamMap: convertToParamMap({}),
  },
};

const alpha = project('11111111-1111-1111-1111-111111111111', 'Alpha');
const beta = project('22222222-2222-2222-2222-222222222222', 'Beta');

// Every dialog in this component resolves through afterClosed(); stubbing it is
// what lets the specs drive create and delete without opening real overlays.
function dialogStub(result: unknown) {
  return { open: () => ({ afterClosed: () => of(result) }) };
}

describe('ProjectsComponent', () => {
  let fixture: ComponentFixture<ProjectsComponent>;
  let httpMock: HttpTestingController;

  async function setup(dialogResult: unknown = undefined) {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [ProjectsComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideTranslocoTesting(),
        { provide: API_URL, useValue: apiUrl },
        { provide: MatDialog, useValue: dialogStub(dialogResult) },
        { provide: ActivatedRoute, useValue: routeStub },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ProjectsComponent);
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

  it('shows the error state when loading fails', async () => {
    await setup();
    httpMock.expectOne(listUrl).flush('boom', { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Failed to load projects. Please try again.');
  });

  // The load path and the mutation path both run through the resource, so this
  // is what proves the resource -> effect -> dataSource wiring actually reaches
  // the rendered table. A broken effect still compiles and still loads.
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

  it('removes a deleted project from the table', async () => {
    await setup(true);
    httpMock.expectOne(listUrl).flush([alpha, beta]);
    await fixture.whenStable();

    fixture.componentInstance.confirmDelete(alpha);
    httpMock.expectOne({ url: `${apiUrl}/projects/${alpha.id}`, method: 'DELETE' }).flush(null);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(rowText()).not.toContain('Alpha');
    expect(rowText()).toContain('Beta');
  });
});
