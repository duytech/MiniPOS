import { expect, test } from '@playwright/test';
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

// Bad data: menu item names of ~300 characters on an order line. No rule says how long names are displayed
// (wrap / truncate), so only CLAUDE.md rules are asserted: amounts equal the API to the cent, shown as $x.xx, and at
// 375 px no horizontal page scroll with money and row actions on screen.

const LENGTH = 300;

function longName(kind: 'no-spaces' | 'words', stamp: string): string {
  const prefix = kind === 'no-spaces' ? `Nospace${stamp}` : `Words ${stamp}`;
  const filler = kind === 'no-spaces' ? 'Bunbohue' : ' bun bo hue';
  let name = prefix;
  while (name.length < LENGTH) name += filler;
  return name.slice(0, LENGTH);
}

for (const kind of ['no-spaces', 'words'] as const) {
  test.describe(`300-character item name (${kind})`, () => {
    let orderId: string;
    let name: string;
    let stamp: string;

    test.beforeEach(async ({ request }, testInfo) => {
      stamp = `${Date.now()}${testInfo.project.name}`;
      name = longName(kind, stamp);
      expect(name).toHaveLength(LENGTH);
      const item = await createMenuItem(request, name, 12.35);
      orderId = await createOrder(request);
      await addItem(request, orderId, item.id, 3);
    });

    test('order detail shows amounts equal to the API and fits at 375 px', async ({ page, request }) => {
      const order = await getOrder(request, orderId);
      await page.goto(`/orders/${orderId}`);
      await expectDetailMatchesApi(page, order);
      if (isPhone(page)) {
        await expectDetailFitsPhone(page);
      }
      await page.screenshot({ path: test.info().outputPath(`detail-${kind}.png`), fullPage: true });
    });

    test('order list total equals the API total and fits at 375 px', async ({ page, request }) => {
      const order = await getOrder(request, orderId);
      await page.goto('/orders');
      await searchOrders(page, stamp);
      await expectListTotalMatchesApi(page, order);
      if (isPhone(page)) {
        await expectListFitsPhone(page);
      }
      await page.screenshot({ path: test.info().outputPath(`list-${kind}.png`), fullPage: true });
    });
  });
}
