---
name: add-endpoint
description: Adds or changes an HTTP endpoint of the Mini POS API (src/Pos.Api) end to end - Domain method, Application command/query + handler + validator, DI registration, controller action, and the unit, integration and concurrency tests this repo requires. Use for any new or changed route under /api, e.g. "add DELETE /api/orders/{id}/lines/{lineId}" or "expose X over the API".
---

# Add an endpoint

CLAUDE.md holds the rules (money, errors, concurrency, paging). This skill is the order of work and the files to copy.
Read `references/templates.md` before writing code.

## 0. Business rules first: stop and ask

Before any code, list every rule the task does not state and ask the user in one message. Typical gaps:
- Which order statuses allow it (others → 422)?
- Money: the base of each amount, rounding, what happens to an existing discount/tip when the order changes.
- Limits (max quantity, max N), and what an empty or edge result means.
- Response: 200 + `OrderDto` (what order changes return today) or something else.

Do not pick a limit, default or merge behavior yourself. Not-found ids are 404 and need no question.

## 1. Domain (only if the order or another entity changes)

- One public method on the entity (`Order.RemoveLine(...)`), next to the related ones. Order changes start with
  `EnsureOpen()`. A broken rule throws `DomainException` (→ 422).
- Totals are only in `Order.CalculateTotals()`. Never add, subtract, round or multiply `Money` outside Domain: the arch
  tests `Only_domain_rounds_amounts` and `Only_domain_adds_up_money` fail.
- Persisted shape changes (new column, owned type) need an EF migration in `src/Pos.Infrastructure`.

## 2. Application: one folder per use case

`src/Pos.Application/<Area>/<UseCase>/`:
- `<UseCase>Command` (changes state) or `<UseCase>Query` (reads), a `sealed record` taking ids + value objects
  (`Money`, `Percentage`), never raw `decimal`.
- `<UseCase>Handler` (`ICommandHandler<,>` / `IQueryHandler<,>`): load through the repository, `?? throw new
  NotFoundException(...)`, call Domain, `unitOfWork.SaveChangesAsync` for commands, return a DTO built by `XxxDto.From`.
- `<UseCase>Validator` (`internal sealed`, FluentValidation): shape only: ids not empty, ranges, formats. No rule the
  Domain already enforces.
- Register in `src/Pos.Application/DependencyInjection.cs` with `AddCommandHandler<,,>` / `AddQueryHandler<,,>` (these
  wrap the handler with the validators).

## 3. API

In the area's controller (`src/Pos.Api/Controllers/`):
- Route under `api/<area>`, ids as `{id:guid}`. Body → a `sealed record XxxRequest` at the bottom of the file, with
  `[Required]` on nullable value types that must be sent. Required query params: `[FromQuery, BindRequired]`.
- `[ProducesResponseType]` for 200/201 and each error it can return: 400 (validator or binding), 404, 409 (any order
  change: concurrency token), 422 (Domain rule).
- Inject the handler with `[FromServices]`. No logic in the controller. Errors are mapped by
  `src/Pos.Api/Errors/ApiExceptionHandler.cs`; a 500 is always a bug.

## 4. Tests: what each kind of endpoint needs

| Endpoint | Tests |
|---|---|
| Any | Validator theory in `tests/Pos.Application.Tests/ValidatorTests.cs` |
| Any | Integration tests in `tests/Pos.Api.IntegrationTests`: happy path, each 4xx it declares |
| Returns money | Assert raw JSON strings with exactly 2 decimals (`order.Money("total") == "15.40"`); a midpoint (x.xx5) case; list total == `GET /api/orders/{id}` total |
| Changes Domain money | Domain unit test with exact cents and a midpoint case (`tests/Pos.Domain.Tests`) |
| Changes an order | Concurrency test: N parallel requests (alone and racing add-item), only 200/409, and the stored rows show exactly the accepted requests |

Copy helpers from the existing test classes (see templates); add new HTTP helpers to `ApiClient.cs`.

## 5. Verify

- `dotnet build Pos.slnx` (0 warnings), `docker compose up -d`, `dotnet test Pos.slnx` (integration tests need the DB).
- Check the concurrency test really races: at least one 409 in a run, not N sequential 200s.
- The `PreToolUse` hook re-runs build + fast tests before `git commit`; integration tests are yours to run.
- Summary: endpoint, status codes, the business rules you were given, anything still open.
