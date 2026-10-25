import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { OrderListQuery, OrderSummary } from './order.models';

/** Talks to the Pos.Api orders endpoints. In dev, /api is proxied to the API (see proxy.conf.json). */
@Injectable({ providedIn: 'root' })
export class OrdersApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/orders';

  list(query: OrderListQuery): Observable<OrderSummary[]> {
    let params = new HttpParams();
    if (query.status) {
      params = params.set('status', query.status);
    }
    const search = query.search.trim();
    if (search) {
      params = params.set('search', search);
    }
    return this.http.get<OrderSummary[]>(this.baseUrl, { params });
  }
}
