import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Order, OrderMenuItem, OrderSplit, SplitPart } from '../order.models';
import { GENERIC_ERROR } from '../api-error';
import { CLEARED_MESSAGE, OrderSplitSection, TOO_FEW_ITEMS_MESSAGE } from './order-split';

describe('OrderSplitSection', () => {
  let fixture: ComponentFixture<OrderSplitSection>;
  let http: HttpTestingController;
  let changedCount: number;

  const SPLIT_URL = '/api/orders/o1/split';
  const PREVIEW_URL = `${SPLIT_URL}/preview`;

  const item = (id: string, name: string, itemTotal: number): OrderMenuItem => ({
    id,
    menuItemId: `m-${id}`,
    name,
    unitPrice: itemTotal,
    quantity: 1,
    notes: null,
    itemTotal,
  });

  const ITEMS = [item('l1', 'Pho', 60), item('l2', 'Banh mi', 40), item('l3', 'Iced tea', 3)];

  const order = (overrides: Partial<Order> = {}): Order => ({
    id: 'o1',
    number: 12,
    status: 'Open',
    createdAt: '2026-10-26T07:30:00+00:00',
    paymentMethod: 'Cash',
    items: ITEMS,
    subtotal: 103,
    discount: 0,
    surcharge: 0,
    tax: 9.14,
    tip: 0,
    total: 112.14,
    ...overrides,
  });

  const part = (itemIds: string[], overrides: Partial<SplitPart> = {}): SplitPart => ({
    itemIds,
    subtotal: 60,
    discount: 0,
    surcharge: 0,
    tax: 5.33,
    tip: 0,
    total: 65.33,
    ...overrides,
  });

  const el = (testId: string) =>
    fixture.nativeElement.querySelector(`[data-testid="${testId}"]`) as HTMLElement | null;
  const all = (testId: string) =>
    Array.from(fixture.nativeElement.querySelectorAll(`[data-testid="${testId}"]`)) as HTMLElement[];
  const text = (testId: string) => el(testId)?.textContent?.trim() ?? null;
  const texts = (testId: string) => all(testId).map((e) => e.textContent?.trim());
  const itemSelects = () => all('split-item-part') as HTMLSelectElement[];
  const assignments = () => itemSelects().map((s) => s.value);
  const partCountOptions = () =>
    Array.from((el('split-part-count') as HTMLSelectElement).options).map((o) => o.value);

  const choose = (select: HTMLElement | null, value: string) => {
    const element = select as HTMLSelectElement;
    element.value = value;
    element.dispatchEvent(new Event('change'));
    fixture.detectChanges();
  };
  const assign = (index: number, partNumber: number | null) =>
    choose(itemSelects()[index], partNumber === null ? '' : String(partNumber - 1));
  const click = (testId: string) => {
    (el(testId) as HTMLButtonElement).click();
    fixture.detectChanges();
  };

  const expectGet = (): TestRequest => {
    const req = http.expectOne((r) => r.method === 'GET' && r.url === SPLIT_URL);
    return req;
  };
  const expectPreview = (body?: unknown): TestRequest => {
    const req = http.expectOne((r) => r.method === 'POST' && r.url === PREVIEW_URL);
    if (body !== undefined) {
      expect(req.request.body).toEqual(body);
    }
    return req;
  };
  const expectSave = (): TestRequest => http.expectOne((r) => r.method === 'PUT' && r.url === SPLIT_URL);
  const expectRemove = (): TestRequest => http.expectOne((r) => r.method === 'DELETE' && r.url === SPLIT_URL);
  const noPreview = () => http.expectNone((r) => r.url === PREVIEW_URL);

  const ok = <T>(data: T) => ({ isSuccess: true, data, error: null });
  const failed = (code: string, message = '') => ({ isSuccess: false, data: null, error: { code, message } });
  const flush = (req: TestRequest, body: OrderSplit | null) => {
    req.flush(ok(body));
    fixture.detectChanges();
  };
  const fail = (req: TestRequest, code: string, message = '') => {
    req.flush(failed(code, message));
    fixture.detectChanges();
  };
  const failHttp = (req: TestRequest, status: number) => {
    req.flush(null, { status, statusText: 'Error' });
    fixture.detectChanges();
  };

  const show = (value: Order) => {
    fixture.componentRef.setInput('order', value);
    fixture.detectChanges();
  };
  /** Shows the order and answers its GET split: a saved split, or 404 when `split` is null. */
  const load = (value: Order = order(), split: OrderSplit | null = null) => {
    show(value);
    if (split) {
      flush(expectGet(), split);
    } else {
      fail(expectGet(), 'SplitNotFound', "Split 'o1' was not found.");
    }
  };

  /** Items l1, l2 → part 1; l3 → part 2: the preview fires on the last assignment. */
  const assignAll = () => {
    assign(0, 1);
    assign(1, 1);
    assign(2, 2);
  };
  const ALL_ASSIGNED_BODY = { parts: [{ itemIds: ['l1', 'l2'] }, { itemIds: ['l3'] }] };
  const PREVIEW: OrderSplit = {
    parts: [
      part(['l1', 'l2'], { subtotal: 100, tax: 8.87, total: 108.87 }),
      part(['l3'], { subtotal: 3, tax: 0.27, total: 3.27 }),
    ],
  };
  const SAVED: OrderSplit = { parts: [part(['l1']), part(['l2'], { subtotal: 40, tax: 3.55, total: 43.55 }), part(['l3'])] };

  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({
      imports: [OrderSplitSection],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    fixture = TestBed.createComponent(OrderSplitSection);
    http = TestBed.inject(HttpTestingController);
    changedCount = 0;
    fixture.componentInstance.changed.subscribe(() => changedCount++);
  });

  afterEach(() => {
    http.verify();
    vi.useRealTimers();
  });

  it('with no saved split shows 2 parts, every item unassigned and sends no preview', () => {
    load();

    expect(el('split-section')).not.toBeNull();
    expect((el('split-part-count') as HTMLSelectElement).value).toBe('2');
    expect(partCountOptions()).toEqual(['2', '3']);
    expect(assignments()).toEqual(['', '', '']);
    expect(Array.from(itemSelects()[0].options).map((o) => o.textContent?.trim())).toEqual([
      'Unassigned',
      'Part 1',
      'Part 2',
    ]);
    expect(all('split-part-row')).toHaveLength(0);
    expect(el('split-remove')).toBeNull();
    expect(el('split-save')).not.toBeNull();
    noPreview();
  });

  it('fills the builder from a saved split: part count is its number of parts', () => {
    load(order(), SAVED);

    expect((el('split-part-count') as HTMLSelectElement).value).toBe('3');
    expect(assignments()).toEqual(['0', '1', '2']);
    expect(all('split-part-row')).toHaveLength(3);
    expect(texts('split-part-items')).toEqual(['Pho', 'Banh mi', 'Iced tea']);
    expect(el('split-remove')).not.toBeNull();
    noPreview();
  });

  it('previews only once every item has a part, with the parts in order', () => {
    load();

    assign(0, 1);
    assign(1, 1);
    noPreview();
    assign(2, 2);

    flush(expectPreview(ALL_ASSIGNED_BODY), PREVIEW);
    expect(all('split-part-row')).toHaveLength(2);
    expect(texts('split-part-items')).toEqual(['Pho, Banh mi', 'Iced tea']);
  });

  it('cancels the pending preview when the assignment changes again (switchMap)', () => {
    load();
    assignAll();
    const first = expectPreview(ALL_ASSIGNED_BODY);

    assign(2, 1);
    const second = expectPreview({ parts: [{ itemIds: ['l1', 'l2', 'l3'] }, { itemIds: [] }] });

    expect(first.cancelled).toBe(true);
    fail(second, 'InvalidSplit', 'Every part of a split needs at least one item.');
    expect(text('split-error')).toBe('Every part of a split needs at least one item.');
  });

  it('shows each part amount exactly as the API sent it', () => {
    load();
    assignAll();

    flush(expectPreview(), {
      parts: [
        part(['l1', 'l2'], { subtotal: 15.4, discount: 1.09, surcharge: 0.46, tax: 1.2, tip: 2, total: 18.02 }),
        part(['l3'], { subtotal: 3, discount: 0, surcharge: 0, tax: 0, tip: 0, total: 0 }),
      ],
    });

    expect(texts('split-part-subtotal')).toEqual(['$15.40', '$3.00']);
    expect(texts('split-part-discount')).toEqual(['$1.09', '$0.00']);
    expect(texts('split-part-surcharge')).toEqual(['$0.46', '$0.00']);
    expect(texts('split-part-tax')).toEqual(['$1.20', '$0.00']);
    expect(texts('split-part-tip')).toEqual(['$2.00', '$0.00']);
    expect(texts('split-part-total')).toEqual(['$18.02', '$0.00']);
  });

  it('keeps the previous parts on screen with the loading indicator while a preview runs', () => {
    load();
    assignAll();
    flush(expectPreview(), PREVIEW);
    expect(el('split-loading')).toBeNull();

    assign(1, 2);
    const pending = expectPreview({ parts: [{ itemIds: ['l1'] }, { itemIds: ['l2', 'l3'] }] });

    expect(el('split-loading')).not.toBeNull();
    expect(texts('split-part-total')).toEqual(['$108.87', '$3.27']);

    flush(pending, { parts: [part(['l1']), part(['l2', 'l3'], { subtotal: 43, total: 46.82 })] });
    expect(el('split-loading')).toBeNull();
    expect(texts('split-part-total')).toEqual(['$65.33', '$46.82']);
  });

  it('saves the parts in order and shows the split the API returned; a second click while saving is ignored', () => {
    load();
    assignAll();
    flush(expectPreview(), PREVIEW);

    click('split-save');
    click('split-save');
    const save = expectSave();
    expect(save.request.body).toEqual(ALL_ASSIGNED_BODY);
    expect(el('split-loading')).not.toBeNull();

    flush(save, { parts: [part(['l1', 'l2'], { total: 108.88 }), part(['l3'], { total: 3.26 })] });

    expect(texts('split-part-total')).toEqual(['$108.88', '$3.26']);
    expect(el('split-remove')).not.toBeNull();
    expect(el('split-loading')).toBeNull();
  });

  it('lowering the part count unassigns the items of the removed parts', () => {
    load(order(), SAVED);

    choose(el('split-part-count'), '2');

    expect(assignments()).toEqual(['0', '1', '']);
    expect(Array.from(itemSelects()[0].options).map((o) => o.value)).toEqual(['', '0', '1']);
    expect(all('split-part-row')).toHaveLength(0); // not every item has a part: no amounts to show
    noPreview();
  });

  it('raising the part count keeps the assignments and previews if every item still has a part', () => {
    load();
    assign(0, 1);
    assign(1, 2);
    choose(el('split-part-count'), '3');
    assign(2, 3);

    flush(expectPreview({ parts: [{ itemIds: ['l1'] }, { itemIds: ['l2'] }, { itemIds: ['l3'] }] }), SAVED);
    expect(assignments()).toEqual(['0', '1', '2']);
  });

  it('shows the message instead of the builder when an open order has fewer than 2 items', () => {
    load(order({ items: [ITEMS[0]] }));

    expect(text('split-too-few-items')).toBe(TOO_FEW_ITEMS_MESSAGE);
    expect(el('split-part-count')).toBeNull();
    expect(el('split-save')).toBeNull();
  });

  it.each(['Paid', 'Cancelled'] as const)('shows a saved split read-only on a %s order', (status) => {
    load(order({ status }), SAVED);

    expect(el('split-section')).not.toBeNull();
    expect(texts('split-part-total')).toEqual(['$65.33', '$43.55', '$65.33']);
    expect(el('split-part-count')).toBeNull();
    expect(el('split-item-part')).toBeNull();
    expect(el('split-save')).toBeNull();
    expect(el('split-remove')).toBeNull();
  });

  it.each(['Paid', 'Cancelled'] as const)('hides the section on a %s order with no saved split', (status) => {
    load(order({ status }));

    expect(el('split-section')).toBeNull();
  });

  it('after an item change clears the saved split, says so and keeps the draft of the remaining items', () => {
    load(order(), SAVED);

    // l2 was removed and l4 added: the API cleared the split (R5).
    show(order({ items: [ITEMS[0], ITEMS[2], item('l4', 'Spring rolls', 8)] }));
    // While the GET runs, the draft is already reconciled.
    expect(assignments()).toEqual(['0', '2', '']);
    fail(expectGet(), 'SplitNotFound');

    expect(text('split-message')).toBe(CLEARED_MESSAGE);
    expect(el('split-remove')).toBeNull();
    expect((el('split-part-count') as HTMLSelectElement).value).toBe('3');
    expect(assignments()).toEqual(['0', '2', '']);
    noPreview();
  });

  it('after an item change with every remaining item still assigned, previews the new amounts', () => {
    load(order(), { parts: [part(['l1', 'l3']), part(['l2'])] });

    show(order({ items: [ITEMS[0], ITEMS[1]] }));
    fail(expectGet(), 'SplitNotFound');

    expect(text('split-message')).toBe(CLEARED_MESSAGE);
    expect((el('split-part-count') as HTMLSelectElement).value).toBe('2');
    flush(expectPreview({ parts: [{ itemIds: ['l1'] }, { itemIds: ['l2'] }] }), {
      parts: [part(['l1']), part(['l2'], { total: 43.55 })],
    });
    expect(texts('split-part-total')).toEqual(['$65.33', '$43.55']);
  });

  it('drops the part count to the number of items when items are removed', () => {
    load(order(), SAVED);

    show(order({ items: [ITEMS[0], ITEMS[1]] }));
    fail(expectGet(), 'SplitNotFound');

    expect((el('split-part-count') as HTMLSelectElement).value).toBe('2');
    expect(partCountOptions()).toEqual(['2']);
    expect(assignments()).toEqual(['0', '1']);
    flush(expectPreview({ parts: [{ itemIds: ['l1'] }, { itemIds: ['l2'] }] }), PREVIEW);
  });

  it('does not show the cleared message when no split was saved', () => {
    load();

    show(order({ items: [ITEMS[0], ITEMS[1]] }));
    fail(expectGet(), 'SplitNotFound');

    expect(el('split-message')).toBeNull();
  });

  it('shows the message of a broken rule when saving fails and keeps the parts', () => {
    load();
    assignAll();
    flush(expectPreview(), PREVIEW);
    const detail = 'The order is Paid; only an open order can be changed.';

    click('split-save');
    fail(expectSave(), 'OrderNotOpen', detail);

    expect(text('split-error')).toBe(detail);
    expect(texts('split-part-total')).toEqual(['$108.87', '$3.27']);
    expect(changedCount).toBe(0);
  });

  it('sends an incomplete draft as it is and shows the message of a broken rule', () => {
    load();
    assign(0, 1);

    click('split-save');
    const save = expectSave();
    expect(save.request.body).toEqual({ parts: [{ itemIds: ['l1'] }, { itemIds: [] }] });
    fail(save, 'InvalidSplit', 'Every item of the order must be in a part of the split.');

    expect(text('split-error')).toBe('Every item of the order must be in a part of the split.');
  });

  it('on a conflict when saving emits changed for the parent to reload', () => {
    load();
    assignAll();
    flush(expectPreview(), PREVIEW);

    click('split-save');
    fail(expectSave(), 'ConcurrentChange', 'The data was changed by another request. Reload and try again.');

    expect(changedCount).toBe(1);
    expect(el('split-error')).toBeNull();
    expect(el('split-loading')).toBeNull();
  });

  it('after the conflict reload refills the builder from the saved split', () => {
    load();
    assign(0, 2);

    show(order());
    flush(expectGet(), SAVED);

    expect(assignments()).toEqual(['0', '1', '2']);
    expect((el('split-part-count') as HTMLSelectElement).value).toBe('3');
    expect(el('split-message')).toBeNull();
  });

  it('on a server error when saving shows the generic message and keeps the previous parts', () => {
    load();
    assignAll();
    flush(expectPreview(), PREVIEW);

    click('split-save');
    failHttp(expectSave(), 500);

    expect(text('split-error')).toBe(GENERIC_ERROR);
    expect(texts('split-part-total')).toEqual(['$108.87', '$3.27']);
  });

  it('on a network error in a preview shows the generic message and keeps the previous parts', () => {
    load();
    assignAll();
    flush(expectPreview(), PREVIEW);

    assign(1, 2);
    expectPreview().error(new ProgressEvent('error'));
    fixture.detectChanges();

    expect(text('split-error')).toBe(GENERIC_ERROR);
    expect(texts('split-part-total')).toEqual(['$108.87', '$3.27']);
    expect(el('split-loading')).toBeNull();
  });

  it('treats a conflict from preview like any other error: the generic message, no reload', () => {
    load();
    assignAll();

    fail(expectPreview(), 'ConcurrentChange', 'Conflict');

    expect(text('split-error')).toBe(GENERIC_ERROR);
    expect(changedCount).toBe(0);
  });

  it('shows the message of a broken rule from a preview', () => {
    load();
    assignAll();

    fail(expectPreview(), 'ItemNotOnOrder', 'Item l3 is not on this order.');

    expect(text('split-error')).toBe('Item l3 is not on this order.');
  });

  it('removes the saved split and hides Remove split; a second click while removing is ignored', () => {
    load(order(), SAVED);

    click('split-remove');
    click('split-remove');
    const remove = expectRemove();
    expect(remove.request.body).toBeNull();
    remove.flush(ok(true));
    fixture.detectChanges();

    expect(el('split-remove')).toBeNull();
    expect(el('split-error')).toBeNull();
    expect(assignments()).toEqual(['0', '1', '2']);
  });

  it.each(['SplitNotFound', 'ConcurrentChange'])('on %s when removing emits changed for the parent to reload', (code) => {
    load(order(), SAVED);

    click('split-remove');
    fail(expectRemove(), code);

    expect(changedCount).toBe(1);
    expect(el('split-error')).toBeNull();
  });

  it('shows the message of a broken rule when removing fails', () => {
    load(order(), SAVED);
    const detail = 'The order is Cancelled; only an open order can be changed.';

    click('split-remove');
    fail(expectRemove(), 'OrderNotOpen', detail);

    expect(text('split-error')).toBe(detail);
    expect(changedCount).toBe(0);
  });

  it('shows the generic message when the saved split cannot be read', () => {
    show(order());
    failHttp(expectGet(), 500);

    expect(text('split-error')).toBe(GENERIC_ERROR);
    expect(assignments()).toEqual(['', '', '']);
  });
});
