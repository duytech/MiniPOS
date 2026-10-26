using Pos.Application.Common;

namespace Pos.Application.Orders.GetOrder;

public sealed class GetOrderHandler(IOrderRepository orders) : IQueryHandler<GetOrderQuery, OrderDto>
{
    public async Task<OrderDto> HandleAsync(GetOrderQuery query, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new NotFoundException("Order", query.Id);
        return OrderDto.From(order);
    }
}
