import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Gates a route behind a backend permission code, mirroring the same
 * `RequirePermission` check the API enforces server-side. The guard is a UX nicety —
 * hiding a link nobody can use — not the security boundary; the API rejects the request
 * either way.
 */
export function permissionGuard(code: string): CanActivateFn {
  return () => {
    const auth = inject(AuthService);
    const router = inject(Router);

    if (!auth.isAuthenticated()) {
      return router.parseUrl('/login');
    }

    return auth.hasPermission(code) ? true : router.parseUrl('/');
  };
}
