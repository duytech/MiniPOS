# Clean State Checklist

Run this checklist before opening a PR and at the end of each session. Skip a section only when the change does not
touch that side of the repo (`src/`, `web/`). The commands are in the Verification section of `CLAUDE.md`.

## Build

- [ ] `bash init.sh --full` ends with "All checks passed"
- [ ] `dotnet build Pos.slnx` passes with 0 warnings (analyzers and S3776 included)
- [ ] `npm run lint` passes in `web/`
- [ ] `npm run build` passes in `web/`
- [ ] No rule turned off in `.editorconfig` or ESLint without a one-line reason; no complexity limit raised

## Tests (in this order; stop at the first level that fails)

- [ ] Level 1, unit: `dotnet test tests/Pos.Domain.Tests`, `tests/Pos.Application.Tests`,
      `tests/Pos.ArchitectureTests`; `npx ng test --watch=false` in `web/`
- [ ] Level 2, integration: `docker compose up -d`, then `dotnet test Pos.slnx`
- [ ] Level 3, end to end: `npx tsc -p .` and `npx playwright test` in `tests/e2e` (setup in `tests/e2e/README.md`)
- [ ] New behavior has tests: every money rule has an exact-cent unit test with a midpoint case (x.xx5)
- [ ] Every endpoint that changes an order has a parallel-request integration test that checks the stored result

## Architecture

- [ ] Rules in `docs/architecture.md` hold (use-case folders, validators vs domain, repositories)
- [ ] Template leftovers (e.g. `WeatherForecast`) deleted in the projects the change touched

## Money

- [ ] All amounts are `Money`; no `double`/`float`, no raw `decimal` in Domain/Application signatures
- [ ] Totals computed only by `OrderPricing` in Domain, in the order discount, surcharge, tax, tip, each
      rounded with `MidpointRounding.AwayFromZero`
- [ ] The base of each pricing step is named in code and tests
- [ ] No totals in SQL, LINQ projections or the frontend; a list that shows totals has a test that they equal
      `GET /api/orders/{id}`
- [ ] API returns money with exactly 2 decimals

## API

- [ ] Expected answers are HTTP 200 with the envelope and an `ErrorCode`; no expected DB error returns 500
- [ ] List endpoints are paged (`page`, `pageSize`, `totalCount`) and never cap results silently

## Frontend

- [ ] Money only displayed (`currency` pipe, `'USD'`), never calculated
- [ ] Reloading keeps the previous data with a small loading indicator
- [ ] No environment URLs (hosts, ports) in components or messages

## Business rules

- [ ] No business rule, limit, entity or seed data that the task does not state
- [ ] Assumptions and open business questions listed in the summary or PR, not decided silently

## Repository

- [ ] `git status` shows no unintended files (build output, `node_modules`, test results, scratch files)
- [ ] No passwords or connection strings with credentials staged; only `*.example` config files
- [ ] Text files are UTF-8
- [ ] Branch name and every commit message start with the task id; no `Co-Authored-By: Claude`
- [ ] Nothing committed or pushed to `main`
- [ ] `docs/business-rules.md` updated if a rule was added or changed
- [ ] `docs/DECISIONS.md` updated if an important design decision was made
