import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { Order, OrderListQuery, OrderSplit, OrderSummary, PagedResult, SaveSplitRequest } from './order.models';

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

  /** The saved split; 404 when the order has none. */
  getSplit(orderId: string): Observable<OrderSplit> {
    return this.http.get<OrderSplit>(this.splitUrl(orderId));
  }

  /** Works out the parts' amounts without saving anything. */
  previewSplit(orderId: string, request: SaveSplitRequest): Observable<OrderSplit> {
    return this.http.post<OrderSplit>(`${this.splitUrl(orderId)}/preview`, request);
  }

  /** Saves the split, replacing a saved one, and returns it. */
  saveSplit(orderId: string, request: SaveSplitRequest): Observable<OrderSplit> {
    return this.http.put<OrderSplit>(this.splitUrl(orderId), request);
  }

  /** Removes the saved split (204, no body). */
  removeSplit(orderId: string): Observable<void> {
    return this.http.delete<void>(this.splitUrl(orderId));
  }

  private splitUrl(orderId: string): string {
    return `/api/orders/${encodeURIComponent(orderId)}/split`;
  }
}
