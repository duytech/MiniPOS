import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'orders' },
  {
    path: 'orders',
    title: 'Orders - Mini POS',
    loadComponent: () => import('./orders/orders-list').then((m) => m.OrdersList),
  },
  { path: '**', redirectTo: 'orders' },
];
