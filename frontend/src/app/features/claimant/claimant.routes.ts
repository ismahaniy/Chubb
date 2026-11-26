import { Routes } from '@angular/router';

export const claimantRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/my-claims.page').then((m) => m.MyClaimsPage),
  },
  {
    path: 'new',
    title: 'Report a claim · ClaimDesk',
    loadComponent: () => import('./pages/submit-claim.page').then((m) => m.SubmitClaimPage),
  },
  {
    path: 'claims/:id',
    title: 'Claim · ClaimDesk',
    loadComponent: () => import('./pages/claim.page').then((m) => m.ClaimPage),
  },
];
