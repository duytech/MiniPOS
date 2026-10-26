using Pos.Domain.Orders;

namespace Pos.Application.Orders.ListOrders;

/// <param name="Search">
/// Matches orders with an item whose name contains the text (case-insensitive), or whose number is the text as a
/// whole number, with or without a leading '#'.
/// </param>
/// <param name="Status">Only orders in this status; all statuses when null.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Orders per page.</param>
public sealed record ListOrdersQuery(string? Search, OrderStatus? Status, int Page, int PageSize);
