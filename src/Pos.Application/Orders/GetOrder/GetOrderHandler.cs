using Pos.Application.Common;
using Pos.Domain.Common;

namespace Pos.Application.Orders.GetOrder;

public sealed class GetOrderHandler(IOrderRepository orders) : IQueryHandler<GetOrderQuery, OrderDto>
{
    public async Task<Result<OrderDto>> HandleAsync(GetOrderQuery query, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(query.Id, cancellationToken);
        if (order is null)
        {
            return Result.Fail<OrderDto>(NotFoundErrors.Order(query.Id));
        }

        return Result.Ok(OrderDto.From(order));
    }
}
