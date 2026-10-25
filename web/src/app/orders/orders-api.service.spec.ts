import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { OrderSummary } from './order.models';
import { OrdersApiService } from './orders-api.service';

describe('OrdersApiService', () => {
  let service: OrdersApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(OrdersApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('calls GET /api/orders without params when no filters are set', () => {
    const rows: OrderSummary[] = [
      { id: 'a', createdAtUtc: '2026-01-01T00:00:00Z', status: 'Open', itemCount: 1, total: 2.5 },
    ];
    let result: OrderSummary[] | undefined;

    service.list({ status: '', search: '   ' }).subscribe((r) => (result = r));

    const req = http.expectOne((r) => r.url === '/api/orders');
    expect(req.request.method).toBe('GET');
    expect(req.request.params.keys()).toEqual([]);
    req.flush(rows);
    expect(result).toEqual(rows);
  });

  it('sends status and trimmed search as query params', () => {
    service.list({ status: 'Paid', search: '  latte ' }).subscribe();

    const req = http.expectOne((r) => r.url === '/api/orders');
    expect(req.request.params.get('status')).toBe('Paid');
    expect(req.request.params.get('search')).toBe('latte');
    req.flush([]);
  });
});
