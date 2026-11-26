import { Routes } from '@angular/router';

export const claimantRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/my-claims.page').then((m) => m.MyClaimsPage),
  },
  {
    path: 'claims/:id',
    title: 'Claim · ClaimDesk',
    loadComponent: () => import('./pages/claim.page').then((m) => m.ClaimPage),
  },
];
