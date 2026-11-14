using Pos.Application.Common;
using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders.PreviewSplit;

/// <summary>Prices a split by item without saving it. Only an open order.</summary>
public sealed class PreviewSplitHandler(IOrderRepository orders) : IQueryHandler<PreviewSplitQuery, OrderSplitDto>
{
    public async Task<Result<OrderSplitDto>> HandleAsync(PreviewSplitQuery query, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(query.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Fail<OrderSplitDto>(NotFoundErrors.Order(query.OrderId));
        }

        if (!OrderRules.IsOpen(order))
        {
            return Result.Fail<OrderSplitDto>(OrderErrors.NotOpen(order.Status));
        }

        var itemIdsPerPart = query.Parts!.Select(p => (IReadOnlyCollection<Guid>)p!).ToList();
        var parts = OrderSplitting.Split(order, itemIdsPerPart);
        return parts.IsSuccess ? Result.Ok(OrderSplitDto.From(parts.Data!)) : Result.Fail<OrderSplitDto>(parts.Error);
    }
}
