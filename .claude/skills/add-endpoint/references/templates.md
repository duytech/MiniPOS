# Templates: copy from these files

Read the file, copy its shape, rename. They follow every rule in CLAUDE.md and pass the arch tests.

| Need | Copy from |
|---|---|
| Command that changes an order | `src/Pos.Application/Orders/SetTip/` (Command, Handler, Validator) |
| Command with no body | `src/Pos.Application/Orders/RemoveTip/` |
| Query that reads an order | `src/Pos.Application/Orders/SplitOrderEvenly/` |
| Paged list query | `src/Pos.Application/Orders/ListOrders/` |
| Domain change with a broken rule | `Order.ApplyDiscount` in `src/Pos.Domain/Orders/Order.cs` |
| Controller actions | `SetTip`, `RemoveTip`, `SplitEvenly` in `src/Pos.Api/Controllers/OrdersController.cs` |
| Validator tests | `Tip_rate_must_fit_the_percentage_column`, `Add_item_requires_the_menu_item_id` in `tests/Pos.Application.Tests/ValidatorTests.cs` |
| Integration tests | `tests/Pos.Api.IntegrationTests/OrderAdjustmentsApiTests.cs` (money strings, error codes, closed order via `PayOrderAsync`/`CancelOrderAsync` in `ApiClient.cs`) |
| Concurrency tests | `tests/Pos.Api.IntegrationTests/ConcurrentOrderAdjustmentTests.cs` (`ConcurrentlyAsync`, `AssertOnlyOkOrConflict`, `StoredAsync`) |
| Domain money tests | `tests/Pos.Domain.Tests/OrderPricingTests.cs` |

## Command handler shape

```csharp
public sealed class RemoveTipHandler(IOrderRepository orders, IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveTipCommand, OrderDto>
{
    public async Task<Result<OrderDto>> HandleAsync(RemoveTipCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Fail<OrderDto>(NotFoundErrors.Order(command.OrderId));
        }

        var changed = order.RemoveTip();
        if (!changed.IsSuccess)
        {
            return Result.Fail<OrderDto>(changed.Error);
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? Result.Ok(OrderDto.From(order)) : Result.Fail<OrderDto>(saved.Error);
    }
}
```

## Controller action shape (order change)

```csharp
[HttpDelete("{id:guid}/tip")]
public async Task<ApiResponse<OrderDto>> RemoveTip(
    Guid id,
    [FromServices] ICommandHandler<RemoveTipCommand, OrderDto> handler,
    CancellationToken cancellationToken) =>
    (await handler.HandleAsync(new RemoveTipCommand(id), cancellationToken)).ToResponse();
```

## Concurrency test shape

N parallel requests of the new endpoint that would each succeed alone (different targets, e.g. different lines: N
removals of the *same* line are correctly not found after the first), then interleaved with add-item; then:
`AssertOnlyOkOrConflict(results)` and compare the stored rows (`StoredAsync`) with the accepted requests only.
Example: `Parallel_line_removals_and_add_items_lose_neither` in `ConcurrentOrderAdjustmentTests.cs`.
