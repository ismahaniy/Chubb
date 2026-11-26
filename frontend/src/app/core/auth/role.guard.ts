import { inject } from '@angular/core';
import { CanMatchFn, Router } from '@angular/router';
import type { Role } from '../api/api';
import { AuthStore } from './auth.store';
import { ROLE_HOME } from './role-home';

/** Signed out -> /login. Signed in with another role -> that role's home. */
export function roleGuard(...allowed: Role[]): CanMatchFn {
  return () => {
    const router = inject(Router);
    const role = inject(AuthStore).role();

    if (role === null) return router.createUrlTree(['/login']);
    return allowed.includes(role) || router.parseUrl(ROLE_HOME[role]);
  };
}
