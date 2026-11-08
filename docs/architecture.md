# Architecture

Rules for the .NET Clean Architecture API (`src/`). `dotnet test tests/Pos.ArchitectureTests` checks them; a failure
lists each offending member.

- One use case = one folder in Application with `XxxCommand`/`XxxQuery` (record), `XxxHandler`
  (`ICommandHandler<TCommand, TResult>` / `IQueryHandler<TQuery, TResult>`), and `XxxValidator` (FluentValidation) when
  the request has shape to check.
  No service classes, no MediatR.
- Expected errors are values: Domain and handlers return `Result` / `Result<T>` with an `Error` (`ErrorCode` + message)
  and never throw for them. Check every `Result` before going on: an architecture test fails when one is thrown away.
  The one expected exception is a repository save's `SaveFailedException` (below). Other exceptions are left for bugs
  (constructor guards), which end as a 500. The controller returns the
  `Result` as it is: it serializes as the envelope.
- Domain classes mapped to the database (`Order`, `OrderLine`, `MenuItem`, `SavedSplitPart`, `SavedSplitLine`) hold only
  `{ get; set; }` properties: no methods, computed properties or constructors with parameters (`EntityShapeTests`).
  A use case's logic lives in its handler (e.g. `PayOrderHandler` checks the order is open, then sets `Status` and
  `PaidAt`). What several handlers share stays in Domain as static classes: `OrderPricing` (all amounts, the only
  place totals are calculated), `OrderSplitting` (split bill) and `OrderRules` (open-only, fixed discount ≤ subtotal).
- Validators check request shape (required, ranges, formats). Handlers and `OrderRules` enforce the business rules
  (status transitions, discount limits). Don't duplicate one in the other.
- Repositories save: `CreateAsync` adds a new entity and saves it, `SaveAsync` saves the changes to tracked entities
  loaded by `GetByIdAsync`. They return `Task`, not `Result`: an expected failure throws `ConcurrentChangeException` or
  `DataConflictException` (both `SaveFailedException`, in Application). The handler catches `SaveFailedException`
  around the save, logs it at Error (`SaveFailedLog`) and returns `Result.Fail(ex.ToError())`. Handlers never call the
  DbContext.
- Repositories return domain entities. Read models for lists live in Application (query + projection); Infrastructure
  never returns Application DTOs from a repository.
- Delete template leftovers (e.g. `WeatherForecast`) when you touch that project.
