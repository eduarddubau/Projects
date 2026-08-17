import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { WorkspaceContextService } from '../services/workspace-context.service';

/**
 * Sends /dashboard to the home of the best workspace.
 *
 * The home page reads its workspace from the path, so it cannot live at a URL that has
 * none. Rather than teach every caller of "home" — the guest guard, the admin guard, the
 * landing page, login and register — how to resolve a workspace, /dashboard stays as the
 * one stable home URL and this guard forwards it. Old bookmarks keep working too.
 *
 * Always returns a UrlTree, so the route it guards never activates.
 */
export const workspaceHomeGuard: CanActivateFn = () => {
  const context = inject(WorkspaceContextService);
  const router = inject(Router);

  return context.ensureLoaded().pipe(
    map(() => {
      // No workspaces at all — /w is unreachable, so offer the list instead.
      const best = context.resolve(null);
      return router.parseUrl(best ? `/w/${best}` : '/workspaces');
    }),
    // A failed list must not reject the navigation. This route has no component, so
    // an error leaves the caller exactly where they were with nothing rendered — and
    // for the post-login redirect that means sitting on the login form, apparently
    // ignored, after a login that actually succeeded. /workspaces refetches the same
    // list and has an error state to show for it.
    catchError(() => of(router.parseUrl('/workspaces'))),
  );
};
