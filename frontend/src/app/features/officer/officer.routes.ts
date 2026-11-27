import { Routes } from '@angular/router';

export const officerRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/queue.page').then((m) => m.QueuePage),
  },
  {
    path: 'claims/:id',
    title: 'Claim · ClaimDesk',
    loadComponent: () => import('./pages/workbench.page').then((m) => m.WorkbenchPage),
  },
];
