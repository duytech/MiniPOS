import { expect, type APIRequestContext, type Locator, type Page } from '@playwright/test';
import { getOrderJson } from './api';
import { apiCents, expectAllOnScreen, expectNoHorizontalPageScroll, shownCents } from './checks';

// Reading the /orders and /orders/{id} pages. Labels (column headers, summary terms, button names) come from the
// pages' accessibility tree, not from a contract; the loading indicator selector comes from web/src markup.

export const RELOAD_INDICATOR = '[data-testid="loading"]';
export const INITIAL_LOADING = '[data-testid="initial-loading"]';

export interface ApiLine {
  id: string;
  name: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
}

export interface ApiOrder {
  id: string;
  number: number;
  lines: ApiLine[];
  subtotal: number;
  discount: number;
  surcharge: number;
  tax: number;
  tip: number;
  total: number;
}

export async function getOrder(api: APIRequestContext, orderId: string): Promise<ApiOrder> {
  return (await getOrderJson(api, orderId)) as unknown as ApiOrder;
}

/** Index of the column whose header text is `header` in the first table on the page. */
async function columnIndex(table: Locator, header: string): Promise<number> {
  const headers = (await table.locator('thead th').allTextContents()).map((t) => t.trim());
  const index = headers.indexOf(header);
  expect(index, `column "${header}" in headers ${JSON.stringify(headers)}`).toBeGreaterThanOrEqual(0);
  return index;
}

const SUMMARY: ReadonlyArray<[string, keyof ApiOrder]> = [
  ['Subtotal', 'subtotal'],
  ['Discount', 'discount'],
  ['Surcharge', 'surcharge'],
  ['Tax', 'tax'],
  ['Tip', 'tip'],
  ['Total', 'total'],
];

export function summaryValue(page: Page, term: string): Locator {
  return page.locator('dt').filter({ hasText: new RegExp(`^\\s*${term}\\s*$`) }).locator('xpath=following-sibling::dd[1]');
}

interface ShownRow {
  cells: { text: string; visible: boolean }[];
}

/** Visible text of each body row's cells (innerText, so hidden responsive copies are left out). */
async function shownRows(rows: Locator): Promise<ShownRow[]> {
  return rows.evaluateAll((trs) =>
    trs.map((tr) => ({
      cells: Array.from(tr.querySelectorAll('td')).map((td) => ({
        text: (td as HTMLElement).innerText.trim(),
        visible: td.checkVisibility(),
      })),
    })),
  );
}

/** The row whose item cell shows `name` followed by nothing or by whitespace (so "line 1" does not match "line 10"). */
function rowFor(rows: ShownRow[], name: string): { row: ShownRow; rest: string } | undefined {
  for (const row of rows) {
    const item = row.cells[0]?.text ?? '';
    if (item.startsWith(name) && (item.length === name.length || /\s/.test(item[name.length]))) {
      return { row, rest: item.slice(name.length).trim() };
    }
  }
  return undefined;
}

/**
 * The unit price the user sees: the "Unit price" column when it is visible; otherwise (narrow layout, column hidden)
 * the "$x.xx each" text under the item name.
 */
function shownUnitPrice(found: { row: ShownRow; rest: string }, unitIdx: number): string {
  if (unitIdx >= 0 && found.row.cells[unitIdx]?.visible) return found.row.cells[unitIdx].text;
  const match = /^(\S+)\s+each$/.exec(found.rest);
  return match ? match[1] : `<no visible unit price; item cell rest "${found.rest}">`;
}

/** Each line's unit price and line total, and every summary amount, equal GET /api/orders/{id} to the cent. */
export async function expectDetailMatchesApi(page: Page, order: ApiOrder): Promise<void> {
  const table = page.getByRole('table').first();
  const rows = table.locator('tbody tr');
  await expect(rows).toHaveCount(order.lines.length);

  const headers = (await table.locator('thead th').allTextContents()).map((t) => t.trim());
  const unitIdx = headers.indexOf('Unit price');
  const totalIdx = await columnIndex(table, 'Line total');
  const shown = await shownRows(rows);

  const mismatches: string[] = [];
  for (const line of order.lines) {
    const label = line.name.slice(0, 40);
    const found = rowFor(shown, line.name);
    if (!found) {
      mismatches.push(`no row whose item cell shows "${label}..."`);
      continue;
    }
    const unit = shownUnitPrice(found, unitIdx);
    if (shownCents(unit) !== apiCents(line.unitPrice)) {
      mismatches.push(`${label} unit price shown ${unit} API ${line.unitPrice}`);
    }
    const total = found.row.cells[totalIdx]?.text ?? '';
    if (shownCents(total) !== apiCents(line.lineTotal)) {
      mismatches.push(`${label} line total shown ${total} API ${line.lineTotal}`);
    }
  }
  expect(mismatches, 'detail lines equal the API to the cent').toEqual([]);

  for (const [term, field] of SUMMARY) {
    const value = summaryValue(page, term);
    await expect(value, `summary "${term}" shown`).toHaveCount(1);
    expect(shownCents(await value.textContent()), `${term} equals API ${String(order[field])}`).toBe(
      apiCents(order[field]),
    );
  }
}

/** At 375 px: no horizontal page scroll; prices, line totals, summary amounts and Remove buttons on screen. */
export async function expectDetailFitsPhone(page: Page): Promise<void> {
  await expectNoHorizontalPageScroll(page);
  const table = page.getByRole('table').first();
  const unitIdx = await columnIndex(table, 'Unit price');
  const totalIdx = await columnIndex(table, 'Line total');
  const rows = table.locator('tbody tr');
  // The unit price is either its own column or, when that column is hidden, "$x.xx each" under the item name.
  const unitPrices = rows.locator(`td:nth-child(${unitIdx + 1})`).or(rows.getByText(/^\S+ each$/));
  await expectAllOnScreen(page, unitPrices, 'unit prices');
  await expectAllOnScreen(page, rows.locator(`td:nth-child(${totalIdx + 1})`), 'line total cells');
  await expectAllOnScreen(page, page.getByRole('button', { name: /^Remove / }), 'Remove buttons', 'aria-label');
  await expectAllOnScreen(page, page.locator('dd'), 'summary amounts');
}

export function listRow(page: Page, orderNumber: number): Locator {
  return page
    .getByRole('table')
    .first()
    .locator('tbody tr')
    .filter({ has: page.getByRole('link', { name: `#${orderNumber}`, exact: true }) });
}

/** The order's row on /orders shows the same total as GET /api/orders/{id}. */
export async function expectListTotalMatchesApi(page: Page, order: ApiOrder): Promise<Locator> {
  const table = page.getByRole('table').first();
  const row = listRow(page, order.number);
  await expect(row).toHaveCount(1);
  const totalIdx = await columnIndex(table, 'Total');
  const totalCell = row.locator(`td:nth-child(${totalIdx + 1})`);
  expect(shownCents(await totalCell.textContent()), `list total equals API total ${order.total}`).toBe(
    apiCents(order.total),
  );
  return totalCell;
}

/** At 375 px on /orders: no horizontal page scroll; Total cells and order links on screen. */
export async function expectListFitsPhone(page: Page): Promise<void> {
  await expectNoHorizontalPageScroll(page);
  const table = page.getByRole('table').first();
  const totalIdx = await columnIndex(table, 'Total');
  const rows = table.locator('tbody tr');
  await expectAllOnScreen(page, rows.locator(`td:nth-child(${totalIdx + 1})`), 'list Total cells');
  await expectAllOnScreen(page, rows.getByRole('link'), 'order links');
}

export async function searchOrders(page: Page, text: string): Promise<void> {
  await page.getByRole('searchbox', { name: 'Search orders' }).fill(text);
}
