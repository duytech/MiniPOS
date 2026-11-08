import { expect, type APIRequestContext } from '@playwright/test';

// Test data goes in through the public API, the same way a cashier's actions would, so no test reaches into the
// database. Requests use the Playwright baseURL, i.e. the web app's /api proxy.

interface ApiAnswer {
  status(): number;
  text(): Promise<string>;
  json(): Promise<unknown>;
}

interface Envelope {
  isSuccess: boolean;
  data: unknown;
  error: { code: string; message: string } | null;
}

/** The whole envelope; every expected answer is HTTP 200, success or failure. */
export async function envelopeOf(response: ApiAnswer): Promise<Envelope> {
  expect(response.status(), await response.text()).toBe(200);
  return (await response.json()) as Envelope;
}

/** The data of a successful answer; fails on an expected failure too, which is also HTTP 200. */
export async function dataOf<T>(response: ApiAnswer): Promise<T> {
  const envelope = await envelopeOf(response);
  expect(envelope.isSuccess, await response.text()).toBe(true);
  return envelope.data as T;
}

export interface MenuItem {
  id: string;
  name: string;
  price: number;
}

export async function createMenuItem(api: APIRequestContext, name: string, price: number): Promise<MenuItem> {
  const response = await api.post('/api/menu-items', { data: { name, price } });
  return dataOf<MenuItem>(response);
}

export async function createOrder(api: APIRequestContext): Promise<string> {
  const response = await api.post('/api/orders');
  return (await dataOf<{ id: string }>(response)).id;
}

export async function addItem(
  api: APIRequestContext,
  orderId: string,
  menuItemId: string,
  quantity: number,
  notes?: string,
): Promise<void> {
  const response = await api.post(`/api/orders/${orderId}/items`, { data: { menuItemId, quantity, notes } });
  await dataOf(response);
}

/** The order exactly as the API returns it; money fields keep their JSON text so tests can compare cents. */
export async function getOrderJson(api: APIRequestContext, orderId: string): Promise<Record<string, unknown>> {
  const response = await api.get(`/api/orders/${orderId}`);
  return dataOf<Record<string, unknown>>(response);
}
