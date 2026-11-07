using Pos.Application.Common;
using Pos.Domain.Common;

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

        var parts = order.SplitEvenly(query.Parts);
        return parts.IsSuccess
            ? Result.Ok<IReadOnlyList<SplitPartDto>>(parts.Data.Select(SplitPartDto.From).ToList())
            : Result.Fail<IReadOnlyList<SplitPartDto>>(parts.Error);
    }
}
