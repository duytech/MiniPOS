# Architecture

Rules for the .NET Clean Architecture API (`src/`) and the web app (`web/`). `dotnet test tests/Pos.ArchitectureTests`
checks the .NET layering rules; a failure lists each offending member.

## .NET layers

- One use case = one folder in Application with `XxxCommand`/`XxxQuery` (record), `XxxHandler`
  (`ICommandHandler<TCommand, TResult>` / `IQueryHandler<TQuery, TResult>`), and `XxxValidator` (FluentValidation) when
  the request has shape to check.
  No service classes, no MediatR.
- Expected errors are values: Domain and handlers return `Result` / `Result<T>` with an `Error` (`ErrorCode` + message)
  and never throw for them. Check every `Result` before going on: an architecture test fails when one is thrown away.
  The one expected exception is a repository save's `SaveFailedException` (below). Other exceptions are left for bugs
  (constructor guards), which end as a 500. The controller returns the
  `Result` as it is: it serializes as the envelope.
- Domain classes mapped to the database (`Order`, `OrderMenuItem`, `MenuItem`, `SavedSplitPart`, `SavedSplitMenuItem`) hold only
  `{ get; set; }` properties: no methods, computed properties or constructors with parameters (`EntityShapeTests`).
  A use case's logic lives in its handler (e.g. `PayOrderHandler` checks the order is open, then sets `Status` and
  `PaidAt`). What several handlers share stays in Domain as static classes: `OrderPricing` (all amounts, the only
  place totals are calculated), `OrderSplitting` (split bill) and `OrderRules` (open-only, fixed discount ≤ subtotal).
- Validators check request shape (required, ranges, formats). Handlers and `OrderRules` enforce the business rules
  (status transitions, discount limits). Don't duplicate one in the other.
- Repositories save (through `BaseRepository.SaveChangesAsync`): `CreateAsync` adds a new entity and saves it, `SaveAsync` saves the changes to tracked entities
  loaded by `GetByIdAsync`. They return `Task`, not `Result`: an expected failure throws `ConcurrentChangeException` or
  `DataConflictException` (both `SaveFailedException`, in Application). The handler catches `SaveFailedException`
  around the save, logs it at Error (`SaveFailedLog`) and returns `Result.Fail(ex.ToError())`. Handlers never call the
  DbContext.
- Repositories return domain entities. Read models for lists live in Application (query + projection); Infrastructure
  never returns Application DTOs from a repository.
- Delete template leftovers (e.g. `WeatherForecast`) when you touch that project.

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
