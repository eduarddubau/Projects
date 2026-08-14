import { PRIMARY_OUTLET, Router, UrlSegment, UrlTree } from '@angular/router';

/**
 * `/w/{a}/members?tab=1` -> `/w/{b}/members?tab=1`. Query params and the
 * fragment hang off the tree, not the segments, so they ride along.
 * Null when the URL is not under `/w/:workspaceId`.
 */
export function withWorkspaceId(router: Router, url: string, workspaceId: string): UrlTree | null {
  const tree = router.parseUrl(url);
  const segments = tree.root.children[PRIMARY_OUTLET]?.segments ?? [];

  if (segments.length < 2 || segments[0].path !== 'w') return null;

  segments[1] = new UrlSegment(workspaceId, {});
  return tree;
}
