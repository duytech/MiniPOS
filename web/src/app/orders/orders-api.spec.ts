import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { OrderListQuery } from './order.models';
import { OrdersApi } from './orders-api';

describe('OrdersApi', () => {
  let api: OrdersApi;
  let http: HttpTestingController;

  const query = (overrides: Partial<OrderListQuery> = {}): OrderListQuery => ({
    search: '',
    status: '',
    page: 1,
    pageSize: 20,
    ...overrides,
  });

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(OrdersApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends only paging when there is no search or status', () => {
    api.list(query({ page: 3, pageSize: 50 })).subscribe();

    const req = http.expectOne((r) => r.url === '/api/orders');
    expect(req.request.method).toBe('GET');
    expect(req.request.params.keys().sort()).toEqual(['page', 'pageSize']);
    expect(req.request.params.get('page')).toBe('3');
    expect(req.request.params.get('pageSize')).toBe('50');
    req.flush({ items: [], page: 3, pageSize: 50, totalCount: 0 });
  });

  it('sends search and status when they are set', () => {
    api.list(query({ search: 'pho', status: 'Paid' })).subscribe();

    const req = http.expectOne((r) => r.url === '/api/orders');
    expect(req.request.params.get('search')).toBe('pho');
    expect(req.request.params.get('status')).toBe('Paid');
    req.flush({ items: [], page: 1, pageSize: 20, totalCount: 0 });
  });

  it('returns the page exactly as the API sent it', () => {
    const body = {
      items: [
        {
          id: 'a',
          number: 7,
          status: 'Open',
          createdAt: '2026-10-26T07:30:00+00:00',
          itemNames: ['Pho'],
          total: 15.4,
        },
      ],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    };
    let received: unknown;
    api.list(query()).subscribe((result) => (received = result));

    http.expectOne((r) => r.url === '/api/orders').flush(body);

    expect(received).toEqual(body);
  });
});
