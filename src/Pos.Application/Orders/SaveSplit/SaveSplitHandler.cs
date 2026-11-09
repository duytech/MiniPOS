using Microsoft.Extensions.Logging;
using Pos.Application.Common;
using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders.SaveSplit;

/// <summary>
/// Saves the split by item, replacing any saved one. Only the item assignment is stored; its amounts are recomputed on
/// every read. Only an open order. A real change to the order's items, discount, tip or payment method clears it.
/// </summary>
public sealed class SaveSplitHandler(IOrderRepository orders, ILogger<SaveSplitHandler> logger)
    : ICommandHandler<SaveSplitCommand, OrderSplitDto>
{
    public async Task<Result<OrderSplitDto>> HandleAsync(SaveSplitCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Fail<OrderSplitDto>(NotFoundErrors.Order(command.OrderId));
        }

        if (OrderRules.OpenError(order) is { } notOpen)
        {
            return Result.Fail<OrderSplitDto>(notOpen);
        }

        var itemIdsPerPart = command.Parts!.Select(p => (IReadOnlyCollection<Guid>)p!).ToList();
        var parts = OrderSplitting.Split(order, itemIdsPerPart);
        if (!parts.IsSuccess)
        {
            return Result.Fail<OrderSplitDto>(parts.Error);
        }

        order.SavedSplitParts.Clear();
        order.SavedSplitParts.AddRange(itemIdsPerPart.Select((itemIds, position) => new SavedSplitPart
        {
            Id = Guid.NewGuid(),
            Position = position,
            Items = itemIds
                .Select((itemId, itemPosition) => new SavedSplitMenuItem { OrderMenuItemId = itemId, Position = itemPosition })
                .ToList(),
        }));

        try
        {
            await orders.SaveAsync(cancellationToken);
        }
        catch (SaveFailedException ex)
        {
            logger.OrderSaveFailed(ex, order.Id, ex.Code);
            return Result.Fail<OrderSplitDto>(ex.ToError());
        }

        return Result.Ok(OrderSplitDto.From(parts.Data!));
    }
}
