import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { Order, OrderListQuery, OrderSummary, PagedResult } from './order.models';

@Injectable({ providedIn: 'root' })
export class OrdersApi {
  private readonly http = inject(HttpClient);

  list(query: OrderListQuery): Observable<PagedResult<OrderSummary>> {
    let params = new HttpParams().set('page', query.page).set('pageSize', query.pageSize);
    if (query.search) {
      params = params.set('search', query.search);
    }
    if (query.status) {
      params = params.set('status', query.status);
    }
    return this.http.get<PagedResult<OrderSummary>>('/api/orders', { params });
  }

  get(id: string): Observable<Order> {
    return this.http.get<Order>(`/api/orders/${encodeURIComponent(id)}`);
  }

  removeLine(orderId: string, lineId: string): Observable<Order> {
    return this.http.delete<Order>(`/api/orders/${encodeURIComponent(orderId)}/lines/${encodeURIComponent(lineId)}`);
  }

  changeLineQuantity(orderId: string, lineId: string, quantity: number): Observable<Order> {
    return this.http.put<Order>(
      `/api/orders/${encodeURIComponent(orderId)}/lines/${encodeURIComponent(lineId)}/quantity`,
      { quantity },
    );
  }
}
