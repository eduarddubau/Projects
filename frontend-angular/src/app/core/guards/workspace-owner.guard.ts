import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { WorkspaceContextService } from '../services/workspace-context.service';

/**
 * Keeps an owner-only destination out of a plain member's reach.
 *
 * Only for routes under /w/:workspaceId: workspaceGuard has loaded the list and set the
 * current workspace by the time a child guard runs, so the role is a synchronous read.
 * Sends a member home rather than rendering a refusal — the link is hidden from the
 * sidebar too, so anyone arriving here typed the URL or followed a stale one.
 */
export const workspaceOwnerGuard: CanActivateFn = () => {
  const context = inject(WorkspaceContextService);
  const router = inject(Router);

  if (context.isOwner()) return true;

  const id = context.currentWorkspaceId();
  return router.parseUrl(id ? `/w/${id}` : '/workspaces');
};
