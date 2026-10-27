using Pos.Application.Common;

namespace Pos.Application.Orders.SplitOrderEvenly;

public sealed class SplitOrderEvenlyHandler(IOrderRepository orders)
    : IQueryHandler<SplitOrderEvenlyQuery, IReadOnlyList<SplitPartDto>>
{
    public async Task<IReadOnlyList<SplitPartDto>> HandleAsync(
        SplitOrderEvenlyQuery query, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(query.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", query.OrderId);
        return order.SplitEvenly(query.Parts).Select(SplitPartDto.From).ToList();
    }
}
