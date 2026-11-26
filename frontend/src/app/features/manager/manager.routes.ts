import { Routes } from '@angular/router';

export const managerRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/manager-home.page').then((m) => m.ManagerHomePage),
  },
];
