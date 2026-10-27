import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'orders' },
  {
    path: 'orders',
    title: 'Orders · Mini POS',
    loadComponent: () => import('./orders/orders-list/orders-list').then((m) => m.OrdersList),
  },
  {
    path: 'orders/:id',
    title: 'Order · Mini POS',
    loadComponent: () => import('./orders/order-detail/order-detail').then((m) => m.OrderDetail),
  },
];
