import { inject } from '@angular/core';
import { Routes } from '@angular/router';
import { AuthStore } from './core/auth/auth.store';
import { ROLE_HOME } from './core/auth/role-home';
import { roleGuard } from './core/auth/role.guard';

/** Signed-in users go to their role's home, everyone else to the login page. */
const toHome = () => {
  const role = inject(AuthStore).role();
  return role ? ROLE_HOME[role] : '/login';
};

export const routes: Routes = [
  {
    path: 'login',
    title: 'Sign in · ClaimDesk',
    loadComponent: () => import('./features/auth/pages/login.page').then((m) => m.LoginPage),
  },
  {
    path: 'claimant',
    title: 'My claims · ClaimDesk',
    canMatch: [roleGuard('Claimant')],
    loadChildren: () => import('./features/claimant/claimant.routes').then((m) => m.claimantRoutes),
  },
  {
    path: 'officer',
    title: 'Work queue · ClaimDesk',
    canMatch: [roleGuard('Officer')],
    loadChildren: () => import('./features/officer/officer.routes').then((m) => m.officerRoutes),
  },
  {
    path: 'manager',
    title: 'Dashboard · ClaimDesk',
    canMatch: [roleGuard('Manager')],
    loadChildren: () => import('./features/manager/manager.routes').then((m) => m.managerRoutes),
  },
  { path: '', pathMatch: 'full', redirectTo: toHome },
  { path: '**', redirectTo: toHome },
];
