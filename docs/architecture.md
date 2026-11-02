# Architecture

Rules for the .NET Clean Architecture API (`src/`). `dotnet test tests/Pos.ArchitectureTests` checks them; a failure
lists each offending member.

- One use case = one folder in Application with `XxxCommand`/`XxxQuery` (record), `XxxHandler`
  (`ICommandHandler<TCommand, TResult>` / `IQueryHandler<TQuery, TResult>`), and `XxxValidator` (FluentValidation).
  No service classes, no MediatR.
- Validators check request shape (required, ranges, formats). Domain entities enforce invariants (status transitions,
  totals). Don't duplicate one in the other.
- Repositories return domain entities. Read models for lists live in Application (query + projection); Infrastructure
  never returns Application DTOs from a repository.
- Delete template leftovers (e.g. `WeatherForecast`) when you touch that project.
