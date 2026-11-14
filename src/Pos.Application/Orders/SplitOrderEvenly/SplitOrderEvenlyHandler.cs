using Pos.Application.Common;
using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders.SplitOrderEvenly;

public sealed class SplitOrderEvenlyHandler(IOrderRepository orders)
    : IQueryHandler<SplitOrderEvenlyQuery, IReadOnlyList<SplitPartDto>>
{
    public async Task<Result<IReadOnlyList<SplitPartDto>>> HandleAsync(
        SplitOrderEvenlyQuery query, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(query.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Fail<IReadOnlyList<SplitPartDto>>(NotFoundErrors.Order(query.OrderId));
        }

        var parts = OrderSplitting.SplitEvenly(order, query.Parts);
        if (!parts.IsSuccess)
        {
            return Result.Fail<IReadOnlyList<SplitPartDto>>(parts.Error);
        }

        var partDtos = parts.Data!.Select(SplitPartDto.From).ToList();
        return Result.Ok<IReadOnlyList<SplitPartDto>>(partDtos);
    }
}
