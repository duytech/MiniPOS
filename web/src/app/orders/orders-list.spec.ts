import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';

import { OrderSummary } from './order.models';
import { OrdersList, SEARCH_DEBOUNCE_MS } from './orders-list';

const ORDER_A: OrderSummary = {
  id: 'aaaaaaaa-1111-1111-1111-111111111111',
  createdAtUtc: '2026-03-01T09:30:00Z',
  status: 'Open',
  itemCount: 3,
  total: 15.4,
};
const ORDER_B: OrderSummary = {
  id: 'bbbbbbbb-2222-2222-2222-222222222222',
  createdAtUtc: '2026-03-01T08:00:00Z',
  status: 'Paid',
  itemCount: 1,
  total: 7.9,
};

describe('OrdersList', () => {
  let fixture: ComponentFixture<OrdersList>;
  let http: HttpTestingController;

  const el = () => fixture.nativeElement as HTMLElement;
  const rows = () => el().querySelectorAll('[data-testid="order-row"]');
  const searchInput = () => el().querySelector<HTMLInputElement>('[data-testid="search"]')!;
  const statusSelect = () => el().querySelector<HTMLSelectElement>('[data-testid="status"]')!;

  const expectList = (params: Record<string, string>): TestRequest => {
    const req = http.expectOne((r) => r.url === '/api/orders');
    const actual: Record<string, string> = {};
    for (const key of req.request.params.keys()) {
      actual[key] = req.request.params.get(key)!;
    }
    expect(actual).toEqual(params);
    return req;
  };

  const typeSearch = (text: string) => {
    searchInput().value = text;
    searchInput().dispatchEvent(new Event('input'));
  };

  const selectStatus = (value: string) => {
    statusSelect().value = value;
    statusSelect().dispatchEvent(new Event('change'));
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [OrdersList],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(OrdersList);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('loads all orders on start and renders them', () => {
    expect(el().querySelector('[data-testid="loading"]')).not.toBeNull();

    expectList({}).flush([ORDER_A, ORDER_B]);
    fixture.detectChanges();

    expect(rows().length).toBe(2);
    expect(rows()[0].textContent).toContain('#aaaaaaaa');
    expect(rows()[0].textContent).toContain('15.40');
    expect(rows()[1].textContent).toContain('Paid');
  });

  it('debounces the search box and sends one request with the trimmed text', fakeAsync(() => {
    expectList({}).flush([ORDER_A, ORDER_B]);

    typeSearch('l');
    tick(100);
    typeSearch('lat');
    tick(100);
    typeSearch(' latte ');
    tick(SEARCH_DEBOUNCE_MS - 1);
    http.expectNone((r) => r.url === '/api/orders');

    tick(1);
    expectList({ search: 'latte' }).flush([ORDER_A]);
    fixture.detectChanges();

    expect(rows().length).toBe(1);
  }));

  it('does not re-query when the trimmed search text is unchanged', fakeAsync(() => {
    expectList({}).flush([]);

    typeSearch('   ');
    tick(SEARCH_DEBOUNCE_MS);

    http.expectNone((r) => r.url === '/api/orders');
  }));

  it('combines the status filter with the current search text', fakeAsync(() => {
    expectList({}).flush([ORDER_A, ORDER_B]);

    typeSearch('mocha');
    tick(SEARCH_DEBOUNCE_MS);
    expectList({ search: 'mocha' }).flush([ORDER_A, ORDER_B]);

    selectStatus('Paid');
    expectList({ search: 'mocha', status: 'Paid' }).flush([ORDER_B]);
    fixture.detectChanges();

    expect(rows().length).toBe(1);
    expect(rows()[0].textContent).toContain('Paid');
  }));

  it('cancels the in-flight request when the query changes (switchMap)', fakeAsync(() => {
    const first = expectList({});

    selectStatus('Open');
    const second = expectList({ status: 'Open' });

    expect(first.cancelled).toBeTrue();
    second.flush([ORDER_A]);
    fixture.detectChanges();
    expect(rows().length).toBe(1);
  }));

  it('shows an error and keeps working for the next query', fakeAsync(() => {
    expectList({}).flush(
      { status: 400, detail: 'Unknown order status' },
      { status: 400, statusText: 'Bad Request' },
    );
    fixture.detectChanges();
    expect(el().querySelector('[data-testid="error"]')?.textContent).toContain('Unknown order status');

    selectStatus('Paid');
    expectList({ status: 'Paid' }).flush([ORDER_B]);
    fixture.detectChanges();

    expect(el().querySelector('[data-testid="error"]')).toBeNull();
    expect(rows().length).toBe(1);
  }));

  it('shows a friendly message when the API is unreachable', () => {
    expectList({}).error(new ProgressEvent('error'), { status: 0 });
    fixture.detectChanges();

    expect(el().querySelector('[data-testid="error"]')?.textContent).toContain('Cannot reach the API');
  });

  it('shows an empty state with a button that clears the filters', fakeAsync(() => {
    expectList({}).flush([ORDER_A]);

    selectStatus('Cancelled');
    expectList({ status: 'Cancelled' }).flush([]);
    fixture.detectChanges();
    expect(el().querySelector('[data-testid="empty"]')).not.toBeNull();

    el().querySelector<HTMLButtonElement>('[data-testid="empty"] button')!.click();
    expectList({}).flush([ORDER_A]);
    fixture.detectChanges();

    expect(statusSelect().value).toBe('');
    expect(rows().length).toBe(1);

    // Clearing the (already empty) search must not trigger a second request.
    tick(SEARCH_DEBOUNCE_MS);
    http.expectNone((r) => r.url === '/api/orders');
  }));
});
