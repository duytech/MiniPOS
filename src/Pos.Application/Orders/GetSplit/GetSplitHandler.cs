using Pos.Application.Common;

namespace Pos.Application.Orders.GetSplit;

public sealed class GetSplitHandler(IOrderRepository orders) : IQueryHandler<GetSplitQuery, OrderSplitDto>
{
    public async Task<OrderSplitDto> HandleAsync(GetSplitQuery query, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(query.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", query.OrderId);
        var parts = order.GetSavedSplit() ?? throw new NotFoundException("Split", query.OrderId);
        return OrderSplitDto.From(parts);
    }
}
