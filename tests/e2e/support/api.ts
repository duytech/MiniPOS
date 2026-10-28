import { expect, type APIRequestContext } from '@playwright/test';

// Test data goes in through the public API, the same way a cashier's actions would, so no test reaches into the
// database. Requests use the Playwright baseURL, i.e. the web app's /api proxy.

export interface MenuItem {
  id: string;
  name: string;
  price: number;
}

export async function createMenuItem(api: APIRequestContext, name: string, price: number): Promise<MenuItem> {
  const response = await api.post('/api/menu-items', { data: { name, price } });
  expect(response.status(), await response.text()).toBe(201);
  return (await response.json()) as MenuItem;
}

export async function createOrder(api: APIRequestContext): Promise<string> {
  const response = await api.post('/api/orders');
  expect(response.status(), await response.text()).toBe(201);
  return ((await response.json()) as { id: string }).id;
}

export async function addItem(
  api: APIRequestContext,
  orderId: string,
  menuItemId: string,
  quantity: number,
  notes?: string,
): Promise<void> {
  const response = await api.post(`/api/orders/${orderId}/items`, { data: { menuItemId, quantity, notes } });
  expect(response.status(), await response.text()).toBe(200);
}

/** The order exactly as the API returns it; money fields keep their JSON text so tests can compare cents. */
export async function getOrderJson(api: APIRequestContext, orderId: string): Promise<Record<string, unknown>> {
  const response = await api.get(`/api/orders/${orderId}`);
  expect(response.status(), await response.text()).toBe(200);
  return (await response.json()) as Record<string, unknown>;
}
