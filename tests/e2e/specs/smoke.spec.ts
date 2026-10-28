import { expect, test } from '@playwright/test';
import { addItem, createMenuItem, createOrder } from '../support/api';

test('order detail shows a line added through the API', async ({ page, request }) => {
  const item = await createMenuItem(request, `Smoke pho ${Date.now()}`, 7.7);
  const orderId = await createOrder(request);
  await addItem(request, orderId, item.id, 2);

  await page.goto(`/orders/${orderId}`);

  await expect(page.getByText(item.name, { exact: true })).toBeVisible();
});
