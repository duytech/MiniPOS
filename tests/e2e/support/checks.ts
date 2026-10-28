import { expect, type Locator, type Page } from '@playwright/test';

// Shared assertions for the CLAUDE.md rules the E2E specs check:
// - money is shown as USD with exactly 2 decimals and equals the API value to the cent;
// - at 375 px there is no horizontal page scroll and money / row actions stay on screen.

/** Phone project width from playwright.config.ts; the layout rules apply at this width. */
export const PHONE_WIDTH = 375;

const MONEY_TEXT = /^\$(\d{1,3}(?:,\d{3})*)\.(\d{2})$/;

/** Cents of an API money number (the API sends 2-decimal numbers; JSON parsing may drop trailing zeros). */
export function apiCents(value: unknown): number {
  expect(typeof value, `API money value ${String(value)} is a number`).toBe('number');
  return Math.round((value as number) * 100);
}

/** Cents of a money text shown on the page; fails unless it reads `$x.xx` (thousands separators allowed). */
export function shownCents(text: string | null): number {
  const trimmed = (text ?? '').trim();
  const match = MONEY_TEXT.exec(trimmed);
  expect(match, `"${trimmed}" is shown as $x.xx`).not.toBeNull();
  const [, dollars, cents] = match as RegExpExecArray;
  return Number(dollars.replaceAll(',', '')) * 100 + Number(cents);
}

export function isPhone(page: Page): boolean {
  return (page.viewportSize()?.width ?? Number.POSITIVE_INFINITY) <= PHONE_WIDTH;
}

export async function expectNoHorizontalPageScroll(page: Page): Promise<void> {
  const size = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    clientWidth: document.documentElement.clientWidth,
  }));
  // Soft, so the on-screen checks that follow still run and report their measurements.
  expect.soft(size.scrollWidth, `page scrollWidth ${size.scrollWidth} <= clientWidth ${size.clientWidth}`)
    .toBeLessThanOrEqual(size.clientWidth);
}

export interface Offscreen {
  label: string;
  left: number;
  right: number;
}

/**
 * Boxes of the visible elements matched by `locator` (hidden responsive copies are left out). Buttons are measured by
 * their own box; other elements by the box of their content (a Range), so a block that stretches wider than its text
 * is judged by where the text actually is.
 */
async function visibleBoxes(locator: Locator, labelOf: string): Promise<Offscreen[]> {
  return locator.evaluateAll(
    (elements, attr) =>
      elements
        .filter((el) => el.checkVisibility())
        .map((el) => {
          let rect = el.getBoundingClientRect();
          if (el.tagName !== 'BUTTON') {
            const range = document.createRange();
            range.selectNodeContents(el);
            rect = range.getBoundingClientRect();
          }
          const label = attr === 'text' ? (el.textContent ?? '').trim() : (el.getAttribute(attr) ?? '');
          return { label: label.slice(0, 60), left: rect.left, right: rect.right };
        }),
    labelOf,
  );
}

export async function expectAllOnScreen(page: Page, locator: Locator, what: string, labelOf = 'text'): Promise<void> {
  const width = page.viewportSize()?.width ?? PHONE_WIDTH;
  const boxes = await visibleBoxes(locator, labelOf);
  const count = boxes.length;
  expect(count, `${what}: at least one visible element to measure`).toBeGreaterThan(0);
  const off = boxes.filter((b) => b.left < 0 || b.right > width + 0.5);
  // Soft, so every layout check of a page runs and reports its measurements; the test still fails.
  expect.soft(
    off.length,
    `${what} inside viewport width ${width}px; ${off.length} of ${count} outside, first: ${JSON.stringify(off.slice(0, 5))}`,
  ).toBe(0);
}
