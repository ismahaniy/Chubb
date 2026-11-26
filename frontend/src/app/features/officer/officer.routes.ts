import { Routes } from '@angular/router';

export const officerRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/officer-home.page').then((m) => m.OfficerHomePage),
  },
];
