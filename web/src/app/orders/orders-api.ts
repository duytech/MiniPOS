import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiResponse, unwrap } from './api-error';
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
    return this.http.get<ApiResponse<PagedResult<OrderSummary>>>('/api/orders', { params }).pipe(unwrap());
  }

  get(id: string): Observable<Order> {
    return this.http.get<ApiResponse<Order>>(`/api/orders/${encodeURIComponent(id)}`).pipe(unwrap());
  }

  removeItem(orderId: string, itemId: string): Observable<Order> {
    return this.http.delete<ApiResponse<Order>>(`/api/orders/${encodeURIComponent(orderId)}/items/${encodeURIComponent(itemId)}`).pipe(unwrap());
  }

  changeItemQuantity(orderId: string, itemId: string, quantity: number): Observable<Order> {
    return this.http.put<ApiResponse<Order>>(
      `/api/orders/${encodeURIComponent(orderId)}/items/${encodeURIComponent(itemId)}/quantity`,
      { quantity },
    ).pipe(unwrap());
  }

  /** The saved split; fails with SplitNotFound when the order has none. */
  getSplit(orderId: string): Observable<OrderSplit> {
    return this.http.get<ApiResponse<OrderSplit>>(this.buildSplitUrl(orderId)).pipe(unwrap());
  }

  /** Works out the parts' amounts without saving anything. */
  previewSplit(orderId: string, request: SaveSplitRequest): Observable<OrderSplit> {
    return this.http.post<ApiResponse<OrderSplit>>(`${this.buildSplitUrl(orderId)}/preview`, request).pipe(unwrap());
  }

  /** Saves the split, replacing a saved one, and returns it. */
  saveSplit(orderId: string, request: SaveSplitRequest): Observable<OrderSplit> {
    return this.http.put<ApiResponse<OrderSplit>>(this.buildSplitUrl(orderId), request).pipe(unwrap());
  }

  /** Removes the saved split. */
  removeSplit(orderId: string): Observable<boolean> {
    return this.http.delete<ApiResponse<boolean>>(this.buildSplitUrl(orderId)).pipe(unwrap());
  }

  private buildSplitUrl(orderId: string): string {
    return `/api/orders/${encodeURIComponent(orderId)}/split`;
  }
}
