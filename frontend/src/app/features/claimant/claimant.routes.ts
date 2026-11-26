import { Routes } from '@angular/router';

export const claimantRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/claimant-home.page').then((m) => m.ClaimantHomePage),
  },
];
