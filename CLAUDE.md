# Mini POS

.NET 10 Clean Architecture API + Angular 20 (Tailwind) + PostgreSQL 17. Restaurant POS: orders, items, tax, tip,
discounts, surcharge/dual pricing, split bill. **Some existing code predates these rules: follow this file, not the
surrounding code.**

## Commands
- DB: `docker compose up -d` (Postgres on **localhost:5433**; 5432 is taken by another install on this machine)
- Build: `dotnet build Pos.slnx` · Test: `dotnet test Pos.slnx` (integration tests need the DB running)
- Run API: `dotnet run --project src/Pos.Api --launch-profile http` → http://localhost:5191 (migrates on startup in Development)
- Web (in `web/`): `npm start` → http://localhost:4200 (proxies `/api` to 5191) · `npm run build` ·
  `npx ng test --watch=false --browsers=ChromeHeadless`
- Known red: the 2 tests in `ConcurrentAddItemTests` fail until order concurrency is fixed. Any other red test is yours.

## Architecture
- Dependencies point inward: Api → Application → Domain; Infrastructure → Application. Domain references nothing.
- One use case = one folder in Application with `XxxCommand`/`XxxQuery` (record), `XxxHandler`
  (`ICommandHandler<TCommand, TResult>` / `IQueryHandler<TQuery, TResult>`), and `XxxValidator` (FluentValidation).
  No service classes, no MediatR.
- Validators check request shape (required, ranges, formats). Domain entities enforce invariants (status transitions,
  totals). Don't duplicate one in the other.
- Controllers are thin: map HTTP → command/query → handler → HTTP. No logic.
- Repositories return domain entities. Read models for lists live in Application (query + projection); Infrastructure
  never returns Application DTOs from a repository.
- Delete template leftovers (e.g. `WeatherForecast`) when you touch that project.

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
- Unit prices are snapshotted onto the order line when added. DB columns are `numeric(12,2)`.
- API returns money as numbers with exactly 2 decimals (`15.40`, never `15.4` or `15.4000`), everywhere.
- Frontend only displays money (`currency` pipe, `'USD'`); it never adds, multiplies, or rounds amounts.
- Every money rule gets a unit test that asserts exact cents, including a midpoint case (x.xx5).

## Concurrency
- Aggregates that hold money (Order) use Postgres `xmin` as the EF Core concurrency token.
- A concurrency conflict returns **409** ProblemDetails. Any expected DB error (unique violation, FK, conflict) maps to a
  4xx; a 500 is always a bug.
- Every endpoint that changes an order needs an integration test that fires parallel requests and checks the stored
  result (see `ConcurrentAddItemTests` for the pattern).

## Business rules
- Do not invent business rules, limits, or scope that the task doesn't state (max quantities, merge behavior, extra
  entities, seed data). Ask, or list them as open questions in your summary.
- Adding an item: identical line (same menu item, same snapshotted unit price, same notes/modifiers) → increase quantity;
  anything different → new line.

## API
- Errors are ProblemDetails: 400 validation, 404 not found, 409 conflict, 422 domain rule broken.
- List endpoints are paged (`page`, `pageSize`, response includes `totalCount`). Never silently cap results.

## Frontend (web/)
- Standalone components, `OnPush`, `inject()`, `@if`/`@for`, `toSignal` for streams; no manual `subscribe` without
  `takeUntilDestroyed`.
- When reloading data, keep showing the previous data with a small loading indicator; don't swap the table for a spinner.
- No environment URLs (hosts, ports) in components or messages.

## Config & secrets
- Never commit passwords or connection strings with credentials, including test defaults. Use user-secrets / environment
  variables; commit only `*.example` files. Read the test DB connection from configuration.

## Definition of done
- `dotnet build` with 0 warnings, `dotnet test` green except the known red above, `npm run build` passes if `web/` changed.
- New behavior has tests: money rules → exact-cent unit tests; order changes → concurrency test.
- Summary lists any assumptions or open business questions instead of silently deciding them.
