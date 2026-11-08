using Pos.Application.Common;
using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders.GetSplit;

public sealed class GetSplitHandler(IOrderRepository orders) : IQueryHandler<GetSplitQuery, OrderSplitDto>
{
    public async Task<Result<OrderSplitDto>> HandleAsync(GetSplitQuery query, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(query.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Fail<OrderSplitDto>(NotFoundErrors.Order(query.OrderId));
        }

        if (order.SavedSplitParts.Count == 0)
        {
            return Result.Fail<OrderSplitDto>(NotFoundErrors.Split(query.OrderId));
        }

        var parts = OrderSplitting.Split(order, OrderSplitting.SavedSplitLineIds(order));
        return parts.IsSuccess ? Result.Ok(OrderSplitDto.From(parts.Data)) : Result.Fail<OrderSplitDto>(parts.Error);
    }
}
