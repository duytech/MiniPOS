import { expect, test, type Page, type Response } from '@playwright/test';
import { createMenuItem, createOrder, addItem } from '../support/api';
import { expectAllOnScreen, expectNoHorizontalPageScroll, isPhone } from '../support/checks';
import { expectDetailFitsPhone, expectDetailMatchesApi, getOrder } from '../support/pages';
import {
  CLEARED_MESSAGE,
  TOO_FEW_LINES_MESSAGE,
  assignLine,
  cancelOrder,
  expectPartColumn,
  expectControlsOnScreen,
  expectPartsMatch,
  getSplit,
  lineIdsOf,
  linePartSelect,
  orderWithLines,
  payOrder,
  saveSplit,
  selectedLabel,
  setPartCount,
  splitSection,
} from '../support/split';

// Split bill by item (plans/tuan-3-ngay-7-split-bill.md, R1-R14 and the Contract). Pricing config: tax 8.875%.
// Contract example: lines 60.00 + 40.00 -> tax 5.33 / 3.55, totals 65.33 / 43.55.

function stampOf(projectName: string): string {
  return `${Date.now()}${projectName}`;
}

function isSplitUrl(url: string, orderId: string): boolean {
  return new URL(url).pathname === `/api/orders/${orderId}/split`;
}

function isSplitGet(response: Response, orderId: string): boolean {
  return response.request().method() === 'GET' && isSplitUrl(response.url(), orderId);
}

function isPreview(url: string, orderId: string): boolean {
  return new URL(url).pathname === `/api/orders/${orderId}/split/preview`;
}

/** Opens the detail page and waits for its GET /split answer (200 or 404), so the builder state is the loaded one. */
async function openDetail(page: Page, orderId: string): Promise<void> {
  const splitLoaded = page.waitForResponse((r) => isSplitGet(r, orderId));
  await page.goto(`/orders/${orderId}`);
  await splitLoaded;
}

function sorted(ids: string[]): string[] {
  return [...ids].sort();
}

test('builds a split on the detail page; preview and saved split equal the API', async ({ page, request }, testInfo) => {
  const { orderId, order, names } = await orderWithLines(request, `Split build ${stampOf(testInfo.project.name)}`, [60, 40]);
  const [a, b] = lineIdsOf(order, names);
  const previews: string[] = [];
  page.on('request', (r) => {
    if (r.method() === 'POST' && isPreview(r.url(), orderId)) previews.push(r.postData() ?? '');
  });

  await openDetail(page, orderId);

  // R13: no saved split -> 2 parts, every line unassigned, no preview yet.
  const section = splitSection(page);
  await expect(section).toBeVisible();
  expect(await selectedLabel(section.getByTestId('split-part-count'))).toBe('2');
  for (const name of names) {
    expect(await selectedLabel(await linePartSelect(page, name, names)), `${name} starts unassigned`).toBe('Unassigned');
  }

  await assignLine(page, names[0], names, 1);
  // R13: preview only runs once every line has a part.
  await expect(section.getByTestId('split-part-row')).toHaveCount(0);
  expect(previews, 'no preview while a line is unassigned').toEqual([]);

  const previewed = page.waitForResponse((r) => r.request().method() === 'POST' && isPreview(r.url(), orderId));
  await assignLine(page, names[1], names, 2);
  expect((await previewed).status()).toBe(200);

  await expectPartColumn(page, 'subtotal', ['$60.00', '$40.00']);
  await expectPartColumn(page, 'tax', ['$5.33', '$3.55']);
  await expectPartColumn(page, 'total', ['$65.33', '$43.55']);
  expect(previews, 'one preview POST for the change that completed the assignment').toHaveLength(1);
  expect(await getSplit(request, orderId), 'preview saves nothing (R9)').toBeNull();

  const saved = page.waitForResponse((r) => r.request().method() === 'PUT' && isSplitUrl(r.url(), orderId));
  await section.getByTestId('split-save').click();
  expect((await saved).status()).toBe(200);

  const split = await getSplit(request, orderId);
  expect(split, 'GET /split after Save split').not.toBeNull();
  expect(split!.parts.map((p) => sorted(p.lineIds))).toEqual([[a], [b]]);
  await expectPartsMatch(page, split!);
  // The split section sits outside the order summary: the order's own amounts are still found once each.
  await expectDetailMatchesApi(page, await getOrder(request, orderId));
});

test('a saved split fills the builder after a reload', async ({ page, request }, testInfo) => {
  const { orderId, order, names } = await orderWithLines(request, `Split reload ${stampOf(testInfo.project.name)}`, [
    12.5, 7.25, 3.1,
  ]);
  const [a, b, c] = lineIdsOf(order, names);
  await openDetail(page, orderId);

  await setPartCount(page, 3);
  await assignLine(page, names[0], names, 3);
  await assignLine(page, names[1], names, 1);
  const previewed = page.waitForResponse((r) => r.request().method() === 'POST' && isPreview(r.url(), orderId));
  await assignLine(page, names[2], names, 2);
  await previewed;
  const saved = page.waitForResponse((r) => r.request().method() === 'PUT' && isSplitUrl(r.url(), orderId));
  await splitSection(page).getByTestId('split-save').click();
  expect((await saved).status()).toBe(200);

  const split = await getSplit(request, orderId);
  expect(split!.parts.map((p) => sorted(p.lineIds))).toEqual([[b], [c], [a]]);

  const reloaded = page.waitForResponse((r) => isSplitGet(r, orderId));
  await page.reload();
  expect((await reloaded).status()).toBe(200);

  // R13: saved split -> builder filled from it, part count = its number of parts.
  const section = splitSection(page);
  expect(await selectedLabel(section.getByTestId('split-part-count'))).toBe('3');
  expect(await selectedLabel(await linePartSelect(page, names[0], names))).toBe('Part 3');
  expect(await selectedLabel(await linePartSelect(page, names[1], names))).toBe('Part 1');
  expect(await selectedLabel(await linePartSelect(page, names[2], names))).toBe('Part 2');
  await expectPartsMatch(page, split!);
});

test('removing a line clears the saved split and keeps the other assignments', async ({ page, request }, testInfo) => {
  const { orderId, order, names } = await orderWithLines(request, `Split clear ${stampOf(testInfo.project.name)}`, [
    9, 6, 3,
  ]);
  const [a, b, c] = lineIdsOf(order, names);
  await saveSplit(request, orderId, [[a], [b, c]]);
  await openDetail(page, orderId);
  const section = splitSection(page);
  expect(await selectedLabel(await linePartSelect(page, names[2], names))).toBe('Part 2');
  await expect(section.getByTestId('split-message')).toHaveCount(0);

  await page.getByRole('button', { name: `Remove ${names[2]}`, exact: true }).click();

  // R14: the cleared message; the draft keeps the remaining lines' parts and drops the removed line.
  await expect(section.getByTestId('split-message')).toHaveText(CLEARED_MESSAGE);
  await expect(section.getByTestId('split-line-part')).toHaveCount(2);
  expect(await selectedLabel(await linePartSelect(page, names[0], names.slice(0, 2)))).toBe('Part 1');
  expect(await selectedLabel(await linePartSelect(page, names[1], names.slice(0, 2)))).toBe('Part 2');
  expect(await getSplit(request, orderId), 'R5: removing a line clears the saved split').toBeNull();
});

for (const lineCount of [0, 1]) {
  test(`an open order with ${lineCount} line(s) shows the too-few-lines message`, async ({ page, request }, testInfo) => {
    const orderId = await createOrder(request);
    for (let i = 0; i < lineCount; i++) {
      const item = await createMenuItem(request, `Split few ${stampOf(testInfo.project.name)} ${i}`, 4.5);
      await addItem(request, orderId, item.id, 1);
    }

    await page.goto(`/orders/${orderId}`);

    await expect(page.getByTestId('split-too-few-lines')).toHaveText(TOO_FEW_LINES_MESSAGE);
  });
}

for (const status of ['Paid', 'Cancelled'] as const) {
  test(`a ${status} order shows its saved split read-only`, async ({ page, request }, testInfo) => {
    const { orderId, order, names } = await orderWithLines(request, `Split ${status} ${stampOf(testInfo.project.name)}`, [
      60, 40,
    ]);
    const [a, b] = lineIdsOf(order, names);
    const saved = await saveSplit(request, orderId, [[a], [b]]);
    await (status === 'Paid' ? payOrder(request, orderId) : cancelOrder(request, orderId));

    await openDetail(page, orderId);

    // R3/R14: the split can still be read; no builder, no buttons.
    const section = splitSection(page);
    await expect(section).toBeVisible();
    await expectPartsMatch(page, saved);
    await expectPartColumn(page, 'total', ['$65.33', '$43.55']);
    await expect(section.getByTestId('split-part-count')).toHaveCount(0);
    await expect(section.getByTestId('split-line-part')).toHaveCount(0);
    await expect(section.getByTestId('split-save')).toHaveCount(0);
    await expect(section.getByTestId('split-remove')).toHaveCount(0);
    await expect(section.getByRole('button')).toHaveCount(0);
    await expect(section.getByRole('combobox')).toHaveCount(0);
  });

  test(`a ${status} order with no saved split hides the split section`, async ({ page, request }, testInfo) => {
    const { orderId } = await orderWithLines(request, `Split none ${status} ${stampOf(testInfo.project.name)}`, [5, 6]);
    await (status === 'Paid' ? payOrder(request, orderId) : cancelOrder(request, orderId));

    await openDetail(page, orderId);

    await expect(splitSection(page)).toHaveCount(0);
  });
}

test('the split builder and parts fit at 375 px with no horizontal scroll', async ({ page, request }, testInfo) => {
  const { orderId, order, names } = await orderWithLines(request, `Split phone ${stampOf(testInfo.project.name)}`, [
    1234.56, 987.65, 45.1,
  ]);
  const [a, b, c] = lineIdsOf(order, names);
  const split = await saveSplit(request, orderId, [[a, c], [b]]);

  await openDetail(page, orderId);
  await expectPartsMatch(page, split);
  await page.screenshot({ path: testInfo.outputPath('split-phone.png'), fullPage: true });

  if (isPhone(page)) {
    const section = splitSection(page);
    await expectNoHorizontalPageScroll(page);
    await expectDetailFitsPhone(page);
    await expectControlsOnScreen(page, section.getByTestId('split-part-count'), 'Parts select');
    await expectControlsOnScreen(page, section.getByTestId('split-line-part'), 'Part N selects');
    for (const field of ['subtotal', 'discount', 'surcharge', 'tax', 'tip', 'total']) {
      await expectAllOnScreen(page, section.getByTestId(`split-part-${field}`), `part ${field} cells`);
    }
    await expectAllOnScreen(page, section.getByTestId('split-save'), 'Save split button', 'data-testid');
    await expectAllOnScreen(page, section.getByTestId('split-remove'), 'Remove split button', 'data-testid');
  }
});
