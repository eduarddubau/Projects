import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }

  // Encoded because the target can carry its own query string — an invitation
  // link is `/invitations/accept?token=…`, and an unencoded `&` in any such URL
  // would split into a second parameter on /login and be silently dropped.
  return router.parseUrl(`/login?returnUrl=${encodeURIComponent(state.url)}`);
};
