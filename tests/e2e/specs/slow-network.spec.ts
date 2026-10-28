import { expect, test, type Page } from '@playwright/test';
import { addItem, createMenuItem, createOrder } from '../support/api';
import {
  expectDetailMatchesApi,
  expectListTotalMatchesApi,
  getOrder,
  INITIAL_LOADING,
  listRow,
  RELOAD_INDICATOR,
  searchOrders,
} from '../support/pages';

// Slow network: every /api call waits ~2 s before it goes out. CLAUDE.md: when reloading data the previous data
// stays visible with a small loading indicator; the table is not swapped for a spinner. Amounts still equal the API.

const DELAY_MS = 2_000;

async function slowApi(page: Page): Promise<void> {
  await page.route('**/api/**', async (route) => {
    await new Promise((resolve) => setTimeout(resolve, DELAY_MS));
    await route.continue();
  });
}

test.describe('slow network', () => {
  let orderId: string;
  let stamp: string;
  let itemName: string;

  test.beforeEach(async ({ request }, testInfo) => {
    stamp = `${Date.now()}${testInfo.project.name}`;
    itemName = `Slow com tam ${stamp}`;
    const item = await createMenuItem(request, itemName, 0.05);
    const other = await createMenuItem(request, `Slow tra da ${stamp}`, 12.35);
    orderId = await createOrder(request);
    await addItem(request, orderId, item.id, 3);
    await addItem(request, orderId, other.id, 2);
  });

  test('changing the search on /orders keeps the previous rows with a loading indicator', async ({
    page,
    request,
  }) => {
    const order = await getOrder(request, orderId);
    await page.goto('/orders');
    const table = page.getByRole('table').first();
    const ourRow = listRow(page, order.number);
    await expect(ourRow).toBeVisible();
    const previousRows = await table.locator('tbody tr').count();

    await slowApi(page);
    const searched = page.waitForResponse((r) => r.url().includes('/api/orders') && r.url().includes(stamp));
    await searchOrders(page, stamp);

    await expect(page.locator(RELOAD_INDICATOR), 'small loading indicator while reloading').toBeVisible();
    await expect(table, 'table stays while reloading').toBeVisible();
    await expect(ourRow, 'previous rows stay while reloading').toBeVisible();
    expect(await table.locator('tbody tr').count(), 'previous row count while reloading').toBe(previousRows);
    await expect(page.locator(INITIAL_LOADING), 'no full spinner while reloading').toHaveCount(0);
    await page.screenshot({ path: test.info().outputPath('list-search-reloading.png') });

    await searched;
    await expect(page.locator(RELOAD_INDICATOR)).toHaveCount(0);
    await expect(table.locator('tbody tr')).toHaveCount(1);
    await expectListTotalMatchesApi(page, order);
  });

  test('changing the status filter on /orders keeps the previous rows with a loading indicator', async ({
    page,
    request,
  }) => {
    const order = await getOrder(request, orderId);
    await page.goto('/orders');
    const table = page.getByRole('table').first();
    await searchOrders(page, stamp);
    const ourRow = listRow(page, order.number);
    await expect(table.locator('tbody tr')).toHaveCount(1);
    await expect(ourRow).toBeVisible();

    await slowApi(page);
    const filtered = page.waitForResponse((r) => r.url().includes('/api/orders') && /status=Open/i.test(r.url()));
    await page.getByRole('combobox', { name: 'Status' }).selectOption({ label: 'Open' });

    await expect(page.locator(RELOAD_INDICATOR), 'small loading indicator while reloading').toBeVisible();
    await expect(table, 'table stays while reloading').toBeVisible();
    await expect(ourRow, 'previous row stays while reloading').toBeVisible();
    await expect(page.locator(INITIAL_LOADING), 'no full spinner while reloading').toHaveCount(0);
    await page.screenshot({ path: test.info().outputPath('list-status-reloading.png') });

    await filtered;
    await expect(page.locator(RELOAD_INDICATOR)).toHaveCount(0);
    await expectListTotalMatchesApi(page, order);
  });

  test('/orders/{id} loads on a slow network with amounts equal to the API', async ({ page, request }) => {
    const order = await getOrder(request, orderId);
    await slowApi(page);
    await page.goto(`/orders/${orderId}`);
    await expect(page.getByText(itemName, { exact: true })).toBeVisible({ timeout: 10_000 });
    await expectDetailMatchesApi(page, order);
  });

  test('changing a line on /orders/{id} keeps the lines visible with a loading indicator', async ({
    page,
    request,
  }) => {
    await page.goto(`/orders/${orderId}`);
    const table = page.getByRole('table').first();
    const row = table.locator('tbody tr').filter({ hasText: itemName });
    await expect(row).toBeVisible();

    await slowApi(page);
    const changed = page.waitForResponse(
      (r) => r.url().includes(`/api/orders/${orderId}`) && r.request().method() !== 'GET',
    );
    await page.getByRole('button', { name: `Increase quantity of ${itemName}`, exact: true }).click();

    await expect(page.locator(RELOAD_INDICATOR), 'small loading indicator while reloading').toBeVisible();
    await expect(table, 'table stays while reloading').toBeVisible();
    await expect(row, 'line stays while reloading').toBeVisible();
    await expect(page.locator(INITIAL_LOADING), 'no full spinner while reloading').toHaveCount(0);
    await page.screenshot({ path: test.info().outputPath('detail-change-reloading.png') });

    await changed;
    await expect(page.locator(RELOAD_INDICATOR)).toHaveCount(0, { timeout: 10_000 });
    await expectDetailMatchesApi(page, await getOrder(request, orderId));
  });
});
