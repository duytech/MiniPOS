import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { Order, OrderListQuery, OrderSplit, SaveSplitRequest } from './order.models';
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

  const ok = <T>(data: T) => ({ isSuccess: true, data, error: null });

  afterEach(() => http.verify());

  it('sends only paging when there is no search or status', () => {
    api.list(query({ page: 3, pageSize: 50 })).subscribe();

    const req = http.expectOne((r) => r.url === '/api/orders');
    expect(req.request.method).toBe('GET');
    expect(req.request.params.keys().sort()).toEqual(['page', 'pageSize']);
    expect(req.request.params.get('page')).toBe('3');
    expect(req.request.params.get('pageSize')).toBe('50');
    req.flush(ok({ items: [], page: 3, pageSize: 50, totalCount: 0 }));
  });

  it('sends search and status when they are set', () => {
    api.list(query({ search: 'pho', status: 'Paid' })).subscribe();

    const req = http.expectOne((r) => r.url === '/api/orders');
    expect(req.request.params.get('search')).toBe('pho');
    expect(req.request.params.get('status')).toBe('Paid');
    req.flush(ok({ items: [], page: 1, pageSize: 20, totalCount: 0 }));
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

    http.expectOne((r) => r.url === '/api/orders').flush(ok(body));

    expect(received).toEqual(body);
  });

  const order: Order = {
    id: 'o1',
    number: 12,
    status: 'Open',
    createdAt: '2026-10-26T07:30:00+00:00',
    paymentMethod: 'Cash',
    items: [
      { id: 'l1', menuItemId: 'm1', name: 'Pho', unitPrice: 12.5, quantity: 2, notes: null, itemTotal: 25 },
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
    req.flush(ok(order));

    expect(received).toEqual(order);
  });

  it('removes an item with DELETE and returns the order the API sent back', () => {
    let received: unknown;
    api.removeItem('o1', 'l1').subscribe((result) => (received = result));

    const req = http.expectOne('/api/orders/o1/items/l1');
    expect(req.request.method).toBe('DELETE');
    req.flush(ok(order));

    expect(received).toEqual(order);
  });

  it('changes an item quantity with PUT { quantity } and returns the order the API sent back', () => {
    let received: unknown;
    api.changeItemQuantity('o1', 'l1', 3).subscribe((result) => (received = result));

    const req = http.expectOne('/api/orders/o1/items/l1/quantity');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ quantity: 3 });
    req.flush(ok(order));

    expect(received).toEqual(order);
  });

  const split: OrderSplit = {
    parts: [
      { itemIds: ['l1'], subtotal: 60, discount: 0, surcharge: 0, tax: 5.33, tip: 0, total: 65.33 },
      { itemIds: ['l2'], subtotal: 40, discount: 0, surcharge: 0, tax: 3.55, tip: 0, total: 43.55 },
    ],
  };
  const splitRequest: SaveSplitRequest = { parts: [{ itemIds: ['l1'] }, { itemIds: ['l2', 'l3'] }] };

  it('gets the saved split with GET and returns it unchanged', () => {
    let received: unknown;
    api.getSplit('o1').subscribe((result) => (received = result));

    const req = http.expectOne('/api/orders/o1/split');
    expect(req.request.method).toBe('GET');
    expect(req.request.body).toBeNull();
    req.flush(ok(split));

    expect(received).toEqual(split);
  });

  it('previews a split with POST { parts } and returns the parts the API sent back', () => {
    let received: unknown;
    api.previewSplit('o1', splitRequest).subscribe((result) => (received = result));

    const req = http.expectOne('/api/orders/o1/split/preview');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ parts: [{ itemIds: ['l1'] }, { itemIds: ['l2', 'l3'] }] });
    req.flush(ok(split));

    expect(received).toEqual(split);
  });

  it('saves a split with PUT { parts } and returns the split the API sent back', () => {
    let received: unknown;
    api.saveSplit('o1', splitRequest).subscribe((result) => (received = result));

    const req = http.expectOne('/api/orders/o1/split');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ parts: [{ itemIds: ['l1'] }, { itemIds: ['l2', 'l3'] }] });
    req.flush(ok(split));

    expect(received).toEqual(split);
  });

  it('removes the saved split with DELETE and completes', () => {
    let completed = false;
    api.removeSplit('o1').subscribe({ complete: () => (completed = true) });

    const req = http.expectOne('/api/orders/o1/split');
    expect(req.request.method).toBe('DELETE');
    req.flush(ok(true));

    expect(completed).toBe(true);
  });
});
