# Plan: close an order (pay / cancel)

Written by the `planner` agent (Week 3 Day 3) after the user answered its 8 questions. API only.

## Rules used
- Only Open → Paid and Open → Cancelled. Paid and Cancelled are final; anything else → 422. (User, answer 1)
- Paying a Paid order or cancelling a Cancelled one → 422. (User, answer 2)
- An empty order (no lines, 0.00) can be paid and cancelled. (User, answer 3)
- Pay records only the status (+ `PaidAt`): stored `PaymentMethod`, no request body, one payment for the whole order. (User, answer 4)
- `PaidAt` and `CancelledAt`: two fields, set from `TimeProvider` in the handler (like `CreatedAt`), stored by a
  migration, in `OrderDto`. (User, answer 5)
- Cancel takes no reason, no body. (User, answer 6)
- `POST /api/orders/{id}/pay` and `POST /api/orders/{id}/cancel`, folders `PayOrder` and `CancelOrder`, 200 + `OrderDto`. (User, answer 7)
- Closing changes no money; totals stay what `Order.CalculateTotals()` returns. (Code)
- Existing rows already Paid/Cancelled (only by manual SQL) are not backfilled: `paid_at`/`cancelled_at` stay null. (Code; confirmed by the user)

## Files by layer

**Domain** — `src/Pos.Domain/Orders/Order.cs`
- `public DateTimeOffset? PaidAt { get; private set; }`, `public DateTimeOffset? CancelledAt { get; private set; }`.
- `Pay(DateTimeOffset paidAt)` and `Cancel(DateTimeOffset cancelledAt)` next to `SetPaymentMethod`: `EnsureOpen()`, then
  set status and store the time `.ToUniversalTime()` like `Create`. No empty-order check. Update the `EnsureOpen` summary.

**Application** (copy `src/Pos.Application/Orders/RemoveTip/`)
- `Orders/PayOrder/`: `PayOrderCommand(Guid OrderId)`, `PayOrderHandler` (`IOrderRepository`, `IUnitOfWork`,
  `TimeProvider` like `CreateOrderHandler`: load → `?? throw new NotFoundException` → `order.Pay(timeProvider.GetUtcNow())`
  → save → `OrderDto.From`), `PayOrderValidator` (`internal sealed`, `OrderId` NotEmpty).
- `Orders/CancelOrder/`: same shape, `order.Cancel(...)`.
- `OrderDto.cs`: `DateTimeOffset? PaidAt`, `DateTimeOffset? CancelledAt` after `CreatedAt`. `OrderSummaryDto` unchanged.
- `DependencyInjection.cs`: two `AddCommandHandler<,,>` lines.

**Infrastructure**
- `Persistence/Configurations/OrderConfiguration.cs`: `paid_at`, `cancelled_at` like `created_at` (timestamptz, nullable).
- Migration `OrderPaidAtCancelledAt` (`dotnet ef migrations add ... --project src/Pos.Infrastructure --startup-project
  src/Pos.Api`): two nullable `AddColumn`, no data update.

**Api** — `Controllers/OrdersController.cs`
- `[HttpPost("{id:guid}/pay")]`, `[HttpPost("{id:guid}/cancel")]` from the `RemoveTip` template; `ProducesResponseType`
  200 (`OrderDto`), 400, 404, 409, 422. No request record. `Pos.Api.http`: two sample requests.

**web/**: none.

## Tests
| Skill row | Cases |
|---|---|
| Validator theory | `ValidatorTests.Order_changes_require_the_order_id`: `PayOrderCommand(Guid.Empty)`, `CancelOrderCommand(Guid.Empty)` invalid |
| Domain | Pay/Cancel on open order sets status + timestamp, other stays null, `CalculateTotals()` unchanged; empty order works; theory Paid/Cancelled × Pay/Cancel → `DomainException`, nothing changes; non-UTC time stored as UTC |
| Integration | New `CloseOrderApiTests.cs` (copy `OrderAdjustmentsApiTests.cs`), theory pay/cancel: 200 + status + timestamps, `GET` agrees; empty order 200 total `"0.00"`; 400 for `00000000-…`; 404 problem+json; 422 for the 4 repeat/cross combinations, `GET` unchanged |
| Returns money | 0.70 + 15% discount then pay: `"0.70"`, `"0.11"` (0.105 midpoint), `"0.05"`, `"0.64"`; list total == `GET` total; Card order: pay response money == `GET` before pay |
| Concurrency | In `ConcurrentOrderAdjustmentTests.cs`: (1) 10 parallel pays: only 200/409/422, exactly one 200, stored Paid + `paid_at`; (2) 5 pays + 5 cancels: exactly one 200, stored status matches it, other timestamp null; (3) 10 add-items + 1 pay: only 200/409/422, stored quantity = 1 + accepted adds, Paid if pay accepted. At least one 409 in (1). Helper `AssertOnlyOkConflictOrUnprocessable` |
| HTTP helpers | `ApiClient.PayOrderAsync`, `CancelOrderAsync` |

## Existing code to update
- `tests/Pos.Domain.Tests/OrderTests.cs`: drop the reflection `SetStatus` helper; close with `order.Pay/Cancel`.
- Drop the SQL `SetStatusAsync` helpers and use the new API helpers in `OrderAdjustmentsApiTests.cs`,
  `ChangeOrderLineQuantityApiTests.cs`, `RemoveOrderLineApiTests.cs`, `OrdersListApiTests.cs`; remove unused usings.
- `.claude/skills/add-endpoint/references/templates.md`: "closed order via `SetStatusAsync`" → the new API helpers.
- `OrderPricingPersistenceTests.cs`: add the two fields if it asserts the full `OrderDto` shape.

## Verify
- `dotnet build Pos.slnx` 0 warnings; `docker compose up -d`; `dotnet test Pos.slnx` green; at least one 409 in the race.
- Run the API: pay → 200 Paid + `paidAt`, totals unchanged; pay again / cancel / add-item → 422; empty order cancel → 200
  `0.00`; `GET /api/orders?status=Paid` total == `GET /api/orders/{id}` total.

## Out of scope
Web buttons; `paidAt`/`cancelledAt` in the list; tendered amount/change, per-part payment, refund/void, reopen, cancel
reason; backfill of timestamps for rows closed by manual SQL.
