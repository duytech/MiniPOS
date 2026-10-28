---
name: verify-ui
description: Opens the Mini POS web app in a real browser through the Playwright MCP server and checks each changed page - accessibility snapshot, screenshot, console, network, each action once, phone width. Use after any change under web/src that renders (component, template, style, route, API call) and before saying the UI work is done; add-angular-feature step 5 sends you here.
---

# Verify the UI in a browser

Build, lint, `ngc` and Vitest do not see the rendered page: Vitest mocks HTTP and runs in jsdom, so a wrong URL, a
money string from the wrong field, a broken layout or a mangled character all pass. This loop looks at the page the
cashier sees. It uses the `playwright` server from `.mcp.json` (tools `mcp__playwright__browser_*`). If those tools
are not in your session, say so in the summary and stop: do not report the UI as checked.

## 1. Start the app

- `docker compose up -d` (if it cannot reach the Docker API, Docker Desktop is not running: start it and wait for
  `docker info` to succeed), then the API and the web app: `preview_start` with `api` and `web` from
  `.claude/launch.json` when you have it, otherwise `dotnet run --project src/Pos.Api --launch-profile http` and
  `npm start` in `web/`, both in the background. Wait until http://localhost:4200 answers.
- Use the data already in the dev DB. If a page needs data that is not there, create it through the API (the same
  calls the UI makes) and list what you created in the summary. No seed scripts, no SQL.

## 2. The loop, once per changed page

1. `browser_navigate` to the page (`/orders`, `/orders/{id}`). Its result (and the result of every click, type or
   select) only links a `.yml` snapshot file; it does not show the tree.
2. `browser_snapshot` with no `target`: the accessibility tree is the main check. Read it, do not skim it. A
   snapshot with `target` returns only that subtree and drops the refs outside it, so take a full one (or use
   `browser_find`) before you act on another element.
   - every money value is `$x.xx` and equals the value in the API response (check one with `GET /api/...`);
   - the page title (`Orders · Mini POS`) and all text show the right characters, no `�`;
   - buttons and inputs have names a cashier can read; no empty `button` or unlabeled `textbox`.
3. `browser_take_screenshot` (`scale: "css"`, `fullPage: true`). The result is only a path: open the PNG with
   `Read` and look at it: alignment, columns that overflow or wrap badly, text
   cut off, the loading indicator. Without `filename` it lands in `.playwright-mcp/page-<timestamp>.png`; a relative
   `filename` resolves against the repo root, not the output dir, so pass `.playwright-mcp/<page>.png`. The folder is
   ignored by git; give the paths in the summary.
4. `browser_console_messages` with `level: "warning"` (errors + warnings; the default `info` adds noise): none.
   `browser_network_requests` with `static: false` (and `filter: "/api/"`): every `/api` call is the URL and status
   you expect; any 4xx/5xx you did not cause on purpose is a finding.
5. Each action the change adds or touches, once: `browser_click` / `browser_type` with `target` set to the `ref`
   from the snapshot (`e12`), then `browser_snapshot` again and check the new state (the returned totals, the
   message for an error case, previous data still on screen while it reloads). Use `browser_wait_for` with the
   text you expect instead of a fixed sleep.
6. `browser_resize` to 375 × 812, snapshot + screenshot again: nothing cut off, no horizontal scroll. The page
   can have no horizontal scroll while a table scrolls inside its own `overflow-x-auto` box and hides columns, so
   also run `browser_evaluate` (after `browser_wait_for` the data, or it measures an empty page) on each `table` (`scrollWidth` vs its parent's `clientWidth`) and check that the
   money columns and the action buttons are on screen without a sideways swipe. Resize back to 1280 × 800.

## 3. When something is wrong

Fix the code, then rerun the loop from step 1 for that page. Never change a test or an assertion to match what the
screen shows; if the screen is right and a test is wrong, say so and ask. A finding you cannot fix (an open business
question, an API bug outside the task) goes in the summary.

## 4. Summary

For each page: URL, what you checked (snapshot, screenshot, console, network, actions, phone width), the screenshot
paths, findings and what you fixed. Then the data you created and anything you could not check. Close the browser
with `browser_close`.
