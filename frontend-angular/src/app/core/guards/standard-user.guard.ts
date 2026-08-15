import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '@core/services/auth.service';

/**
 * Keeps administrators out of the workspace side of the app, which their token
 * cannot reach anyway — the API's StandardUser policy excludes the Admin role.
 */
export const standardUserGuard: CanActivateFn = () => {
  const router = inject(Router);
  return inject(AuthService).isAdmin() ? router.parseUrl('/admin') : true;
};
