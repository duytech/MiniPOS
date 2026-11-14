using Pos.Application.Common;
using Pos.Domain.Common;

namespace Pos.Application.Orders.ListOrders;

public sealed class ListOrdersHandler(IOrderRepository orders)
    : IQueryHandler<ListOrdersQuery, PagedResult<OrderSummaryDto>>
{
    public async Task<Result<PagedResult<OrderSummaryDto>>> HandleAsync(ListOrdersQuery query, CancellationToken cancellationToken)
    {
        var (page, totalCount) = await orders.ListAsync(
            query.Status, OrderSearch.Parse(query.Search), query.Page, query.PageSize, cancellationToken);
        var summaries = page.Select(OrderSummaryDto.From).ToList();
        var pagedSummaries = new PagedResult<OrderSummaryDto>(summaries, query.Page, query.PageSize, totalCount);
        return Result.Ok(pagedSummaries);
    }
}
