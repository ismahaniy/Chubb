import { Routes } from '@angular/router';

export const officerRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/queue.page').then((m) => m.QueuePage),
  },
];
