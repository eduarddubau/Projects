import { PRIMARY_OUTLET, Router, UrlSegment, UrlTree } from '@angular/router';

/** Every id the app mints is a GUID, so this is what an entity segment looks like. */
const ENTITY_ID = /^[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}$/i;

/**
 * `/w/{a}/members?tab=1` -> `/w/{b}/members?tab=1`. Query params and the
 * fragment hang off the tree, not the segments, so they ride along.
 * Null when the URL is not under `/w/:workspaceId`.
 *
 * A **section** carries over to the other workspace; a **specific entity** cannot.
 * `/w/{a}/projects/{id}` names a project that exists only in `{a}`, and swapping just the
 * workspace id left the detail page happily rendering it under `{b}`'s URL — the API
 * resolves a project by id and checks membership, not the workspace in the path. Those go
 * to the new workspace's home instead, and the query goes with them, since `?view=` and
 * friends describe the page being left.
 */
export function withWorkspaceId(router: Router, url: string, workspaceId: string): UrlTree | null {
  const tree = router.parseUrl(url);
  const segments = tree.root.children[PRIMARY_OUTLET]?.segments ?? [];

  if (segments.length < 2 || segments[0].path !== 'w') return null;

  if (segments.some((segment, i) => i > 1 && ENTITY_ID.test(segment.path))) {
    return router.createUrlTree(['/w', workspaceId]);
  }

  segments[1] = new UrlSegment(workspaceId, {});
  return tree;
}
