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
