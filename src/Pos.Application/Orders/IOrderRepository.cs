using Pos.Application.Orders.ListOrders;
using Pos.Domain.Orders;

namespace Pos.Application.Orders;

public interface IOrderRepository
{
    /// <summary>Loads the order with its lines, tracked for changes.</summary>
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Loads one page (1-based) of matching orders with their lines, not tracked, newest first, and the number of all
    /// matching orders.
    /// </summary>
    Task<(IReadOnlyList<Order> Orders, int TotalCount)> ListAsync(
        OrderStatus? status, OrderSearch? search, int page, int pageSize, CancellationToken cancellationToken);

    void Add(Order order);
}
