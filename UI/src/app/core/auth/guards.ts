import { inject } from '@angular/core';
import { CanActivateFn, Router, UrlTree } from '@angular/router';
import { AuthService } from './auth.service';
import { Role } from '../models/models';

/** Blocks routes for anonymous users. */
export const authGuard: CanActivateFn = (): boolean | UrlTree => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isAuthenticated() ? true : router.createUrlTree(['/login']);
};

/** Blocks routes unless the user holds one of the given roles. */
export const roleGuard = (...roles: Role[]): CanActivateFn => {
  return (): boolean | UrlTree => {
    const auth = inject(AuthService);
    const router = inject(Router);
    if (!auth.isAuthenticated()) {
      return router.createUrlTree(['/login']);
    }
    return auth.hasAnyRole(...roles) ? true : router.createUrlTree(['/dashboard']);
  };
};
