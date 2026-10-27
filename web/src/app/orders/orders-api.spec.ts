import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { Order, OrderListQuery } from './order.models';
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

  const order: Order = {
    id: 'o1',
    number: 12,
    status: 'Open',
    createdAt: '2026-10-26T07:30:00+00:00',
    paymentMethod: 'Cash',
    lines: [
      { id: 'l1', menuItemId: 'm1', name: 'Pho', unitPrice: 12.5, quantity: 2, notes: null, lineTotal: 25 },
    ],
    subtotal: 25,
    discount: 0,
    surcharge: 0,
    tax: 2.06,
    tip: 0,
    total: 27.06,
  };

  it('gets one order by id and returns it unchanged', () => {
    let received: unknown;
    api.get('o1').subscribe((result) => (received = result));

    const req = http.expectOne('/api/orders/o1');
    expect(req.request.method).toBe('GET');
    req.flush(order);

    expect(received).toEqual(order);
  });

  it('removes a line with DELETE and returns the order the API sent back', () => {
    let received: unknown;
    api.removeLine('o1', 'l1').subscribe((result) => (received = result));

    const req = http.expectOne('/api/orders/o1/lines/l1');
    expect(req.request.method).toBe('DELETE');
    req.flush(order);

    expect(received).toEqual(order);
  });

  it('changes a line quantity with PUT { quantity } and returns the order the API sent back', () => {
    let received: unknown;
    api.changeLineQuantity('o1', 'l1', 3).subscribe((result) => (received = result));

    const req = http.expectOne('/api/orders/o1/lines/l1/quantity');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ quantity: 3 });
    req.flush(order);

    expect(received).toEqual(order);
  });
});
