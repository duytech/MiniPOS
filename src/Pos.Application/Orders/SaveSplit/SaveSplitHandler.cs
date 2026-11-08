using Pos.Application.Common;
using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders.SaveSplit;

/// <summary>
/// Saves the split by item, replacing any saved one. Only the line assignment is stored; its amounts are recomputed on
/// every read. Only an open order. A real change to the order's lines, discount, tip or payment method clears it.
/// </summary>
public sealed class SaveSplitHandler(IOrderRepository orders)
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

        var lineIdsPerPart = command.Parts!.Select(p => (IReadOnlyCollection<Guid>)p!).ToList();
        var parts = OrderSplitting.Split(order, lineIdsPerPart);
        if (!parts.IsSuccess)
        {
            return Result.Fail<OrderSplitDto>(parts.Error);
        }

        order.SavedSplitParts.Clear();
        order.SavedSplitParts.AddRange(lineIdsPerPart.Select((lineIds, position) => new SavedSplitPart
        {
            Id = Guid.NewGuid(),
            Position = position,
            Lines = lineIds
                .Select((lineId, linePosition) => new SavedSplitLine { LineId = lineId, Position = linePosition })
                .ToList(),
        }));

        var saved = await orders.SaveAsync(cancellationToken);
        return saved.IsSuccess ? Result.Ok(OrderSplitDto.From(parts.Data!)) : Result.Fail<OrderSplitDto>(saved.Error);
    }
}
