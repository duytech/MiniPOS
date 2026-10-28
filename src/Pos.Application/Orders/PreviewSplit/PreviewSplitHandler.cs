using Pos.Application.Common;

namespace Pos.Application.Orders.PreviewSplit;

public sealed class PreviewSplitHandler(IOrderRepository orders) : IQueryHandler<PreviewSplitQuery, OrderSplitDto>
{
    public async Task<OrderSplitDto> HandleAsync(PreviewSplitQuery query, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(query.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", query.OrderId);
        return OrderSplitDto.From(order.PreviewSplit(query.Parts!.Select(p => (IReadOnlyCollection<Guid>)p!).ToList()));
    }
}
