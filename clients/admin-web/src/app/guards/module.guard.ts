import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { ModuleVisibilityService } from '../services/module-visibility.service';

/**
 * Route guard that redirects to '/' if the module or submodule key is not visible.
 *
 * Implements S4 (spec.md sec. 3): entering by URL to a hidden module or submodule
 * redirects to the home screen.
 */
export function moduleGuard(key: string): CanActivateFn {
  return () => {
    const visibility = inject(ModuleVisibilityService);
    const router = inject(Router);

    if (!visibility.isVisible(key)) {
      return router.parseUrl('/');
    }

    return true;
  };
}
