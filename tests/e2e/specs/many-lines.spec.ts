import { expect, test, type APIRequestContext } from '@playwright/test';
import { addItem, createMenuItem, createOrder } from '../support/api';
import { isPhone } from '../support/checks';
import {
  expectDetailFitsPhone,
  expectDetailMatchesApi,
  expectListFitsPhone,
  expectListTotalMatchesApi,
  getOrder,
  searchOrders,
} from '../support/pages';

// Bad data: one order with 200 different lines (200 menu items), prices with cents (0.05, 12.35, ...), quantity > 1
// on some lines. No rule covers layout or paging of 200 lines, so only CLAUDE.md rules are asserted: every shown
// amount equals the API to the cent as $x.xx, the list total equals the detail total, and at 375 px no horizontal
// page scroll with money and Remove buttons on screen.

const LINES = 200;
const PRICE_CENTS = [5, 1235, 1, 99, 310, 1995, 770, 4555, 15, 125, 105, 999];

function priceOf(i: number): number {
  return (PRICE_CENTS[i % PRICE_CENTS.length] + Math.floor(i / PRICE_CENTS.length)) / 100;
}

function quantityOf(i: number): number {
  if (i % 3 === 0) return 3;
  if (i % 7 === 0) return 2;
  return 1;
}

async function createBigOrder(api: APIRequestContext, stamp: string): Promise<string> {
  const items = await Promise.all(
    Array.from({ length: LINES }, (_, i) => createMenuItem(api, `Bulk ${stamp} line ${i + 1}`, priceOf(i))),
  );
  const orderId = await createOrder(api);
  // Sequential: parallel adds to one order may legitimately get 409.
  for (const [i, item] of items.entries()) {
    await addItem(api, orderId, item.id, quantityOf(i));
  }
  return orderId;
}

test.describe('order with 200 lines', () => {
  let orderId: string;
  let stamp: string;

  test.beforeEach(async ({ request }, testInfo) => {
    // Setup creates 400 records through the API; the time limit covers data setup, not page waits.
    testInfo.setTimeout(180_000);
    stamp = `${Date.now()}${testInfo.project.name}`;
    orderId = await createBigOrder(request, stamp);
  });

  test('order detail shows all amounts equal to the API and fits at 375 px', async ({ page, request }) => {
    const order = await getOrder(request, orderId);
    expect(order.lines).toHaveLength(LINES);
    await page.goto(`/orders/${orderId}`);
    await expectDetailMatchesApi(page, order);
    if (isPhone(page)) {
      await expectDetailFitsPhone(page);
    }
    await page.screenshot({ path: test.info().outputPath('detail-200-lines.png') });
  });

  test('order list total equals the API total and fits at 375 px', async ({ page, request }) => {
    const order = await getOrder(request, orderId);
    await page.goto('/orders');
    await searchOrders(page, stamp);
    await expectListTotalMatchesApi(page, order);
    if (isPhone(page)) {
      await expectListFitsPhone(page);
    }
    await page.screenshot({ path: test.info().outputPath('list-200-lines.png') });
  });
});
