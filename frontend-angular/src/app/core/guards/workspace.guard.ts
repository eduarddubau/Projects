import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { WorkspaceContextService } from '../services/workspace-context.service';
import { withWorkspaceId } from '@core/utils/workspace-url';

export const workspaceGuard: CanActivateFn = (route, state) => {
  const context = inject(WorkspaceContextService);
  const router = inject(Router);

  const urlId = route.paramMap.get('workspaceId');

  return context.ensureLoaded().pipe(
    map(() => {
      // Inside the map, never outside: resolve() reads the loaded list
      // synchronously, so calling it before the load emits sees an empty list
      // and sends everyone to the fallback.
      const best = context.resolve(urlId);

      // No workspaces at all. Redirect anywhere this guard covers and the
      // redirect below re-enters it forever, so leave the /w tree entirely.
      if (!best) return router.parseUrl('/workspaces');

      if (best === urlId) {
        context.setCurrent(best);
        return true;
      }

      // Swap the id in place so the rest of the path survives: /w/bogus/members
      // has to land on /w/{best}/members, not /w/{best}.
      return withWorkspaceId(router, state.url, best) ?? router.parseUrl('/workspaces');
    }),
  );
};
