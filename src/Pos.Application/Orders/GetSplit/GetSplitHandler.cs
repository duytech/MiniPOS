using Pos.Application.Common;
using Pos.Domain.Common;

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

        if (!order.HasSavedSplit)
        {
            return Result.Fail<OrderSplitDto>(NotFoundErrors.Split(query.OrderId));
        }

        var parts = order.GetSavedSplit();
        return parts.IsSuccess ? Result.Ok(OrderSplitDto.From(parts.Data)) : Result.Fail<OrderSplitDto>(parts.Error);
    }
}
