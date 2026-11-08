# Mini POS

.NET 10 Clean Architecture API + Angular 20 (Tailwind) + PostgreSQL 17. Restaurant POS: orders, items, tax, tip,
discounts, surcharge/dual pricing, split bill. **Some existing code predates these rules: follow this file, not the
surrounding code.**

Read `docs/business-rules.md` before adding a feature: business rules already decided.

Read `docs/feature-list.json` before adding a feature: existing features (API routes, web pages) with how each is
verified. Add or update the feature's entry when you add or change one, with `verification` steps that run its tests.
- `state`, `testedAt` and `evidence` are set only by `node scripts/verify-features.mjs [id ...]` (it runs the steps;
  a hook blocks hand edits). A new entry starts `not_started`, with `testedAt` and `evidence` `null`. It starts the
  services a step `requires` when they are down (Docker Desktop, Postgres, the API on `pos_e2e`, the web app) and stops
  at the end only what it started; `--no-start` skips those steps instead (`blocked`). An API already running on
  `E2E_API_URL` is refused, not reused (it cannot tell its database): those steps are `blocked` until you stop it.
- Work on one feature at a time: at most one feature is `active` (a failed verification) while you work.
- A feature is done when the verifier sets it to `passing`.
- Do not invent business rules, limits, or scope that the task doesn't state (max quantities, max page size, merge
  behavior, extra entities, seed data). If the task can't be done without one, stop and ask **before** building it;
  listing it afterwards as an open question is not enough. A rule the user decides goes into `docs/business-rules.md`.

Read `docs/architecture.md` before adding or changing code in `src/`: architecture rules (use-case folders, validators
vs domain, repositories).

## Commands
- DB: `docker compose up -d` (Postgres on **127.0.0.1:5433**; 5432 is taken by another install on this machine)
- Whole app in Docker: `docker compose --profile app up -d` → http://localhost:8080 (images from GHCR, built by
  `.github/workflows/cd.yml`; add `--build` to build from source)
- Build: `dotnet build Pos.slnx` · Test: `dotnet test Pos.slnx` (integration tests need the DB running). Warnings are
  errors and Roslyn analyzers (`latest-recommended`) run on build (`Directory.Build.props`, `.editorconfig`): fix the
  code; a rule is only turned off in `.editorconfig` with a one-line reason. From Sonar (`SonarAnalyzer.CSharp`) only
  S3776 Cognitive Complexity runs (max 15 per method): split the method, don't raise the limit. `sonar.globalconfig`
  keeps every other Sonar rule off
- Architecture rules only (no DB, ~1 s): `dotnet test tests/Pos.ArchitectureTests`; a failure lists each offending member
- Run API: `dotnet run --project src/Pos.Api --launch-profile http` → http://localhost:5191 (migrates on startup in Development)
- Web (in `web/`): `npm start` → http://localhost:4200 (proxies `/api` to 5191) · `npm run build` · `npm run lint`
  (ESLint) · `npx ng test --watch=false` (Vitest + jsdom, no browser; `fakeAsync` does not work, use
  `vi.useFakeTimers()`). ESLint runs one Sonar rule, `sonarjs/cognitive-complexity` (max 15 per function, `.ts`
  only), same as S3776 in .NET: split the function, don't raise the limit

- Read `docs/hooks.md` when a hook blocks you or before you change one: what each hook does. Hooks check your changes
  and block some commands. When they report errors, fix them.
- No `Co-Authored-By: Claude` in commits, PRs or issues.
- Never commit or push to `main`: branch, push the branch, open a PR.
- Text files are UTF-8.
- CI (`.github/workflows/ci.yml`): its jobs `dotnet`, `web`, `e2e` are required checks of `main`. A red check is
  fixed in the branch, never bypassed.

## Money (the rules that matter most)
- All amounts are `Money` (Domain value object: `decimal Amount` + `Currency`, USD only for now). Never `double`/`float`,
  never raw `decimal` in Domain/Application signatures.
- Calculation order and rounding, in **one place** in Domain (`OrderPricing.CalculateTotals`):
  1. line total = unit price × quantity (exact cents, no rounding needed)
  2. subtotal = sum of line totals
  3. then discount → surcharge → tax → tip, each rounded to 2 decimals **immediately** with
     `MidpointRounding.AwayFromZero`, before the next step uses it
- The base of each step (e.g. is tip on the pre-tax or post-tax amount? is the surcharge taxed?) is a business rule:
  take it from the task; if the task doesn't say, ask. Name the base explicitly in code and tests.
- Totals are computed only by Domain. Never re-implement a total in SQL, LINQ projections, or the frontend. If a list
  needs totals, store them on the order when it changes or load through Domain; add a test that the list total equals
  `GET /api/orders/{id}` total.
- API returns money as numbers with exactly 2 decimals (`15.40`, never `15.4` or `15.4000`), everywhere.
- Frontend only displays money (`currency` pipe, `'USD'`); it never adds, multiplies, or rounds amounts.
- Every money rule gets a unit test that asserts exact cents, including a midpoint case (x.xx5).

## Concurrency
- Aggregates that hold money (Order) use Postgres `xmin` as the EF Core concurrency token.
- A concurrency conflict is the error code `ConcurrentChange`. Any expected DB error (unique violation, FK, conflict)
  maps to an error code; a 500 is always a bug.
- Every endpoint that changes an order needs an integration test that fires parallel requests and checks the stored
  result (e.g. N parallel add-item requests: only successes and `ConcurrentChange`, stored quantity = number of successes).

## API
- Every expected answer is HTTP 200 with `{ isSuccess, data, error: { code, message } }`, including requests
  rejected before the handler (malformed JSON, wrong type, missing required field). `code` is an `ErrorCode`, never an
  HTTP status. Different: an unexpected error (a bug) is a 500 ProblemDetails with no details, and a request that never
  reaches a controller (unknown route or an id that is not a GUID, wrong method, wrong Content-Type) gets the framework's
  404, 405 or 415.
- HTTP 200 does not mean success: a test checks `isSuccess` or the error code, not the status.
- List endpoints are paged (`page`, `pageSize`, response includes `totalCount`). Never silently cap results.

## Frontend (web/)
- When reloading data, keep showing the previous data with a small loading indicator; don't swap the table for a spinner.
- No environment URLs (hosts, ports) in components or messages.

## Config & secrets
- Never commit passwords or connection strings with credentials, including test defaults. Use user-secrets / environment
  variables; commit only `*.example` files. Read the test DB connection from configuration.

## Verification
Run what the change touches, in this order; CI runs all of it on every PR.
- Start of a session: `bash init.sh` (tools, local config, harness files, build, no-DB tests, web lint/build/Vitest);
  `bash init.sh --full` also runs every .NET test against Postgres. Fix what it reports before you start.
- .NET (`src/`, `tests/Pos.*`): `docker compose up -d`, `dotnet build Pos.slnx`, then `dotnet test Pos.slnx` (unit,
  architecture, integration and concurrency tests; integration needs the DB). Only the no-DB projects:
  `dotnet test tests/Pos.Domain.Tests`, `tests/Pos.Application.Tests`, `tests/Pos.ArchitectureTests`.
- Web (`web/`, in that folder): `npm run lint`, `npm run build`, `npx ng test --watch=false`.
- E2E (a change the user sees in the browser): Read `tests/e2e/README.md` for setup and run: API on the `pos_e2e` database
  (`tests/e2e/start-api.ps1`), `npm start` in `web/`, then in `tests/e2e` `npx tsc -p .` and `npx playwright test`.
- Verifier scripts (`scripts/`): `node --test scripts/services.test.mjs`.
- Hooks (`.claude/hooks/`): `node --test .claude/hooks/check-utf8.test.mjs .claude/hooks/block-main-branch.test.mjs .claude/hooks/block-claude-coauthor.test.mjs .claude/hooks/verify-on-edit.test.mjs`.
- Build and tests run on their own only before `git push` (`verify-on-edit.mjs`: all of the above except the hook tests,
  on the working tree). During a turn and at a commit, run what the change touches.

## Definition of done
- A feature is complete when it has enough code tests, not "code is written".
- Read `docs/clean-state-checklist.md` before opening a PR and at the end of each session, and go through it.

## Development workflow
- Every change needs a task id (a GitHub issue number).
- A plan has a `## Execution Steps` section: the numbered task list you will track while building it, same steps in
  the same order (branch, each piece of work, verification, PR). Each step names the plan section it comes from. Once
  the plan is approved, the task list is exactly that list.
- A plan has a `## Implementation Steps` section right below `## Execution Steps`: the concrete actions, short, in the
  order they are done (file or command and what changes). The detail stays in the plan's own sections, and the task
  list stays the Execution Steps.
- A plan ends with `## New files` and `## Changed files`, in that order after `## Implementation Steps`: file paths
  only. Write `None` when a list is empty.
- Git commits as checkpoints. Commit after completing each atomic unit of work. Commit messages should explain what was done and why, Must start with a task id like: 13245 | Do something.
- Branch name must start with a task id.
- Run the `reviewer` agent only when the user asks for it, on `git diff origin/main...HEAD`. A finding the user
  explicitly waives does not count against the verdict: one with a concrete wrong output is recorded in the limits in
  `docs/DECISIONS.md`, any other is named in the reviewer's prompt.
- A reviewer finding with a concrete wrong output is fixed, or moved to documented limits only with the user's OK.
- Before a `traced` finding about behavior is fixed, the main session reproduces it with a sample input (in `src/` or
  `web/`, a failing test) and adds that input to the set it re-runs.
- Read `docs/DECISIONS.md` for past design decisions. Record important new ones and their reasons there. No need for detailed design documents — just "what decision, why, when".
