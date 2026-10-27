# Templates: copy from these files

Read the file, copy its shape, rename. They follow every rule in CLAUDE.md and pass the arch tests.

| Need | Copy from |
|---|---|
| Command that changes an order | `src/Pos.Application/Orders/SetTip/` (Command, Handler, Validator) |
| Command with no body | `src/Pos.Application/Orders/RemoveTip/` |
| Query that reads an order | `src/Pos.Application/Orders/SplitOrderEvenly/` |
| Paged list query | `src/Pos.Application/Orders/ListOrders/` |
| Domain change with a 422 | `Order.ApplyDiscount` in `src/Pos.Domain/Orders/Order.cs` |
| Controller actions | `SetTip`, `RemoveTip`, `SplitEvenly` in `src/Pos.Api/Controllers/OrdersController.cs` |
| Validator tests | `Tip_rate_must_fit_the_percentage_column`, `Order_changes_require_the_order_id` in `tests/Pos.Application.Tests/ValidatorTests.cs` |
| Integration tests | `tests/Pos.Api.IntegrationTests/OrderAdjustmentsApiTests.cs` (money strings, 404/422, closed order via `PayOrderAsync`/`CancelOrderAsync` in `ApiClient.cs`) |
| Concurrency tests | `tests/Pos.Api.IntegrationTests/ConcurrentOrderAdjustmentTests.cs` (`ConcurrentlyAsync`, `AssertOnlyOkOrConflict`, `StoredAsync`) |
| Domain money tests | `tests/Pos.Domain.Tests/OrderPricingTests.cs` |

## Command handler shape

```csharp
public sealed class RemoveTipHandler(IOrderRepository orders, IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveTipCommand, OrderDto>
{
    public async Task<OrderDto> HandleAsync(RemoveTipCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(command.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", command.OrderId);

        order.RemoveTip();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return OrderDto.From(order);
    }
}
```

## Controller action shape (order change)

```csharp
[HttpDelete("{id:guid}/tip")]
[ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
[ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
public Task<OrderDto> RemoveTip(
    Guid id,
    [FromServices] ICommandHandler<RemoveTipCommand, OrderDto> handler,
    CancellationToken cancellationToken) =>
    handler.HandleAsync(new RemoveTipCommand(id), cancellationToken);
```

## Concurrency test shape

N parallel requests of the new endpoint that would each succeed alone (different targets, e.g. different lines: N
removals of the *same* line correctly 404 after the first), then interleaved with add-item; then:
`AssertOnlyOkOrConflict(results)` and compare the stored rows (`StoredAsync`) with the accepted requests only.
Example: `Parallel_line_removals_and_add_items_lose_neither` in `ConcurrentOrderAdjustmentTests.cs`.
