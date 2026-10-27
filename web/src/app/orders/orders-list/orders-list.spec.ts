import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { OrderSummary, PagedResult } from '../order.models';
import { OrdersList, PAGE_SIZE, SEARCH_DEBOUNCE_MS } from './orders-list';

describe('OrdersList', () => {
  let fixture: ComponentFixture<OrdersList>;
  let http: HttpTestingController;

  const order = (overrides: Partial<OrderSummary> = {}): OrderSummary => ({
    id: crypto.randomUUID(),
    number: 12,
    status: 'Open',
    createdAt: '2026-10-26T07:30:00+00:00',
    itemNames: ['Pho', 'Iced tea'],
    total: 18.85,
    ...overrides,
  });

  const page = (items: OrderSummary[], totalCount = items.length, pageNumber = 1): PagedResult<OrderSummary> => ({
    items,
    page: pageNumber,
    pageSize: PAGE_SIZE,
    totalCount,
  });

  const el = (testId: string) =>
    fixture.nativeElement.querySelector(`[data-testid="${testId}"]`) as HTMLElement | null;
  const rows = () =>
    Array.from(fixture.nativeElement.querySelectorAll('[data-testid="order-row"]')) as HTMLElement[];

  const expectList = (): TestRequest => http.expectOne((req) => req.url === '/api/orders');
  const params = (req: TestRequest) => ({
    search: req.request.params.get('search'),
    status: req.request.params.get('status'),
    page: req.request.params.get('page'),
    pageSize: req.request.params.get('pageSize'),
  });

  const typeSearch = (text: string) => {
    const input = el('search') as HTMLInputElement;
    input.value = text;
    input.dispatchEvent(new Event('input'));
  };

  const selectStatus = (status: string) => {
    const select = el('status') as HTMLSelectElement;
    select.value = status;
    select.dispatchEvent(new Event('change'));
  };

  const loadFirstPage = (result = page([order()])) => {
    fixture.detectChanges();
    expectList().flush(result);
    fixture.detectChanges();
  };

  beforeEach(() => {
    // Fake timers drive the search debounce; tests advance them with vi.advanceTimersByTime.
    vi.useFakeTimers();
    TestBed.configureTestingModule({
      imports: [OrdersList],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    fixture = TestBed.createComponent(OrdersList);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    vi.useRealTimers();
  });

  it('loads the first page with no filters and shows the orders', () => {
    fixture.detectChanges();
    const req = expectList();

    expect(params(req)).toEqual({ search: null, status: null, page: '1', pageSize: String(PAGE_SIZE) });
    req.flush(page([order({ number: 12, total: 18.85, status: 'Paid' })]));
    fixture.detectChanges();

    expect(rows().length).toBe(1);
    expect(rows()[0].textContent).toContain('#12');
    expect(rows()[0].textContent).toContain('Pho, Iced tea');
    expect(rows()[0].textContent).toContain('Paid');
    expect(rows()[0].textContent).toContain('$18.85');
    expect(el('range')?.textContent).toContain('Showing 1–1 of 1');
  });

  it('shows money exactly as the API sent it, with two decimals', () => {
    loadFirstPage(page([order({ total: 15.4 }), order({ total: 0 })]));

    expect(rows()[0].textContent).toContain('$15.40');
    expect(rows()[1].textContent).toContain('$0.00');
  });

  it('links only the order number to the order detail page', () => {
    loadFirstPage(page([order({ id: 'abc', number: 12 })]));

    const links = rows()[0].querySelectorAll('a');
    expect(links.length).toBe(1);
    expect(links[0].textContent?.trim()).toBe('#12');
    expect(links[0].getAttribute('href')).toBe('/orders/abc');
  });

  it('debounces the search and sends only the last text', () => {
    loadFirstPage();

    typeSearch('p');
    vi.advanceTimersByTime(100);
    typeSearch('ph');
    vi.advanceTimersByTime(100);
    typeSearch('  pho ');
    http.expectNone((req) => req.url === '/api/orders');
    vi.advanceTimersByTime(SEARCH_DEBOUNCE_MS);

    expect(params(expectList())).toEqual({ search: 'pho', status: null, page: '1', pageSize: String(PAGE_SIZE) });
  });

  it('cancels the request in flight when the search changes (switchMap)', () => {
    loadFirstPage();

    typeSearch('tea');
    vi.advanceTimersByTime(SEARCH_DEBOUNCE_MS);
    const stale = expectList();
    typeSearch('coffee');
    vi.advanceTimersByTime(SEARCH_DEBOUNCE_MS);
    const latest = expectList();

    expect(stale.cancelled).toBe(true);
    expect(params(latest).search).toBe('coffee');
    latest.flush(page([order({ number: 99, itemNames: ['Coffee'] })]));
    fixture.detectChanges();
    expect(rows().map((r) => r.textContent)).toEqual([expect.stringContaining('#99')]);
  });

  it('combines the search text with the status filter (combineLatest)', () => {
    loadFirstPage();

    typeSearch('pho');
    vi.advanceTimersByTime(SEARCH_DEBOUNCE_MS);
    expectList().flush(page([order()]));
    selectStatus('Paid');

    expect(params(expectList())).toEqual({ search: 'pho', status: 'Paid', page: '1', pageSize: String(PAGE_SIZE) });

    typeSearch('tea');
    vi.advanceTimersByTime(SEARCH_DEBOUNCE_MS);
    expect(params(expectList())).toEqual({ search: 'tea', status: 'Paid', page: '1', pageSize: String(PAGE_SIZE) });
  });

  it('does not send a request when the trimmed search text did not change', () => {
    loadFirstPage();

    typeSearch('   ');
    vi.advanceTimersByTime(SEARCH_DEBOUNCE_MS);
    fixture.detectChanges();

    http.expectNone((req) => req.url === '/api/orders');
    expect(el('loading')).toBeNull();
    expect(rows().length).toBe(1);
  });

  it('pages with Next and goes back to page 1 when a filter changes', () => {
    loadFirstPage(page([order()], 45));
    expect(el('range')?.textContent).toContain('of 45');

    (el('next') as HTMLButtonElement).click();
    const second = expectList();
    expect(params(second).page).toBe('2');
    second.flush(page([order({ number: 3 })], 45, 2));
    fixture.detectChanges();
    expect(el('range')?.textContent).toContain('Showing 21–21 of 45');
    expect((el('previous') as HTMLButtonElement).disabled).toBe(false);

    selectStatus('Cancelled');
    expect(params(expectList()).page).toBe('1');
  });

  it('keeps showing the previous rows with a loading indicator while reloading', () => {
    loadFirstPage(page([order({ number: 12 })]));

    typeSearch('tea');
    vi.advanceTimersByTime(SEARCH_DEBOUNCE_MS);
    fixture.detectChanges();

    expect(rows().length).toBe(1);
    expect(rows()[0].textContent).toContain('#12');
    expect(el('loading')).not.toBeNull();

    expectList().flush(page([order({ number: 7 })]));
    fixture.detectChanges();
    expect(rows()[0].textContent).toContain('#7');
    expect(el('loading')).toBeNull();
  });

  it('shows an error but keeps the previous rows when a request fails, and recovers on the next search', () => {
    loadFirstPage(page([order({ number: 12 })]));

    typeSearch('tea');
    vi.advanceTimersByTime(SEARCH_DEBOUNCE_MS);
    expectList().flush(null, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(el('error')).not.toBeNull();
    expect(rows()[0].textContent).toContain('#12');

    typeSearch('coffee');
    vi.advanceTimersByTime(SEARCH_DEBOUNCE_MS);
    expectList().flush(page([order({ number: 5 })]));
    fixture.detectChanges();
    expect(el('error')).toBeNull();
    expect(rows()[0].textContent).toContain('#5');
  });

  it('shows an empty state when nothing matches', () => {
    loadFirstPage(page([]));

    expect(el('empty')?.textContent).toContain('No orders match');
    expect(el('range')?.textContent).toContain('0 orders');
    expect((el('next') as HTMLButtonElement).disabled).toBe(true);
  });
});
