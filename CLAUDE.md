# Mini POS

.NET 10 Clean Architecture API + Angular 20 (Tailwind) + PostgreSQL 17. Restaurant POS: orders, items, tax, tip,
discounts, surcharge/dual pricing, split bill. **Some existing code predates these rules: follow this file, not the
surrounding code.**

Existing features (API routes, web pages) with how each is verified: `docs/feature-list.json`. Business rules already
decided: `docs/business-rules.md`. Read both before adding a feature; add or update the feature's entry when you add or
change one, with `verification` steps that run its tests.
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

Architecture rules (use-case folders, validators vs domain, repositories): `docs/architecture.md`. Read it before
adding or changing code in `src/`.

## Commands
- DB: `docker compose up -d` (Postgres on **localhost:5433**; 5432 is taken by another install on this machine)
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

- A `Stop` hook (`.claude/hooks/verify-on-edit.mjs`) checks the uncommitted changes when you finish a turn: `.cs` →
  build + the tests that need no DB; `web/src/**/*.ts|html` → eslint on those files + `ngc` (`tsc` for specs). The
  same checks run as a `PreToolUse` hook before any `git commit` and block it when they fail. A separate hook on the
  same two events (`.claude/hooks/check-utf8.mjs`) rejects changed text files that are not UTF-8. When they report
  errors, fix them.
- No `Co-Authored-By: Claude` in commits, PRs or issues. A `PreToolUse` hook (`.claude/hooks/block-claude-coauthor.mjs`)
  blocks such commands.
- Never commit or push to `main`: branch, push the branch, open a PR. A `PreToolUse` hook
  (`.claude/hooks/block-main-branch.mjs`) blocks `git commit` on main and any `git push` that updates main.
- CI (`.github/workflows/ci.yml`): its jobs `dotnet`, `web`, `e2e` are required checks of `main`. A red check is
  fixed in the branch, never bypassed.

## Money (the rules that matter most)
- All amounts are `Money` (Domain value object: `decimal Amount` + `Currency`, USD only for now). Never `double`/`float`,
  never raw `decimal` in Domain/Application signatures.
- Calculation order and rounding, in **one place** in Domain (the order's pricing method):
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
- A concurrency conflict returns **409** ProblemDetails. Any expected DB error (unique violation, FK, conflict) maps to a
  4xx; a 500 is always a bug.
- Every endpoint that changes an order needs an integration test that fires parallel requests and checks the stored
  result (e.g. N parallel add-item requests: only 200/409 responses, stored quantity = number of 200s).

## API
- Errors are ProblemDetails: 400 validation, 404 not found, 409 conflict, 422 domain rule broken.
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
- E2E (a change the user sees in the browser): setup and run in `tests/e2e/README.md`: API on the `pos_e2e` database
  (`tests/e2e/start-api.ps1`), `npm start` in `web/`, then in `tests/e2e` `npx tsc -p .` and `npx playwright test`.
- Verifier scripts (`scripts/`): `node --test scripts/services.test.mjs`.
- The hooks run only build, analyzers, eslint, `ngc` and the no-DB tests: integration, Vitest and E2E are yours to run.

## Definition of done
- A feature is complete when it has enough code tests, not "code is written".
- Go through `docs/clean-state-checklist.md` before opening a PR and at the end of each session

## Development workflow
- Every change needs a task id (a GitHub issue number).
- A plan ends with a `## Steps` section: the numbered task list you will track while building it, same steps in the
  same order (branch, each piece of work, verification, PR). Each step names the plan section it comes from. Once the
  plan is approved, the task list is exactly that list.
- Git commits as checkpoints. Commit after completing each atomic unit of work. Commit messages should explain what was done and why, Must start with a task id like: 13245 | Do something.
- Branch name must start with a task id.
- Before every `git push`, docs-only and tests-only pushes too: run the `reviewer` agent on `git diff origin/main...HEAD`
  and handle its findings, then from the repo root run `node .claude/hooks/require-review-before-push.mjs --stamp`. Only
  `Verdict: approve` on that full range allows the stamp, not a `partial` verdict. A finding the user explicitly waives
  does not count against the verdict: one with a concrete wrong output is recorded in the limits in
  `docs/DECISIONS.md`, any other is named in the reviewer's prompt. A `PreToolUse` hook
  (`.claude/hooks/require-review-before-push.mjs`) blocks a push when `HEAD` has no stamp, so a new commit needs a new
  review. Run the push as its own command: the hook also blocks one that follows a command that can move `HEAD`, such
  as `git commit` or `git switch`, in the same command.
- A reviewer finding with a concrete wrong output is fixed, or moved to documented limits only with the user's OK.
- Before a `traced` finding about behavior is fixed, the main session reproduces it with a sample input (in `src/` or
  `web/`, a failing test) and adds that input to the set it re-runs.
- File docs/DECISIONS.md. Record important design decisions and reasons. No need for detailed design documents — just "what decision, why, when".
