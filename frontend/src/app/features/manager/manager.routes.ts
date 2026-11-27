import { Routes } from '@angular/router';

export const managerRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/dashboard.page').then((m) => m.DashboardPage),
  },
];
