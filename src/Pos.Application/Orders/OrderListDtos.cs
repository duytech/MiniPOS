using Pos.Domain.Orders;

namespace Pos.Application.Orders;

/// <summary>Query string parameters for listing orders. All are optional.</summary>
public sealed record ListOrdersRequest(string? Status = null, string? Search = null, int? Limit = null);

/// <summary>Validated, normalized filter passed to the repository.</summary>
/// <param name="Status">Only orders with this status; null means any status.</param>
/// <param name="Search">Trimmed search text (matches order id or an item name, case-insensitive); null means no search.</param>
/// <param name="Limit">Maximum number of orders to return, newest first.</param>
public sealed record OrderListFilter(OrderStatus? Status, string? Search, int Limit);

/// <summary>One row of the orders list (no item details).</summary>
public sealed record OrderSummaryDto(Guid Id, DateTime CreatedAtUtc, string Status, int ItemCount, decimal Total);
