import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { Order, OrderLine, OrderSplit } from '../order.models';
import { CHANGED_MESSAGE, GENERIC_ERROR, OrderDetail } from './order-detail';

describe('OrderDetail', () => {
  let fixture: ComponentFixture<OrderDetail>;
  let http: HttpTestingController;

  const ORDER_URL = '/api/orders/o1';
  const SPLIT_URL = `${ORDER_URL}/split`;

  const line = (overrides: Partial<OrderLine> = {}): OrderLine => ({
    id: 'l1',
    menuItemId: 'm1',
    name: 'Pho',
    unitPrice: 12.5,
    quantity: 2,
    notes: null,
    lineTotal: 25,
    ...overrides,
  });

  const order = (overrides: Partial<Order> = {}): Order => ({
    id: 'o1',
    number: 12,
    status: 'Open',
    createdAt: '2026-10-26T07:30:00+00:00',
    paymentMethod: 'Cash',
    lines: [line()],
    subtotal: 25,
    discount: 0,
    surcharge: 0,
    tax: 2.06,
    tip: 0,
    total: 27.06,
    ...overrides,
  });

  const el = (testId: string) =>
    fixture.nativeElement.querySelector(`[data-testid="${testId}"]`) as HTMLElement | null;
  const text = (testId: string) => el(testId)?.textContent?.trim() ?? null;
  const click = (testId: string) => {
    (el(testId) as HTMLButtonElement).click();
    fixture.detectChanges();
  };

  const expectGet = (): TestRequest => {
    const req = http.expectOne(ORDER_URL);
    expect(req.request.method).toBe('GET');
    return req;
  };
  const expectQuantity = (quantity: number): TestRequest => {
    const req = http.expectOne(`${ORDER_URL}/lines/l1/quantity`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ quantity });
    return req;
  };
  const expectRemove = (): TestRequest => {
    const req = http.expectOne(`${ORDER_URL}/lines/l1`);
    expect(req.request.method).toBe('DELETE');
    return req;
  };
  /** Each order shown makes the split section read the saved split; answer "no split" unless a test says otherwise. */
  const flushSplitReads = (body: OrderSplit | null = null) => {
    for (const req of http.match((r) => r.method === 'GET' && r.url === SPLIT_URL)) {
      if (body) {
        req.flush(body);
      } else {
        req.flush({ status: 404 }, { status: 404, statusText: 'Not Found' });
      }
    }
    fixture.detectChanges();
  };
  const flush = (req: TestRequest, body: Order, split: OrderSplit | null = null) => {
    req.flush(body);
    fixture.detectChanges();
    flushSplitReads(split);
  };
  const fail = (req: TestRequest, status: number, body: object | null = null) => {
    req.flush(body, { status, statusText: 'Error' });
    fixture.detectChanges();
  };

  const load = (body: Order = order()) => {
    fixture.componentRef.setInput('id', 'o1');
    fixture.detectChanges();
    flush(expectGet(), body);
  };

  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({
      imports: [OrderDetail],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    fixture = TestBed.createComponent(OrderDetail);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    vi.useRealTimers();
  });

  it('shows a loading message until the order arrives', () => {
    fixture.componentRef.setInput('id', 'o1');
    fixture.detectChanges();

    expect(el('initial-loading')).not.toBeNull();
    flush(expectGet(), order());
    expect(el('initial-loading')).toBeNull();
  });

  it('shows the order number, status, lines and the six totals exactly as the API sent them', () => {
    load(
      order({
        lines: [line({ name: 'Pho', unitPrice: 7.7, quantity: 2, notes: 'No onions', lineTotal: 15.4 })],
        subtotal: 15.4,
        discount: 1.5,
        surcharge: 0.46,
        tax: 1.2,
        tip: 0,
        total: 15.56,
      }),
    );

    expect(text('order-number')).toBe('Order #12');
    expect(text('order-status')).toBe('Open');
    expect(text('line-name')).toBe('Pho');
    expect(text('line-notes')).toBe('No onions');
    expect(text('line-unit-price')).toBe('$7.70');
    expect(text('line-quantity')).toBe('2');
    expect(text('line-total')).toBe('$15.40');
    expect(text('subtotal')).toBe('$15.40');
    expect(text('discount')).toBe('$1.50');
    expect(text('surcharge')).toBe('$0.46');
    expect(text('tax')).toBe('$1.20');
    expect(text('tip')).toBe('$0.00');
    expect(text('total')).toBe('$15.56');
  });

  it('has a Back to orders link', () => {
    load();
    expect(el('back')?.getAttribute('href')).toBe('/orders');
  });

  it('removes a line and shows the order the API returned', () => {
    load(order({ lines: [line(), line({ id: 'l2', name: 'Iced tea' })] }));

    click('remove');
    flush(expectRemove(), order({ lines: [line({ id: 'l2', name: 'Iced tea' })], subtotal: 3, total: 3.25 }));

    expect(text('line-name')).toBe('Iced tea');
    expect(text('subtotal')).toBe('$3.00');
    expect(text('total')).toBe('$3.25');
  });

  it('sends one request for two quick clicks on Remove of the same line', () => {
    load();

    click('remove');
    click('remove');
    flush(expectRemove(), order({ lines: [], subtotal: 0, tax: 0, total: 0 }));

    expect(el('empty')).not.toBeNull();
    expect(text('total')).toBe('$0.00');
  });

  it('counts every click on + in order (concatMap): three quick clicks add 3', () => {
    load(order({ lines: [line({ quantity: 2 })] }));

    click('increase');
    click('increase');
    click('increase');

    // One request at a time; each starts from the quantity the previous response returned.
    flush(expectQuantity(3), order({ lines: [line({ quantity: 3, lineTotal: 37.5 })], total: 40.59 }));
    flush(expectQuantity(4), order({ lines: [line({ quantity: 4, lineTotal: 50 })], total: 54.13 }));
    flush(expectQuantity(5), order({ lines: [line({ quantity: 5, lineTotal: 62.5 })], total: 67.66 }));

    expect(text('line-quantity')).toBe('5');
    expect(text('line-total')).toBe('$62.50');
    expect(text('total')).toBe('$67.66');
  });

  it('decreases with − and disables − at quantity 1', () => {
    load(order({ lines: [line({ quantity: 2 })] }));
    expect((el('decrease') as HTMLButtonElement).disabled).toBe(false);

    click('decrease');
    flush(expectQuantity(1), order({ lines: [line({ quantity: 1, lineTotal: 12.5 })] }));

    expect(text('line-quantity')).toBe('1');
    expect((el('decrease') as HTMLButtonElement).disabled).toBe(true);
  });

  it('does not send quantity 0 when − is clicked twice quickly at quantity 2', () => {
    load(order({ lines: [line({ quantity: 2 })] }));

    click('decrease');
    click('decrease');
    flush(expectQuantity(1), order({ lines: [line({ quantity: 1, lineTotal: 12.5 })] }));

    http.expectNone(`${ORDER_URL}/lines/l1/quantity`);
  });

  it('keeps the order on screen with a loading indicator while a change is in flight', () => {
    load();
    expect(el('loading')).toBeNull();

    click('increase');

    expect(el('loading')).not.toBeNull();
    expect(text('line-quantity')).toBe('2');
    expect(text('total')).toBe('$27.06');

    flush(expectQuantity(3), order({ lines: [line({ quantity: 3, lineTotal: 37.5 })] }));
    expect(el('loading')).toBeNull();
  });

  it('shows the ProblemDetails detail on 422 and keeps the order without reloading', () => {
    load();
    const detail = 'A quantity of 3 would leave a subtotal of $37.50, less than the discount of $40.00.';

    click('increase');
    fail(expectQuantity(3), 422, { status: 422, detail });

    expect(text('error')).toBe(detail);
    expect(text('line-quantity')).toBe('2');
    expect(text('total')).toBe('$27.06');
    http.expectNone(ORDER_URL);
  });

  it('on 409 shows that the order changed and reloads the latest version', () => {
    load();

    click('remove');
    fail(expectRemove(), 409, { status: 409, detail: 'Conflict' });
    flush(expectGet(), order({ lines: [line({ quantity: 6, lineTotal: 75 })], total: 81.19 }));

    expect(text('error')).toBe(CHANGED_MESSAGE);
    expect(text('line-quantity')).toBe('6');
    expect(text('total')).toBe('$81.19');
  });

  it('on 404 for a line (removed by someone else) shows that the order changed and reloads', () => {
    load();

    click('increase');
    fail(expectQuantity(3), 404, { status: 404 });
    flush(expectGet(), order({ lines: [], subtotal: 0, tax: 0, total: 0 }));

    expect(text('error')).toBe(CHANGED_MESSAGE);
    expect(el('empty')).not.toBeNull();
  });

  it('on a server error shows the generic message and keeps the order', () => {
    load();

    click('increase');
    fail(expectQuantity(3), 500);

    expect(text('error')).toBe(GENERIC_ERROR);
    expect(text('line-quantity')).toBe('2');
    expect(text('total')).toBe('$27.06');
  });

  it('on a network error shows the generic message and keeps the order', () => {
    load();

    click('remove');
    expectRemove().error(new ProgressEvent('error'));
    fixture.detectChanges();

    expect(text('error')).toBe(GENERIC_ERROR);
    expect(text('line-name')).toBe('Pho');
  });

  it('clears the error after the next successful change', () => {
    load();
    click('increase');
    fail(expectQuantity(3), 500);

    click('increase');
    flush(expectQuantity(3), order({ lines: [line({ quantity: 3, lineTotal: 37.5 })] }));

    expect(el('error')).toBeNull();
  });

  it('shows Order not found with a link back to the list when the order does not exist', () => {
    fixture.componentRef.setInput('id', 'o1');
    fixture.detectChanges();
    fail(expectGet(), 404, { status: 404 });

    expect(el('not-found')?.textContent).toContain('Order not found.');
    expect(el('not-found')?.querySelector('a')?.getAttribute('href')).toBe('/orders');
    expect(el('totals')).toBeNull();
  });

  it('shows the generic message when the order cannot be loaded for another reason', () => {
    fixture.componentRef.setInput('id', 'o1');
    fixture.detectChanges();
    fail(expectGet(), 500);

    expect(text('error')).toBe(GENERIC_ERROR);
    expect(el('not-found')).toBeNull();
  });

  it('reloads the order when the split section reports a change, keeping the previous order on screen meanwhile', () => {
    const lines = [line(), line({ id: 'l2', name: 'Iced tea', unitPrice: 3, quantity: 1, lineTotal: 3 })];
    const split: OrderSplit = {
      parts: [
        { lineIds: ['l1'], subtotal: 25, discount: 0, surcharge: 0, tax: 2.06, tip: 0, total: 27.06 },
        { lineIds: ['l2'], subtotal: 3, discount: 0, surcharge: 0, tax: 0.25, tip: 0, total: 3.25 },
      ],
    };
    fixture.componentRef.setInput('id', 'o1');
    fixture.detectChanges();
    flush(expectGet(), order({ lines, subtotal: 28, tax: 2.31, total: 30.31 }), split);

    click('split-save');
    const save = http.expectOne((r) => r.method === 'PUT' && r.url === SPLIT_URL);
    fail(save, 409, { status: 409, detail: 'The data was changed by another request. Reload and try again.' });

    // The reload is pending: the previous order stays with the loading indicator.
    const reload = expectGet();
    expect(el('loading')).not.toBeNull();
    expect(text('total')).toBe('$30.31');

    flush(reload, order({ lines, subtotal: 28, tax: 2.31, tip: 5, total: 35.31 }), split);

    expect(text('error')).toBe(CHANGED_MESSAGE);
    expect(text('tip')).toBe('$5.00');
    expect(text('total')).toBe('$35.31');
    expect(el('loading')).toBeNull();
  });

  it('keeps the split amounts out of the order totals block', () => {
    const lines = [line(), line({ id: 'l2', name: 'Iced tea', unitPrice: 3, quantity: 1, lineTotal: 3 })];
    fixture.componentRef.setInput('id', 'o1');
    fixture.detectChanges();
    flush(expectGet(), order({ lines, subtotal: 28, total: 30.31 }), {
      parts: [
        { lineIds: ['l1'], subtotal: 25, discount: 0, surcharge: 0, tax: 2.06, tip: 0, total: 27.06 },
        { lineIds: ['l2'], subtotal: 3, discount: 0, surcharge: 0, tax: 0.25, tip: 0, total: 3.25 },
      ],
    });

    expect(el('split-section')).not.toBeNull();
    expect(el('totals')?.querySelector('[data-testid="split-section"]')).toBeNull();
    expect(el('totals')?.querySelectorAll('[data-testid^="split-"]').length).toBe(0);
  });

  it.each(['Paid', 'Cancelled'] as const)('is read-only when the order is %s', (status) => {
    load(order({ status }));

    expect(el('line-row')).not.toBeNull();
    expect(el('remove')).toBeNull();
    expect(el('increase')).toBeNull();
    expect(el('decrease')).toBeNull();
  });
});
