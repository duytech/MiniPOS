import { expect, type APIRequestContext, type Locator, type Page } from '@playwright/test';
import { addItem, createMenuItem, createOrder } from './api';
import { apiCents, shownCents } from './checks';
import { getOrder, type ApiOrder } from './pages';

// Split bill (plans/tuan-3-ngay-7-split-bill.md). API helpers follow the Contract; page helpers use the plan's test
// ids (split-*) and labels from R14 ("Part N", "Unassigned", "Save split", "Remove split").

export interface ApiSplitPart {
  lineIds: string[];
  subtotal: number;
  discount: number;
  surcharge: number;
  tax: number;
  tip: number;
  total: number;
}

export interface ApiSplit {
  parts: ApiSplitPart[];
}

export const SPLIT_MONEY = ['subtotal', 'discount', 'surcharge', 'tax', 'tip', 'total'] as const;

export const TOO_FEW_LINES_MESSAGE = 'Add at least 2 lines to split the bill.';
export const CLEARED_MESSAGE = 'The saved split was cleared because the order changed.';

/** An open order with one line per item, each from its own menu item named `<prefix> <letter>`; lines in API order. */
export async function orderWithLines(
  api: APIRequestContext,
  prefix: string,
  prices: number[],
): Promise<{ orderId: string; order: ApiOrder; names: string[] }> {
  const orderId = await createOrder(api);
  const names: string[] = [];
  for (const [i, price] of prices.entries()) {
    const item = await createMenuItem(api, `${prefix} ${String.fromCharCode(65 + i)}`, price);
    await addItem(api, orderId, item.id, 1);
    names.push(item.name);
  }
  return { orderId, order: await getOrder(api, orderId), names };
}

/** Line ids of `order` in the order of `names`. */
export function lineIdsOf(order: ApiOrder, names: string[]): string[] {
  return names.map((name) => {
    const line = order.lines.find((l) => l.name === name);
    expect(line, `line "${name}" on order ${order.id}`).toBeDefined();
    return (line as { id: string }).id;
  });
}

/** GET /api/orders/{id}/split; `null` on 404 (no saved split). */
export async function getSplit(api: APIRequestContext, orderId: string): Promise<ApiSplit | null> {
  const response = await api.get(`/api/orders/${orderId}/split`);
  if (response.status() === 404) return null;
  expect(response.status(), await response.text()).toBe(200);
  return (await response.json()) as ApiSplit;
}

export async function saveSplit(api: APIRequestContext, orderId: string, parts: string[][]): Promise<ApiSplit> {
  const response = await api.put(`/api/orders/${orderId}/split`, { data: { parts: parts.map((lineIds) => ({ lineIds })) } });
  expect(response.status(), await response.text()).toBe(200);
  return (await response.json()) as ApiSplit;
}

export async function payOrder(api: APIRequestContext, orderId: string): Promise<void> {
  const response = await api.post(`/api/orders/${orderId}/pay`);
  expect(response.status(), await response.text()).toBe(200);
}

export async function cancelOrder(api: APIRequestContext, orderId: string): Promise<void> {
  const response = await api.post(`/api/orders/${orderId}/cancel`);
  expect(response.status(), await response.text()).toBe(200);
}

// ---------------------------------------------------------------- page

export function splitSection(page: Page): Locator {
  return page.getByTestId('split-section');
}

/**
 * The "Part N" select of the line named `name`: the `split-line-part` select whose nearest ancestor that shows a line
 * name shows exactly this one. The plan does not say how a select is tied to its line, so it is found by the text
 * around it.
 */
export async function linePartSelect(page: Page, name: string, allNames: string[]): Promise<Locator> {
  const selects = splitSection(page).getByTestId('split-line-part');
  const owners = await selects.evaluateAll(
    (elements, names) =>
      elements.map((el) => {
        for (let node = el.parentElement; node; node = node.parentElement) {
          const text = node.textContent ?? '';
          const found = names.filter((n) => text.includes(n));
          if (found.length > 0) return found.length === 1 ? found[0] : null;
        }
        return null;
      }),
    allNames,
  );
  const index = owners.indexOf(name);
  expect(index, `a split-line-part select next to "${name}"; owners ${JSON.stringify(owners)}`).toBeGreaterThanOrEqual(0);
  return selects.nth(index);
}

/** Text of the selected option of a select, e.g. "Part 2" or "Unassigned". */
export async function selectedLabel(select: Locator): Promise<string> {
  return select.evaluate((el) => ((el as HTMLSelectElement).selectedOptions[0]?.textContent ?? '').trim());
}

export async function assignLine(page: Page, name: string, allNames: string[], part: number): Promise<void> {
  await (await linePartSelect(page, name, allNames)).selectOption({ label: `Part ${part}` });
}

export async function setPartCount(page: Page, count: number): Promise<void> {
  await splitSection(page).getByTestId('split-part-count').selectOption(String(count));
}

/** Every `split-part-row` shows the money of the matching API part (same index), to the cent, as $x.xx. */
export async function expectPartsMatch(page: Page, split: ApiSplit): Promise<void> {
  const rows = splitSection(page).getByTestId('split-part-row');
  await expect(rows).toHaveCount(split.parts.length);
  const mismatches: string[] = [];
  for (const [i, part] of split.parts.entries()) {
    for (const field of SPLIT_MONEY) {
      const cell = rows.nth(i).getByTestId(`split-part-${field}`);
      await expect(cell, `part ${i + 1} ${field} shown once`).toHaveCount(1);
      const text = await cell.textContent();
      if (shownCents(text) !== apiCents(part[field])) {
        mismatches.push(`part ${i + 1} ${field} shown ${text?.trim()} API ${part[field]}`);
      }
    }
  }
  expect(mismatches, 'split parts equal the API to the cent').toEqual([]);
}

/** Expected money of each shown part row, as the page shows it (e.g. ["$65.33", "$43.55"] for `total`). */
export async function expectPartColumn(page: Page, field: (typeof SPLIT_MONEY)[number], shown: string[]): Promise<void> {
  const cells = splitSection(page).getByTestId('split-part-row').getByTestId(`split-part-${field}`);
  await expect(cells).toHaveText(shown);
}

/**
 * At the current viewport width every visible element matched by `locator` (selects, inputs) has its own box on
 * screen. `expectAllOnScreen` measures non-buttons by their content, which says nothing for a select.
 */
export async function expectControlsOnScreen(page: Page, locator: Locator, what: string): Promise<void> {
  const width = page.viewportSize()?.width ?? Number.POSITIVE_INFINITY;
  const boxes = await locator.evaluateAll((elements) =>
    elements
      .filter((el) => el.checkVisibility())
      .map((el) => {
        const rect = el.getBoundingClientRect();
        return { left: rect.left, right: rect.right };
      }),
  );
  expect(boxes.length, `${what}: at least one visible element to measure`).toBeGreaterThan(0);
  const off = boxes.filter((b) => b.left < 0 || b.right > width + 0.5);
  expect.soft(off.length, `${what} inside viewport width ${width}px; outside: ${JSON.stringify(off.slice(0, 5))}`).toBe(0);
}
