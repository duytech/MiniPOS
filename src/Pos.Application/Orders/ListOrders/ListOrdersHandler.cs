using Pos.Application.Common;

namespace Pos.Application.Orders.ListOrders;

public sealed class ListOrdersHandler(IOrderRepository orders)
    : IQueryHandler<ListOrdersQuery, PagedResult<OrderSummaryDto>>
{
    public async Task<PagedResult<OrderSummaryDto>> HandleAsync(ListOrdersQuery query, CancellationToken cancellationToken)
    {
        var (page, totalCount) = await orders.ListAsync(
            query.Status, OrderSearch.Parse(query.Search), query.Page, query.PageSize, cancellationToken);
        return new PagedResult<OrderSummaryDto>(
            page.Select(OrderSummaryDto.From).ToList(), query.Page, query.PageSize, totalCount);
    }
}
