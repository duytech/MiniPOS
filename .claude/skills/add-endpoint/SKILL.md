---
name: add-endpoint
description: Adds or changes an HTTP endpoint of the Mini POS API (src/Pos.Api) end to end - Domain property or shared rule, Application command/query + handler + validator, DI registration, controller action, and the unit, integration and concurrency tests this repo requires. Use for any new or changed route under /api, e.g. "add DELETE /api/orders/{id}/items/{itemId}" or "expose X over the API".
---

# Add an endpoint

CLAUDE.md holds the rules (money, errors, concurrency, paging). This skill is the order of work and the files to copy.
Read `references/templates.md` (in this skill's folder, next to this file) before writing code.

## 0. Business rules first: stop and ask

Before any code, list every rule the task does not state and ask the user in one message. Typical gaps:
- Which order statuses allow it (others → `OrderNotOpen`)?
- Money: the base of each amount, rounding, what happens to an existing discount/tip when the order changes.
- Limits (max quantity, max N), and what an empty or edge result means.
- Response: 200 + `OrderDto` (what order changes return today) or something else.

Do not pick a limit, default or merge behavior yourself. Not-found ids are a `*NotFound` code and need no question.

## 1. Domain (only if the order or another entity changes)

- Mapped entities hold only `{ get; set; }` properties (`EntityShapeTests`): add a property, never a method. The use
  case's logic goes in its handler (step 2).
- A rule or calculation that several handlers need goes in a static Domain class: `OrderRules` (e.g. `OpenError`),
  `OrderPricing`, `OrderSplitting`. A broken rule is an `Error(ErrorCode.X, message)`; add a code to `ErrorCode` when no
  existing one fits.
- Totals are only in `OrderPricing`. Never add, subtract, round or multiply `Money` outside Domain: the arch tests
  `Only_domain_rounds_amounts` and `Only_domain_adds_up_money` fail. Add the calculation to `OrderPricing` instead.
- Persisted shape changes (new column, owned type) need an EF migration in `src/Pos.Infrastructure`.

## 2. Application: one folder per use case

`src/Pos.Application/<Area>/<UseCase>/`:
- `<UseCase>Command` (changes state) or `<UseCase>Query` (reads), a `sealed record` taking ids + value objects
  (`Money`, `Percentage`), never raw `decimal`.
- `<UseCase>Handler` (`ICommandHandler<,>` / `IQueryHandler<,>`): load through the repository, on null return
  `Result.Fail<T>(NotFoundErrors.X(id))`; for an order change check `OrderRules.OpenError(order)` first, then apply the
  use case's logic (set properties, call `OrderPricing` / `OrderRules`), the repository save (`orders.SaveAsync`, `CreateAsync` for a new entity) for commands in a `try` that catches
  `SaveFailedException`, logs it (`SaveFailedLog`) and returns `Result.Fail(ex.ToError())`, and check
  that too, return `Result.Ok(XxxDto.From(...))`. Throwing away a `Result` fails `ResultUsageTests`.
- `<UseCase>Validator` (`internal sealed`, FluentValidation), when the request has shape to check: ids in the body not
  empty, ranges, formats. No rule the Domain already enforces. A route id is not validated: the handler answers not found.
- Register in `src/Pos.Application/DependencyInjection.cs` with `AddCommandHandler<,,>` / `AddQueryHandler<,,>` (these
  wrap the handler with the validators).

## 3. API

In the area's controller (`src/Pos.Api/Controllers/`):
- Route under `api/<area>`, ids as `{id:guid}`. Body → a `sealed record XxxRequest` at the bottom of the file, with
  `[Required]` on nullable value types that must be sent. Required query params: `[FromQuery, BindRequired]`.
- Inject the handler with `[FromServices]`. No logic in the controller: return `await handler.HandleAsync(...)`
  as `Task<Result<T>>`; every expected answer is HTTP 200, and a 500 is always a bug.

## 4. Tests: what each kind of endpoint needs

| Endpoint | Tests |
|---|---|
| Has a validator | Validator theory in `tests/Pos.Application.Tests/ValidatorTests.cs` |
| Any | Integration tests in `tests/Pos.Api.IntegrationTests`: happy path, each `ErrorCode` it can return (`AssertSuccessAsync`, `AssertErrorAsync`, not the status) |
| Returns money | Assert raw JSON strings with exactly 2 decimals (`order.Money("total") == "15.40"`); a midpoint (x.xx5) case; list total == `GET /api/orders/{id}` total |
| Changes Domain money | Domain unit test with exact cents and a midpoint case (`tests/Pos.Domain.Tests`) |
| Adds handler logic | Handler test through `OrderActions` (`tests/Pos.Application.Tests`): each rule, the no-op, nothing saved on an error |
| Changes an order | Concurrency test: N parallel requests that would each succeed alone (e.g. removing *different* items), then the same racing add-item; only successes and conflicts, and the stored rows show exactly the accepted requests. When a race can legitimately break a Domain rule (a removal racing a fixed discount), allow that code too and assert the rule holds in the stored rows |
| Moves the order to another status (pay, cancel) | Only one request can ever win, so the "each would succeed alone" pattern does not fit: N parallel requests through `OrderRace.RunAsync` (so every request loads the order before any commits) → exactly one success, the rest `ConcurrentChange`, at least one; without it a loser can load the closed order and answer `OrderNotOpen`; competing transitions (pay vs cancel) → the stored status is the winner's; the transition racing add-item → stored quantity = the accepted adds |

Copy helpers from the existing test classes (see templates); add new HTTP helpers to `ApiClient.cs`.
Tests check money against what the API returns (`GET /api/orders/{id}`), never against a total summed in SQL or LINQ:
that would be a second total implementation. SQL in tests reads stored columns (quantities, ids, rates) only.

## 5. Verify

- `dotnet build Pos.slnx` (0 warnings), `docker compose up -d`, `dotnet test Pos.slnx` (integration tests need the DB).
- Check the concurrency test really races: at least one `ConcurrentChange` in a run, not N sequential successes; use
  `OrderRace.RunAsync` when the test asserts one.
- The `PreToolUse` hook runs every check only before `git push`; while you work, build and tests are yours to run.
- Summary: endpoint, error codes, the business rules you were given, anything still open.
