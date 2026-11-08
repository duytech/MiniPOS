using Pos.Application.Common;
using Pos.Domain.Common;

namespace Pos.Application.Orders.PreviewSplit;

public sealed class PreviewSplitHandler(IOrderRepository orders) : IQueryHandler<PreviewSplitQuery, OrderSplitDto>
{
    public async Task<Result<OrderSplitDto>> HandleAsync(PreviewSplitQuery query, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(query.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Fail<OrderSplitDto>(NotFoundErrors.Order(query.OrderId));
        }

        var parts = order.PreviewSplit(query.Parts!.Select(p => (IReadOnlyCollection<Guid>)p!).ToList());
        return parts.IsSuccess ? Result.Ok(OrderSplitDto.From(parts.Data)) : Result.Fail<OrderSplitDto>(parts.Error);
    }
}
