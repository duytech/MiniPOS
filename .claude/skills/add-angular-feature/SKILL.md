---
name: add-angular-feature
description: Adds or changes a feature of the Mini POS web app (web/, Angular 20 + Tailwind) end to end - models, the API service method, a standalone component with RxJS state, the route, the Tailwind template and the Vitest tests this repo requires. Use for any new or changed page, component or API call under web/src, e.g. "add an order detail page" or "let the cashier remove a line from the UI".
---

# Add an Angular feature

CLAUDE.md holds the rules (money, reloading, no environment URLs, commands). This skill is the order of work and the
files to copy. Read `references/templates.md` (in this skill's folder, next to this file) before writing code.

## 0. UI behavior first: stop and ask

Before any code, list every behavior the task does not state and ask the user in one message. Typical gaps:
- What the user sees on each error the API can return (404, 409 conflict, 422 rule broken): the ProblemDetails
  `detail`, a fixed text, a reload?
- Confirmation before destructive actions (delete, cancel)?
- What refreshes after a change, and what happens to a second click while a change is in flight?
- Where the page is reached from and where it goes back to; what is shown or disabled when an action isn't allowed
  (e.g. a closed order).

Do not invent limits, defaults, wording of business messages or extra screens. API shapes are not questions: read them
from `src/Pos.Api/Controllers` and the DTOs in `src/Pos.Application`.

## 1. Model and service

- Types in `web/src/app/<area>/<area>.models.ts`, mirroring the API DTO. Money fields are `number` with a comment that
  the API computes them.
- One method per endpoint on the area's service (`<Area>Api`, `@Injectable({ providedIn: 'root' })`,
  `inject(HttpClient)`). Relative URLs only (`/api/...`; the dev server proxies them). No hosts or ports anywhere.
- The service returns the HTTP observable; no state, no `subscribe` inside it.

## 2. Component

- Standalone, `ChangeDetectionStrategy.OnPush`, `inject()` (ESLint `prefer-inject`), `templateUrl` next to it.
- One RxJS pipeline per screen state, folded into a single `state` signal with `toSignal(... scan(...))` and an
  explicit initial state (`{ data, loading, error }`). Derived values are `computed`.
- Pick the flattening operator by meaning and say why in a comment:
  - reads (load, search, page): `switchMap`, a newer request makes the old one useless;
  - writes (delete, change quantity, pay): `concatMap` (every click counts, in order) or `exhaustMap` (ignore clicks
    while one runs). Never `switchMap` for writes: it cancels the request on the client, not on the server.
- After a write, show the data the API returned (`OrderDto`), never a total you computed or patched yourself.
- Reloading keeps the previous data on screen with a small indicator; never swap the content for a spinner.
- Money is shown only with `| currency: 'USD'`. The frontend never adds, multiplies, rounds or recomputes amounts,
  not even optimistically.

## 3. Route and template

- Lazy route in `web/src/app/app.routes.ts` with `loadComponent` and a `title` (`'<Page> · Mini POS'`). Links use
  `routerLink`, not `href` strings built by hand.
- Tailwind, in the style of `orders-list.html` (slate/emerald palette, `rounded-md`, `tabular-nums` for money).
- Accessibility (ESLint template rules): every input has a label (`sr-only` if hidden), buttons are `<button
  type="button">`, status messages `role="status"`, errors `role="alert"`, clickable things are buttons or links.
- `data-testid` on everything a test reads or clicks.

## 4. Tests (Vitest + jsdom)

| What | Test |
|---|---|
| Each new service method | method, URL, params/body; returns the body unchanged (`orders-api.spec.ts`) |
| Money on screen | the exact string from the API value (`15.4` → `$15.40`, `0` → `$0.00`) |
| Each write | request sent; screen shows the returned order; the chosen operator's behavior on a second click |
| Each error from step 0 | the agreed message, and the previous data stays on screen |
| Reload | previous data + loading indicator while the request is pending |

Use `HttpTestingController` and `vi.useFakeTimers()`; `fakeAsync`/`tick` do not work here. When a test fails, read the
first failure: later ones are often fallout (`http.verify()` in `afterEach`).

## 5. Verify

- In `web/`: `npm run build`, `npm run lint`, `npx ng test --watch=false`.
- The `PreToolUse` hook runs eslint + `ngc` before `git commit`; Vitest and the build are yours to run.
- If the API and DB can run (`docker compose up -d`, `dotnet run --project src/Pos.Api --launch-profile http`,
  `npm start`), open the page and try each action once.
- Summary: the behaviors you were given, the operator you chose for each write and why, anything still open.
