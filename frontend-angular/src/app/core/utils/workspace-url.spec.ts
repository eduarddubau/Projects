import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { withWorkspaceId } from './workspace-url';

describe('withWorkspaceId', () => {
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    router = TestBed.inject(Router);
  });

  function swap(url: string, id = 'b2'): string | null {
    const tree = withWorkspaceId(router, url, id);
    return tree ? router.serializeUrl(tree) : null;
  }

  it('keeps the rest of the path', () => {
    expect(swap('/w/a1/members')).toBe('/w/b2/members');
  });

  it('keeps query params and the fragment', () => {
    expect(swap('/w/a1/settings?tab=general#danger')).toBe('/w/b2/settings?tab=general#danger');
  });

  it('handles a bare /w/:id with nothing after it', () => {
    expect(swap('/w/a1')).toBe('/w/b2');
  });

  // The bug: switching workspace from a project page swapped the id and stayed put, so
  // the detail page rendered the old workspace's project under the new workspace's URL —
  // the API resolves a project by id and checks membership, not the path.
  it('drops an entity id rather than pointing the new workspace at another one’s project', () => {
    expect(swap('/w/a1/projects/0195f1a2-3b4c-7d8e-9f01-23456789abcd')).toBe('/w/b2');
  });

  it('drops the query with it, since it described the page being left', () => {
    expect(swap('/w/a1/projects/0195f1a2-3b4c-7d8e-9f01-23456789abcd?view=list')).toBe('/w/b2');
  });

  // The distinction is section vs entity, not depth: /projects/trash has to survive.
  it('keeps a nested section that is not an entity', () => {
    expect(swap('/w/a1/projects')).toBe('/w/b2/projects');
    expect(swap('/w/a1/tasks?filter=overdue')).toBe('/w/b2/tasks?filter=overdue');
  });

  it('returns null outside the /w tree, so callers can tell not to navigate', () => {
    expect(swap('/dashboard')).toBeNull();
    expect(swap('/workspaces')).toBeNull();
    expect(swap('/')).toBeNull();
  });

  // Writing at [1] here would append a segment and invent a route.
  it('returns null for /w with no id', () => {
    expect(swap('/w')).toBeNull();
  });
});
